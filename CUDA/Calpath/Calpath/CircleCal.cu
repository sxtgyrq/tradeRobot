#include "CircleCal.cuh"

#include <stdio.h>

namespace
{
    int cudaErrorCode(cudaError_t status)
    {
        return -1000 - static_cast<int>(status);
    }
}

Cal::Cal(
    const int* cudaPoints,
    int cudaPointLength,
    const int* connect,
    int connectLength,
    const int* lastFP,
    int lastFPLength)
{
    cudaPointLengthValue = cudaPointLength;
    connectLengthValue = connectLength;
    lastFPLengthValue = lastFPLength;

    if (cudaPointLengthValue < 0 || connectLengthValue < 0 || lastFPLengthValue < 0)
    {
        statusCodeValue = -1;
        statusMessageValue = "negative length";
        return;
    }

    if (cudaPointLengthValue % 3 != 0)
    {
        statusCodeValue = -2;
        statusMessageValue = "cuda point length must be a multiple of 3";
        return;
    }

    pointCountValue = cudaPointLengthValue / 3;

    if (connectLengthValue != pointCountValue)
    {
        statusCodeValue = -3;
        statusMessageValue = "connect length must equal point count";
        return;
    }

    if (pointCountValue == 0)
    {
        if (lastFPLengthValue != 0)
        {
            statusCodeValue = -4;
            statusMessageValue = "lastFP must be empty when point count is zero";
        }

        return;
    }

    if (lastFPLengthValue % pointCountValue != 0)
    {
        statusCodeValue = -5;
        statusMessageValue = "lastFP length must be point count multiplied by unit count";
        return;
    }

    unitCountValue = lastFPLengthValue / pointCountValue;

    cudaError_t status = cudaSetDevice(0);
    if (status != cudaSuccess)
    {
        SetCudaError(status, "cudaSetDevice failed");
        return;
    }

    if (CopyToDevice(cudaPoints, cudaPointLengthValue, &cudaPointsGpu, "cudaPoints") != 0)
    {
        return;
    }

    if (CopyToDevice(connect, connectLengthValue, &connectGpu, "connect") != 0)
    {
        return;
    }

    if (CopyToDevice(lastFP, lastFPLengthValue, &lastFPGpu, "lastFP") != 0)
    {
        return;
    }

    if (AllocateDeviceBuffer(lastFPLengthValue, &distanceGpu, "distance") != 0)
    {
        return;
    }

    if (AllocateDeviceBuffer(1, &changedGpu, "changed") != 0)
    {
        return;
    }
}

Cal::~Cal()
{
    Release();
}

int Cal::StatusCode() const
{
    return statusCodeValue;
}

const char* Cal::StatusMessage() const
{
    return statusMessageValue;
}

int* Cal::DeviceCudaPoints() const
{
    return cudaPointsGpu;
}

int Cal::CudaPointLength() const
{
    return cudaPointLengthValue;
}

int Cal::PointCount() const
{
    return pointCountValue;
}

int* Cal::DeviceConnect() const
{
    return connectGpu;
}

int Cal::ConnectLength() const
{
    return connectLengthValue;
}

int* Cal::DeviceLastFP() const
{
    return lastFPGpu;
}

int Cal::LastFPLength() const
{
    return lastFPLengthValue;
}

int Cal::UnitCount() const
{
    return unitCountValue;
}

unsigned long long* Cal::DeviceDistance() const
{
    return distanceGpu;
}

int* Cal::DeviceChanged() const
{
    return changedGpu;
}

int Cal::TotalLength() const
{
    return cudaPointLengthValue + connectLengthValue + lastFPLengthValue;
}

int Cal::CopyLastFPToHost(int* target, int length) const
{
    if (length != lastFPLengthValue)
    {
        fprintf(stderr, "lastFP output length does not match input length.\n");
        return -6;
    }

    if (length == 0)
    {
        return 0;
    }

    if (target == nullptr)
    {
        fprintf(stderr, "lastFP output target must not be null.\n");
        return -7;
    }

    cudaError_t status = cudaMemcpy(
        target,
        lastFPGpu,
        static_cast<size_t>(length) * sizeof(int),
        cudaMemcpyDeviceToHost);

    if (status != cudaSuccess)
    {
        fprintf(stderr, "cudaMemcpy lastFP result failed: %s\n", cudaGetErrorString(status));
        return cudaErrorCode(status);
    }

    return 0;
}

int Cal::CopyToDevice(const int* source, int length, int** target, const char* name)
{
    if (AllocateDeviceBuffer(length, target, name) != 0)
    {
        return statusCodeValue;
    }

    if (length == 0)
    {
        return 0;
    }

    if (source == nullptr)
    {
        statusCodeValue = -7;
        statusMessageValue = "null host pointer";
        fprintf(stderr, "%s must not be null when length is greater than 0.\n", name);
        return statusCodeValue;
    }

    cudaError_t status = cudaMemcpy(
        *target,
        source,
        static_cast<size_t>(length) * sizeof(int),
        cudaMemcpyHostToDevice);

    if (status != cudaSuccess)
    {
        SetCudaError(status, "cudaMemcpy failed");
        fprintf(stderr, "cudaMemcpy %s failed: %s\n", name, cudaGetErrorString(status));
        return statusCodeValue;
    }

    return 0;
}

int Cal::AllocateDeviceBuffer(int length, int** target, const char* name)
{
    *target = nullptr;

    if (length < 0)
    {
        statusCodeValue = -1;
        statusMessageValue = "negative length";
        fprintf(stderr, "%s length must not be negative.\n", name);
        return statusCodeValue;
    }

    if (length == 0)
    {
        return 0;
    }

    cudaError_t status = cudaMalloc(
        reinterpret_cast<void**>(target),
        static_cast<size_t>(length) * sizeof(int));

    if (status != cudaSuccess)
    {
        SetCudaError(status, "cudaMalloc failed");
        fprintf(stderr, "cudaMalloc %s failed: %s\n", name, cudaGetErrorString(status));
        return statusCodeValue;
    }

    return 0;
}

int Cal::AllocateDeviceBuffer(int length, unsigned long long** target, const char* name)
{
    *target = nullptr;

    if (length < 0)
    {
        statusCodeValue = -1;
        statusMessageValue = "negative length";
        fprintf(stderr, "%s length must not be negative.\n", name);
        return statusCodeValue;
    }

    if (length == 0)
    {
        return 0;
    }

    cudaError_t status = cudaMalloc(
        reinterpret_cast<void**>(target),
        static_cast<size_t>(length) * sizeof(unsigned long long));

    if (status != cudaSuccess)
    {
        SetCudaError(status, "cudaMalloc failed");
        fprintf(stderr, "cudaMalloc %s failed: %s\n", name, cudaGetErrorString(status));
        return statusCodeValue;
    }

    return 0;
}

void Cal::SetCudaError(cudaError_t status, const char* message)
{
    statusCodeValue = cudaErrorCode(status);
    statusMessageValue = message;
}

void Cal::Release()
{
    cudaFree(cudaPointsGpu);
    cudaFree(connectGpu);
    cudaFree(lastFPGpu);
    cudaFree(distanceGpu);
    cudaFree(changedGpu);

    cudaPointsGpu = nullptr;
    connectGpu = nullptr;
    lastFPGpu = nullptr;
    distanceGpu = nullptr;
    changedGpu = nullptr;
}
