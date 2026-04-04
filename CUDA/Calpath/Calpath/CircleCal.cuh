#include "cuda_runtime.h"
#include "device_launch_parameters.h"

class Cal
{
public:
	Cal(int* cudaPoints, int costTimeCount, int* lastFP, int fPCount, int calUnitCount, int* startDic, int* endDic);

	//int[] costTime, int[] lastFP, int[] resultForSave, int costTimeCount, int FPCount, int[] startDic, int[] endDic
	//int* Cal();
	~Cal();
private:
	//int CostTimeCount;

	int PointCount;

	int UnitCount;

	int PointCountPerUnit;
	int SegCountPerUnit;

	int BlockCount;//初始化√

	int* CostTime;
	int* CostTime_GPU = 0;

	int* LastFP;
	int* LastFP_GPU = 0;
	int* LastFP_Out_GPU = 0;

	int* StartDic;
	int* StartDic_GPU = 0;

	int* EndDic;
	int* EndDic_GPU = 0;

	int* MinStepResult_GPU;//初始化√
	//int* MinStepResult_CALMINVALUE_GPU;//初始化√

	int* MinStepResult_OnOff_GPU;//初始化√
	//int* MinStepResult;//初始化√

	cudaError_t CalculateMinStep();
	cudaError_t FindMin();
	cudaError_t Reduce();
	cudaError_t	Copy();
	bool NotFinished();
public:
	int* LastFPResult;
};