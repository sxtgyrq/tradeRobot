using System.Globalization;
using System.Text;

namespace ConsoleMain
{
    internal static class CircleGenerator
    {
        public const int DefaultCircleCount = 256;
        public const string CircleFileName = "Circle.bin";
        public const string DxfFileName = "Circle.dxf";

        internal const int MaxX = 1_000_000;
        internal const int MaxY = 1_000_000;
        internal const int MinRadius = 10;
        internal const int PathPointDensityMultiplier = 300;
        /// <summary>
        /// 普通路径点编码。1 是乘法单位元，表示该路径点当前没有额外角色。
        /// </summary>
        internal const int NormalPathPointCode = 1;

        /// <summary>
        /// 全场唯一收割点编码。2 是质数，后续可通过 pointType % 2 == 0 判断该路径点是否为收割点。
        /// </summary>
        internal const int HarvestPointCode = 2;

        /// <summary>
        /// 每个圆的第一路径点编码。第一路径点不是占用型角色，可以与一个占用型角色叠加。
        /// </summary>
        internal const int FirstPathPointCode = 3;

        /// <summary>
        /// 末尾联通点编码，即路径点 n - 1；末尾联通点属于占用型角色。
        /// </summary>
        internal const int TerminalLinkPointCode = 5;

        /// <summary>
        /// 普通联通点编码，即圆间相交或相切关系产生的双向联通点；普通联通点属于占用型角色。
        /// </summary>
        internal const int OrdinaryLinkPointCode = 7;

        /// <summary>
        /// 采购点编码。采购点属于占用型角色，每个采购点最多只能被一个圆占用。
        /// </summary>
        internal const int PurchasePointCode = 11;
        internal static readonly int MaxRadius = Math.Min(MaxX, MaxY);
        private const int MaxAttemptsPerCircle = 100_000;
        private const double TwoPi = Math.PI * 2.0;
        private const double DistanceTolerance = 1e-7;
        private const double IndexTolerance = 1e-10;
        private const double MinimumVisibleConnectLineLength = 0.5;
        private const double ConnectLineEndpointExtension = 0.25;
        private const double OverlappedOrdinaryConnectMarkerRadius = 0.25;
        private const double PurchaseArrowHeadAngle = Math.PI / 7.0;
        private const double MinimumPurchaseArrowHeadLength = 1.0;
        private const double MaximumPurchaseArrowHeadLength = 30_000.0;
        private const double ZeroLengthPurchaseArrowMarkerRadius = 0.25;
        private const int RoutePathColor = 6;

        private static readonly CircleRecord[] SeedCircles =
        {
            new(-400_000, 0, -499_999),
            new(400_000, 0, 499_999),
        };

        /// <summary>
        /// 使用默认 Circle.bin 路径和默认圆数量生成圆数据文件。
        /// 默认输出路径为当前工作目录下的 <see cref="CircleFileName"/>，默认圆数量为 <see cref="DefaultCircleCount"/>。
        /// </summary>
        public static void GenerateCircle()
        {
            GenerateCircle(Path.Combine(Environment.CurrentDirectory, CircleFileName), DefaultCircleCount);
        }

        public static void DrawToDxf()
        {
            DrawToDxf(
                Path.Combine(Environment.CurrentDirectory, CircleFileName),
                Path.Combine(Environment.CurrentDirectory, DxfFileName));
        }

        public static (int CircleIndex, int PathPointIndex) GenerateHarvestPoint()
        {
            return GenerateHarvestPoint(Path.Combine(Environment.CurrentDirectory, CircleFileName));
        }

        public static IReadOnlyList<DerivedPoint> GenerateDerivedPoints()
        {
            return GenerateDerivedPoints(Path.Combine(Environment.CurrentDirectory, CircleFileName));
        }

        /// <summary>
        /// 使用默认圆数据文件生成派生点数据。
        /// 派生点按需求顺序计算：全场唯一收割点、所有圆的第一路径点、所有圆的末尾联通点、所有普通联通点、所有采购点。
        /// 返回结果包含合并后的派生点集合，以及普通联通点之间的圆间双向连通关系。
        /// </summary>
        /// <returns>生成后的派生点数据。</returns>
        public static GeneratedPointData GenerateDerivedPointData()
        {
            return GenerateDerivedPointData(Path.Combine(Environment.CurrentDirectory, CircleFileName));
        }

        public static int[] BuildCudaPointArray(IReadOnlyList<DerivedPoint> points)
        {
            int[] cudaPoints = new int[checked(points.Count * 3)];

            for (int index = 0; index < points.Count; index++)
            {
                DerivedPoint point = points[index];
                int offset = index * 3;

                cudaPoints[offset] = point.CircleIndex;
                cudaPoints[offset + 1] = point.PointIndex;
                cudaPoints[offset + 2] = point.PointType;
            }

            return cudaPoints;
        }

        public static void ValidateCudaPointOrderAndUniqueness(IReadOnlyList<DerivedPoint> points)
        {
            ArgumentNullException.ThrowIfNull(points);

            HashSet<PathPointKey> seenPoints = new(points.Count);

            for (int index = 0; index < points.Count; index++)
            {
                DerivedPoint current = points[index];
                PathPointKey currentKey = new(current.CircleIndex, current.PointIndex);

                if (!seenPoints.Add(currentKey))
                {
                    throw new InvalidOperationException(
                        $"Duplicate CUDA point position: ({current.CircleIndex}, {current.PointIndex}).");
                }

                if (index == 0)
                {
                    continue;
                }

                DerivedPoint previous = points[index - 1];
                if (CompareCudaPointOrder(previous, current) > 0)
                {
                    throw new InvalidOperationException(
                        $"CUDA points must be sorted by circleIndex, pointIndex, pointType ascending. " +
                        $"Previous=({previous.CircleIndex}, {previous.PointIndex}, {previous.PointType}), " +
                        $"Current=({current.CircleIndex}, {current.PointIndex}, {current.PointType}).");
                }
            }
        }

        public static void ValidateCudaPointRoleCount(IReadOnlyList<DerivedPoint> points, int[] cudaPoints)
        {
            ArgumentNullException.ThrowIfNull(points);
            ArgumentNullException.ThrowIfNull(cudaPoints);

            if (cudaPoints.Length % 3 != 0)
            {
                throw new ArgumentException(
                    "CUDA point array length must be a multiple of 3.",
                    nameof(cudaPoints));
            }

            int cudaPointCount = cudaPoints.Length / 3;
            if (cudaPointCount != points.Count)
            {
                throw new InvalidOperationException(
                    $"CUDA point count {cudaPointCount} does not match merged point count {points.Count}.");
            }

            int roleCountSum = 0;
            int overlapCount = 0;

            foreach (DerivedPoint point in points)
            {
                int roleCount = CountCudaPointRoles(point.PointType);
                roleCountSum = checked(roleCountSum + roleCount);

                if (roleCount > 1)
                {
                    overlapCount = checked(overlapCount + roleCount - 1);
                }
            }

            int expectedCudaPointCount = checked(roleCountSum - overlapCount);
            if (cudaPointCount != expectedCudaPointCount)
            {
                throw new InvalidOperationException(
                    $"CUDA point count {cudaPointCount} does not match role count relation. " +
                    $"Expected={expectedCudaPointCount}, RoleCountSum={roleCountSum}, OverlapCount={overlapCount}.");
            }
        }

        public static void ValidateCudaPointArrayMatchesPoints(IReadOnlyList<DerivedPoint> points, int[] cudaPoints)
        {
            ArgumentNullException.ThrowIfNull(points);
            ArgumentNullException.ThrowIfNull(cudaPoints);

            if (cudaPoints.Length % 3 != 0)
            {
                throw new ArgumentException(
                    "CUDA point array length must be a multiple of 3.",
                    nameof(cudaPoints));
            }

            int pointCount = cudaPoints.Length / 3;
            if (pointCount != points.Count)
            {
                throw new InvalidOperationException(
                    $"CUDA point count {pointCount} does not match merged point count {points.Count}.");
            }

            for (int index = 0; index < points.Count; index++)
            {
                int offset = index * 3;
                DerivedPoint point = points[index];

                if (cudaPoints[offset] != point.CircleIndex ||
                    cudaPoints[offset + 1] != point.PointIndex ||
                    cudaPoints[offset + 2] != point.PointType)
                {
                    throw new InvalidOperationException(
                        $"CUDA point triple {index} does not match sorted derived point. " +
                        $"Expected=({point.CircleIndex}, {point.PointIndex}, {point.PointType}), " +
                        $"Actual=({cudaPoints[offset]}, {cudaPoints[offset + 1]}, {cudaPoints[offset + 2]}).");
                }
            }
        }

        public static void ValidateCudaInputPackage(
            IReadOnlyList<DerivedPoint> points,
            IReadOnlyList<int> pathPointCounts,
            int[] cudaPoints,
            int[] connect,
            IReadOnlyList<int> tradingPoints,
            int harvestPointCount,
            int purchasePointCount,
            int calIndexStarted,
            int batchStartPointCount,
            int[] lastFP)
        {
            ValidateCudaPointOrderAndUniqueness(points);
            ValidateCudaPointArrayMatchesPoints(points, cudaPoints);
            ValidateCudaPointBusinessRules(points, pathPointCounts);
            ValidateCudaPointRoleCount(points, cudaPoints);
            ValidateCudaPointConnectRatio(cudaPoints, connect);
            ValidateCudaConnectArray(points, connect);
            ValidateTradingPoints(cudaPoints, tradingPoints, harvestPointCount, purchasePointCount);
            ValidateCudaBatchRange(tradingPoints, calIndexStarted, batchStartPointCount);
            ValidateLastFPInitialization(cudaPoints, lastFP, batchStartPointCount);
        }

        public static void ValidateCudaPointBusinessRules(
            IReadOnlyList<DerivedPoint> points,
            IReadOnlyList<int> pathPointCounts)
        {
            ArgumentNullException.ThrowIfNull(points);
            ArgumentNullException.ThrowIfNull(pathPointCounts);

            int circleCount = pathPointCounts.Count;
            int harvestPointCount = 0;
            int firstPathPointCount = 0;
            int terminalLinkPointCount = 0;
            int ordinaryLinkPointCount = 0;
            int purchasePointCount = 0;

            for (int index = 0; index < points.Count; index++)
            {
                DerivedPoint point = points[index];

                if (!HasValidPointTypeRoleProduct(point.PointType))
                {
                    throw new InvalidOperationException(
                        $"CUDA point {index} has invalid pointType {point.PointType}.");
                }

                int roleCount = CountCudaPointRoles(point.PointType);
                if (roleCount == 0)
                {
                    throw new InvalidOperationException(
                        $"CUDA point {index} must contain at least one known role. PointType={point.PointType}.");
                }

                int occupyingRoleCount = CountOccupyingPointRoles(point.PointType);
                if (occupyingRoleCount > 1)
                {
                    throw new InvalidOperationException(
                        $"CUDA point {index} has more than one occupying role. PointType={point.PointType}.");
                }

                if (point.CircleIndex < 0 || point.CircleIndex >= circleCount)
                {
                    throw new InvalidOperationException(
                        $"CUDA point {index} has circleIndex {point.CircleIndex} outside [0, {circleCount - 1}].");
                }

                int pathPointCount = pathPointCounts[point.CircleIndex];
                if (pathPointCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Circle {point.CircleIndex} has invalid path point count {pathPointCount}.");
                }

                if (point.PointIndex < 0 || point.PointIndex >= pathPointCount)
                {
                    throw new InvalidOperationException(
                        $"CUDA point {index} has pointIndex {point.PointIndex} outside [0, {pathPointCount - 1}].");
                }

                bool isHarvestPoint = point.PointType % HarvestPointCode == 0;
                bool isFirstPathPoint = point.PointType % FirstPathPointCode == 0;
                bool isTerminalLinkPoint = point.PointType % TerminalLinkPointCode == 0;
                bool isOrdinaryLinkPoint = point.PointType % OrdinaryLinkPointCode == 0;
                bool isPurchasePoint = point.PointType % PurchasePointCode == 0;
                int terminalPointIndex = pathPointCount - 1;

                if (isFirstPathPoint && point.PointIndex != 0)
                {
                    throw new InvalidOperationException(
                        $"First path point must use pointIndex 0. CUDA point {index} uses {point.PointIndex}.");
                }

                if (isTerminalLinkPoint && point.PointIndex != terminalPointIndex)
                {
                    throw new InvalidOperationException(
                        $"Terminal link point must use pointIndex {terminalPointIndex}. CUDA point {index} uses {point.PointIndex}.");
                }

                if ((isHarvestPoint || isOrdinaryLinkPoint || isPurchasePoint) &&
                    point.PointIndex == terminalPointIndex)
                {
                    throw new InvalidOperationException(
                        $"Harvest, ordinary link, and purchase points cannot use terminal pointIndex {terminalPointIndex}. CUDA point {index}.");
                }

                if (isHarvestPoint)
                {
                    harvestPointCount++;
                }

                if (isFirstPathPoint)
                {
                    firstPathPointCount++;
                }

                if (isTerminalLinkPoint)
                {
                    terminalLinkPointCount++;
                }

                if (isOrdinaryLinkPoint)
                {
                    ordinaryLinkPointCount++;
                }

                if (isPurchasePoint)
                {
                    purchasePointCount++;
                }
            }

            if (harvestPointCount != 1)
            {
                throw new InvalidOperationException(
                    $"Harvest point count {harvestPointCount} does not match required count 1.");
            }

            if (firstPathPointCount != circleCount)
            {
                throw new InvalidOperationException(
                    $"First path point count {firstPathPointCount} does not match circle count {circleCount}.");
            }

            if (terminalLinkPointCount != circleCount)
            {
                throw new InvalidOperationException(
                    $"Terminal link point count {terminalLinkPointCount} does not match circle count {circleCount}.");
            }

            if (purchasePointCount != circleCount)
            {
                throw new InvalidOperationException(
                    $"Purchase point count {purchasePointCount} does not match circle count {circleCount}.");
            }

            if (ordinaryLinkPointCount % 2 != 0)
            {
                throw new InvalidOperationException(
                    $"Ordinary link point count {ordinaryLinkPointCount} must be even.");
            }
        }

        private static int CountCudaPointRoles(int pointType)
        {
            int roleCount = 0;

            if (pointType % HarvestPointCode == 0)
            {
                roleCount++;
            }

            if (pointType % FirstPathPointCode == 0)
            {
                roleCount++;
            }

            if (pointType % TerminalLinkPointCode == 0)
            {
                roleCount++;
            }

            if (pointType % OrdinaryLinkPointCode == 0)
            {
                roleCount++;
            }

            if (pointType % PurchasePointCode == 0)
            {
                roleCount++;
            }

            return roleCount;
        }

        private static int CountOccupyingPointRoles(int pointType)
        {
            int roleCount = 0;

            if (pointType % HarvestPointCode == 0)
            {
                roleCount++;
            }

            if (pointType % TerminalLinkPointCode == 0)
            {
                roleCount++;
            }

            if (pointType % OrdinaryLinkPointCode == 0)
            {
                roleCount++;
            }

            if (pointType % PurchasePointCode == 0)
            {
                roleCount++;
            }

            return roleCount;
        }

