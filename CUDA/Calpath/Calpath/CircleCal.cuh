#pragma once
#include "cuda_runtime.h"
#include "device_launch_parameters.h"

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


	int TotalLength() const;
	int CopyLastFPToHost(int* target, int length) const;

	void loopAndFindPath();

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
	int* lastFPOutGpu = nullptr;
	int* passedLengthGPU = nullptr;
	int* finishedStateGpu = nullptr;
	int* unitFinishedGpu = nullptr;
	int* hostUnitFinished = nullptr;

	int BlockCount;//初始化√


	cudaError_t CalculateMinStep(int* minStepResultGpu, int* minStepResultOnOffGpu);
	cudaError_t FindMin(int* minStepResultGpu);
	cudaError_t Reduce(int* minStepResultGpu, int* minStepResultOnOffGpu);
	cudaError_t Copy();
	bool NotFinished();
};
