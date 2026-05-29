#include "CircleCal.cuh"

#include <new>
#include <stdio.h>



const int ThreadCount = 512;
const int MaxValue = 2147483647;
const int CandidateEdgeCountPerPoint = 2;
const int TerminalLinkPointCode = 5;
const bool SynchronizeEveryKernel = true;

const bool ShowNotFinishedProgress = false;

// HM_6 风格的第一阶段：扫描每个 unit 里已经到达的点，计算本轮能继续推进的同圆距离。
// 本 kernel 不直接改 lastFP，只把每个位置的候选推进长度写入 minStepResult。
// minStepResultOnOff 用来冻结“本轮开始时就有效”的边，避免 Reduce 中新到达的点在同一轮继续扩展。
__global__ void getMinStepFF(
	int pointCountValue,
	int lastFPLengthValue,
	const int* cudaPoints,
	const int* lastFP,
	int* minStepResult,
	int* minStepResultOnOff,
	int* passedLength)
{
	// i 是 lastFP 展开表的全局下标：
	// i = unitIndex * pointCountValue + cudaPointIndex。
	int i = blockIdx.x * blockDim.x + threadIdx.x;
	if (i >= lastFPLengthValue)
	{
	}
	else
	{
		minStepResult[i] = MaxValue;
		minStepResultOnOff[i] = 0;

		// 当前点还没到达，就不能从它继续向后推进。
		if (lastFP[i] == -1)
		{
			minStepResult[i] = MaxValue;
		}
		// 同圆推进只检查当前点后面的一个 CUDA 点。
		else if (i + 1 < lastFPLengthValue)
		{
			minStepResult[i] = MaxValue;

			// pointIndex 是当前 unit 内的 CUDA 点序号，不是圆上的 pathPointIndex。
			int pointIndex = i % pointCountValue;

			if (pointIndex + 1 >= pointCountValue)
			{

			}
			else {
				// cudaPoints 每 3 个 int 表示一个点：[circleIndex, pathPointIndex, pointType]。
				int nextCircleIndex = (pointIndex + 1) * 3;
				int currentCircleIndex = pointIndex * 3;

				// 排序后的相邻 CUDA 点必须属于同一个圆，才能按圆内路径继续走。
				if (cudaPoints[nextCircleIndex] == cudaPoints[currentCircleIndex])
				{
					if (lastFP[i + 1] == -1)
					{
						// 同圆相邻点的距离，用圆内 pathPointIndex 的差值计算。
						minStepResult[i] = cudaPoints[nextCircleIndex + 1] - cudaPoints[currentCircleIndex + 1];
						// passedLength[i] 是当前边已经累计推进的长度，本轮只需要补剩余长度。
						minStepResult[i] = minStepResult[i] - passedLength[i];
						if (minStepResult[i] <= 0)
						{
							minStepResult[i] = MaxValue;
						}
						else
						{
							minStepResultOnOff[i] = 1;
						}
					}
				}
			}
		}
		else
		{
			minStepResult[i] = MaxValue;
		}
	}

}

__global__ void FindMinOfMinTimeStepResult(int length, int segCountPerUnit, int step, int* minStepResult)
{
	int i = blockIdx.x * blockDim.x + threadIdx.x;

	if (i < length)
	{
		int columnIndex = i % segCountPerUnit;
		if (columnIndex % (step << 1) == 0) {
			if (columnIndex + step < segCountPerUnit)
			{
				minStepResult[i] =
					minStepResult[i] < minStepResult[i + step] ? minStepResult[i] : minStepResult[i + step];
			}
		}
	}
}

__global__ void CopyMinStepResult(int length, const int* source, int* target)
{
	int i = blockIdx.x * blockDim.x + threadIdx.x;

	if (i < length)
	{
		target[i] = source[i];
	}
}

__global__ void BuildTradingPointFinishedState(
	int length,
	int pointCountValue,
	const int* cudaPoints,
	const int* lastFP,
	int* finishedState)
{
	int i = blockIdx.x * blockDim.x + threadIdx.x;

	if (i < length)
	{
		// cudaPoints 只有一份，多个 unit 共用；所以用 i % pointCountValue 回到点表下标。
		int pointIndex = i % pointCountValue;

		int pointType = cudaPoints[pointIndex * 3 + 2];

		// 交易点 = 收割点或采购点。NotFinished 只判断这些点是否全部到达。
		bool isTradingPoint = pointType % 2 == 0 || pointType % 11 == 0;

		finishedState[i] = isTradingPoint ? lastFP[i] : 0;
	}
}