        private static bool HasValidPointTypeRoleProduct(int pointType)
        {
            if (pointType <= 0)
            {
                return false;
            }

            int remaining = pointType;
            if (!TryRemoveSingleRoleFactor(ref remaining, HarvestPointCode))
            {
                return false;
            }

            if (!TryRemoveSingleRoleFactor(ref remaining, FirstPathPointCode))
            {
                return false;
            }

            if (!TryRemoveSingleRoleFactor(ref remaining, TerminalLinkPointCode))
            {
                return false;
            }

            if (!TryRemoveSingleRoleFactor(ref remaining, OrdinaryLinkPointCode))
            {
                return false;
            }

            if (!TryRemoveSingleRoleFactor(ref remaining, PurchasePointCode))
            {
                return false;
            }

            return remaining == 1;
        }

        private static bool TryRemoveSingleRoleFactor(ref int value, int factor)
        {
            if (value % factor != 0)
            {
                return true;
            }

            value /= factor;

            if (value % factor == 0)
            {
                return false;
            }

            return true;
        }

        private static int CompareCudaPointOrder(DerivedPoint left, DerivedPoint right)
        {
            int circleComparison = left.CircleIndex.CompareTo(right.CircleIndex);
            if (circleComparison != 0)
            {
                return circleComparison;
            }

            int pointComparison = left.PointIndex.CompareTo(right.PointIndex);
            if (pointComparison != 0)
            {
                return pointComparison;
            }

            return left.PointType.CompareTo(right.PointType);
        }

        public static void ValidateCudaPointConnectRatio(int[] cudaPoints, int[] connect)
        {
            ArgumentNullException.ThrowIfNull(cudaPoints);
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
        }

        public static void ValidateCudaConnectArray(IReadOnlyList<DerivedPoint> points, int[] connect)
        {
            ArgumentNullException.ThrowIfNull(points);
            ArgumentNullException.ThrowIfNull(connect);

            if (connect.Length != points.Count)
            {
                throw new ArgumentException(
                    "Connect length must equal merged CUDA point count.",
                    nameof(connect));
            }

            for (int sourceIndex = 0; sourceIndex < connect.Length; sourceIndex++)
            {
                int targetIndex = connect[sourceIndex];
                if (targetIndex == -1)
                {
                    continue;
                }

                if (targetIndex < 0 || targetIndex >= points.Count)
                {
                    throw new InvalidOperationException(
                        $"Connect target {targetIndex} is outside CUDA point range [0, {points.Count - 1}].");
                }

                DerivedPoint sourcePoint = points[sourceIndex];
                DerivedPoint targetPoint = points[targetIndex];
                bool isTerminalLinkPoint = sourcePoint.PointType % TerminalLinkPointCode == 0;
                bool isOrdinaryLinkPoint = sourcePoint.PointType % OrdinaryLinkPointCode == 0;

                if (!isTerminalLinkPoint && !isOrdinaryLinkPoint)
                {
                    throw new InvalidOperationException(
                        $"CUDA point {sourceIndex} has connect target but is not a terminal or ordinary link point.");
                }

                if (isTerminalLinkPoint &&
                    (targetPoint.CircleIndex != sourcePoint.CircleIndex ||
                     targetPoint.PointIndex != 0 ||
                     targetPoint.PointType % FirstPathPointCode != 0))
                {
                    throw new InvalidOperationException(
                        $"Terminal link point {sourceIndex} must connect to first path point on the same circle.");
                }

                if (isOrdinaryLinkPoint)
                {
                    if (targetPoint.PointType % OrdinaryLinkPointCode != 0)
                    {
                        throw new InvalidOperationException(
                            $"Ordinary link point {sourceIndex} must connect to another ordinary link point.");
                    }

                    if (targetPoint.CircleIndex == sourcePoint.CircleIndex)
                    {
                        throw new InvalidOperationException(
                            $"Ordinary link point {sourceIndex} must connect to a point on another circle.");
                    }

                    if (connect[targetIndex] != sourceIndex)
                    {
                        throw new InvalidOperationException(
                            $"Ordinary link point {sourceIndex} must be bidirectional with target {targetIndex}.");
                    }
                }
            }
        }

        public static void ValidateTradingPoints(
            int[] cudaPoints,
            IReadOnlyList<int> tradingPoints,
            int harvestPointCount,
            int purchasePointCount)
        {
            ArgumentNullException.ThrowIfNull(cudaPoints);
            ArgumentNullException.ThrowIfNull(tradingPoints);

            if (cudaPoints.Length % 3 != 0)
            {
                throw new ArgumentException(
                    "CUDA point array length must be a multiple of 3.",
                    nameof(cudaPoints));
            }

            int pointCount = cudaPoints.Length / 3;
            int expectedTradingPointCount = checked(harvestPointCount + purchasePointCount);
            if (harvestPointCount < 0 || purchasePointCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(harvestPointCount),
                    "Harvest and purchase point counts cannot be negative.");
            }

            List<int> expectedTradingPoints = new();
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                int pointType = cudaPoints[pointIndex * 3 + 2];
                if (IsTradingPointType(pointType))
                {
                    expectedTradingPoints.Add(pointIndex);
                }
            }

            if (expectedTradingPoints.Count != expectedTradingPointCount)
            {
                throw new InvalidOperationException(
                    $"Actual trading point count {expectedTradingPoints.Count} does not match harvest + purchase count {expectedTradingPointCount}.");
            }

            if (tradingPoints.Count != expectedTradingPointCount)
            {
                throw new InvalidOperationException(
                    $"Trading point count {tradingPoints.Count} does not match harvest + purchase count {expectedTradingPointCount}.");
            }

