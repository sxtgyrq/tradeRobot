#pragma once

#include "cuda_runtime.h"

class Cal
{
public:
    Cal(
        const int* cudaPoints,
        int cudaPointLength,
        const int* connect,
        int connectLength,
        const int* lastFP,
        int lastFPLength);
    ~Cal();

    int StatusCode() const;
    const char* StatusMessage() const;

    int* DeviceCudaPoints() const;
    int CudaPointLength() const;
    int PointCount() const;

    int* DeviceConnect() const;
    int ConnectLength() const;

    int* DeviceLastFP() const;
    int LastFPLength() const;
    int UnitCount() const;

    unsigned long long* DeviceDistance() const;
    int* DeviceChanged() const;

    int TotalLength() const;
    int CopyLastFPToHost(int* target, int length) const;

private:
    int CopyToDevice(const int* source, int length, int** target, const char* name);
    int AllocateDeviceBuffer(int length, int** target, const char* name);
    int AllocateDeviceBuffer(int length, unsigned long long** target, const char* name);
    void SetCudaError(cudaError_t status, const char* message);
    void Release();

    int cudaPointLengthValue = 0;
    int pointCountValue = 0;
    int connectLengthValue = 0;
    int lastFPLengthValue = 0;
    int unitCountValue = 0;
    int statusCodeValue = 0;
    const char* statusMessageValue = nullptr;

    int* cudaPointsGpu = nullptr;
    int* connectGpu = nullptr;
    int* lastFPGpu = nullptr;
    unsigned long long* distanceGpu = nullptr;
    int* changedGpu = nullptr;
};