// 对每个 unit 内的 finishedState 做最小值归约。
// 只要某个交易点仍为 -1，归约后 unitBase 位置仍会是 -1。
__global__ void FindMinOfIndexOfFPStepResult(int length, int step, int pointCountPerUnit, int* lastFPOut)
{
	int i = blockIdx.x * blockDim.x + threadIdx.x;

	if (i < length)
	{
		int columnIndex = i % pointCountPerUnit;

		// 只在同一个 unit 内做成对归约，不跨 unit。
		if (columnIndex % (step << 1) == 0)
		{
			if (columnIndex + step < pointCountPerUnit)
			{
				lastFPOut[i] =
					lastFPOut[i] < lastFPOut[i + step] ? lastFPOut[i] : lastFPOut[i + step];
			}
		}
	}
}

// 把每个 unit 的 unitBase 完成状态收集成连续数组。
// CPU 只需要读取 unitCount 个 int，就能判断所有起点组是否完成。
__global__ void CopyUnitFinishedState(
	int unitCount,
	int pointCountPerUnit,
	const int* finishedState,
	int* unitFinishedState)
{
	int unitIndex = blockIdx.x * blockDim.x + threadIdx.x;
	if (unitIndex < unitCount)
	{
		unitFinishedState[unitIndex] = finishedState[unitIndex * pointCountPerUnit];
	}
}

// HM_6 风格的 Reduce 阶段：根据每个 unit 本轮的最小推进长度，推进所有本轮有效边。
// 只处理 minStepResultOnOff[i] == 1 的位置，保证“本轮新到达的点”不会在同一轮继续扩展。
__global__ void getReduceF(
	int pointCountValue,
	int lastFPLengthValue,
	const int* cudaPoints,
	const int* connect,
	int* lastFP,
	const int* minStepResult,
	const int* minStepResultOnOff,
	int* passedLength)
{
	int i = blockIdx.x * blockDim.x + threadIdx.x;

	if (i >= lastFPLengthValue)
	{
		return;
	}

	if (minStepResultOnOff[i] != 1)
	{
		return;
	}

	// 冻结有效边后仍做一次防御判断：源点未到达就不能推进。
	if (lastFP[i] == -1)
	{
		return;
	}

	if (i + 1 >= lastFPLengthValue)
	{
		return;
	}

	int pointIndex = i % pointCountValue;
	int unitBase = i - pointIndex;

	// 同圆路径只允许从当前 CUDA 点走到本 unit 内的下一个 CUDA 点。
	if (pointIndex + 1 >= pointCountValue)
	{
		return;
	}

	if (lastFP[i + 1] != -1)
	{
		return;
	}

	int nextCircleIndex = (pointIndex + 1) * 3;
	int currentCircleIndex = pointIndex * 3;

	if (cudaPoints[nextCircleIndex] != cudaPoints[currentCircleIndex])
	{
		return;
	}

	int edgeLength = cudaPoints[nextCircleIndex + 1] - cudaPoints[currentCircleIndex + 1];
	int stepLength = minStepResult[unitBase];

	// MaxValue 表示本轮没有可推进的真实长度，不能参与 passedLength 累加。
	if (edgeLength <= 0 || passedLength[i] < 0 || passedLength[i] == MaxValue || stepLength == MaxValue)
	{
		passedLength[i] = MaxValue;
		return;
	}

	int remainingLength = edgeLength - passedLength[i];
	if (remainingLength < 0)
	{
		passedLength[i] = MaxValue;
		return;
	}

	int leftLength = remainingLength - stepLength;

	if (stepLength != MaxValue && passedLength[i] > MaxValue - stepLength)
	{
		passedLength[i] = MaxValue;
		return;
	}

	if (leftLength == 0)
	{
		// 正好走完这条同圆边时，标记下一个点的上一路口。
		int oldNextFP = atomicCAS(&lastFP[i + 1], -1, pointIndex);
		passedLength[i] += stepLength;

		if (oldNextFP == -1)
		{
			// 如果下一个点有联通关系，则把联通目标也标记为到达。
			int another = connect[pointIndex + 1];
			if (another != -1)
			{
				atomicCAS(&lastFP[unitBase + another], -1, pointIndex + 1);
			}
		}
	}
	else
	{
		passedLength[i] += stepLength;
	}
}
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

	this->BlockCount = (this->lastFPLengthValue + ThreadCount - 1) / ThreadCount;

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

	if (AllocateDeviceBuffer(lastFPLengthValue, &lastFPOutGpu, "lastFPOut") != 0)
	{
		return;
	}

	if (AllocateDeviceBuffer(lastFPLengthValue, &passedLengthGPU, "passedLength") != 0)
	{
		return;
	}

	// NotFinished 每轮都要用 finishedStateGpu。这里一次分配，后续循环复用。
	if (AllocateDeviceBuffer(lastFPLengthValue, &finishedStateGpu, "finishedState") != 0)
	{
		return;
	}

	// unitFinishedGpu 只保存每个 unit 的归约结果，避免每轮把整张 finishedState 拷回 CPU。
	if (AllocateDeviceBuffer(unitCountValue, &unitFinishedGpu, "unitFinished") != 0)
	{
		return;
	}

	// CPU 端同样只缓存 unitCountValue 个结果。
	hostUnitFinished = new (std::nothrow) int[unitCountValue];
	if (hostUnitFinished == nullptr)
	{
		statusCodeValue = -21;
		statusMessageValue = "NotFinished host allocation failed";
		fprintf(stderr, "NotFinished host allocation failed: unitCountValue=%d.\n", unitCountValue);
		return;
	}

	status = cudaMemset(
		passedLengthGPU,
		0,
		static_cast<size_t>(lastFPLengthValue) * sizeof(int));
	if (status != cudaSuccess)
	{
		SetCudaError(status, "cudaMemset passedLength failed");
		fprintf(stderr, "cudaMemset passedLength failed: %s\n", cudaGetErrorString(status));
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
	cudaFree(lastFPOutGpu);
	cudaFree(passedLengthGPU);
	cudaFree(finishedStateGpu);
	cudaFree(unitFinishedGpu);
	delete[] hostUnitFinished;

	cudaPointsGpu = nullptr;
	connectGpu = nullptr;
	lastFPGpu = nullptr;
	lastFPOutGpu = nullptr;
	passedLengthGPU = nullptr;
	finishedStateGpu = nullptr;
	unitFinishedGpu = nullptr;
	hostUnitFinished = nullptr;
}