            int previousPointIndex = -1;
            for (int index = 0; index < tradingPoints.Count; index++)
            {
                int pointIndex = tradingPoints[index];
                if (pointIndex < 0 || pointIndex >= pointCount)
                {
                    throw new InvalidOperationException(
                        $"Trading point index {pointIndex} is outside CUDA point range [0, {pointCount - 1}].");
                }

                if (pointIndex <= previousPointIndex)
                {
                    throw new InvalidOperationException("Trading points must be sorted ascending and unique.");
                }

                int pointType = cudaPoints[pointIndex * 3 + 2];
                if (!IsTradingPointType(pointType))
                {
                    throw new InvalidOperationException(
                        $"CUDA point {pointIndex} is not a trading point. PointType={pointType}.");
                }

                if (pointIndex != expectedTradingPoints[index])
                {
                    throw new InvalidOperationException(
                        $"Trading points must contain all and only trading CUDA point indexes. Expected={expectedTradingPoints[index]}, Actual={pointIndex}.");
                }

                previousPointIndex = pointIndex;
            }
        }

        public static void ValidateLastFPInitialization(int[] cudaPoints, int[] lastFP)
        {
            ValidateLastFPInitializationCore(cudaPoints, lastFP, null);
        }

        public static void ValidateLastFPInitialization(int[] cudaPoints, int[] lastFP, int batchStartPointCount)
        {
            if (batchStartPointCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(batchStartPointCount),
                    "Batch start point count must be greater than 0.");
            }

            ValidateLastFPInitializationCore(cudaPoints, lastFP, batchStartPointCount);
        }

        private static void ValidateLastFPInitializationCore(
            int[] cudaPoints,
            int[] lastFP,
            int? expectedUnitCount)
        {
            ArgumentNullException.ThrowIfNull(cudaPoints);
            ArgumentNullException.ThrowIfNull(lastFP);

            if (cudaPoints.Length % 3 != 0)
            {
                throw new ArgumentException(
                    "CUDA point array length must be a multiple of 3.",
                    nameof(cudaPoints));
            }

            int pointCount = cudaPoints.Length / 3;
            if (pointCount == 0)
            {
                if (lastFP.Length != 0)
                {
                    throw new ArgumentException(
                        "LastFP must be empty when there are no CUDA points.",
                        nameof(lastFP));
                }

                return;
            }

            if (expectedUnitCount is null)
            {
                if (lastFP.Length % pointCount != 0)
                {
                    throw new ArgumentException(
                        "LastFP length must equal CUDA point count multiplied by the parallel start point count.",
                        nameof(lastFP));
                }
            }
            else if (lastFP.Length != pointCount * expectedUnitCount.Value)
            {
                throw new ArgumentException(
                    "LastFP length must equal CUDA point count multiplied by the parallel start point count.",
                    nameof(lastFP));
            }

            int unitCount = expectedUnitCount ?? lastFP.Length / pointCount;
            for (int unitIndex = 0; unitIndex < unitCount; unitIndex++)
            {
                int unitBase = unitIndex * pointCount;
                int startPointIndex = -1;

                for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    int previousPointIndex = lastFP[unitBase + pointIndex];
                    if (previousPointIndex == -1)
                    {
                        continue;
                    }

                    if (startPointIndex != -1)
                    {
                        throw new InvalidOperationException(
                            $"LastFP unit {unitIndex} has more than one initialized start point.");
                    }

                    if (previousPointIndex != pointIndex)
                    {
                        throw new InvalidOperationException(
                            $"LastFP unit {unitIndex} start point must point to itself.");
                    }

                    int pointType = cudaPoints[pointIndex * 3 + 2];
                    if (!IsTradingPointType(pointType))
                    {
                        throw new InvalidOperationException(
                            $"LastFP unit {unitIndex} start point {pointIndex} is not a trading point.");
                    }

                    startPointIndex = pointIndex;
                }

                if (startPointIndex == -1)
                {
                    throw new InvalidOperationException(
                        $"LastFP unit {unitIndex} does not have an initialized start point.");
                }
            }
        }

        private static bool IsTradingPointType(int pointType)
        {
            return pointType % HarvestPointCode == 0 ||
                   pointType % PurchasePointCode == 0;
        }

        public static void ValidateCudaBatchRange(
            IReadOnlyList<int> tradingPoints,
            int calIndexStarted,
            int batchStartPointCount)
        {
            ArgumentNullException.ThrowIfNull(tradingPoints);

            if (calIndexStarted < 0 || calIndexStarted > tradingPoints.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(calIndexStarted),
                    $"Calculation start index must be in [0, {tradingPoints.Count}].");
            }

            if (batchStartPointCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(batchStartPointCount),
                    "Batch start point count must be greater than 0.");
            }

            if (calIndexStarted + batchStartPointCount > tradingPoints.Count)
            {
                throw new InvalidOperationException(
                    $"CUDA batch range [{calIndexStarted}, {calIndexStarted + batchStartPointCount}) exceeds trading point count {tradingPoints.Count}.");
            }
        }

        public static void ValidatePassedLength(int[] lastFP, int[] passedLength)
        {
            ArgumentNullException.ThrowIfNull(lastFP);
            ArgumentNullException.ThrowIfNull(passedLength);

            if (passedLength.Length != lastFP.Length)
            {
                throw new ArgumentException(
                    "PassedLength length must equal LastFP length.",
                    nameof(passedLength));
            }
        }

        public static int[] BuildConnectArray(
            IReadOnlyList<DerivedPoint> points,
            IReadOnlyList<ConnectionPair> ordinaryConnections)
        {
            ArgumentNullException.ThrowIfNull(points);
            ArgumentNullException.ThrowIfNull(ordinaryConnections);

            int[] connect = Enumerable.Repeat(-1, points.Count).ToArray();
            Dictionary<PathPointKey, int> pointIndexes = new(points.Count);

            for (int index = 0; index < points.Count; index++)
            {
                DerivedPoint point = points[index];
                PathPointKey key = new(point.CircleIndex, point.PointIndex);

                if (!pointIndexes.TryAdd(key, index))
                {
                    throw new InvalidOperationException(
                        $"Duplicate CUDA point position: ({point.CircleIndex}, {point.PointIndex}).");
                }
            }

            for (int index = 0; index < points.Count; index++)
            {
                DerivedPoint point = points[index];

                if (point.PointType % TerminalLinkPointCode != 0)
                {
                    continue;
                }

                int firstPointIndex = GetRequiredPointIndex(
                    pointIndexes,
                    new PathPointKey(point.CircleIndex, 0));
                SetConnect(connect, index, firstPointIndex);
            }

            foreach (ConnectionPair connection in ordinaryConnections)
            {
                int leftIndex = GetRequiredPointIndex(
                    pointIndexes,
                    new PathPointKey(connection.LeftCircleIndex, connection.LeftPointIndex));
                int rightIndex = GetRequiredPointIndex(
                    pointIndexes,
                    new PathPointKey(connection.RightCircleIndex, connection.RightPointIndex));

                SetConnect(connect, leftIndex, rightIndex);
                SetConnect(connect, rightIndex, leftIndex);
            }

            return connect;
        }

        internal static (int CircleIndex, int PathPointIndex) GenerateHarvestPoint(string inputPath)
        {
            string fullInputPath = Path.GetFullPath(inputPath);

            if (!File.Exists(fullInputPath))
            {
                throw new FileNotFoundException($"{CircleFileName} does not exist.", fullInputPath);
            }

            List<CircleRecord> circles = ReadCircles(fullInputPath);
            BuildGeometrySet(circles);
            EnsureSingleConnectedComponent(circles);

            PathPointKey harvestPoint = FindHarvestPoint(circles, CalculatePathPointCounts(circles));

            return (harvestPoint.CircleIndex, harvestPoint.PointIndex);
        }

        internal static IReadOnlyList<DerivedPoint> GenerateDerivedPoints(string inputPath)
        {
            return GenerateDerivedPointData(inputPath).Points;
        }

        /// <summary>
        /// 从指定圆数据文件生成派生点数据。
        /// 该方法会读取 Circle.bin 中的圆记录，先校验几何唯一性和同一个连通分量约束，再计算派生点数据。
        /// </summary>
        /// <param name="inputPath">圆数据文件路径，文件内容应按 a、b、r 三个 int 顺序保存圆记录。</param>
        /// <returns>生成后的派生点数据，包含合并后的派生点集合和普通联通点之间的圆间双向连通关系。</returns>
        /// <exception cref="FileNotFoundException">当指定圆数据文件不存在时抛出。</exception>
        internal static GeneratedPointData GenerateDerivedPointData(string inputPath)
        {
            // 将传入的圆数据文件路径转为绝对路径，避免当前工作目录变化导致读取错误文件。
            string fullInputPath = Path.GetFullPath(inputPath);

            // 派生点必须基于已有圆数据文件计算；文件不存在时不能继续生成收割点、第一路径点、末尾联通点、普通联通点和采购点。
            if (!File.Exists(fullInputPath))
            {
                throw new FileNotFoundException($"{CircleFileName} does not exist.", fullInputPath);
            }

            // 读取 Circle.bin 中的圆记录；每条记录按 a、b、r 三个 int 存储，其中 r 为带方向的有符号半径。
            List<CircleRecord> circles = ReadCircles(fullInputPath);

            // 校验圆集合的几何唯一性：不能存在重复的几何圆 (a, b, R)。
            BuildGeometrySet(circles);

            // 校验所有圆属于同一个连通分量，避免派生点计算建立在多个互不连通的圆组上。
            EnsureSingleConnectedComponent(circles);

            // 基于已校验的圆集合继续计算派生点数据：全场唯一收割点、第一路径点、末尾联通点、普通联通点和采购点。
            return GenerateDerivedPointData(circles);
        }

        internal static void DrawToDxf(string inputPath, string outputPath)
        {
            string fullInputPath = Path.GetFullPath(inputPath);

            if (!File.Exists(fullInputPath))
            {
                throw new FileNotFoundException($"{CircleFileName} does not exist.", fullInputPath);
            }

            List<CircleRecord> circles = ReadCircles(fullInputPath);
            BuildGeometrySet(circles);
            EnsureSingleConnectedComponent(circles);

            string fullOutputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);

            using StreamWriter writer = new(fullOutputPath, false, new UTF8Encoding(false));
            WriteDxf(writer, circles);
        }

        internal static void DrawRouteToDxf(
            string inputPath,
            string outputPath,
            IReadOnlyList<int> routePointIndexes)
        {
            ArgumentNullException.ThrowIfNull(routePointIndexes);

            string fullInputPath = Path.GetFullPath(inputPath);

            if (!File.Exists(fullInputPath))
            {
                throw new FileNotFoundException($"{CircleFileName} does not exist.", fullInputPath);
            }

            List<CircleRecord> circles = ReadCircles(fullInputPath);
            BuildGeometrySet(circles);
            EnsureSingleConnectedComponent(circles);

            string fullOutputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);

            using StreamWriter writer = new(fullOutputPath, false, new UTF8Encoding(false));
            WriteDxf(writer, circles, routePointIndexes);
        }

        /// <summary>
        /// 生成或补齐圆数据文件。
        /// 该方法负责维护 <see cref="CircleFileName"/> 对应的二进制圆集合，每条圆记录按 a、b、r 三个 int 写入。
        /// r 是带方向的半径：r &gt; 0 表示逆时针/做多圆，r &lt; 0 表示顺时针/做空圆，真实几何半径使用 abs(r)。
        /// </summary>
        /// <param name="outputPath">
        /// 圆数据文件输出路径；可以是相对路径，也可以是绝对路径。
        /// 方法内部会转成绝对路径，并在写入时创建缺失的目录。
        /// </param>
        /// <param name="count">
        /// 目标圆数量。
        /// 如果文件不存在，则新建到该数量；
        /// 如果文件存在但数量不足，则在已有圆基础上补齐到该数量；
        /// 如果文件存在且数量超过该数量，则抛出异常，不自动截断。
        /// </param>
        /// <remarks>
        /// 生成和补齐都必须满足几何唯一性约束：不能存在相同的 (a, b, abs(r))。
        /// 已有圆集合必须已经是单一连通分量；补圆时每新增一个候选圆，也必须保持整体仍然是单一连通分量。
        /// 如果某个候选圆加入后破坏该约束，会撤销该候选圆并重新生成，直到补齐到目标数量或超过尝试上限。
        /// 这个方法只负责 Circle.bin，不负责计算 Route.bin。
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">
        /// 当 <paramref name="count"/> 小于基础种子圆数量时抛出。
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// 当已有文件损坏、已有圆数量超过目标数量、已有几何重复、已有圆不属于单一连通分量，
        /// 或者在尝试上限内无法生成满足约束的新圆时抛出。
        /// </exception>
        internal static void GenerateCircle(string outputPath, int count)
        {
            // 目标圆数量不能小于基础圆数量。
            // 当前 SeedCircles 至少包含两个基础圆；如果 count < SeedCircles.Length，后续无法建立满足同一个连通分量约束的初始圆集合。
            if (count < SeedCircles.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    $"Circle count must be at least {SeedCircles.Length}.");
            }

            // 将传入路径转成绝对路径，避免相对路径受当前工作目录变化影响。
            string fullPath = Path.GetFullPath(outputPath);

            // 最终要写回 Circle.bin 的圆集合；写回顺序即圆序号顺序。
            List<CircleRecord> circles;

            if (File.Exists(fullPath))
            {
                // 分支一：Circle.bin 已经存在。
                // 这种情况下不能直接覆盖重建，要尽量保留已有圆序号和已有圆数据。

                // 如果文件已存在，先读取已有圆数据。
                // ReadCircles 应负责校验文件长度是否为 12 字节整数倍，并按 a、b、r 三个 int 读取圆记录。
                circles = ReadCircles(fullPath);

                // 已有圆数量大于目标数量时不允许自动截断。
                // 截断会改变已有圆序号，并可能破坏整体连通约束，属于破坏性操作。
                if (circles.Count > count)
                {
                    throw new InvalidOperationException(
                        $"Existing circle count {circles.Count} is greater than requested count {count}.");
                }

                if (circles.Count == 0)
                {
                    // 分支一 A：文件存在但没有任何圆记录。
                    // 空文件没有可保留的圆序号，因此等价于重新生成完整圆集合。

                    // 文件存在但内容为空时，按“无已有圆”处理，重新生成满足同一个连通分量约束的完整圆集合。
                    circles = BuildConnectedCircles(count);
                }
                else
                {
                    // 分支一 B：文件存在且已有圆记录。
                    // 这里必须在已有数据基础上补齐，不能打乱已有圆的顺序。

                    // 根据已有圆构建几何唯一性集合。
                    // BuildGeometrySet 应校验不能存在重复几何圆 (a, b, R)，也不能存在非法半径。
                    HashSet<GeometryKey> geometries = BuildGeometrySet(circles);

                    // 校验已有圆是否属于同一个连通分量。
                    // 如果已有文件已经分裂成多个不连通圆组，这里必须报错，不能继续补齐。
                    EnsureSingleConnectedComponent(circles);

                    while (circles.Count < count)
                    {
                        // 只要已有圆数量还没达到目标数量，就继续补圆。
                        // 每次循环只尝试成功加入一个圆，加入成功后 circles.Count 会增加 1。

                        // 生成并加入一个新圆。
                        // 如果候选圆加入后导致整体不再是单一连通分量，就撤销这个候选圆并重新生成。
                        AddGeneratedCirclePreservingSingleConnectedComponent(circles, geometries);
                    }

                    // 补齐完成后再次校验整体连通性。
                    // 理论上每个新圆都与已有集合连通时应天然成立，这里作为最终防线。
                    EnsureSingleConnectedComponent(circles);
                }
            }
            else
            {
                // 分支二：Circle.bin 不存在。
                // 这种情况下从 SeedCircles 开始，直接生成一套新的完整连通圆集合。

                // 文件不存在时，从种子圆开始生成完整连通圆集合。
                circles = BuildConnectedCircles(count);
            }

            // 将最终圆集合写回目标文件。
            // 写入顺序即圆序号顺序，所以前面不能随意重排 circles。
            WriteCircles(fullPath, circles);
        }

        private static void WriteDxf(
            TextWriter writer,
            IReadOnlyList<CircleRecord> circles,
            IReadOnlyList<int>? routePointIndexes = null)
        {
            bool routeDxfMode = routePointIndexes is not null;
            int[] pathPointCounts = CalculatePathPointCounts(circles);
            GeneratedPointData pointData = GenerateDerivedPointData(circles);
            List<DerivedPoint> points = pointData.Points
                .OrderBy(point => point.CircleIndex)
                .ThenBy(point => point.PointIndex)
                .ThenBy(point => point.PointType)
                .ToList();
            int[] connect = BuildConnectArray(points, pointData.OrdinaryConnections);

            WriteDxfPair(writer, 0, "SECTION");
            WriteDxfPair(writer, 2, "TABLES");
            WriteDxfPair(writer, 0, "TABLE");
            WriteDxfPair(writer, 2, "LAYER");
            WriteDxfPair(writer, 70, routePointIndexes is null ? 5 : 6);
            WriteDxfLayer(writer, "LongCircle", 3);
            WriteDxfLayer(writer, "ShortCircle", 1);
            WriteDxfLayer(writer, "OrdinaryConnect", 2);
            WriteDxfLayer(writer, "TerminalConnect", 30);
            WriteDxfLayer(writer, "PurchaseArrow", 30);
            if (routeDxfMode)
            {
                WriteDxfLayer(writer, "RoutePath", RoutePathColor);
            }
            WriteDxfPair(writer, 0, "ENDTAB");
            WriteDxfPair(writer, 0, "ENDSEC");

            if (!routeDxfMode)
            {
                WriteDxfPair(writer, 0, "SECTION");
                WriteDxfPair(writer, 2, "BLOCKS");
                for (int index = 0; index < circles.Count; index++)
                {
                    WriteDxfCircleBlockDefinition(
                        writer,
                        index,
                        circles[index],
                        circles,
                        pathPointCounts,
                        pointData.PurchaseAssignments[index]);
                }

                WriteDxfPair(writer, 0, "ENDSEC");
            }

            WriteDxfPair(writer, 0, "SECTION");
            WriteDxfPair(writer, 2, "ENTITIES");
            for (int index = 0; index < circles.Count; index++)
            {
                if (routeDxfMode)
                {
                    WriteDxfCircleDirectEntities(
                        writer,
                        index,
                        circles[index],
                        circles,
                        pathPointCounts,
                        pointData.PurchaseAssignments[index]);
                }
                else
                {
                    WriteDxfCircleInsert(writer, index, circles[index]);
                }
            }

            WriteDxfConnectionEntities(writer, circles, pathPointCounts, points, connect);
            if (routePointIndexes is not null)
            {
                WriteDxfRouteEntities(writer, circles, pathPointCounts, points, routePointIndexes);
            }

            WriteDxfPair(writer, 0, "ENDSEC");
            WriteDxfPair(writer, 0, "EOF");
        }

        private static void WriteDxfCircleBlockDefinition(
            TextWriter writer,
            int index,
            CircleRecord circle,
            IReadOnlyList<CircleRecord> circles,
            int[] pathPointCounts,
            PurchaseAssignment purchaseAssignment)
        {
            string blockName = GetCircleBlockName(index);
            string layerName = circle.SignedRadius > 0 ? "LongCircle" : "ShortCircle";
            int color = circle.SignedRadius > 0 ? 3 : 1;
            int textHeight = Math.Clamp(circle.Radius / 8, 5_000, 30_000);
            int textOffset = Math.Max(textHeight, 2_000);

            WriteDxfPair(writer, 0, "BLOCK");
            WriteDxfPair(writer, 8, layerName);
            WriteDxfPair(writer, 2, blockName);
            WriteDxfPair(writer, 70, 0);
            WriteDxfPair(writer, 10, 0.0);
            WriteDxfPair(writer, 20, 0.0);
            WriteDxfPair(writer, 30, 0.0);
            WriteDxfPair(writer, 3, blockName);
            WriteDxfPair(writer, 1, string.Empty);

            WriteDxfPair(writer, 0, "CIRCLE");
            WriteDxfPair(writer, 8, layerName);
            WriteDxfPair(writer, 62, color);
            WriteDxfPair(writer, 10, 0.0);
            WriteDxfPair(writer, 20, 0.0);
            WriteDxfPair(writer, 30, 0.0);
            WriteDxfPair(writer, 40, circle.Radius);

            WriteDxfPair(writer, 0, "TEXT");
            WriteDxfPair(writer, 8, layerName);
            WriteDxfPair(writer, 62, color);
            WriteDxfPair(writer, 10, textOffset);
            WriteDxfPair(writer, 20, textOffset);
            WriteDxfPair(writer, 30, 0.0);
            WriteDxfPair(writer, 40, textHeight);
            WriteDxfPair(writer, 1, $"#{index} a={circle.A} b={circle.B} r={circle.SignedRadius}");

            WriteDxfPurchaseArrowInCircleBlock(
                writer,
                circle,
                circles,
                pathPointCounts,
                purchaseAssignment);

            WriteDxfPair(writer, 0, "ENDBLK");
            WriteDxfPair(writer, 8, layerName);
        }

        private static void WriteDxfCircleInsert(TextWriter writer, int index, CircleRecord circle)
        {
            string layerName = circle.SignedRadius > 0 ? "LongCircle" : "ShortCircle";
            int color = circle.SignedRadius > 0 ? 3 : 1;

            WriteDxfPair(writer, 0, "INSERT");
            WriteDxfPair(writer, 8, layerName);
            WriteDxfPair(writer, 62, color);
            WriteDxfPair(writer, 2, GetCircleBlockName(index));
            WriteDxfPair(writer, 10, circle.A);
            WriteDxfPair(writer, 20, circle.B);
            WriteDxfPair(writer, 30, 0.0);
        }

        private static void WriteDxfCircleDirectEntities(
            TextWriter writer,
            int index,
            CircleRecord circle,
            IReadOnlyList<CircleRecord> circles,
            int[] pathPointCounts,
            PurchaseAssignment purchaseAssignment)
        {
            string layerName = circle.SignedRadius > 0 ? "LongCircle" : "ShortCircle";
            int color = circle.SignedRadius > 0 ? 3 : 1;
            int textHeight = Math.Clamp(circle.Radius / 8, 5_000, 30_000);
            int textOffset = Math.Max(textHeight, 2_000);

            WriteDxfPair(writer, 0, "CIRCLE");
            WriteDxfPair(writer, 8, layerName);
            WriteDxfPair(writer, 62, color);
            WriteDxfPair(writer, 10, circle.A);
            WriteDxfPair(writer, 20, circle.B);
            WriteDxfPair(writer, 30, 0.0);
            WriteDxfPair(writer, 40, circle.Radius);

            WriteDxfPair(writer, 0, "TEXT");
            WriteDxfPair(writer, 8, layerName);
            WriteDxfPair(writer, 62, color);
            WriteDxfPair(writer, 10, circle.A + textOffset);
            WriteDxfPair(writer, 20, circle.B + textOffset);
            WriteDxfPair(writer, 30, 0.0);
            WriteDxfPair(writer, 40, textHeight);
            WriteDxfPair(writer, 1, $"#{index} a={circle.A} b={circle.B} r={circle.SignedRadius}");

            CircleRecord purchaseCircle = circles[purchaseAssignment.PurchaseCircleIndex];
            (double targetX, double targetY) = CalculatePathPointCoordinate(
                purchaseCircle,
                purchaseAssignment.PurchasePointIndex,
                pathPointCounts[purchaseAssignment.PurchaseCircleIndex]);

            WriteDxfPurchaseArrowEntity(
                writer,
                circle.A,
                circle.B,
                targetX,
                targetY);
        }

        private static string GetCircleBlockName(int index)
        {
            return $"Circle_{index}";
        }

        private static void WriteDxfConnectionEntities(
            TextWriter writer,
            IReadOnlyList<CircleRecord> circles,
            int[] pathPointCounts,
            IReadOnlyList<DerivedPoint> points,
            int[] connect)
        {
            for (int sourceIndex = 0; sourceIndex < connect.Length; sourceIndex++)
            {
                int targetIndex = connect[sourceIndex];
                if (targetIndex == -1)
                {
                    continue;
                }

                DerivedPoint sourcePoint = points[sourceIndex];
                bool isTerminalConnect = sourcePoint.PointType % TerminalLinkPointCode == 0;
                bool isOrdinaryConnect = sourcePoint.PointType % OrdinaryLinkPointCode == 0;

                if (!isTerminalConnect && !isOrdinaryConnect)
                {
                    continue;
                }

                if (isOrdinaryConnect && !isTerminalConnect && targetIndex < sourceIndex)
                {
                    continue;
                }

                DerivedPoint targetPoint = points[targetIndex];
                (double sourceX, double sourceY) = CalculateDerivedPointCoordinate(
                    circles,
                    pathPointCounts,
                    sourcePoint);
                (double targetX, double targetY) = CalculateDerivedPointCoordinate(
                    circles,
                    pathPointCounts,
                    targetPoint);

                WriteDxfConnectEntity(
                    writer,
                    sourceX,
                    sourceY,
                    targetX,
                    targetY,
                    isTerminalConnect);
            }
        }

        private static void WriteDxfPurchaseArrowInCircleBlock(
            TextWriter writer,
            CircleRecord sourceCircle,
            IReadOnlyList<CircleRecord> circles,
            int[] pathPointCounts,
            PurchaseAssignment purchaseAssignment)
        {
            CircleRecord purchaseCircle = circles[purchaseAssignment.PurchaseCircleIndex];
            (double targetX, double targetY) = CalculatePathPointCoordinate(
                purchaseCircle,
                purchaseAssignment.PurchasePointIndex,
                pathPointCounts[purchaseAssignment.PurchaseCircleIndex]);

            WriteDxfPurchaseArrowEntity(
                writer,
                0.0,
                0.0,
                targetX - sourceCircle.A,
                targetY - sourceCircle.B);
        }
        private static void WriteDxfPurchaseArrowEntity(
            TextWriter writer,
            double sourceX,
            double sourceY,
            double targetX,
            double targetY)
        {
            const string layerName = "PurchaseArrow";
            const int color = 30;
            double deltaX = targetX - sourceX;
            double deltaY = targetY - sourceY;
            double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            if (length <= DistanceTolerance)
            {
                WriteDxfPair(writer, 0, "CIRCLE");
                WriteDxfPair(writer, 8, layerName);
                WriteDxfPair(writer, 62, color);
                WriteDxfPair(writer, 10, targetX);
                WriteDxfPair(writer, 20, targetY);
                WriteDxfPair(writer, 30, 0.0);
                WriteDxfPair(writer, 40, ZeroLengthPurchaseArrowMarkerRadius);
                return;
            }

            WriteDxfLine(writer, layerName, color, sourceX, sourceY, targetX, targetY);

            double unitX = deltaX / length;
            double unitY = deltaY / length;
            double headLength = Math.Clamp(
                length * 0.08,
                MinimumPurchaseArrowHeadLength,
                MaximumPurchaseArrowHeadLength);
            double cos = Math.Cos(PurchaseArrowHeadAngle);
            double sin = Math.Sin(PurchaseArrowHeadAngle);
            double leftX = targetX - headLength * (unitX * cos - unitY * sin);
            double leftY = targetY - headLength * (unitY * cos + unitX * sin);
            double rightX = targetX - headLength * (unitX * cos + unitY * sin);
            double rightY = targetY - headLength * (unitY * cos - unitX * sin);

            WriteDxfLine(writer, layerName, color, targetX, targetY, leftX, leftY);
            WriteDxfLine(writer, layerName, color, targetX, targetY, rightX, rightY);
        }

        private static void WriteDxfLine(
            TextWriter writer,
            string layerName,
            int color,
            double sourceX,
            double sourceY,
            double targetX,
            double targetY)
        {
            WriteDxfPair(writer, 0, "LINE");
            WriteDxfPair(writer, 8, layerName);
            WriteDxfPair(writer, 62, color);
            WriteDxfPair(writer, 10, sourceX);
            WriteDxfPair(writer, 20, sourceY);
            WriteDxfPair(writer, 30, 0.0);
            WriteDxfPair(writer, 11, targetX);
            WriteDxfPair(writer, 21, targetY);
            WriteDxfPair(writer, 31, 0.0);
        }
        private static (double X, double Y) CalculateDerivedPointCoordinate(
            IReadOnlyList<CircleRecord> circles,
            int[] pathPointCounts,
            DerivedPoint point)
        {
            return CalculatePathPointCoordinate(
                circles[point.CircleIndex],
                point.PointIndex,
                pathPointCounts[point.CircleIndex]);
        }

        private static void WriteDxfConnectEntity(
            TextWriter writer,
            double sourceX,
            double sourceY,
            double targetX,
            double targetY,
            bool isTerminalConnect)
        {
            string layerName = isTerminalConnect ? "TerminalConnect" : "OrdinaryConnect";
            int color = isTerminalConnect ? 30 : 2;
            double deltaX = targetX - sourceX;
            double deltaY = targetY - sourceY;
            double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            if (!isTerminalConnect && length <= DistanceTolerance)
            {
                WriteDxfPair(writer, 0, "CIRCLE");
                WriteDxfPair(writer, 8, layerName);
                WriteDxfPair(writer, 62, color);
                WriteDxfPair(writer, 10, sourceX);
                WriteDxfPair(writer, 20, sourceY);
                WriteDxfPair(writer, 30, 0.0);
                WriteDxfPair(writer, 40, OverlappedOrdinaryConnectMarkerRadius);
                return;
            }

            if (length > DistanceTolerance && length <= MinimumVisibleConnectLineLength)
            {
                double unitX = deltaX / length;
                double unitY = deltaY / length;

                sourceX -= unitX * ConnectLineEndpointExtension;
                sourceY -= unitY * ConnectLineEndpointExtension;
                targetX += unitX * ConnectLineEndpointExtension;
                targetY += unitY * ConnectLineEndpointExtension;
            }

            WriteDxfPair(writer, 0, "LINE");
            WriteDxfPair(writer, 8, layerName);
            WriteDxfPair(writer, 62, color);
            WriteDxfPair(writer, 10, sourceX);
            WriteDxfPair(writer, 20, sourceY);
            WriteDxfPair(writer, 30, 0.0);
            WriteDxfPair(writer, 11, targetX);
            WriteDxfPair(writer, 21, targetY);
            WriteDxfPair(writer, 31, 0.0);
        }

        internal static int CalculatePathPointCount(CircleRecord circle)
        {
            long baseSegmentCount = (long)Math.Floor(TwoPi * circle.Radius);
            long pathPointCount = baseSegmentCount * PathPointDensityMultiplier + 1;

            if (pathPointCount > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Path point count exceeds int.MaxValue: {pathPointCount}.");
            }

            return (int)pathPointCount;
        }

        /// <summary>
        /// 根据圆记录和路径点序号计算该路径点的实际坐标。
        /// 路径点 0 位于圆上 x 最大的极点；r > 0 时按逆时针方向取点，r < 0 时按顺时针方向取点。
        /// </summary>
        /// <param name="circle">路径点所在的圆。</param>
        /// <param name="pathPointIndex">路径点在当前圆上的序号，从 0 到 n - 1。</param>
        /// <param name="pathPointCount">当前圆的路径点总数 n。</param>
        /// <returns>路径点的实际坐标。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当路径点序号不在 [0, n - 1] 范围内时抛出。</exception>
        internal static (double X, double Y) CalculatePathPointCoordinate(
            CircleRecord circle,
            int pathPointIndex,
            int pathPointCount)
        {
            if (pathPointIndex < 0 || pathPointIndex >= pathPointCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pathPointIndex));
            }

            double direction = circle.SignedRadius > 0 ? 1.0 : -1.0;
            double angle = direction * TwoPi * pathPointIndex / pathPointCount;

            return (
                circle.A + circle.Radius * Math.Cos(angle),
                circle.B + circle.Radius * Math.Sin(angle));
        }

        internal static IReadOnlyList<DerivedPoint> GenerateDerivedPoints(IReadOnlyList<CircleRecord> circles)
        {
            return GenerateDerivedPointData(circles).Points;
        }

        internal static GeneratedPointData GenerateDerivedPointData(IReadOnlyList<CircleRecord> circles)
        {
            BuildGeometrySet(circles);
            EnsureSingleConnectedComponent(circles);

            int[] pathPointCounts = CalculatePathPointCounts(circles);
            // pointTypes 记录每个路径点位置对应的角色编码，key 是 (circleIndex, pointIndex)，value 是 pointType。
            Dictionary<PathPointKey, int> pointTypes = new();

            // derivedPoints 保存尚未最终合并的派生点明细；同一位置的多个角色后续会按 (circleIndex, pointIndex) 合并。
            List<DerivedPoint> derivedPoints = new();

            // ordinaryConnections 保存普通联通点之间的圆间双向连通关系，用于后续构建 connect。
            List<ConnectionPair> ordinaryConnections = new();
            List<PurchaseAssignment> purchaseAssignments = new();

            PathPointKey harvestPoint = FindHarvestPoint(circles, pathPointCounts);
            MarkPoint(pointTypes, derivedPoints, harvestPoint, HarvestPointCode, true);

            for (int circleIndex = 0; circleIndex < circles.Count; circleIndex++)
            {
                MarkPoint(
                    pointTypes,
                    derivedPoints,
                    new PathPointKey(circleIndex, 0),
                    FirstPathPointCode,
                    false);
            }

            for (int circleIndex = 0; circleIndex < circles.Count; circleIndex++)
            {
                MarkPoint(
                    pointTypes,
                    derivedPoints,
                    new PathPointKey(circleIndex, pathPointCounts[circleIndex] - 1),
                    TerminalLinkPointCode,
                    true);
            }

            MarkOrdinaryConnectionPoints(
                circles,
                pathPointCounts,
                pointTypes,
                derivedPoints,
                ordinaryConnections);
            MarkPurchasePoints(
                circles,
                pathPointCounts,
                pointTypes,
                derivedPoints,
                purchaseAssignments);

            return new GeneratedPointData(
                MergeSamePositionDerivedPoints(derivedPoints),
                ordinaryConnections,
                purchaseAssignments,
                pathPointCounts);
        }

        private static int GetRequiredPointIndex(
            IReadOnlyDictionary<PathPointKey, int> pointIndexes,
            PathPointKey key)
        {
            if (!pointIndexes.TryGetValue(key, out int index))
            {
                throw new InvalidOperationException(
                    $"CUDA point ({key.CircleIndex}, {key.PointIndex}) does not exist.");
            }

            return index;
        }

        private static void SetConnect(int[] connect, int sourceIndex, int targetIndex)
        {
            if (connect[sourceIndex] != -1 && connect[sourceIndex] != targetIndex)
            {
                throw new InvalidOperationException(
                    $"CUDA point {sourceIndex} already connects to {connect[sourceIndex]}.");
            }

            connect[sourceIndex] = targetIndex;
        }

        /// <summary>
        /// 计算每个圆的路径点数量。
        /// 每个圆的路径点数量按需求公式 n = floor(2 * π * R) * M + 1 计算，其中 R = abs(r)，M = <see cref="PathPointDensityMultiplier"/>。
        /// </summary>
        /// <param name="circles">按圆序号顺序排列的圆集合。</param>
        /// <returns>路径点数量数组；数组下标为 circleIndex，数组值为该圆的路径点总数 n。</returns>
        /// <exception cref="InvalidOperationException">当任意圆的路径点数量超过 int.MaxValue 时抛出。</exception>
        private static int[] CalculatePathPointCounts(IReadOnlyList<CircleRecord> circles)
        {
            // pathPointCounts[circleIndex] 保存该圆的路径点总数 n。
            // 后续会用 n - 1 标记该圆的末尾联通点。
            int[] pathPointCounts = new int[circles.Count];

            // 按圆序号顺序逐个计算路径点数量，保证数组下标与 circleIndex 完全一致。
            for (int circleIndex = 0; circleIndex < circles.Count; circleIndex++)
            {
                // 单个圆的路径点数量由 CalculatePathPointCount 按需求公式计算。
                pathPointCounts[circleIndex] = CalculatePathPointCount(circles[circleIndex]);
            }

            // 返回后，调用方可以通过 pathPointCounts[circleIndex] 获取任意圆的路径点总数。
            return pathPointCounts;
        }

        /// <summary>
        /// 在所有圆的路径点中查找全场唯一收割点。
        /// 收割点应选择距离原点 (0, 0) 最近且不是末尾联通点的路径点。
        /// </summary>
        /// <param name="circles">按圆序号顺序排列的圆集合。</param>
        /// <param name="pathPointCounts">每个圆的路径点数量；数组下标为 circleIndex。</param>
        /// <returns>全场唯一收割点的位置，格式为 (circleIndex, pointIndex)。</returns>
        /// <exception cref="InvalidOperationException">当无法确定可用收割点时抛出。</exception>
        private static PathPointKey FindHarvestPoint(
            IReadOnlyList<CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts)
        {
            // bestCandidate 保存当前已经找到的最佳收割点候选；null 表示还没有找到任何候选。
            HarvestCandidate? bestCandidate = null;

            // 按圆序号顺序遍历所有圆，在每个圆上先找一个距离原点最近的可用路径点候选。
            for (int circleIndex = 0; circleIndex < circles.Count; circleIndex++)
            {
                // 在当前圆上寻找最接近原点且不是末尾联通点的收割点候选。
                HarvestCandidate candidate = FindNearestHarvestCandidate(
                    circles[circleIndex],
                    circleIndex,
                    pathPointCounts[circleIndex]);

                // 将当前圆的候选点与全场最佳候选点比较，按距离和并列排序规则保留更优者。
                if (IsBetterHarvestCandidate(candidate, bestCandidate))
                {
                    bestCandidate = candidate;
                }
            }

            // 所有圆都检查完以后，仍然没有候选点，说明无法确定全场唯一收割点。
            if (bestCandidate is null)
            {
                throw new InvalidOperationException("Harvest point cannot be determined.");
            }

            // 返回全场唯一收割点的位置；这里只返回圆序号和路径点序号，不返回坐标。
            return new PathPointKey(bestCandidate.Value.CircleIndex, bestCandidate.Value.PathPointIndex);
        }

        /// <summary>
        /// 在单个圆上查找距离原点 (0, 0) 最近的收割点候选。
        /// 该候选必须是当前圆上的路径点，并且不能是该圆的末尾联通点。
        /// </summary>
        /// <param name="circle">当前要检查的圆。</param>
        /// <param name="circleIndex">当前圆的圆序号。</param>
        /// <param name="pathPointCount">当前圆的路径点总数 n。</param>
        /// <returns>当前圆上的最佳收割点候选。</returns>
        /// <exception cref="InvalidOperationException">当当前圆上找不到可用收割点候选时抛出。</exception>
        private static HarvestCandidate FindNearestHarvestCandidate(
            CircleRecord circle,
            int circleIndex,
            int pathPointCount)
        {
            // 当前圆的圆心为 C(a, b)，真实几何半径为 R。
            // 目标是：在这个圆的所有路径点里，先找到“理论上距离原点最近”的几何方向。
            //
            // 圆上任意一点 P 可以用角度 θ 表示：
            // P(θ) = C + R * (cosθ, sinθ)
            //      = (a + R * cosθ, b + R * sinθ)。
            //
            // P 到原点 O(0, 0) 的距离平方为：
            // |P(θ)|^2 = (a + Rcosθ)^2 + (b + Rsinθ)^2
            //          = a^2 + b^2 + R^2 + 2R(a cosθ + b sinθ)。
            //
            // 其中 a^2 + b^2 + R^2 是常量；R 也是固定半径。
            // 所以要让 |P(θ)|^2 最小，本质上就是让 a cosθ + b sinθ 最小。
            //
            // a cosθ + b sinθ 是两个向量的点积：
            // (a, b) · (cosθ, sinθ)。
            // 点积在两个单位方向完全相反时取得最小值。
            // 因此，当 (cosθ, sinθ) 指向 (-a, -b) 时，圆上点 P 离原点最近。
            //
            // (-a, -b) 正是“从圆心 C(a, b) 指向原点 O(0, 0)”的向量。
            // Math.Atan2(y, x) 用来求向量 (x, y) 的方向角；
            // 所以 Math.Atan2(-b, -a) 就是在求圆心指向原点这条向量的角度。
            //
            // 如果圆心 C 正好等于原点，圆上所有点到原点的距离都等于 R，没有唯一最近方向。
            // 这种并列情况下稳定选择 0 角度，也就是圆上 x 最大的极点方向。
            double nearestAngle = circle.A == 0 && circle.B == 0
                ? 0.0
                : NormalizeAngle(Math.Atan2(-circle.B, -circle.A));

            // 路径点序号是按圆方向计算的。
            // 路径点 0 位于圆上 x 最大的极点，对应几何角度 θ = 0。
            // r > 0 时，圆按逆时针方向生成路径点：pointIndex / n = θ / 2π。
            // r < 0 时，圆按顺时针方向生成路径点：pointIndex / n = (-θ) / 2π。
            // 所以顺时针圆需要把 nearestAngle 取反，再换算成路径点序号。
            double pathDirectionAngle = circle.SignedRadius > 0
                ? nearestAngle
                : NormalizeAngle(-nearestAngle);

            // 将方向角度换算成理论路径点序号：rawIndex = angle / 2π * n。
            // rawIndex 通常不是整数，因为理论最近点不一定刚好落在离散路径点上。
            // 因此先取 floorIndex，再检查它附近的几个离散路径点。
            double rawIndex = pathDirectionAngle / TwoPi * pathPointCount;
            int floorIndex = (int)Math.Floor(rawIndex);

            // bestCandidate 保存当前圆上已经找到的最佳收割点候选。
            HarvestCandidate? bestCandidate = null;

            // 由于理论最近位置可能落在两个路径点之间，这里检查 floorIndex 前后几个点，避免取整误差导致选错候选。
            for (int offset = -3; offset <= 3; offset++)
            {
                // 将候选序号规范到 [0, pathPointCount - 1] 范围内，处理圆形路径上的越界回绕。
                int pathPointIndex = NormalizePathPointIndex(floorIndex + offset, pathPointCount);

                // 路径点 n - 1 是末尾联通点，需求规定不能被选为收割点。
                if (pathPointIndex == pathPointCount - 1)
                {
                    continue;
                }

                // 反推当前候选路径点的实际坐标。
                (double x, double y) = CalculatePathPointCoordinate(circle, pathPointIndex, pathPointCount);

                // 收割点比较使用到原点的距离平方：distance^2 = x^2 + y^2。
                // 平方距离和真实距离的大小顺序完全一致，但可以避免开平方。
                HarvestCandidate candidate = new(circleIndex, pathPointIndex, x * x + y * y);

                // 按距离和并列排序规则，保留当前圆上更好的收割点候选。
                if (IsBetterHarvestCandidate(candidate, bestCandidate))
                {
                    bestCandidate = candidate;
                }
            }

            // 当前圆所有候选都不可用时，说明无法为该圆提供收割点候选。
            if (bestCandidate is null)
            {
                throw new InvalidOperationException(
                    $"No available harvest candidate path point for circle {circleIndex}.");
            }

            // 返回当前圆上的最佳收割点候选，由上层再与其他圆的候选进行全场比较。
            return bestCandidate.Value;
        }

        /// <summary>
        /// 判断新的收割点候选是否优于当前最佳收割点候选。
        /// 比较顺序为：距离原点的平方距离更小者优先；距离在容差内视为并列时，圆序号更小者优先；圆序号也相同时，路径点序号更小者优先。
        /// </summary>
        /// <param name="candidate">新的收割点候选。</param>
        /// <param name="currentBest">当前最佳收割点候选；为 null 时表示还没有任何候选。</param>
        /// <returns>当 candidate 应替换 currentBest 时返回 true，否则返回 false。</returns>
        private static bool IsBetterHarvestCandidate(
            HarvestCandidate candidate,
            HarvestCandidate? currentBest)
        {
            // 如果当前还没有最佳候选，那么第一个合法候选必然成为当前最佳。
            if (currentBest is null)
            {
                return true;
            }

            // 收割点优先选择距离原点 (0, 0) 最近的路径点。
            // 这里比较的是距离平方 distance^2，而不是真实距离 distance。
            // 因为平方函数在非负数范围内单调递增：
            // d1 < d2  等价于  d1^2 < d2^2。
            // 所以比较距离平方不会改变远近顺序，同时可以避免开平方带来的额外计算和浮点误差。
            double distanceDifference = candidate.DistanceSquared - currentBest.Value.DistanceSquared;

            // distanceDifference < 0 表示 candidate 更近。
            // 但 double 计算存在微小误差，所以只有当 candidate 至少比 currentBest 小 DistanceTolerance，才认为它“明确更近”。
            // 也就是：candidate.DistanceSquared + DistanceTolerance < currentBest.DistanceSquared。
            if (distanceDifference < -DistanceTolerance)
            {
                return true;
            }

            // 如果两个平方距离的差值已经超过容差，并且没有进入上面的“candidate 明确更近”分支，
            // 那就说明 candidate 明确更远，不能替换 currentBest。
            if (Math.Abs(distanceDifference) > DistanceTolerance)
            {
                return false;
            }

            // 能走到这里，说明两个候选点到原点的平方距离在容差范围内，按需求视为距离并列。
            // 并列时使用稳定排序规则，先比较圆序号，保证多次运行选出的全场唯一收割点一致。
            if (candidate.CircleIndex != currentBest.Value.CircleIndex)
            {
                return candidate.CircleIndex < currentBest.Value.CircleIndex;
            }

            // 如果距离并列且圆序号也相同，则比较路径点序号。
            // 路径点序号更小者优先，这也是并列时的最后稳定规则。
            return candidate.PathPointIndex < currentBest.Value.PathPointIndex;
        }

        /// <summary>
        /// 计算并标记所有普通联通点，同时记录普通联通关系。
        /// </summary>
        /// <param name="circles">按圆生成顺序排列的圆集合。</param>
        /// <param name="pathPointCounts">每个圆对应的路径点数量。</param>
        /// <param name="pointTypes">路径点角色表，用来判断路径点是否已经被占用，并合并同一位置的 pointType。</param>
        /// <param name="derivedPoints">派生点明细列表，方法会把普通联通点追加到这里。</param>
        /// <param name="ordinaryConnections">普通联通关系列表，方法会把两个普通联通点之间的双向关系追加到这里。</param>
        private static void MarkOrdinaryConnectionPoints(
            IReadOnlyList<CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts,
            Dictionary<PathPointKey, int> pointTypes,
            List<DerivedPoint> derivedPoints,
            List<ConnectionPair> ordinaryConnections)
        {
            // leftIndex 从 0 开始遍历每个圆，作为当前圆对里的左侧圆序号。
            for (int leftIndex = 0; leftIndex < circles.Count; leftIndex++)
            {
                // rightIndex 从 leftIndex + 1 开始，保证每一组不同的两个圆只计算一次。
                // 例如 (0, 1) 算过以后，就不再重复计算 (1, 0)。
                for (int rightIndex = leftIndex + 1; rightIndex < circles.Count; rightIndex++)
                {
                    // 取出当前要判断普通联通关系的两个圆。
                    CircleRecord left = circles[leftIndex];
                    CircleRecord right = circles[rightIndex];

                    // 只有两个圆的圆周相交或相切，才有交点，也才可能建立普通联通关系。
                    // 一个圆完全包住另一个圆但圆周没有交点时，这里会跳过，不算普通联通。
                    if (!HasCircumferenceIntersectionOrTangency(left, right))
                    {
                        continue;
                    }

                    // 计算两个圆的圆周交点。
                    // 两圆相交时通常返回两个交点；两圆相切时返回一个交点。
                    // 需求要求：每一个交点都要尝试建立一条普通联通关系。
                    var intersections = CalculateCircleIntersections(left, right);
                    foreach (PointCoordinate intersection in intersections)
                    {
                        // 在 left 圆上，寻找距离当前交点最近的可用路径点。
                        // 可用路径点不能已经被占用型角色占用；普通联通点本身也是占用型角色。
                        PathPointKey leftPoint = FindNearestAvailablePathPoint(
                            left,
                            leftIndex,
                            pathPointCounts[leftIndex],
                            intersection,
                            pointTypes,
                            "ordinary connection");

                        // 在 right 圆上，使用同样规则寻找距离当前交点最近的可用路径点。
                        // 找到的两个路径点必须分别属于这两个圆，后续才可以组成普通联通关系。
                        PathPointKey rightPoint = FindNearestAvailablePathPoint(
                            right,
                            rightIndex,
                            pathPointCounts[rightIndex],
                            intersection,
                            pointTypes,
                            "ordinary connection");

                        // 把 left 圆上的路径点标记为普通联通点。
                        // occupyingRole 为 true，表示它会占用该路径点，防止后续普通联通点或采购点重复占用。
                        MarkPoint(pointTypes, derivedPoints, leftPoint, OrdinaryLinkPointCode, true);

                        // 把 right 圆上的路径点也标记为普通联通点。
                        // 这样一条普通联通关系一定由两个普通联通点组成。
                        MarkPoint(pointTypes, derivedPoints, rightPoint, OrdinaryLinkPointCode, true);

                        // 记录这两个普通联通点之间的普通联通关系。
                        // ordinaryConnections 只记录点对；后面构建 connect 时，再把它转换成 CUDA 使用的双向索引关系。
                        ordinaryConnections.Add(new ConnectionPair(
                            leftPoint.CircleIndex,
                            leftPoint.PointIndex,
                            rightPoint.CircleIndex,
                            rightPoint.PointIndex));
                    }
                }
            }
        }

        private static void WriteDxfRouteEntities(
            TextWriter writer,
            IReadOnlyList<CircleRecord> circles,
            int[] pathPointCounts,
            IReadOnlyList<DerivedPoint> points,
            IReadOnlyList<int> routePointIndexes)
        {
            if (routePointIndexes.Count < 2)
            {
                return;
            }

            for (int index = 0; index < routePointIndexes.Count - 1; index++)
            {
                int sourceIndex = routePointIndexes[index];
                int targetIndex = routePointIndexes[index + 1];

                if (sourceIndex < 0 || sourceIndex >= points.Count)
                {
                    throw new InvalidOperationException(
                        $"Route source point index {sourceIndex} is outside CUDA point range [0, {points.Count - 1}].");
                }

                if (targetIndex < 0 || targetIndex >= points.Count)
                {
                    throw new InvalidOperationException(
                        $"Route target point index {targetIndex} is outside CUDA point range [0, {points.Count - 1}].");
                }

                DerivedPoint sourcePoint = points[sourceIndex];
                DerivedPoint targetPoint = points[targetIndex];

                if (sourcePoint.CircleIndex == targetPoint.CircleIndex &&
                    !(sourcePoint.PointIndex == pathPointCounts[sourcePoint.CircleIndex] - 1 &&
                      targetPoint.PointIndex == 0))
                {
                    WriteDxfRouteArcEntity(
                        writer,
                        circles[sourcePoint.CircleIndex],
                        pathPointCounts[sourcePoint.CircleIndex],
                        sourcePoint.PointIndex,
                        targetPoint.PointIndex);
                }
                else
                {
                    (double sourceX, double sourceY) = CalculateDerivedPointCoordinate(
                        circles,
                        pathPointCounts,
                        sourcePoint);
                    (double targetX, double targetY) = CalculateDerivedPointCoordinate(
                        circles,
                        pathPointCounts,
                        targetPoint);

                    WriteDxfRouteLineEntity(writer, sourceX, sourceY, targetX, targetY);
                }
            }
        }

        private static void WriteDxfRouteArcEntity(
            TextWriter writer,
            CircleRecord circle,
            int pathPointCount,
            int sourcePathPointIndex,
            int targetPathPointIndex)
        {
            if (sourcePathPointIndex == targetPathPointIndex)
            {
                return;
            }

            (double startAngle, double endAngle) = CalculateDxfRouteArcAngles(
                circle,
                pathPointCount,
                sourcePathPointIndex,
                targetPathPointIndex);

            WriteDxfPair(writer, 0, "ARC");
            WriteDxfPair(writer, 8, "RoutePath");
            WriteDxfPair(writer, 62, RoutePathColor);
            WriteDxfPair(writer, 10, circle.A);
            WriteDxfPair(writer, 20, circle.B);
            WriteDxfPair(writer, 30, 0.0);
            WriteDxfPair(writer, 40, circle.Radius);
            WriteDxfPair(writer, 50, startAngle);
            WriteDxfPair(writer, 51, endAngle);
        }

        /// <summary>
        /// 计算路线弧线写入 DXF ARC 实体时使用的起止角度。
        /// DXF 的 ARC 实体固定按逆时针从 50 组角度画到 51 组角度；
        /// 因此这里必须先按 r 的正负计算路径点真实几何角度，再把顺时针圆反向写入 DXF。
        /// </summary>
        /// <param name="circle">路线所在圆。</param>
        /// <param name="pathPointCount">当前圆路径点总数 n。</param>
        /// <param name="sourcePathPointIndex">路线源路径点序号。</param>
        /// <param name="targetPathPointIndex">路线目标路径点序号。</param>
        /// <returns>DXF ARC 的起始角度和结束角度，单位为度。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当路径点数量或序号非法时抛出。</exception>
        internal static (double StartAngle, double EndAngle) CalculateDxfRouteArcAngles(
            CircleRecord circle,
            int pathPointCount,
            int sourcePathPointIndex,
            int targetPathPointIndex)
        {
            if (pathPointCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pathPointCount));
            }

            if (sourcePathPointIndex < 0 || sourcePathPointIndex >= pathPointCount)
            {
                throw new ArgumentOutOfRangeException(nameof(sourcePathPointIndex));
            }

            if (targetPathPointIndex < 0 || targetPathPointIndex >= pathPointCount)
            {
                throw new ArgumentOutOfRangeException(nameof(targetPathPointIndex));
            }

            double sourceAngle = CalculateDxfPathPointAngle(circle, sourcePathPointIndex, pathPointCount);
            double targetAngle = CalculateDxfPathPointAngle(circle, targetPathPointIndex, pathPointCount);

            return circle.SignedRadius > 0
                ? (sourceAngle, targetAngle)
                : (targetAngle, sourceAngle);
        }

        private static double CalculateDxfPathPointAngle(
            CircleRecord circle,
            int pathPointIndex,
            int pathPointCount)
        {
            double direction = circle.SignedRadius > 0 ? 1.0 : -1.0;
            return NormalizeDxfAngle(direction * 360.0 * pathPointIndex / pathPointCount);
        }

        private static void WriteDxfRouteLineEntity(
            TextWriter writer,
            double sourceX,
            double sourceY,
            double targetX,
            double targetY)
        {
            WriteDxfPair(writer, 0, "LINE");
            WriteDxfPair(writer, 8, "RoutePath");
            WriteDxfPair(writer, 62, RoutePathColor);
            WriteDxfPair(writer, 10, sourceX);
            WriteDxfPair(writer, 20, sourceY);
            WriteDxfPair(writer, 30, 0.0);
            WriteDxfPair(writer, 11, targetX);
            WriteDxfPair(writer, 21, targetY);
            WriteDxfPair(writer, 31, 0.0);
        }

        /// <summary>
        /// 按圆生成顺序，为每个圆分配并标记一个采购点。
        /// </summary>
        /// <param name="circles">按圆生成顺序排列的圆集合；集合下标就是圆序号。</param>
        /// <param name="pathPointCounts">每个圆对应的路径点数量，用于限制每个候选圆可选择的路径点范围。</param>
        /// <param name="pointTypes">路径点角色表，用来判断候选路径点是否已经被占用型角色占用。</param>
        /// <param name="derivedPoints">派生点明细列表；方法会把每个圆选中的采购点追加到该列表。</param>
        private static void MarkPurchasePoints(
            IReadOnlyList<CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts,
            Dictionary<PathPointKey, int> pointTypes,
            List<DerivedPoint> derivedPoints,
            List<PurchaseAssignment> purchaseAssignments)
        {
            // 按圆生成顺序依次处理每个圆。
            // 需求规定：每个圆必须分配一个采购点；采购点是占用型角色，同一个采购点最多只能被一个圆占用。
            for (int circleIndex = 0; circleIndex < circles.Count; circleIndex++)
            {
                // 为当前圆寻找采购点。
                // 注意：采购点不是只从当前圆自身路径点中找，而是从全场所有圆的路径点中找。
                // 选择标准是：排除已被占用的路径点后，找到距离当前圆圆心最近的路径点。
                PathPointKey purchasePoint = FindPurchasePoint(circles, pathPointCounts, circleIndex, pointTypes);

                // 把找到的路径点标记为采购点。
                // PurchasePointCode = 11；occupyingRole = true 表示采购点属于占用型角色。
                // MarkPoint 会维护 pointTypes 和 derivedPoints，并阻止采购点与其他占用型角色重复占用同一路径点。
                MarkPoint(pointTypes, derivedPoints, purchasePoint, PurchasePointCode, true);
                purchaseAssignments.Add(new PurchaseAssignment(circleIndex, purchasePoint.CircleIndex, purchasePoint.PointIndex));
            }
        }



        /// <summary>
        /// 从全场所有路径点中，为指定圆寻找距离其圆心最近的可用采购点。
        /// </summary>
        /// <param name="circles">按圆生成顺序排列的圆集合。</param>
        /// <param name="pathPointCounts">每个圆对应的路径点数量。</param>
        /// <param name="targetCircleIndex">当前需要分配采购点的圆序号。</param>
        /// <param name="pointTypes">路径点角色表，用来排除已经被占用型角色占用的候选点。</param>
        /// <returns>当前圆最终分配到的采购点位置。</returns>
        /// <exception cref="InvalidOperationException">当全场找不到任何可用采购点时抛出。</exception>
        private static PathPointKey FindPurchasePoint(
            IReadOnlyList<CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts,
            int targetCircleIndex,
            IReadOnlyDictionary<PathPointKey, int> pointTypes)
        {
            // 当前需要分配采购点的圆。
            CircleRecord targetCircle = circles[targetCircleIndex];

            // 采购点选择时，以当前圆的圆心为目标点。
            // 候选点可以来自任意圆，但距离统一计算为：候选路径点 P 到当前圆圆心 C 的距离。
            PointCoordinate target = new(targetCircle.A, targetCircle.B);

            // bestCandidate 保存全场当前最优采购点候选。
            SelectionCandidate? bestCandidate = null;

            // 遍历全场所有圆；每个圆都可能贡献采购点候选。
            for (int candidateCircleIndex = 0; candidateCircleIndex < circles.Count; candidateCircleIndex++)
            {
                CircleRecord candidateCircle = circles[candidateCircleIndex];
                int candidatePathPointCount = pathPointCounts[candidateCircleIndex];

                // 先在候选圆上反推“最接近当前圆圆心方向”的理论路径点序号。
                // 这样避免枚举该圆的全部路径点；正常情况下只需检查理论最近点附近的少量路径点。
                double targetAngle = NormalizeAngle(Math.Atan2(target.Y - candidateCircle.B, target.X - candidateCircle.A));
                double pathDirectionAngle = candidateCircle.SignedRadius > 0
                    ? targetAngle
                    : NormalizeAngle(-targetAngle);
                double rawIndex = pathDirectionAngle / TwoPi * candidatePathPointCount;
                int nearestIndex = NormalizePathPointIndex(
                    RoundPathPointIndexToNearest(rawIndex),
                    candidatePathPointCount);

                // 从候选圆的理论最近点开始向两侧扩散，找到该候选圆上最近的可用路径点。
                // 可用路径点必须不是收割点、末尾联通点、普通联通点，也不能已经被其他采购点占用。
                for (int offset = 0; offset < candidatePathPointCount; offset++)
                {
                    SelectionCandidate? circleBestCandidate = null;

                    ConsiderSelectionCandidate(
                        candidateCircle,
                        candidateCircleIndex,
                        candidatePathPointCount,
                        target,
                        NormalizePathPointIndex((long)nearestIndex + offset, candidatePathPointCount),
                        pointTypes,
                        ref circleBestCandidate);

                    if (offset > 0)
                    {
                        ConsiderSelectionCandidate(
                            candidateCircle,
                            candidateCircleIndex,
                            candidatePathPointCount,
                            target,
                            NormalizePathPointIndex((long)nearestIndex - offset, candidatePathPointCount),
                            pointTypes,
                            ref circleBestCandidate);
                    }

                    // 当前候选圆这一圈已经找到可用点时，它就是该候选圆上距离目标圆心最近的可用点。
                    // 再把它拿去和全场最佳候选点比较。
                    if (circleBestCandidate is not null)
                    {
                        if (IsBetterSelectionCandidate(circleBestCandidate.Value, bestCandidate))
                        {
                            bestCandidate = circleBestCandidate.Value;
                        }

                        break;
                    }
                }
            }

            if (bestCandidate is null)
            {
                throw new InvalidOperationException($"No available purchase point for circle {targetCircleIndex}.");
            }

            return bestCandidate.Value.Key;
        }

        /// <summary>
        /// 在指定圆上，寻找距离目标坐标最近的可用路径点。
        /// </summary>
        /// <param name="circle">要查找路径点的圆。</param>
        /// <param name="circleIndex">当前圆的圆序号。</param>
        /// <param name="pathPointCount">当前圆的路径点数量。</param>
        /// <param name="target">目标坐标，通常是两个圆的圆周交点。</param>
        /// <param name="pointTypes">已经标记过的路径点角色表，用来判断路径点是否可被占用。</param>
        /// <param name="roleName">当前要分配的角色名称，只用于报错信息，便于定位失败场景。</param>
        /// <returns>距离目标坐标最近、且还能被占用型角色使用的路径点。</returns>
        /// <exception cref="InvalidOperationException">当当前圆上找不到任何可用路径点时抛出。</exception>
        private static PathPointKey FindNearestAvailablePathPoint(
            CircleRecord circle,
            int circleIndex,
            int pathPointCount,
            PointCoordinate target,
            IReadOnlyDictionary<PathPointKey, int> pointTypes,
            string roleName)
        {
            // 设圆心为 C(a, b)，目标点为 T(x, y)。
            // 这里的 target 通常是两个圆的圆周交点，它一定是一个平面坐标点。
            //
            // 要在当前圆上找离 T 最近的路径点，第一步不是枚举全部路径点，
            // 而是先求出“从圆心 C 指向目标点 T 的方向角”。
            //
            // 从 C 指向 T 的向量为：
            // V = T - C = (x - a, y - b)。
            // 如果把圆心 C 当作局部坐标原点，那么圆上的任意点都可以写成：
            // P(θ) = C + R * (cosθ, sinθ)。
            // 当 θ 与向量 V 的方向一致时，P(θ) 就是圆周上朝向目标点 T 的那个理论最近位置。
            //
            // Math.Atan2(vy, vx) 会返回向量 (vx, vy) 的方向角，范围通常是 (-π, π]。
            // NormalizeAngle 再把它统一转成 [0, 2π)，后续才能稳定换算成路径点序号。
            double targetAngle = NormalizeAngle(Math.Atan2(target.Y - circle.B, target.X - circle.A));

            // 路径点序号不是单纯按几何角度增长，而是要服从圆的方向：
            // 1. r > 0：逆时针圆/做多圆。
            //    第 0 个路径点在 x 最大的极点，随后序号按逆时针增加。
            //    数学上的标准角度 θ 也是从 x 正方向开始，按逆时针增加。
            //    所以逆时针圆里，路径点方向角 = targetAngle。
            //
            // 2. r < 0：顺时针圆/做空圆。
            //    第 0 个路径点仍然在 x 最大的极点，但随后序号按顺时针增加。
            //    顺时针方向与数学标准角度方向相反，因此需要取 -targetAngle。
            //    取反后可能变成负角度，所以再 NormalizeAngle 到 [0, 2π)。
            double pathDirectionAngle = circle.SignedRadius > 0
                ? targetAngle
                : NormalizeAngle(-targetAngle);

            // 将方向角换算成理论路径点序号。
            // 当前圆一整圈是 2π，被离散成 pathPointCount 个路径点。
            //
            // 方向比例 = pathDirectionAngle / 2π。
            // 理论序号 = 方向比例 * pathPointCount。
            //
            // 例如：
            // pathDirectionAngle = 0       -> rawIndex = 0，对应 x 最大的极点。
            // pathDirectionAngle = π       -> rawIndex ≈ pathPointCount / 2，对应 x 最小方向。
            // pathDirectionAngle = π / 2   -> rawIndex ≈ pathPointCount / 4，对应 y 最大方向。
            //
            // rawIndex 是 double，因为目标方向通常不会刚好落在某个整数路径点序号上。
            double rawIndex = pathDirectionAngle / TwoPi * pathPointCount;

            // rawIndex 是理论浮点序号，而实际路径点序号必须是整数。
            // 因此先把 rawIndex 四舍五入成最靠近目标方向的路径点序号。
            //
            // RoundPathPointIndexToNearest 的作用是处理 .5 边界：
            // 例如 rawIndex = 10.5 时，两个相邻路径点距离理论方向一样近。
            // 按通用并列排序规则，应稳定选择序号更小的 10，而不是偏向 11。
            //
            // NormalizePathPointIndex 用于处理边界：
            // 如果四舍五入后等于 pathPointCount，就绕回 0；
            // 如果后续扩散搜索出现负数，也能绕回圆尾部。
            int nearestIndex = NormalizePathPointIndex(
                RoundPathPointIndexToNearest(rawIndex),
                pathPointCount);

            // 从 nearestIndex 开始向两边扩散查找可用路径点。
            //
            // 为什么可以这样找？
            // 因为路径点沿圆周按序号排列，相邻序号对应相邻角度。
            // nearestIndex 是理论最近方向，所以 offset 越小，候选点通常离 target 越近。
            // 如果 nearestIndex 已经被占用，就检查 nearestIndex + 1 和 nearestIndex - 1；
            // 再不行就检查 +2 和 -2，依此类推。
            //
            // 这里最多循环 pathPointCount 次，表示最坏情况下把整圆路径点都检查一遍。
            for (int offset = 0; offset < pathPointCount; offset++)
            {
                // bestCandidate 只保存当前 offset 这一圈里的最佳候选点。
                // offset = 0 时只有一个候选点；offset > 0 时最多有左右两个候选点。
                SelectionCandidate? bestCandidate = null;

                // 检查右侧候选点：nearestIndex + offset。
                // NormalizePathPointIndex 会把超过末尾的序号绕回开头。
                // 例如 pathPointCount = 100，nearestIndex = 98，offset = 3，实际检查序号 1。
                ConsiderSelectionCandidate(
                    circle,
                    circleIndex,
                    pathPointCount,
                    target,
                    NormalizePathPointIndex((long)nearestIndex + offset, pathPointCount),
                    pointTypes,
                    ref bestCandidate);

                // offset = 0 时，nearestIndex + 0 和 nearestIndex - 0 是同一个点，不能重复检查。
                // offset > 0 时，再检查左侧候选点：nearestIndex - offset。
                // 这样每一圈都按“离理论最近序号相同步长”的左右两个方向比较。
                if (offset > 0)
                {
                    ConsiderSelectionCandidate(
                        circle,
                        circleIndex,
                        pathPointCount,
                        target,
                        NormalizePathPointIndex((long)nearestIndex - offset, pathPointCount),
                        pointTypes,
                        ref bestCandidate);
                }

                // 如果当前 offset 这一圈找到了可用路径点，就立即返回。
                //
                // 因为搜索是从理论最近序号开始逐步向外扩散的，
                // 当前 offset 是第一圈出现可用点的位置；继续往外找只会离理论最近方向更远。
                //
                // 如果同一圈左右两个候选点都可用，ConsiderSelectionCandidate 会比较实际坐标距离平方，
                // 并在距离并列时使用稳定规则，保证结果可复现。
                if (bestCandidate is not null)
                {
                    return bestCandidate.Value.Key;
                }
            }

            // 能走到这里，说明当前圆上没有任何可用于该角色的路径点。
            // 按需求，普通联通点或采购点计算失败时必须报错，不能悄悄跳过。
            throw new InvalidOperationException(
                $"No available path point for {roleName} on circle {circleIndex}.");
        }


        private static void ConsiderSelectionCandidate(
            CircleRecord circle,
            int circleIndex,
            int pathPointCount,
            PointCoordinate target,
            int pointIndex,
            IReadOnlyDictionary<PathPointKey, int> pointTypes,
            ref SelectionCandidate? bestCandidate)
        {
            PathPointKey key = new(circleIndex, pointIndex);

            if (!IsAvailableForOccupyingRole(key, pathPointCount, pointTypes))
            {
                return;
            }

            (double x, double y) = CalculatePathPointCoordinate(circle, pointIndex, pathPointCount);
            double dx = x - target.X;
            double dy = y - target.Y;
            SelectionCandidate candidate = new(key, dx * dx + dy * dy);

            if (IsBetterSelectionCandidate(candidate, bestCandidate))
            {
                bestCandidate = candidate;
            }
        }

        private static bool IsBetterSelectionCandidate(
            SelectionCandidate candidate,
            SelectionCandidate? currentBest)
        {
            if (currentBest is null)
            {
                return true;
            }

            double distanceDifference = candidate.DistanceSquared - currentBest.Value.DistanceSquared;

            if (distanceDifference < -DistanceTolerance)
            {
                return true;
            }

            if (Math.Abs(distanceDifference) > DistanceTolerance)
            {
                return false;
            }

            if (candidate.Key.CircleIndex != currentBest.Value.Key.CircleIndex)
            {
                return candidate.Key.CircleIndex < currentBest.Value.Key.CircleIndex;
            }

            return candidate.Key.PointIndex < currentBest.Value.Key.PointIndex;
        }


        private static bool IsAvailableForOccupyingRole(
            PathPointKey key,
            int pathPointCount,
            IReadOnlyDictionary<PathPointKey, int> pointTypes)
        {
            if (key.PointIndex == pathPointCount - 1)
            {
                return false;
            }

            return !pointTypes.TryGetValue(key, out int pointType) || !HasOccupyingRole(pointType);
        }

        /// <summary>
        /// 给指定路径点标记一个角色编码，并维护该路径点的合成 pointType。
        /// 角色编码使用需求中定义的质数编码；同一路径点拥有多个角色时，通过乘积合并为合数编码。
        /// </summary>
        /// <param name="pointTypes">按路径点位置记录当前合成 pointType 的字典，key 为 (circleIndex, pointIndex)。</param>
        /// <param name="derivedPoints">保存尚未最终合并的派生点明细，每次新增角色先记录一条单角色派生点。</param>
        /// <param name="key">要标记角色的路径点位置。</param>
        /// <param name="roleCode">要标记的角色编码，例如收割点 2、第一路径点 3、末尾联通点 5、普通联通点 7、采购点 11。</param>
        /// <param name="occupyingRole">是否为占用型角色；收割点、末尾联通点、普通联通点、采购点属于占用型角色。</param>
        /// <exception cref="InvalidOperationException">当同一路径点重复标记占用型角色，或已经存在其他占用型角色时抛出。</exception>
        private static void MarkPoint(
            Dictionary<PathPointKey, int> pointTypes,
            List<DerivedPoint> derivedPoints,
            PathPointKey key,
            int roleCode,
            bool occupyingRole)
        {
            // 先读取该路径点当前已经拥有的合成 pointType。
            // 如果这个路径点从未出现过，先按普通路径点编码 1 建立记录。
            if (!pointTypes.TryGetValue(key, out int pointType))
            {
                pointType = NormalPathPointCode;
                pointTypes.Add(key, pointType);
            }

            // 角色编码是质数。
            // 如果 pointType 能被 roleCode 整除，说明该路径点已经包含当前角色。
            // 例如 pointType = 6 时，6 % 2 == 0 表示它已经是收割点，6 % 3 == 0 表示它也是第一路径点。
            if (pointType % roleCode == 0)
            {
                // 占用型角色不能重复标记。
                // 例如同一个路径点不能被两次标记为普通联通点，也不能重复成为采购点。
                if (occupyingRole)
                {
                    throw new InvalidOperationException(
                        $"Path point ({key.CircleIndex}, {key.PointIndex}) is already occupied by role {roleCode}.");
                }

                // 非占用型角色重复标记时直接忽略。
                // 当前主要用于第一路径点标记，它只是 pointIndex = 0 的标记，不属于占用型角色。
                return;
            }

            // 如果当前要添加的是占用型角色，则必须确认这个路径点还没有其他占用型角色。
            // 需求规定：收割点、末尾联通点、普通联通点、采购点互斥；一个路径点最多只能拥有一个占用型角色。
            if (occupyingRole && HasOccupyingRole(pointType))
            {
                throw new InvalidOperationException(
                    $"Path point ({key.CircleIndex}, {key.PointIndex}) already has an occupying role.");
            }

            // 将新角色编码乘入当前 pointType，形成合成编码。
            // 例如第一路径点 3 再叠加收割点 2，得到 pointType = 6。
            pointTypes[key] = pointType * roleCode;

            // derivedPoints 先记录本次新增的单角色派生点。
            // 后续 MergeSamePositionDerivedPoints 会按 (circleIndex, pointIndex) 合并同一位置的角色，并把 pointType 相乘。
            derivedPoints.Add(new DerivedPoint(key.CircleIndex, key.PointIndex, roleCode));
        }

        private static bool HasOccupyingRole(int pointType)
        {
            return pointType % HarvestPointCode == 0 ||
                   pointType % TerminalLinkPointCode == 0 ||
                   pointType % OrdinaryLinkPointCode == 0 ||
                   pointType % PurchasePointCode == 0;
        }

        private static IReadOnlyList<DerivedPoint> MergeSamePositionDerivedPoints(
            IReadOnlyList<DerivedPoint> derivedPoints)
        {
            List<DerivedPoint> mergedPoints = new();
            Dictionary<PathPointKey, int> mergedIndexes = new();

            foreach (DerivedPoint point in derivedPoints)
            {
                PathPointKey key = new(point.CircleIndex, point.PointIndex);

                if (!mergedIndexes.TryGetValue(key, out int mergedIndex))
                {
                    mergedIndexes.Add(key, mergedPoints.Count);
                    mergedPoints.Add(point);
                    continue;
                }

                DerivedPoint existing = mergedPoints[mergedIndex];
                int mergedPointType = checked(existing.PointType * point.PointType);
                mergedPoints[mergedIndex] = new DerivedPoint(point.CircleIndex, point.PointIndex, mergedPointType);
            }

            return mergedPoints;
        }

        private static IReadOnlyList<PointCoordinate> CalculateCircleIntersections(
            CircleRecord left,
            CircleRecord right)
        {
            double dx = right.A - left.A;
            double dy = right.B - left.B;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance <= DistanceTolerance)
            {
                return Array.Empty<PointCoordinate>();
            }

            double leftRadius = left.Radius;
            double rightRadius = right.Radius;
            double centerLineDistance = (leftRadius * leftRadius - rightRadius * rightRadius + distance * distance) /
                (2.0 * distance);
            double heightSquared = leftRadius * leftRadius - centerLineDistance * centerLineDistance;

            if (heightSquared < -DistanceTolerance)
            {
                return Array.Empty<PointCoordinate>();
            }

            if (heightSquared < 0.0)
            {
                heightSquared = 0.0;
            }

            double baseX = left.A + centerLineDistance * dx / distance;
            double baseY = left.B + centerLineDistance * dy / distance;
            double height = Math.Sqrt(heightSquared);

            if (height <= DistanceTolerance)
            {
                return new[] { new PointCoordinate(baseX, baseY) };
            }

            double offsetX = -dy / distance * height;
            double offsetY = dx / distance * height;

            PointCoordinate first = new(baseX + offsetX, baseY + offsetY);
            PointCoordinate second = new(baseX - offsetX, baseY - offsetY);

            return first.X < second.X ||
                   Math.Abs(first.X - second.X) <= DistanceTolerance && first.Y <= second.Y
                ? new[] { first, second }
                : new[] { second, first };
        }

        private static double NormalizeAngle(double angle)
        {
            double normalized = angle % TwoPi;

            if (normalized < 0)
            {
                normalized += TwoPi;
            }

            return normalized;
        }

        private static long RoundPathPointIndexToNearest(double rawIndex)
        {
            double floorValue = Math.Floor(rawIndex);
            double fraction = rawIndex - floorValue;

            if (fraction <= 0.5 + IndexTolerance)
            {
                return (long)floorValue;
            }

            return (long)floorValue + 1L;
        }

        private static int NormalizePathPointIndex(long pathPointIndex, int pathPointCount)
        {
            long normalized = pathPointIndex % pathPointCount;

            if (normalized < 0)
            {
                normalized += pathPointCount;
            }

            return (int)normalized;
        }

        private static double NormalizeDxfAngle(double angle)
        {
            double normalized = angle % 360.0;

            if (normalized < 0.0)
            {
                normalized += 360.0;
            }

            return normalized;
        }

        private static void WriteDxfLayer(
            TextWriter writer,
            string layerName,
            int color,
            int? transparency = null,
            int? lineWeight = null)
        {
            WriteDxfPair(writer, 0, "LAYER");
            WriteDxfPair(writer, 2, layerName);
            WriteDxfPair(writer, 70, 0);
            WriteDxfPair(writer, 62, color);
            WriteDxfPair(writer, 6, "CONTINUOUS");
            if (lineWeight.HasValue)
            {
                WriteDxfPair(writer, 370, lineWeight.Value);
            }

            if (transparency.HasValue)
            {
                WriteDxfPair(writer, 440, transparency.Value);
            }
        }

        private static void WriteDxfPair(TextWriter writer, int groupCode, string value)
        {
            writer.WriteLine(groupCode.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine(value);
        }

        private static void WriteDxfPair(TextWriter writer, int groupCode, int value)
        {
            writer.WriteLine(groupCode.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteDxfPair(TextWriter writer, int groupCode, double value)
        {
            writer.WriteLine(groupCode.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine(value.ToString("G17", CultureInfo.InvariantCulture));
        }

        internal static List<CircleRecord> BuildConnectedCircles(int count)
        {
            List<CircleRecord> circles = new(count);
            HashSet<GeometryKey> geometries = new();

            foreach (CircleRecord seed in SeedCircles)
            {
                AddCircle(seed, circles, geometries);
            }

            while (circles.Count < count)
            {
                AddGeneratedCirclePreservingSingleConnectedComponent(circles, geometries);
            }

            return circles;
        }

        /// <summary>
        /// 生成并加入一个新圆，同时保证加入后所有圆仍然只属于一个连通分量。
        /// </summary>
        /// <param name="circles">
        /// 当前已有圆集合。
        /// 方法成功返回后，该集合会新增一个圆；如果所有候选圆都失败，则集合应保持在失败前的有效状态。
        /// </param>
        /// <param name="geometries">
        /// 当前已有圆的几何唯一性集合，保存 (a, b, abs(r))。
        /// 方法成功返回后，该集合会同步新增成功圆的 GeometryKey。
        /// </param>
        /// <remarks>
        /// 这个方法是补圆流程的“外层重试器”。
        /// 每次循环先生成一个满足基本约束的候选圆，再调用 <see cref="TryAddCirclePreservingSingleConnectedComponent"/>
        /// 尝试把它加入圆集合。
        /// 如果候选圆加入后导致整体连通分量不为一，下层方法会负责撤销该候选圆，然后这里继续生成下一个候选圆。
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// 在 <see cref="MaxAttemptsPerCircle"/> 次尝试内仍然无法加入一个满足单一连通分量约束的新圆时抛出。
        /// </exception>
        private static void AddGeneratedCirclePreservingSingleConnectedComponent(
            List<CircleRecord> circles,
            HashSet<GeometryKey> geometries)
        {
            // 最多尝试 MaxAttemptsPerCircle 次。
            // 每一次尝试目标都是：成功新增一个圆，并且新增后整体仍然是单一连通分量。
            for (int attempt = 0; attempt < MaxAttemptsPerCircle; attempt++)
            {
                // 生成一个随机候选圆。
                // GenerateConnectedRandomCircle 会保证候选圆满足：
                // 1. 半径和边界合法；
                // 2. 几何键 (a, b, abs(r)) 不重复；
                // 3. 至少与已有圆集合中的一个圆相交或相切。
                CircleRecord candidate = GenerateConnectedRandomCircle(circles, geometries);

                // 尝试把候选圆加入集合。
                // 如果加入后仍然是单一连通分量，说明本次补圆成功，直接返回。
                // 如果加入后连通分量不是一，TryAdd... 会删除刚加的候选圆并返回 false。
                if (TryAddCirclePreservingSingleConnectedComponent(candidate, circles, geometries))
                {
                    // 成功新增一个圆，本方法任务完成。
                    return;
                }
            }

            // 能走到这里，说明连续 MaxAttemptsPerCircle 次都没有找到可用候选圆。
            // 继续死循环没有意义，因此直接报错，让上层知道当前参数或随机空间已经无法满足约束。
            throw new InvalidOperationException(
                $"Failed to add a circle while preserving one connected component after {MaxAttemptsPerCircle} attempts.");
        }

        private static bool TryAddCirclePreservingSingleConnectedComponent(
            CircleRecord circle,
            List<CircleRecord> circles,
            HashSet<GeometryKey> geometries)
        {
            AddCircle(circle, circles, geometries);

            try
            {
                EnsureSingleConnectedComponent(circles);
                return true;
            }
            catch (InvalidOperationException)
            {
                circles.RemoveAt(circles.Count - 1);
                geometries.Remove(circle.GeometryKey);
                return false;
            }
        }

        private static CircleRecord GenerateConnectedRandomCircle(
            IReadOnlyList<CircleRecord> existingCircles,
            HashSet<GeometryKey> geometries)
        {
            for (int attempt = 0; attempt < MaxAttemptsPerCircle; attempt++)
            {
                int radius = Random.Shared.Next(MinRadius, MaxRadius + 1);
                int a = Random.Shared.Next(-MaxX + radius, MaxX - radius + 1);
                int b = Random.Shared.Next(-MaxY + radius, MaxY - radius + 1);
                int signedRadius = Random.Shared.Next(2) == 0 ? radius : -radius;

                CircleRecord candidate = new(a, b, signedRadius);

                if (geometries.Contains(candidate.GeometryKey))
                {
                    continue;
                }

                if (CanConnectToAnyExistingCircle(candidate, existingCircles))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException(
                $"Failed to generate a connected circle after {MaxAttemptsPerCircle} attempts.");
        }

        private static void AddCircle(
            CircleRecord circle,
            List<CircleRecord> circles,
            HashSet<GeometryKey> geometries)
        {
            ValidateCircle(circle);

            if (!geometries.Add(circle.GeometryKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate geometry circle: ({circle.A}, {circle.B}, {circle.Radius}).");
            }

            if (circles.Count > 0 && !CanConnectToAnyExistingCircle(circle, circles))
            {
                throw new InvalidOperationException(
                    $"Circle does not connect to the existing circle set: ({circle.A}, {circle.B}, {circle.SignedRadius}).");
            }

            circles.Add(circle);
        }

        internal static void ValidateCircle(CircleRecord circle)
        {
            if (circle.Radius < MinRadius)
            {
                throw new InvalidOperationException("Circle geometry radius must be at least 10.");
            }

            if (Math.Abs((long)circle.A) + circle.Radius > MaxX ||
                Math.Abs((long)circle.B) + circle.Radius > MaxY)
            {
                throw new InvalidOperationException(
                    $"Circle is out of bounds: ({circle.A}, {circle.B}, {circle.SignedRadius}).");
            }
        }

        private static List<CircleRecord> ReadCircles(string inputPath)
        {
            FileInfo fileInfo = new(inputPath);
            int recordSize = 3 * sizeof(int);

            if (fileInfo.Length % recordSize != 0)
            {
                throw new InvalidOperationException(
                    $"{CircleFileName} is corrupted: file length must be a multiple of {recordSize} bytes.");
            }

            List<CircleRecord> circles = new((int)(fileInfo.Length / recordSize));

            using FileStream stream = File.OpenRead(inputPath);
            using BinaryReader reader = new(stream);

            while (stream.Position < stream.Length)
            {
                circles.Add(new CircleRecord(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32()));
            }

            return circles;
        }

        private static void WriteCircles(string outputPath, IReadOnlyList<CircleRecord> circles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using FileStream stream = File.Create(outputPath);
            using BinaryWriter writer = new(stream);

            foreach (CircleRecord circle in circles)
            {
                writer.Write(circle.A);
                writer.Write(circle.B);
                writer.Write(circle.SignedRadius);
            }
        }

        /// <summary>
        /// 根据圆集合构建几何唯一键集合，并校验每个圆记录是否合法。
        /// 几何唯一键只使用 (a, b, R)，其中 R = abs(r)；因此相同几何圆不允许以不同方向重复存在。
        /// </summary>
        /// <param name="circles">按圆序号顺序排列的圆集合。</param>
        /// <returns>包含所有圆几何唯一键的集合。</returns>
        /// <exception cref="InvalidOperationException">当圆记录非法，或存在重复几何圆 (a, b, R) 时抛出。</exception>
        private static HashSet<GeometryKey> BuildGeometrySet(IReadOnlyList<CircleRecord> circles)
        {
            HashSet<GeometryKey> geometries = new();

            foreach (CircleRecord circle in circles)
            {
                ValidateCircle(circle);

                if (!geometries.Add(circle.GeometryKey))
                {
                    throw new InvalidOperationException(
                        $"Duplicate geometry circle: ({circle.A}, {circle.B}, {circle.Radius}).");
                }
            }

            return geometries;
        }

        /// <summary>
        /// 校验圆集合是否只形成一个连通分量。
        /// 连通关系以圆周相交或相切为准；完全包含但圆周没有交点的两个圆不视为直接连通。
        /// </summary>
        /// <param name="circles">按圆序号顺序排列的圆集合。</param>
        /// <exception cref="InvalidOperationException">当圆集合被拆成多个互不连通的圆组时抛出。</exception>
        private static void EnsureSingleConnectedComponent(IReadOnlyList<CircleRecord> circles)
        {
            // 如果圆集合为空或只有一个圆，就不可能分裂成多个互不连通的圆组。
            if (circles.Count <= 1)
            {
                // 0 个圆或 1 个圆天然满足“单一连通分量”约束，直接返回。
                return;
            }

            // visited 记录已经确认与 0 号圆处于同一连通分量的圆序号；初始只包含 0 号圆。
            HashSet<int> visited = new() { 0 };

            // pending 是待扩展队列：里面放的是“已经确认属于同一个连通分量，但还没有继续查找其直接连通圆”的圆序号。
            Queue<int> pending = new();
            // 使用队列是为了按广度优先搜索顺序扩展同一个连通分量。
            // 从 0 号圆开始做广度优先搜索；后续能从 0 号圆一路连过去的圆，都会进入 visited。
            pending.Enqueue(0);

            // 只要还有待扩展的圆，就继续向外查找与它圆周相交或相切的圆。
            while (pending.Count > 0)
            {
                // 取出一个当前要扩展的圆序号。
                //Dequeue() 会把队头元素拿出来，并且从队列里删除。
                int current = pending.Dequeue();

                // 用 current 圆去和全量圆集合逐个比较，寻找尚未访问过的直接连通圆。
                for (int next = 0; next < circles.Count; next++)
                {
                    // 如果 next 圆已经确认连通，就不重复处理，避免死循环和重复入队。
                    if (visited.Contains(next))
                    {
                        // 已访问过的圆跳过。
                        continue;
                    }

                    // 判断 current 圆和 next 圆的圆周是否相交或相切；这是两个圆直接连通的定义。
                    if (!HasCircumferenceIntersectionOrTangency(circles[current], circles[next]))
                    {
                        // 两个圆周没有交点，也不是相切，说明 current 不能直接走到 next。
                        continue;
                    }

                    // 能走到这里，说明 next 圆与 current 圆直接连通，因此 next 也属于 0 号圆所在的连通分量。
                    visited.Add(next);

                    // 把 next 放入队列，后面继续用它向外扩展，查找 next 能连到的其他圆。
                    pending.Enqueue(next);
                }
            }

            // 广度优先搜索结束后，如果 visited 数量小于总圆数，说明还有圆无法从 0 号圆到达。
            if (visited.Count != circles.Count)
            {
                // 存在至少一个独立圆组，违反“所有圆必须形成一个整体连通集合”的需求。
                throw new InvalidOperationException("Circle set must remain one connected component.");
            }
        }

        private static bool CanConnectToAnyExistingCircle(
            CircleRecord candidate,
            IReadOnlyList<CircleRecord> existingCircles)
        {
            foreach (CircleRecord existing in existingCircles)
            {
                if (HasCircumferenceIntersectionOrTangency(candidate, existing))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断两个圆的圆周是否相交或相切。
        /// 完全包含但圆周没有交点的两个圆不算相交；只有圆周存在一个或两个交点时才返回 true。
        /// </summary>
        /// <param name="left">第一个圆。</param>
        /// <param name="right">第二个圆。</param>
        /// <returns>两个圆的圆周相交或相切时返回 true，否则返回 false。</returns>
        internal static bool HasCircumferenceIntersectionOrTangency(CircleRecord left, CircleRecord right)
        {
            // 设两个圆心分别为 C1(left.A, left.B)、C2(right.A, right.B)。
            // dx、dy 是两个圆心在 x、y 方向上的差值。
            long dx = (long)left.A - right.A;
            long dy = (long)left.B - right.B;

            // distanceSquared = d^2，其中 d 是两个圆心之间的距离。
            // 这里不计算 sqrt(dx * dx + dy * dy)，而是直接比较平方值，避免浮点误差。
            long distanceSquared = dx * dx + dy * dy;

            // radiusSum = R1 + R2。
            // 如果 d > R1 + R2，两个圆离得太远，圆周没有交点。
            long radiusSum = (long)left.Radius + right.Radius;

            // radiusDifference = |R1 - R2|。
            // 如果 d < |R1 - R2|，说明一个圆完全包住另一个圆，并且两条圆周没有交点。
            long radiusDifference = Math.Abs((long)left.Radius - right.Radius);

            // 两个圆的圆周相交或相切的充要条件是：
            // |R1 - R2| <= d <= R1 + R2。
            // 为了避免开平方，把两边同时平方，得到：
            // (R1 - R2)^2 <= d^2 <= (R1 + R2)^2。
            // 等号成立时表示内切或外切；严格在中间时表示两个交点。
            return distanceSquared >= radiusDifference * radiusDifference &&
                   distanceSquared <= radiusSum * radiusSum;
        }

        /// <summary>
        /// 表示 Circle.bin 中读取或写入的一条圆记录。
        /// 这里使用普通 readonly struct 写法，明确写出构造函数和属性，方便逐行走查。
        /// </summary>
        internal readonly struct CircleRecord
        {
            /// <summary>
            /// 创建一条圆记录。
            /// </summary>
            /// <param name="a">圆心 x 坐标，对应需求中的 a。</param>
            /// <param name="b">圆心 y 坐标，对应需求中的 b。</param>
            /// <param name="signedRadius">带方向的有符号半径 r；正负号表示圆方向。</param>
            public CircleRecord(int a, int b, int signedRadius)
            {
                A = a;
                B = b;
                SignedRadius = signedRadius;
            }

            /// <summary>
            /// 圆心 x 坐标，对应需求中的 a。
            /// </summary>
            public int A { get; }

            /// <summary>
            /// 圆心 y 坐标，对应需求中的 b。
            /// </summary>
            public int B { get; }

            /// <summary>
            /// 带方向的有符号半径 r；正负号表示圆方向。
            /// </summary>
            public int SignedRadius { get; }

            /// <summary>
            /// 真实几何半径 R，始终取 <see cref="SignedRadius"/> 的绝对值。
            /// 几何判断只使用该值，不使用 r 的正负方向。
            /// </summary>
            public int Radius
            {
                get
                {
                    // int.MinValue 取绝对值会溢出，因此这里单独拦截，避免生成非法真实几何半径。
                    if (SignedRadius == int.MinValue)
                    {
                        throw new InvalidOperationException("Circle signed radius is outside the supported range.");
                    }

                    // R = abs(r)。r 的正负号只表达方向；几何半径必须是非负长度。
                    return Math.Abs(SignedRadius);
                }
            }

            /// <summary>
            /// 当前圆的几何唯一键，只使用 (a, b, R)，不包含 r 的方向正负号。
            /// 因此相同几何圆不允许以不同方向重复存在。
            /// </summary>
            public GeometryKey GeometryKey => new(A, B, Radius);
        }

        /// <summary>
        /// 圆的几何唯一键，用于校验不能存在重复几何圆 (a, b, R)。
        /// </summary>
        internal readonly struct GeometryKey : IEquatable<GeometryKey>
        {
            /// <summary>
            /// 创建圆的几何唯一键。
            /// </summary>
            public GeometryKey(int a, int b, int radius)
            {
                A = a;
                B = b;
                Radius = radius;
            }

            /// <summary>
            /// 圆心 x 坐标，对应需求中的 a。
            /// </summary>
            public int A { get; }

            /// <summary>
            /// 圆心 y 坐标，对应需求中的 b。
            /// </summary>
            public int B { get; }

            /// <summary>
            /// 真实几何半径 R，即 abs(r)。
            /// </summary>
            public int Radius { get; }

            public bool Equals(GeometryKey other)
            {
                return A == other.A && B == other.B && Radius == other.Radius;
            }

            public override bool Equals(object? obj)
            {
                return obj is GeometryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(A, B, Radius);
            }
        }

        /// <summary>
        /// 表示一个合并后的派生点，输出格式固定为 (circleIndex, pointIndex, pointType)。
        /// </summary>
        internal readonly struct DerivedPoint : IEquatable<DerivedPoint>
        {
            /// <summary>
            /// 创建一个派生点。
            /// </summary>
            public DerivedPoint(int circleIndex, int pointIndex, int pointType)
            {
                CircleIndex = circleIndex;
                PointIndex = pointIndex;
                PointType = pointType;
            }

            /// <summary>
            /// 圆序号，从 0 开始，对应 Circle.bin 中的记录顺序。
            /// </summary>
            public int CircleIndex { get; }

            /// <summary>
            /// 该点作为路径点时，在所在圆上的路径点序号，从 0 开始。
            /// </summary>
            public int PointIndex { get; }

            /// <summary>
            /// 路径点角色编码；同一路径点叠加多个角色时，该值为各角色编码的乘积。
            /// </summary>
            public int PointType { get; }

            public bool Equals(DerivedPoint other)
            {
                return CircleIndex == other.CircleIndex &&
                       PointIndex == other.PointIndex &&
                       PointType == other.PointType;
            }

            public override bool Equals(object? obj)
            {
                return obj is DerivedPoint other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(CircleIndex, PointIndex, PointType);
            }

            /// <summary>
            /// 按需求里的派生点格式输出，便于控制台走查。
            /// </summary>
            public override string ToString()
            {
                return $"({CircleIndex}, {PointIndex}, {PointType})";
            }
        }

        internal readonly struct ConnectionPair : IEquatable<ConnectionPair>
        {
            public ConnectionPair(int leftCircleIndex, int leftPointIndex, int rightCircleIndex, int rightPointIndex)
            {
                LeftCircleIndex = leftCircleIndex;
                LeftPointIndex = leftPointIndex;
                RightCircleIndex = rightCircleIndex;
                RightPointIndex = rightPointIndex;
            }

            public int LeftCircleIndex { get; }

            public int LeftPointIndex { get; }

            public int RightCircleIndex { get; }

            public int RightPointIndex { get; }

            public bool Equals(ConnectionPair other)
            {
                return LeftCircleIndex == other.LeftCircleIndex &&
                       LeftPointIndex == other.LeftPointIndex &&
                       RightCircleIndex == other.RightCircleIndex &&
                       RightPointIndex == other.RightPointIndex;
            }

            public override bool Equals(object? obj)
            {
                return obj is ConnectionPair other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(LeftCircleIndex, LeftPointIndex, RightCircleIndex, RightPointIndex);
            }
        }

        internal readonly struct PurchaseAssignment : IEquatable<PurchaseAssignment>
        {
            public PurchaseAssignment(int circleIndex, int purchaseCircleIndex, int purchasePointIndex)
            {
                CircleIndex = circleIndex;
                PurchaseCircleIndex = purchaseCircleIndex;
                PurchasePointIndex = purchasePointIndex;
            }

            public int CircleIndex { get; }

            public int PurchaseCircleIndex { get; }

            public int PurchasePointIndex { get; }

            public bool Equals(PurchaseAssignment other)
            {
                return CircleIndex == other.CircleIndex &&
                       PurchaseCircleIndex == other.PurchaseCircleIndex &&
                       PurchasePointIndex == other.PurchasePointIndex;
            }

            public override bool Equals(object? obj)
            {
                return obj is PurchaseAssignment other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(CircleIndex, PurchaseCircleIndex, PurchasePointIndex);
            }
        }

        internal readonly struct GeneratedPointData
        {
            public GeneratedPointData(
                IReadOnlyList<DerivedPoint> points,
                IReadOnlyList<ConnectionPair> ordinaryConnections,
                IReadOnlyList<PurchaseAssignment> purchaseAssignments,
                IReadOnlyList<int> pathPointCounts)
            {
                Points = points;
                OrdinaryConnections = ordinaryConnections;
                PurchaseAssignments = purchaseAssignments;
                PathPointCounts = pathPointCounts;
            }

            public IReadOnlyList<DerivedPoint> Points { get; }

            public IReadOnlyList<ConnectionPair> OrdinaryConnections { get; }

            public IReadOnlyList<PurchaseAssignment> PurchaseAssignments { get; }

            public IReadOnlyList<int> PathPointCounts { get; }
        }

        private readonly struct PathPointKey : IEquatable<PathPointKey>
        {
            public PathPointKey(int circleIndex, int pointIndex)
            {
                CircleIndex = circleIndex;
                PointIndex = pointIndex;
            }

            public int CircleIndex { get; }

            public int PointIndex { get; }

            public bool Equals(PathPointKey other)
            {
                return CircleIndex == other.CircleIndex && PointIndex == other.PointIndex;
            }

            public override bool Equals(object? obj)
            {
                return obj is PathPointKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(CircleIndex, PointIndex);
            }
        }

        private readonly struct PointCoordinate
        {
            public PointCoordinate(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X { get; }

            public double Y { get; }
        }

        private readonly struct SelectionCandidate
        {
            public SelectionCandidate(PathPointKey key, double distanceSquared)
            {
                Key = key;
                DistanceSquared = distanceSquared;
            }

            public PathPointKey Key { get; }

            public double DistanceSquared { get; }
        }

        private readonly struct HarvestCandidate
        {
            public HarvestCandidate(int circleIndex, int pathPointIndex, double distanceSquared)
            {
                CircleIndex = circleIndex;
                PathPointIndex = pathPointIndex;
                DistanceSquared = distanceSquared;
            }

            public int CircleIndex { get; }

            public int PathPointIndex { get; }

            public double DistanceSquared { get; }
        }
    }
}
