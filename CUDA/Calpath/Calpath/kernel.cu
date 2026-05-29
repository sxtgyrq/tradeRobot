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

    cal.loopAndFindPath();

    return cal.CopyLastFPToHost(lastFP, lastFPLength);
}

extern "C" __declspec(dllexport) int Calpath_GetParallelStartPointRecommendation(
    int cudaPointLength,
    int* fullLoadCount,
    int* recommendedCount)
{
    if (fullLoadCount == nullptr || recommendedCount == nullptr)
    {
        return -2;
    }

    *fullLoadCount = 0;
    *recommendedCount = 0;

    if (cudaPointLength < 0 || cudaPointLength % 3 != 0)
    {
        return -1;
    }

    int pointCount = cudaPointLength / 3;
    if (pointCount == 0)
    {
        return 0;
    }

    cudaError_t status = cudaSetDevice(0);
    if (status != cudaSuccess)
    {
        return errorCode(status);
    }

    size_t freeBytes = 0;
    size_t totalBytes = 0;
    status = cudaMemGetInfo(&freeBytes, &totalBytes);
    if (status != cudaSuccess)
    {
        return errorCode(status);
    }

    unsigned long long fixedBytes =
        static_cast<unsigned long long>(cudaPointLength) * sizeof(int) +
        static_cast<unsigned long long>(pointCount) * sizeof(int);

    unsigned long long perUnitBytes =
        static_cast<unsigned long long>(pointCount) * sizeof(int) * 6ULL +
        sizeof(int);

    if (perUnitBytes == 0 || static_cast<unsigned long long>(freeBytes) <= fixedBytes)
    {
        return -6;
    }

    unsigned long long maxUnits =
        (static_cast<unsigned long long>(freeBytes) - fixedBytes) / perUnitBytes;

    if (maxUnits == 0)
    {
        maxUnits = 1;
    }

    if (maxUnits > 2147483647ULL)
    {
        maxUnits = 2147483647ULL;
    }

    unsigned long long recommendedUnits = maxUnits * 8ULL / 10ULL;
    if (recommendedUnits == 0)
    {
        recommendedUnits = 1;
    }

    *fullLoadCount = static_cast<int>(maxUnits);
    *recommendedCount = static_cast<int>(recommendedUnits);
    return 0;
}