cudaError_t Cal::FindMin(int* minStepResultGpu)
{
	cudaError_t cudaStatus = cudaSuccess;
	int step = 1;
	while (step < this->pointCountValue)
	{

		FindMinOfMinTimeStepResult << <this->BlockCount, ThreadCount >> > (
			this->lastFPLengthValue,
			this->pointCountValue,
			step,
			minStepResultGpu);
		cudaStatus = cudaGetLastError();
		if (cudaStatus != cudaSuccess) {
			fprintf(stderr, "FindMinOfMinStepResult launch failed: %s\n", cudaGetErrorString(cudaStatus));
			goto Error;
		}
		if (SynchronizeEveryKernel)
		{
			cudaStatus = cudaDeviceSynchronize();
			if (cudaStatus != cudaSuccess) {
				fprintf(stderr, "cudaDeviceSynchronize returned error code %d after launching FindMinOfMinStepResult!\n", cudaStatus);
				goto Error;
			}
		}
		step = step << 1;
	}
	return cudaStatus;
Error:
	return cudaStatus;
}

cudaError_t Cal::Reduce(int* minStepResultGpu, int* minStepResultOnOffGpu)
{
	int blockCount = (this->lastFPLengthValue + ThreadCount - 1) / ThreadCount;

	getReduceF << <blockCount, ThreadCount >> > (
		this->pointCountValue,
		this->lastFPLengthValue,
		this->cudaPointsGpu,
		this->connectGpu,
		this->lastFPGpu,
		minStepResultGpu,
		minStepResultOnOffGpu,
		this->passedLengthGPU);

	cudaError_t cudaStatus = cudaGetLastError();
	if (cudaStatus != cudaSuccess)
	{
		fprintf(stderr, "getReduceF launch failed: %s\n", cudaGetErrorString(cudaStatus));
		return cudaStatus;
	}

	if (SynchronizeEveryKernel)
	{
		cudaStatus = cudaDeviceSynchronize();
		if (cudaStatus != cudaSuccess)
		{
			fprintf(stderr, "getReduceF synchronize failed: %s\n", cudaGetErrorString(cudaStatus));
		}
	}

	return cudaStatus;
}

