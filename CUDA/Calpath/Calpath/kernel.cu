#include "cuda_runtime.h"
#include "device_launch_parameters.h"

#include "CircleCal.cuh"

#include <stdio.h>

namespace
{
    constexpr int ThreadCount = 256;
    constexpr int MaxBlockCount = 1024;
    constexpr unsigned long long Infinity = 0x7fffffffffffffffULL;
    constexpr int TerminalLinkPointCode = 5;

    int errorCode(cudaError_t status)
    {
        return -1000 - static_cast<int>(status);
    }

    int blockCountForLength(int length)
    {
        int blockCount = (length + ThreadCount - 1) / ThreadCount;
        if (blockCount < 1)
        {
            return 1;
        }

        if (blockCount > MaxBlockCount)
        {
            return MaxBlockCount;
        }

        return blockCount;
    }

    __global__ void initializeDistanceKernel(
        const int* lastFP,
        unsigned long long* distance,
        int length)
    {
        int index = blockIdx.x * blockDim.x + threadIdx.x;
        int stride = blockDim.x * gridDim.x;

        for (int i = index; i < length; i += stride)
        {
            distance[i] = lastFP[i] == -1 ? Infinity : 0ULL;
        }
    }

    __device__ void relaxEdge(
        int sourceIndex,
        int targetIndex,
        int edgeCost,
        int unitBase,
        int pointCount,
        unsigned long long* distance,
        int* lastFP,
        int* changed)
    {
        if (targetIndex < 0 || targetIndex >= pointCount || edgeCost < 0)
        {
            return;
        }

        int sourceOffset = unitBase + sourceIndex;
        unsigned long long sourceDistance = distance[sourceOffset];
        if (sourceDistance >= Infinity)
        {
            return;
        }

        unsigned long long proposedDistance = sourceDistance + static_cast<unsigned long long>(edgeCost);
        if (proposedDistance < sourceDistance || proposedDistance > Infinity)
        {
            proposedDistance = Infinity;
        }

        int targetOffset = unitBase + targetIndex;
        unsigned long long oldDistance = atomicMin(&distance[targetOffset], proposedDistance);
        if (proposedDistance < oldDistance)
        {
            lastFP[targetOffset] = sourceIndex;
            atomicExch(changed, 1);
        }
    }

    __global__ void relaxPathKernel(
        const int* cudaPoints,
        int pointCount,
        const int* connect,
        int* lastFP,
        unsigned long long* distance,
        int unitCount,
        int* changed)
    {
        int index = blockIdx.x * blockDim.x + threadIdx.x;
        int stride = blockDim.x * gridDim.x;
        int length = pointCount * unitCount;

        for (int i = index; i < length; i += stride)
        {
            int unitIndex = i / pointCount;
            int sourceIndex = i % pointCount;
            int unitBase = unitIndex * pointCount;
            int sourcePointOffset = sourceIndex * 3;
            int sourceCircleIndex = cudaPoints[sourcePointOffset];
            int sourcePointIndex = cudaPoints[sourcePointOffset + 1];
            int sourcePointType = cudaPoints[sourcePointOffset + 2];

            int nextIndex = sourceIndex + 1;
            if (nextIndex < pointCount)
            {
                int nextPointOffset = nextIndex * 3;
                int nextCircleIndex = cudaPoints[nextPointOffset];
                if (nextCircleIndex == sourceCircleIndex)
                {
                    int nextPointIndex = cudaPoints[nextPointOffset + 1];
                    int edgeCost = nextPointIndex - sourcePointIndex;
                    if (edgeCost < 1)
                    {
                        edgeCost = 1;
                    }

                    relaxEdge(
                        sourceIndex,
                        nextIndex,
                        edgeCost,
                        unitBase,
                        pointCount,
                        distance,
                        lastFP,
                        changed);
                }
            }

            int connectIndex = connect[sourceIndex];
            if (connectIndex != -1)
            {
                int edgeCost = sourcePointType % TerminalLinkPointCode == 0 ? 1 : 0;
                relaxEdge(
                    sourceIndex,
                    connectIndex,
                    edgeCost,
                    unitBase,
                    pointCount,
                    distance,
                    lastFP,
                    changed);
            }
        }
    }
}

