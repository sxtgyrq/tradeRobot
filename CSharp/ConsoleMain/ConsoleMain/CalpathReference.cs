namespace ConsoleMain
{
    internal static class CalpathReference
    {
        private const int MaxValue = int.MaxValue;

        public static int CalculatePaths(int[] cudaPoints, int[] lastFP, int[] connect)
        {
            ArgumentNullException.ThrowIfNull(cudaPoints);
            ArgumentNullException.ThrowIfNull(lastFP);
            ArgumentNullException.ThrowIfNull(connect);

            if (cudaPoints.Length % 3 != 0)
            {
                throw new ArgumentException(
                    "CUDA point array length must be a multiple of 3.",
                    nameof(cudaPoints));
            }

            int pointCount = cudaPoints.Length / 3;
            if (connect.Length != pointCount)
            {
                throw new ArgumentException(
                    "Connect length must equal cudaPoints.Length / 3.",
                    nameof(connect));
            }

            if (pointCount == 0)
            {
                if (lastFP.Length != 0)
                {
                    throw new ArgumentException(
                        "LastFP must be empty when there are no CUDA points.",
                        nameof(lastFP));
                }

                return 0;
            }

            if (lastFP.Length % pointCount != 0)
            {
                throw new ArgumentException(
                    "LastFP length must equal cudaPoints.Length / 3 multiplied by the parallel start point count.",
                    nameof(lastFP));
            }

            int unitCount = lastFP.Length / pointCount;
            if (unitCount == 0)
            {
                return 0;
            }

            int[] passedLength = new int[lastFP.Length];

            while (HasUnfinishedTradingPoint(cudaPoints, lastFP, pointCount, unitCount))
            {
                int[] minStepResult = Enumerable.Repeat(MaxValue, lastFP.Length).ToArray();
                int[] minStepResultOnOff = new int[lastFP.Length];

                CalculateMinStep(
                    cudaPoints,
                    lastFP,
                    passedLength,
                    pointCount,
                    minStepResult,
                    minStepResultOnOff);

                bool changed = Reduce(
                    cudaPoints,
                    connect,
                    lastFP,
                    passedLength,
                    pointCount,
                    unitCount,
                    minStepResult,
                    minStepResultOnOff);

                if (!changed)
                {
                    return 0;
                }
            }

            return 0;
        }

        private static void CalculateMinStep(
            int[] cudaPoints,
            int[] lastFP,
            int[] passedLength,
            int pointCount,
            int[] minStepResult,
            int[] minStepResultOnOff)
        {
            for (int i = 0; i < lastFP.Length; i++)
            {
                minStepResult[i] = MaxValue;
                minStepResultOnOff[i] = 0;

                if (lastFP[i] == -1 || i + 1 >= lastFP.Length)
                {
                    continue;
                }

                int pointIndex = i % pointCount;
                if (pointIndex + 1 >= pointCount)
                {
                    continue;
                }

                if (lastFP[i + 1] != -1)
                {
                    continue;
                }

                int currentOffset = pointIndex * 3;
                int nextOffset = (pointIndex + 1) * 3;
                if (cudaPoints[currentOffset] != cudaPoints[nextOffset])
                {
                    continue;
                }

                int remainingLength = cudaPoints[nextOffset + 1] - cudaPoints[currentOffset + 1] - passedLength[i];
                if (remainingLength <= 0)
                {
                    continue;
                }

                minStepResult[i] = remainingLength;
                minStepResultOnOff[i] = 1;
            }
        }

        private static bool Reduce(
            int[] cudaPoints,
            int[] connect,
            int[] lastFP,
            int[] passedLength,
            int pointCount,
            int unitCount,
            int[] minStepResult,
            int[] minStepResultOnOff)
        {
            bool changed = false;

            for (int unitIndex = 0; unitIndex < unitCount; unitIndex++)
            {
                int unitBase = unitIndex * pointCount;
                int stepLength = MaxValue;

                for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    int offset = unitBase + pointIndex;
                    if (minStepResult[offset] < stepLength)
                    {
                        stepLength = minStepResult[offset];
                    }
                }

                if (stepLength == MaxValue)
                {
                    continue;
                }

                for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    int offset = unitBase + pointIndex;
                    if (minStepResultOnOff[offset] != 1)
                    {
                        continue;
                    }

                    if (lastFP[offset] == -1 || pointIndex + 1 >= pointCount)
                    {
                        continue;
                    }

                    int targetOffset = offset + 1;
                    if (lastFP[targetOffset] != -1)
                    {
                        continue;
                    }

                    int currentPointOffset = pointIndex * 3;
                    int nextPointOffset = (pointIndex + 1) * 3;
                    if (cudaPoints[currentPointOffset] != cudaPoints[nextPointOffset])
                    {
                        continue;
                    }

                    int edgeLength = cudaPoints[nextPointOffset + 1] - cudaPoints[currentPointOffset + 1];
                    int remainingLength = edgeLength - passedLength[offset];
                    int leftLength = remainingLength - stepLength;

                    if (passedLength[offset] > MaxValue - stepLength)
                    {
                        passedLength[offset] = MaxValue;
                        continue;
                    }

                    if (leftLength == 0)
                    {
                        lastFP[targetOffset] = pointIndex;
                        passedLength[offset] += stepLength;
                        changed = true;

                        int another = connect[pointIndex + 1];
                        if (another != -1 && lastFP[unitBase + another] == -1)
                        {
                            lastFP[unitBase + another] = pointIndex + 1;
                            changed = true;
                        }
                    }
                    else
                    {
                        passedLength[offset] += stepLength;
                    }
                }
            }

            return changed;
        }

        private static bool HasUnfinishedTradingPoint(
            int[] cudaPoints,
            int[] lastFP,
            int pointCount,
            int unitCount)
        {
            for (int unitIndex = 0; unitIndex < unitCount; unitIndex++)
            {
                int unitBase = unitIndex * pointCount;

                for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    int pointType = cudaPoints[pointIndex * 3 + 2];
                    if (!IsTradingPoint(pointType))
                    {
                        continue;
                    }

                    if (lastFP[unitBase + pointIndex] == -1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsTradingPoint(int pointType)
        {
            return pointType % CircleGenerator.HarvestPointCode == 0 ||
                   pointType % CircleGenerator.PurchasePointCode == 0;
        }
    }
}