cudaError_t Cal::Copy()
{
	int blockCount = this->BlockCount;

	CopyMinStepResult << <blockCount, ThreadCount >> > (
		this->lastFPLengthValue,
		this->lastFPGpu,
		this->lastFPOutGpu);

	cudaError_t cudaStatus = cudaGetLastError();
	if (cudaStatus != cudaSuccess)
	{
		fprintf(stderr, "CopyMinStepResult launch failed: %s\n", cudaGetErrorString(cudaStatus));
		return cudaStatus;
	}

	if (SynchronizeEveryKernel)
	{
		cudaStatus = cudaDeviceSynchronize();
		if (cudaStatus != cudaSuccess)
		{
			fprintf(stderr, "CopyMinStepResult synchronize failed: %s\n", cudaGetErrorString(cudaStatus));
		}
	}

	return cudaStatus;
}

bool Cal::NotFinished()
{
	// 这里出现非法长度不是“已经完成”，而是调用前置状态错误。
	if (this->lastFPLengthValue <= 0 || this->pointCountValue <= 0 || this->cudaPointLengthValue <= 0)
	{
		fprintf(stderr,
			"NotFinished invalid length: lastFPLengthValue=%d, pointCountValue=%d, cudaPointLengthValue=%d.\n",
			this->lastFPLengthValue,
			this->pointCountValue,
			this->cudaPointLengthValue);
		statusCodeValue = -20;
		statusMessageValue = "NotFinished invalid length";
		return true;
	}

	// 这些缓存由构造函数创建，NotFinished 只复用，不在每轮分配释放。
	if (this->finishedStateGpu == nullptr || this->unitFinishedGpu == nullptr || this->hostUnitFinished == nullptr)
	{
		fprintf(stderr, "NotFinished cached buffers are not initialized.\n");
		statusCodeValue = -22;
		statusMessageValue = "NotFinished cached buffers are not initialized";
		return true;
	}

	int blockCount = (this->lastFPLengthValue + ThreadCount - 1) / ThreadCount;
	BuildTradingPointFinishedState << <blockCount, ThreadCount >> > (
		this->lastFPLengthValue,
		this->pointCountValue,
		this->cudaPointsGpu,
		this->lastFPOutGpu,
		this->finishedStateGpu);

	cudaError_t cudaStatus = cudaGetLastError();
	if (cudaStatus != cudaSuccess)
	{
		fprintf(stderr, "BuildTradingPointFinishedState launch failed: %s\n", cudaGetErrorString(cudaStatus));
		SetCudaError(cudaStatus, "BuildTradingPointFinishedState launch failed");
		return true;
	}

	if (SynchronizeEveryKernel)
	{
		cudaStatus = cudaDeviceSynchronize();
		if (cudaStatus != cudaSuccess)
		{
			fprintf(stderr, "BuildTradingPointFinishedState synchronize failed: %s\n", cudaGetErrorString(cudaStatus));
			SetCudaError(cudaStatus, "BuildTradingPointFinishedState synchronize failed");
			return true;
		}
	}

	int step = 1;
	while (step < this->pointCountValue)
	{
		FindMinOfIndexOfFPStepResult << <blockCount, ThreadCount >> > (
			this->lastFPLengthValue,
			step,
			this->pointCountValue,
			this->finishedStateGpu);

		cudaStatus = cudaGetLastError();
		if (cudaStatus != cudaSuccess)
		{
			fprintf(stderr, "FindMinOfIndexOfFPStepResult launch failed: %s\n", cudaGetErrorString(cudaStatus));
			SetCudaError(cudaStatus, "FindMinOfIndexOfFPStepResult launch failed");
			return true;
		}

		if (SynchronizeEveryKernel)
		{
			cudaStatus = cudaDeviceSynchronize();
			if (cudaStatus != cudaSuccess)
			{
				fprintf(stderr, "FindMinOfIndexOfFPStepResult synchronize failed: %s\n", cudaGetErrorString(cudaStatus));
				SetCudaError(cudaStatus, "FindMinOfIndexOfFPStepResult synchronize failed");
				return true;
			}
		}

		step = step << 1;
	}

	int unitBlockCount = (this->unitCountValue + ThreadCount - 1) / ThreadCount;
	CopyUnitFinishedState << <unitBlockCount, ThreadCount >> > (
		this->unitCountValue,
		this->pointCountValue,
		this->finishedStateGpu,
		this->unitFinishedGpu);

	cudaStatus = cudaGetLastError();
	if (cudaStatus != cudaSuccess)
	{
		fprintf(stderr, "CopyUnitFinishedState launch failed: %s\n", cudaGetErrorString(cudaStatus));
		SetCudaError(cudaStatus, "CopyUnitFinishedState launch failed");
		return true;
	}

	cudaStatus = cudaMemcpy(
		this->hostUnitFinished,
		this->unitFinishedGpu,
		static_cast<size_t>(this->unitCountValue) * sizeof(int),
		cudaMemcpyDeviceToHost);

	if (cudaStatus != cudaSuccess)
	{
		fprintf(stderr, "cudaMemcpy unitFinished failed: %s\n", cudaGetErrorString(cudaStatus));
		SetCudaError(cudaStatus, "cudaMemcpy unitFinished failed");
		return true;
	}

	if (ShowNotFinishedProgress)
	{
		// 调试开关打开时才把 lastFPOut 和 cudaPoints 拷回 CPU 统计明细。
		// 生产阶段 ShowNotFinishedProgress=false，不走这段昂贵逻辑。
		int* hLastFPOut = new (std::nothrow) int[this->lastFPLengthValue];
		int* hCudaPoints = new (std::nothrow) int[this->cudaPointLengthValue];
		if (hLastFPOut == nullptr || hCudaPoints == nullptr)
		{
			fprintf(stderr,
				"NotFinished progress allocation failed: lastFPLengthValue=%d, cudaPointLengthValue=%d.\n",
				this->lastFPLengthValue,
				this->cudaPointLengthValue);
		}
		else
		{
			cudaError_t progressStatus = cudaMemcpy(
				hLastFPOut,
				this->lastFPOutGpu,
				static_cast<size_t>(this->lastFPLengthValue) * sizeof(int),
				cudaMemcpyDeviceToHost);
			if (progressStatus != cudaSuccess)
			{
				fprintf(stderr, "cudaMemcpy lastFPOut progress failed: %s\n", cudaGetErrorString(progressStatus));
			}
			else
			{
				progressStatus = cudaMemcpy(
					hCudaPoints,
					this->cudaPointsGpu,
					static_cast<size_t>(this->cudaPointLengthValue) * sizeof(int),
					cudaMemcpyDeviceToHost);
				if (progressStatus != cudaSuccess)
				{
					fprintf(stderr, "cudaMemcpy cudaPoints progress failed: %s\n", cudaGetErrorString(progressStatus));
				}
				else
				{
					for (int unitIndex = 0; unitIndex < this->unitCountValue; unitIndex++)
					{
						int unitBase = unitIndex * this->pointCountValue;
						int totalTradingPointCount = 0;
						int finishedTradingPointCount = 0;

						for (int pointIndex = 0; pointIndex < this->pointCountValue; pointIndex++)
						{
							int pointType = hCudaPoints[pointIndex * 3 + 2];
							bool isTradingPoint = pointType % 2 == 0 || pointType % 11 == 0;
							if (!isTradingPoint)
							{
								continue;
							}

							totalTradingPointCount++;
							if (hLastFPOut[unitBase + pointIndex] != -1)
							{
								finishedTradingPointCount++;
							}
						}

						printf(
							"NotFinished group %d/%d finished trading points: %d/%d.\n",
							unitIndex + 1,
							this->unitCountValue,
							finishedTradingPointCount,
							totalTradingPointCount);
						fflush(stdout);
					}
				}
			}
		}

		delete[] hLastFPOut;
		delete[] hCudaPoints;
	}
	else
	{
	}

	bool notFinished = false;
	for (int unitIndex = 0; unitIndex < this->unitCountValue; unitIndex++)
	{
		// -1 表示这个 unit 里至少还有一个交易点未到达。
		if (this->hostUnitFinished[unitIndex] == -1)
		{
			notFinished = true;
			break;
		}
	}

	return notFinished;
}
cudaError_t Cal::CalculateMinStep(int* minStepResultGpu, int* minStepResultOnOffGpu)
{
	// 第一阶段：计算每个已到达点继续走到同圆下一个点还差多少长度。
	// 第二阶段：FindMin 把每个 unit 内的最小推进长度归约到 unitBase。
	getMinStepFF << <this->BlockCount, ThreadCount >> >
		(
			this->pointCountValue,
			this->lastFPLengthValue,
			this->cudaPointsGpu,
			this->lastFPGpu,
			minStepResultGpu,
			minStepResultOnOffGpu,
			this->passedLengthGPU);

	cudaError_t cudaStatus = cudaGetLastError();
	if (cudaStatus != cudaSuccess)
	{
		fprintf(stderr, "getMinStepFF launch failed: %s\n", cudaGetErrorString(cudaStatus));
		SetCudaError(cudaStatus, "getMinStepFF launch failed");
		return cudaStatus;
	}

	if (SynchronizeEveryKernel)
	{
		cudaStatus = cudaDeviceSynchronize();
		if (cudaStatus != cudaSuccess)
		{
			fprintf(stderr, "getMinStepFF synchronize failed: %s\n", cudaGetErrorString(cudaStatus));
			SetCudaError(cudaStatus, "getMinStepFF synchronize failed");
			return cudaStatus;
		}
	}

	cudaStatus = FindMin(minStepResultGpu);
	if (cudaStatus != cudaSuccess)
	{
		SetCudaError(cudaStatus, "FindMin failed");
		return cudaStatus;
	}

	return cudaStatus;
}