extern "C" __declspec(dllexport) int Calpath_AcceptPoints(
    const int* cudaPoints,
    int length,
    int* lastFP,
    int lastFPLength,
    const int* connect)
{
    if (length < 0 || length % 3 != 0 || lastFPLength < 0)
    {
        return -1;
    }

    int pointCount = length / 3;
    int connectLength = pointCount;

    if (pointCount == 0)
    {
        return lastFPLength == 0 ? 0 : -5;
    }

    if (lastFPLength % pointCount != 0)
    {
        return -5;
    }

    int unitCount = lastFPLength / pointCount;
    if (unitCount == 0)
    {
        return 0;
    }

    if (cudaPoints == nullptr)
    {
        return -2;
    }

    if (lastFP == nullptr)
    {
        return -3;
    }

    if (connect == nullptr)
    {
        return -4;
    }

    Cal cal(cudaPoints, length, connect, connectLength, lastFP, lastFPLength);
    if (cal.StatusCode() != 0)
    {
        if (cal.StatusMessage() != nullptr)
        {
            fprintf(stderr, "Cal init failed: %s\n", cal.StatusMessage());
        }

        return cal.StatusCode();
    }

    cudaError_t status = cudaSuccess;
    int hostChanged = 0;
    int lastFPBlockCount = blockCountForLength(lastFPLength);

    initializeDistanceKernel<<<lastFPBlockCount, ThreadCount>>>(
        cal.DeviceLastFP(),
        cal.DeviceDistance(),
        cal.LastFPLength());

    status = cudaGetLastError();
    if (status != cudaSuccess)
    {
        fprintf(stderr, "initializeDistanceKernel launch failed: %s\n", cudaGetErrorString(status));
        return errorCode(status);
    }

    status = cudaDeviceSynchronize();
    if (status != cudaSuccess)
    {
        fprintf(stderr, "initializeDistanceKernel synchronize failed: %s\n", cudaGetErrorString(status));
        return errorCode(status);
    }

    for (int iteration = 0; iteration < pointCount; iteration++)
    {
        status = cudaMemset(cal.DeviceChanged(), 0, sizeof(int));
        if (status != cudaSuccess)
        {
            fprintf(stderr, "cudaMemset changed failed: %s\n", cudaGetErrorString(status));
            return errorCode(status);
        }

        relaxPathKernel<<<lastFPBlockCount, ThreadCount>>>(
            cal.DeviceCudaPoints(),
            cal.PointCount(),
            cal.DeviceConnect(),
            cal.DeviceLastFP(),
            cal.DeviceDistance(),
            cal.UnitCount(),
            cal.DeviceChanged());

        status = cudaGetLastError();
        if (status != cudaSuccess)
        {
            fprintf(stderr, "relaxPathKernel launch failed: %s\n", cudaGetErrorString(status));
            return errorCode(status);
        }

        status = cudaDeviceSynchronize();
        if (status != cudaSuccess)
        {
            fprintf(stderr, "relaxPathKernel synchronize failed: %s\n", cudaGetErrorString(status));
            return errorCode(status);
        }

        status = cudaMemcpy(&hostChanged, cal.DeviceChanged(), sizeof(int), cudaMemcpyDeviceToHost);
        if (status != cudaSuccess)
        {
            fprintf(stderr, "cudaMemcpy changed failed: %s\n", cudaGetErrorString(status));
            return errorCode(status);
        }

        if (hostChanged == 0)
        {
            break;
        }
    }

    return cal.CopyLastFPToHost(lastFP, lastFPLength);
}
