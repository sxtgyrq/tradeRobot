using System.Globalization;
using System.Text;

namespace ConsoleMain
{
    internal static class CircleGenerator
    {
        public const int DefaultCircleCount = 10;
        public const string CircleFileName = "Circle.bin";
        public const string DxfFileName = "Circle.dxf";

        internal const int MaxX = 1_000_000;
        internal const int MaxY = 1_000_000;
        internal const int MinRadius = 10;
        internal const int PathPointDensityMultiplier = 300;
        internal const int NormalPathPointCode = 1;
        internal const int HarvestPointCode = 2;
        internal const int FirstPathPointCode = 3;
        internal const int TerminalLinkPointCode = 5;
        internal const int OrdinaryLinkPointCode = 7;
        internal const int PurchasePointCode = 11;
        internal static readonly int MaxRadius = Math.Min(MaxX, MaxY);
        private const int MaxAttemptsPerCircle = 100_000;
        private const double TwoPi = Math.PI * 2.0;
        private const double DistanceTolerance = 1e-7;

        private static readonly CircleRecord[] SeedCircles =
        {
            new(-400_000, 0, -499_999),
            new(400_000, 0, 499_999),
        };

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

        internal static GeneratedPointData GenerateDerivedPointData(string inputPath)
        {
            string fullInputPath = Path.GetFullPath(inputPath);

            if (!File.Exists(fullInputPath))
            {
                throw new FileNotFoundException($"{CircleFileName} does not exist.", fullInputPath);
            }

            List<CircleRecord> circles = ReadCircles(fullInputPath);
            BuildGeometrySet(circles);
            EnsureSingleConnectedComponent(circles);

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

        internal static void GenerateCircle(string outputPath, int count)
        {
            if (count < SeedCircles.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    $"Circle count must be at least {SeedCircles.Length}.");
            }

            string fullPath = Path.GetFullPath(outputPath);
            List<CircleRecord> circles;

            if (File.Exists(fullPath))
            {
                circles = ReadCircles(fullPath);

                if (circles.Count > count)
                {
                    throw new InvalidOperationException(
                        $"Existing circle count {circles.Count} is greater than requested count {count}.");
                }

                if (circles.Count == 0)
                {
                    circles = BuildConnectedCircles(count);
                }
                else
                {
                    HashSet<GeometryKey> geometries = BuildGeometrySet(circles);
                    EnsureSingleConnectedComponent(circles);

                    while (circles.Count < count)
                    {
                        CircleRecord candidate = GenerateConnectedRandomCircle(circles, geometries);
                        AddCircle(candidate, circles, geometries);
                    }

                    EnsureSingleConnectedComponent(circles);
                }
            }
            else
            {
                circles = BuildConnectedCircles(count);
            }

            WriteCircles(fullPath, circles);
        }