void Cal::loopAndFindPath()
{
	bool shouldContinue = false;
	int* minStepResultGpu = nullptr;
	int* minStepResultOnOffGpu = nullptr;

	// 这两个结果数组长度固定，进入循环前分配一次，避免每轮 cudaMalloc/cudaFree。
	cudaError_t cudaStatus = cudaMalloc(
		reinterpret_cast<void**>(&minStepResultGpu),
		static_cast<size_t>(this->lastFPLengthValue) * sizeof(int));
	if (cudaStatus != cudaSuccess)
	{
		fprintf(stderr, "minStepResult cudaMalloc failed: %s\n", cudaGetErrorString(cudaStatus));
		SetCudaError(cudaStatus, "minStepResult cudaMalloc failed");
		return;
	}

	cudaStatus = cudaMalloc(
		reinterpret_cast<void**>(&minStepResultOnOffGpu),
		static_cast<size_t>(this->lastFPLengthValue) * sizeof(int));
	if (cudaStatus != cudaSuccess)
	{
		fprintf(stderr, "minStepResultOnOff cudaMalloc failed: %s\n", cudaGetErrorString(cudaStatus));
		SetCudaError(cudaStatus, "minStepResultOnOff cudaMalloc failed");
		cudaFree(minStepResultGpu);
		return;
	}

	do
	{
		// HM_6 三段式：CalculateMinStep -> Reduce -> Copy -> NotFinished。
		cudaStatus = CalculateMinStep(minStepResultGpu, minStepResultOnOffGpu);
		if (cudaStatus != cudaSuccess)
		{
			break;
		}

		cudaStatus = Reduce(minStepResultGpu, minStepResultOnOffGpu);
		if (cudaStatus != cudaSuccess)
		{
			SetCudaError(cudaStatus, "Reduce failed");
			break;
		}

		cudaStatus = Copy();
		if (cudaStatus != cudaSuccess)
		{
			SetCudaError(cudaStatus, "Copy failed");
			break;
		}

		shouldContinue = this->NotFinished();
		if (this->statusCodeValue != 0)
		{
			break;
		}
	} while (shouldContinue);

	cudaFree(minStepResultGpu);
	cudaFree(minStepResultOnOffGpu);

	if (this->statusCodeValue == 0)
	{
		cudaError_t cudaStatus = Copy();
		if (cudaStatus != cudaSuccess)
		{
			SetCudaError(cudaStatus, "final Copy failed");
		}
	}
}
