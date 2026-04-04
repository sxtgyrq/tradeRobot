#include "cuda_runtime.h"
#include "device_launch_parameters.h"

#include <stdio.h>

namespace
{
    constexpr int ThreadCount = 256;
    constexpr int MaxBlockCount = 1024;

    __global__ void checksumKernel(
        const int* cudaPoints,
        int pointLength,
        const int* lastFP,
        int lastFPLength,
        int* checksum)
    {
        int index = blockIdx.x * blockDim.x + threadIdx.x;
        int stride = blockDim.x * gridDim.x;
        int totalLength = pointLength + lastFPLength;
        int localChecksum = 0;

        for (int i = index; i < totalLength; i += stride)
        {
            if (i < pointLength)
            {
                localChecksum += cudaPoints[i];
            }
            else
            {
                localChecksum += lastFP[i - pointLength];
            }
        }

        atomicAdd(checksum, localChecksum);
    }

    int errorCode(cudaError_t status)
    {
        return -1000 - static_cast<int>(status);
    }
}

extern "C" __declspec(dllexport) int Calpath_AcceptPoints(
    const int* cudaPoints,
    int length,
    const int* lastFP,
    int lastFPLength)
{
    if (length < 0 || length % 3 != 0 || lastFPLength < 0)
    {
        return -1;
    }

    int totalLength = length + lastFPLength;

    if (totalLength == 0)
    {
        return 0;
    }

    if (length > 0 && cudaPoints == nullptr)
    {
        return -2;
    }

    if (lastFPLength > 0 && lastFP == nullptr)
    {
        return -3;
    }

    int* devicePoints = nullptr;
    int* deviceLastFP = nullptr;
    int* deviceChecksum = nullptr;
    int hostChecksum = 0;
    cudaError_t status = cudaSetDevice(0);

    if (status != cudaSuccess)
    {
        fprintf(stderr, "cudaSetDevice failed: %s\n", cudaGetErrorString(status));
        return errorCode(status);
    }

    if (length > 0)
    {
        status = cudaMalloc(reinterpret_cast<void**>(&devicePoints), length * sizeof(int));
        if (status != cudaSuccess)
        {
            fprintf(stderr, "cudaMalloc devicePoints failed: %s\n", cudaGetErrorString(status));
            goto Error;
        }

        status = cudaMemcpy(devicePoints, cudaPoints, length * sizeof(int), cudaMemcpyHostToDevice);
        if (status != cudaSuccess)
        {
            fprintf(stderr, "cudaMemcpy cudaPoints failed: %s\n", cudaGetErrorString(status));
            goto Error;
        }
    }

    if (lastFPLength > 0)
    {
        status = cudaMalloc(reinterpret_cast<void**>(&deviceLastFP), lastFPLength * sizeof(int));
        if (status != cudaSuccess)
        {
            fprintf(stderr, "cudaMalloc deviceLastFP failed: %s\n", cudaGetErrorString(status));
            goto Error;
        }

        status = cudaMemcpy(deviceLastFP, lastFP, lastFPLength * sizeof(int), cudaMemcpyHostToDevice);
        if (status != cudaSuccess)
        {
            fprintf(stderr, "cudaMemcpy lastFP failed: %s\n", cudaGetErrorString(status));
            goto Error;
        }
    }

    status = cudaMalloc(reinterpret_cast<void**>(&deviceChecksum), sizeof(int));
    if (status != cudaSuccess)
    {
        fprintf(stderr, "cudaMalloc deviceChecksum failed: %s\n", cudaGetErrorString(status));
        goto Error;
    }

    status = cudaMemset(deviceChecksum, 0, sizeof(int));
    if (status != cudaSuccess)
    {
        fprintf(stderr, "cudaMemset deviceChecksum failed: %s\n", cudaGetErrorString(status));
        goto Error;
    }

    {
        int blockCount = (totalLength + ThreadCount - 1) / ThreadCount;
        if (blockCount < 1)
        {
            blockCount = 1;
        }
        else if (blockCount > MaxBlockCount)
        {
            blockCount = MaxBlockCount;
        }

        checksumKernel<<<blockCount, ThreadCount>>>(
            devicePoints,
            length,
            deviceLastFP,
            lastFPLength,
            deviceChecksum);
    }

    status = cudaGetLastError();
    if (status != cudaSuccess)
    {
        fprintf(stderr, "checksumKernel launch failed: %s\n", cudaGetErrorString(status));
        goto Error;
    }

    status = cudaDeviceSynchronize();
    if (status != cudaSuccess)
    {
        fprintf(stderr, "cudaDeviceSynchronize failed: %s\n", cudaGetErrorString(status));
        goto Error;
    }

    status = cudaMemcpy(&hostChecksum, deviceChecksum, sizeof(int), cudaMemcpyDeviceToHost);
    if (status != cudaSuccess)
    {
        fprintf(stderr, "cudaMemcpy checksum failed: %s\n", cudaGetErrorString(status));
        goto Error;
    }

    cudaFree(devicePoints);
    cudaFree(deviceLastFP);
    cudaFree(deviceChecksum);
    return hostChecksum;

Error:
    cudaFree(devicePoints);
    cudaFree(deviceLastFP);
    cudaFree(deviceChecksum);
    return errorCode(status);
}