        private static void WriteDxf(TextWriter writer, IReadOnlyList<CircleRecord> circles)
        {
            WriteDxfPair(writer, 0, "SECTION");
            WriteDxfPair(writer, 2, "TABLES");
            WriteDxfPair(writer, 0, "TABLE");
            WriteDxfPair(writer, 2, "LAYER");
            WriteDxfPair(writer, 70, 2);
            WriteDxfLayer(writer, "LongCircle", 3);
            WriteDxfLayer(writer, "ShortCircle", 1);
            WriteDxfPair(writer, 0, "ENDTAB");
            WriteDxfPair(writer, 0, "ENDSEC");

            WriteDxfPair(writer, 0, "SECTION");
            WriteDxfPair(writer, 2, "ENTITIES");

            for (int index = 0; index < circles.Count; index++)
            {
                CircleRecord circle = circles[index];
                string layerName = circle.SignedRadius > 0 ? "LongCircle" : "ShortCircle";
                int color = circle.SignedRadius > 0 ? 3 : 1;
                int textHeight = Math.Clamp(circle.Radius / 8, 5_000, 30_000);
                int textOffset = Math.Max(textHeight, 2_000);

                WriteDxfPair(writer, 0, "CIRCLE");
                WriteDxfPair(writer, 8, layerName);
                WriteDxfPair(writer, 62, color);
                WriteDxfPair(writer, 10, circle.A);
                WriteDxfPair(writer, 20, circle.B);
                WriteDxfPair(writer, 30, 0);
                WriteDxfPair(writer, 40, circle.Radius);

                WriteDxfPair(writer, 0, "TEXT");
                WriteDxfPair(writer, 8, layerName);
                WriteDxfPair(writer, 62, color);
                WriteDxfPair(writer, 10, circle.A + textOffset);
                WriteDxfPair(writer, 20, circle.B + textOffset);
                WriteDxfPair(writer, 30, 0);
                WriteDxfPair(writer, 40, textHeight);
                WriteDxfPair(writer, 1, $"#{index} a={circle.A} b={circle.B} r={circle.SignedRadius}");
            }

            WriteDxfPair(writer, 0, "ENDSEC");
            WriteDxfPair(writer, 0, "EOF");
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
            Dictionary<PathPointKey, int> pointTypes = new();
            List<DerivedPoint> derivedPoints = new();
            List<ConnectionPair> ordinaryConnections = new();

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
            MarkPurchasePoints(circles, pathPointCounts, pointTypes, derivedPoints);

            return new GeneratedPointData(
                MergeSamePositionDerivedPoints(derivedPoints),
                ordinaryConnections);
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

        private static int[] CalculatePathPointCounts(IReadOnlyList<CircleRecord> circles)
        {
            int[] pathPointCounts = new int[circles.Count];

            for (int circleIndex = 0; circleIndex < circles.Count; circleIndex++)
            {
                pathPointCounts[circleIndex] = CalculatePathPointCount(circles[circleIndex]);
            }

            return pathPointCounts;
        }

        private static PathPointKey FindHarvestPoint(
            IReadOnlyList<CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts)
        {
            HarvestCandidate? bestCandidate = null;

            for (int circleIndex = 0; circleIndex < circles.Count; circleIndex++)
            {
                HarvestCandidate candidate = FindNearestHarvestCandidate(
                    circles[circleIndex],
                    circleIndex,
                    pathPointCounts[circleIndex]);

                if (IsBetterHarvestCandidate(candidate, bestCandidate))
                {
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate is null)
            {
                throw new InvalidOperationException("Harvest point cannot be determined.");
            }

            return new PathPointKey(bestCandidate.Value.CircleIndex, bestCandidate.Value.PathPointIndex);
        }

        private static HarvestCandidate FindNearestHarvestCandidate(
            CircleRecord circle,
            int circleIndex,
            int pathPointCount)
        {
            double nearestAngle = circle.A == 0 && circle.B == 0
                ? 0.0
                : NormalizeAngle(Math.Atan2(-circle.B, -circle.A));
            double pathDirectionAngle = circle.SignedRadius > 0
                ? nearestAngle
                : NormalizeAngle(-nearestAngle);
            double rawIndex = pathDirectionAngle / TwoPi * pathPointCount;
            int floorIndex = (int)Math.Floor(rawIndex);

            HarvestCandidate? bestCandidate = null;

            for (int offset = -3; offset <= 3; offset++)
            {
                int pathPointIndex = NormalizePathPointIndex(floorIndex + offset, pathPointCount);

                if (pathPointIndex == pathPointCount - 1)
                {
                    continue;
                }

                (double x, double y) = CalculatePathPointCoordinate(circle, pathPointIndex, pathPointCount);
                HarvestCandidate candidate = new(circleIndex, pathPointIndex, x * x + y * y);

                if (IsBetterHarvestCandidate(candidate, bestCandidate))
                {
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate is null)
            {
                throw new InvalidOperationException(
                    $"No available harvest candidate path point for circle {circleIndex}.");
            }

            return bestCandidate.Value;
        }

        private static bool IsBetterHarvestCandidate(
            HarvestCandidate candidate,
            HarvestCandidate? currentBest)
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

            if (candidate.CircleIndex != currentBest.Value.CircleIndex)
            {
                return candidate.CircleIndex < currentBest.Value.CircleIndex;
            }

            return candidate.PathPointIndex < currentBest.Value.PathPointIndex;
        }

        private static void MarkOrdinaryConnectionPoints(
            IReadOnlyList<CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts,
            Dictionary<PathPointKey, int> pointTypes,
            List<DerivedPoint> derivedPoints,
            List<ConnectionPair> ordinaryConnections)
        {
            for (int leftIndex = 0; leftIndex < circles.Count; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1; rightIndex < circles.Count; rightIndex++)
                {
                    CircleRecord left = circles[leftIndex];
                    CircleRecord right = circles[rightIndex];

                    if (!HasCircumferenceIntersectionOrTangency(left, right))
                    {
                        continue;
                    }

                    foreach (PointCoordinate intersection in CalculateCircleIntersections(left, right))
                    {
                        PathPointKey leftPoint = FindNearestAvailablePathPoint(
                            left,
                            leftIndex,
                            pathPointCounts[leftIndex],
                            intersection,
                            pointTypes,
                            "ordinary connection");
                        PathPointKey rightPoint = FindNearestAvailablePathPoint(
                            right,
                            rightIndex,
                            pathPointCounts[rightIndex],
                            intersection,
                            pointTypes,
                            "ordinary connection");

                        MarkPoint(pointTypes, derivedPoints, leftPoint, OrdinaryLinkPointCode, true);
                        MarkPoint(pointTypes, derivedPoints, rightPoint, OrdinaryLinkPointCode, true);
                        ordinaryConnections.Add(new ConnectionPair(
                            leftPoint.CircleIndex,
                            leftPoint.PointIndex,
                            rightPoint.CircleIndex,
                            rightPoint.PointIndex));
                    }
                }
            }
        }

        private static void MarkPurchasePoints(
            IReadOnlyList<CircleRecord> circles,
            IReadOnlyList<int> pathPointCounts,
            Dictionary<PathPointKey, int> pointTypes,
            List<DerivedPoint> derivedPoints)
        {
            for (int circleIndex = 0; circleIndex < circles.Count; circleIndex++)
            {
                PathPointKey purchasePoint = FindPurchasePoint(circleIndex, pathPointCounts[circleIndex], pointTypes);

                MarkPoint(pointTypes, derivedPoints, purchasePoint, PurchasePointCode, true);
            }
        }

        private static PathPointKey FindPurchasePoint(
            int circleIndex,
            int pathPointCount,
            IReadOnlyDictionary<PathPointKey, int> pointTypes)
        {
            for (int pointIndex = 0; pointIndex < pathPointCount - 1; pointIndex++)
            {
                PathPointKey key = new(circleIndex, pointIndex);

                if (IsAvailableForOccupyingRole(key, pathPointCount, pointTypes))
                {
                    return key;
                }
            }

            throw new InvalidOperationException($"No available purchase point for circle {circleIndex}.");
        }

        private static PathPointKey FindNearestAvailablePathPoint(
            CircleRecord circle,
            int circleIndex,
            int pathPointCount,
            PointCoordinate target,
            IReadOnlyDictionary<PathPointKey, int> pointTypes,
            string roleName)
        {
            double targetAngle = NormalizeAngle(Math.Atan2(target.Y - circle.B, target.X - circle.A));
            double pathDirectionAngle = circle.SignedRadius > 0
                ? targetAngle
                : NormalizeAngle(-targetAngle);
            double rawIndex = pathDirectionAngle / TwoPi * pathPointCount;
            int nearestIndex = NormalizePathPointIndex(
                (long)Math.Round(rawIndex, MidpointRounding.AwayFromZero),
                pathPointCount);

            for (int offset = 0; offset < pathPointCount; offset++)
            {
                SelectionCandidate? bestCandidate = null;

                ConsiderSelectionCandidate(
                    circle,
                    circleIndex,
                    pathPointCount,
                    target,
                    NormalizePathPointIndex((long)nearestIndex + offset, pathPointCount),
                    pointTypes,
                    ref bestCandidate);

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

                if (bestCandidate is not null)
                {
                    return bestCandidate.Value.Key;
                }
            }

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

        private static void MarkPoint(
            Dictionary<PathPointKey, int> pointTypes,
            List<DerivedPoint> derivedPoints,
            PathPointKey key,
            int roleCode,
            bool occupyingRole)
        {
            if (!pointTypes.TryGetValue(key, out int pointType))
            {
                pointType = NormalPathPointCode;
                pointTypes.Add(key, pointType);
            }

            if (pointType % roleCode == 0)
            {
                if (occupyingRole)
                {
                    throw new InvalidOperationException(
                        $"Path point ({key.CircleIndex}, {key.PointIndex}) is already occupied by role {roleCode}.");
                }

                return;
            }

            if (occupyingRole && HasOccupyingRole(pointType))
            {
                throw new InvalidOperationException(
                    $"Path point ({key.CircleIndex}, {key.PointIndex}) already has an occupying role.");
            }

            pointTypes[key] = pointType * roleCode;
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

        private static int NormalizePathPointIndex(long pathPointIndex, int pathPointCount)
        {
            long normalized = pathPointIndex % pathPointCount;

            if (normalized < 0)
            {
                normalized += pathPointCount;
            }

            return (int)normalized;
        }

        private static void WriteDxfLayer(TextWriter writer, string layerName, int color)
        {
            WriteDxfPair(writer, 0, "LAYER");
            WriteDxfPair(writer, 2, layerName);
            WriteDxfPair(writer, 70, 0);
            WriteDxfPair(writer, 62, color);
            WriteDxfPair(writer, 6, "CONTINUOUS");
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
                CircleRecord candidate = GenerateConnectedRandomCircle(circles, geometries);
                AddCircle(candidate, circles, geometries);
            }

            return circles;
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

        private static void EnsureSingleConnectedComponent(IReadOnlyList<CircleRecord> circles)
        {
            if (circles.Count <= 1)
            {
                return;
            }

            HashSet<int> visited = new() { 0 };
            Queue<int> pending = new();
            pending.Enqueue(0);

            while (pending.Count > 0)
            {
                int current = pending.Dequeue();

                for (int next = 0; next < circles.Count; next++)
                {
                    if (visited.Contains(next))
                    {
                        continue;
                    }

                    if (!HasCircumferenceIntersectionOrTangency(circles[current], circles[next]))
                    {
                        continue;
                    }

                    visited.Add(next);
                    pending.Enqueue(next);
                }
            }

            if (visited.Count != circles.Count)
            {
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

        internal static bool HasCircumferenceIntersectionOrTangency(CircleRecord left, CircleRecord right)
        {
            long dx = (long)left.A - right.A;
            long dy = (long)left.B - right.B;
            long distanceSquared = dx * dx + dy * dy;

            long radiusSum = (long)left.Radius + right.Radius;
            long radiusDifference = Math.Abs((long)left.Radius - right.Radius);

            return distanceSquared >= radiusDifference * radiusDifference &&
                   distanceSquared <= radiusSum * radiusSum;
        }

        internal readonly record struct CircleRecord(int A, int B, int SignedRadius)
        {
            public int Radius
            {
                get
                {
                    if (SignedRadius == int.MinValue)
                    {
                        throw new InvalidOperationException("Circle signed radius is outside the supported range.");
                    }

                    return Math.Abs(SignedRadius);
                }
            }

            public GeometryKey GeometryKey => new(A, B, Radius);
        }

        internal readonly record struct GeometryKey(int A, int B, int Radius);

        internal readonly record struct DerivedPoint(int CircleIndex, int PointIndex, int PointType)
        {
            public override string ToString()
            {
                return $"({CircleIndex}, {PointIndex}, {PointType})";
            }
        }

        internal readonly record struct ConnectionPair(
            int LeftCircleIndex,
            int LeftPointIndex,
            int RightCircleIndex,
            int RightPointIndex);

        internal readonly record struct GeneratedPointData(
            IReadOnlyList<DerivedPoint> Points,
            IReadOnlyList<ConnectionPair> OrdinaryConnections);

        private readonly record struct PathPointKey(int CircleIndex, int PointIndex);

        private readonly record struct PointCoordinate(double X, double Y);

        private readonly record struct SelectionCandidate(PathPointKey Key, double DistanceSquared);

        private readonly record struct HarvestCandidate(
            int CircleIndex,
            int PathPointIndex,
            double DistanceSquared);
    }
}
