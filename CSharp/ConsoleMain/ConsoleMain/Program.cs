
namespace ConsoleMain
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //string outputPath = args.Length > 0
            //    ? args[0]
            //    : Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);

            Console.WriteLine(
                "输入选择" + Environment.NewLine +
                "GENERATE          ----------生成圆并计算路径" + Environment.NewLine +
                "CHECK             ----------检查路径" + Environment.NewLine +
                "WEB               ----------启动 Web 服务");

            string? inputSwitchValue = args.Length > 0
                ? args[0]
                : Console.ReadLine();
            if (string.IsNullOrWhiteSpace(inputSwitchValue))
            {
                Console.WriteLine("输入不能为空。");
                return;
            }

            inputSwitchValue = inputSwitchValue.Trim().TrimStart('\uFEFF');

            switch (inputSwitchValue.ToUpperInvariant())
            {
                case "GENERATE":
                    GenerateCircleAndCalculatePaths();
                    break;
                case "CHECK":
                    CheckExistingCircleAndCalculatePaths();
                    break;
                case "WEB":
                    {
                        WebSceneServer.Start();
                    }
                    break;
                default:
                    Console.WriteLine($"无效输入 {inputSwitchValue}");
                    break;
            }




        }

        private static void CheckExistingCircleAndCalculatePaths()
        {
            string circlePath = Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);
            if (!File.Exists(circlePath))
            {
                throw new FileNotFoundException($"{CircleGenerator.CircleFileName} does not exist.", circlePath);
            }

            CircleGenerator.GeneratedPointData pointData = CircleGenerator.GenerateDerivedPointData(circlePath);
            IReadOnlyList<CircleGenerator.DerivedPoint> points = pointData.Points;
            points = points.OrderBy(item => item.CircleIndex).ThenBy(item => item.PointIndex).ThenBy(item => item.PointType).ToList();
            CircleGenerator.ValidateCudaPointOrderAndUniqueness(points);
            CircleGenerator.ValidateCudaPointBusinessRules(points, pointData.PathPointCounts);

            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(points);
            int pointCount = cudaPoints.Length / 3;
            List<int> tradingPoints = BuildTradingPointIndexes(cudaPoints);
            int harvestPointCount = CountPointsByCode(points, CircleGenerator.HarvestPointCode);
            int purchasePointCount = CountPointsByCode(points, CircleGenerator.PurchasePointCode);
            CircleGenerator.ValidateTradingPoints(
                cudaPoints,
                tradingPoints,
                harvestPointCount,
                purchasePointCount);

            (int startSelector, int targetSelector) = ReadRouteSelectorPair();
            int startPointIndex = ResolveRouteSelectorToCudaPointIndex(startSelector, points, pointData.PurchaseAssignments);
            int targetPointIndex = ResolveRouteSelectorToCudaPointIndex(targetSelector, points, pointData.PurchaseAssignments);
            int startTradingRow = tradingPoints.IndexOf(startPointIndex);

            if (startTradingRow < 0)
            {
                throw new InvalidOperationException(
                    $"Start selector {startSelector} resolved to CUDA point {startPointIndex}, but it is not a trading point.");
            }

            string routePath = BuildRouteFilePath(circlePath);
            int[] routeRow = ReadRouteRow(routePath, startTradingRow, tradingPoints.Count, pointCount);
            List<int> routePointIndexes = BuildRoutePointIndexes(routeRow, startPointIndex, targetPointIndex);
            string requestedOutputPath = Path.Combine(
                Environment.CurrentDirectory,
                $"CircleRoute_{startSelector}_{targetSelector}.dxf");
            string outputPath = ResolveWritableRouteDxfPath(requestedOutputPath);

            CircleGenerator.DrawRouteToDxf(circlePath, outputPath, routePointIndexes);
            Console.WriteLine($"Route point count: {routePointIndexes.Count}");
            Console.WriteLine($"Route DXF file: {outputPath}");
        }

        internal static void GenerateCircleAndCalculatePaths()
        {
            KChartRunWithFiveElementsAPI.GenerateCircle();
            CalculateRouteForExistingCircle();
        }

        internal static void CalculateRouteForExistingCircle()
        {
            CalculateRouteForExistingCircle(
                promptForParallelStartPointCount: true,
                defaultParallelStartPointCount: 3);
        }

        internal static void CalculateRouteForExistingCircleWithoutInput()
        {
            CalculateRouteForExistingCircle(
                promptForParallelStartPointCount: false,
                defaultParallelStartPointCount: 3);
        }

        private static void CalculateRouteForExistingCircle(
            bool promptForParallelStartPointCount,
            int defaultParallelStartPointCount)
        {
         //   CircleGenerator.DrawToDxf();
            string circlePath = Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);
            CircleGenerator.GeneratedPointData pointData = CircleGenerator.GenerateDerivedPointData();
            IReadOnlyList<CircleGenerator.DerivedPoint> points = pointData.Points;
            points = points.OrderBy(item => item.CircleIndex).ThenBy(item => item.PointIndex).ThenBy(item => item.PointType).ToList();
            CircleGenerator.ValidateCudaPointOrderAndUniqueness(points);
            CircleGenerator.ValidateCudaPointBusinessRules(points, pointData.PathPointCounts);
            //foreach (CircleGenerator.DerivedPoint point in points)
            //{
            //    Console.WriteLine(point);
            //}

            int circleCount = pointData.PathPointCounts.Count;
            int harvestPointCount = CountPointsByCode(points, CircleGenerator.HarvestPointCode);
            int firstPathPointCount = CountPointsByCode(points, CircleGenerator.FirstPathPointCode);
            int terminalLinkPointCount = CountPointsByCode(points, CircleGenerator.TerminalLinkPointCode);
            int ordinaryLinkPointCount = CountPointsByCode(points, CircleGenerator.OrdinaryLinkPointCode);
            int purchasePointCount = CountPointsByCode(points, CircleGenerator.PurchasePointCode);

            if (harvestPointCount != 1)
            {
                throw new InvalidOperationException(
                    $"Harvest point count {harvestPointCount} does not match required count 1.");
            }

            if (purchasePointCount != circleCount)
            {
                throw new InvalidOperationException(
                    $"Purchase point count {purchasePointCount} does not match circle count {circleCount}.");
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

            Console.WriteLine($"Circle count: {circleCount}");
            Console.WriteLine($"Harvest point count: {harvestPointCount}");
            Console.WriteLine($"First path point count: {firstPathPointCount}");
            Console.WriteLine($"Terminal link point count: {terminalLinkPointCount}");
            Console.WriteLine($"Ordinary link point count: {ordinaryLinkPointCount}");
            Console.WriteLine($"Purchase point count: {purchasePointCount}");

            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(points);
            CircleGenerator.ValidateCudaPointRoleCount(points, cudaPoints);
            int[] connect = CircleGenerator.BuildConnectArray(points, pointData.OrdinaryConnections);
            CircleGenerator.ValidateCudaPointConnectRatio(cudaPoints, connect);
            CircleGenerator.ValidateCudaConnectArray(points, connect);
            Console.WriteLine($"Connect int length: {connect.Length}");
            Console.WriteLine($"CUDA points int length: {cudaPoints.Length}");

            if (cudaPoints.Length != connect.Length * 3)
            {
                throw new InvalidOperationException(
                    $"CUDA points length {cudaPoints.Length} does not match connect length {connect.Length} * 3.");
            }

            List<int> tradingPoints = BuildTradingPointIndexes(cudaPoints);
            CircleGenerator.ValidateTradingPoints(
                cudaPoints,
                tradingPoints,
                harvestPointCount,
                purchasePointCount);
            Console.WriteLine($"Trading point count: {tradingPoints.Count}");

            // parallelStartPointCount means how many start points one CUDA batch calculates in parallel.
            int parallelStartPointCount = defaultParallelStartPointCount;
            if (CalpathCuda.TryGetParallelStartPointRecommendation(
                cudaPoints.Length,
                out int gpuFullLoadParallelStartPointCount,
                out int gpuRecommendedParallelStartPointCount))
            {
                Console.WriteLine(
                    $"GPU full-load parallel start point count: {gpuFullLoadParallelStartPointCount}, recommended 80%: {gpuRecommendedParallelStartPointCount}.");
                if (promptForParallelStartPointCount)
                {
                    parallelStartPointCount = Math.Min(
                        Math.Max(gpuRecommendedParallelStartPointCount, 1),
                        Math.Max(tradingPoints.Count, 1));
                }
            }
            else
            {
                Console.WriteLine($"GPU parallel start point recommendation is unavailable; default value is {defaultParallelStartPointCount}.");
            }

            if (promptForParallelStartPointCount)
            {
                Console.WriteLine(
                    $"Input CUDA parallel start point count. Empty uses {parallelStartPointCount}. It must be greater than 0. Values larger than remaining trading points will be capped.");
                string? parallelStartPointInput = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(parallelStartPointInput) &&
                    !int.TryParse(parallelStartPointInput, out parallelStartPointCount))
                {
                    parallelStartPointCount = defaultParallelStartPointCount;
                }
            }
            else
            {
                Console.WriteLine(
                    $"Use CUDA parallel start point count: {parallelStartPointCount}. Values larger than remaining trading points will be capped.");
            }

            if (parallelStartPointCount <= 0)
            {
                throw new InvalidOperationException(
                    $"CUDA parallel start point count must be greater than 0. Actual: {parallelStartPointCount}.");
            }

            if (CalpathCuda.IsAvailable)
            {
                int pointCount = cudaPoints.Length / 3;
                string routePath = BuildRouteFilePath(circlePath);
                string tempRoutePath = routePath + ".tmp";
                long routeResultCount = 0;
                long expectedRouteResultCount = checked((long)tradingPoints.Count * pointCount);
                int calIndexStarted = 0;
                using (FileStream routeStream = File.Create(tempRoutePath))
                using (BinaryWriter routeWriter = new(routeStream))
                {
                    while (calIndexStarted < tradingPoints.Count)
                    {
                        //parallelStartPointCount = Math.Min(parallelStartPointCount, tradingPoints.Count - calIndexStarted);

                        int batchStartPointCount = Math.Min(parallelStartPointCount, tradingPoints.Count - calIndexStarted);
                        CircleGenerator.ValidateCudaBatchRange(tradingPoints, calIndexStarted, batchStartPointCount);
                        List<int> lastFP = new List<int>(pointCount * batchStartPointCount);
                        for (int i = 0; i < pointCount * batchStartPointCount; i++)
                        {
                            lastFP.Add(-1);
                        }

                        for (int unitIndex = 0; unitIndex < batchStartPointCount; unitIndex++)
                        {
                            int tradingPointIndex = calIndexStarted + unitIndex;
                            int startPointIndex = tradingPoints[tradingPointIndex];

                            int pointType = cudaPoints[startPointIndex * 3 + 2];
                            bool isTradingPoint =
                                pointType % CircleGenerator.HarvestPointCode == 0 ||
                                pointType % CircleGenerator.PurchasePointCode == 0;

                            if (!isTradingPoint)
                            {
                                throw new InvalidOperationException(
                                    $"Point {startPointIndex} is not a trading point. PointType={pointType}.");
                            }
                            // Mark the start point as reached; self means path backtracking stops here.
                            lastFP[unitIndex * pointCount + startPointIndex] = startPointIndex;
                        }
                        int[] lastFPResult = lastFP.ToArray();
                        int status = CalpathCuda.AcceptPoints(
                            points,
                            pointData.PathPointCounts,
                            cudaPoints,
                            lastFPResult,
                            connect,
                            tradingPoints,
                            harvestPointCount,
                            purchasePointCount,
                            calIndexStarted,
                            batchStartPointCount);
                        if (status != 0)
                        {
                            throw new InvalidOperationException($"CUDA path calculation failed: {status}.");
                        }

                        WriteRouteValues(
                            routeWriter,
                            lastFPResult,
                            routeResultCount,
                            expectedRouteResultCount);
                        routeResultCount += lastFPResult.Length;

                        int reachedTradingPathCount = 0;
                        for (int unitIndex = 0; unitIndex < batchStartPointCount; unitIndex++)
                        {
                            int unitBase = unitIndex * pointCount;
                            foreach (int targetPointIndex in tradingPoints)
                            {
                                if (lastFPResult[unitBase + targetPointIndex] != -1)
                                {
                                    reachedTradingPathCount++;
                                }
                            }
                        }

                        Console.WriteLine($"CUDA path status: {status}, reached trading paths: {reachedTradingPathCount}/{batchStartPointCount * tradingPoints.Count}");
                        calIndexStarted += batchStartPointCount;
                    }
                }

                if (routeResultCount != expectedRouteResultCount)
                {
                    throw new InvalidOperationException(
                        $"Route result count {routeResultCount} does not match trading point count * CUDA point count {expectedRouteResultCount}.");
                }

                File.Move(tempRoutePath, routePath, true);
                Console.WriteLine($"Route file: {routePath}");
            }
            else
            {
                Console.WriteLine($"CUDA DLL not found: {CalpathCuda.DllPath}");
            }

            Console.WriteLine($"Generated {CircleGenerator.DefaultCircleCount}");
        }

        internal static int CountPointsByCode(
            IEnumerable<CircleGenerator.DerivedPoint> points,
            int pointCode)
        {
            return points.Count(point => point.PointType % pointCode == 0);
        }

        internal static List<int> BuildTradingPointIndexes(int[] cudaPoints)
        {
            List<int> tradingPoints = new List<int>();

            for (int pointArrayIndex = 0; pointArrayIndex < cudaPoints.Length; pointArrayIndex += 3)
            {
                int pointType = cudaPoints[pointArrayIndex + 2];
                if (pointType % CircleGenerator.HarvestPointCode == 0 ||
                    pointType % CircleGenerator.PurchasePointCode == 0)
                {
                    tradingPoints.Add(pointArrayIndex / 3);
                }
            }

            return tradingPoints;
        }

        private static (int StartSelector, int TargetSelector) ReadRouteSelectorPair()
        {
            Console.WriteLine("Input start integer. 0 means harvest point; n > 0 means purchase point of circle n - 1.");
            int startSelector = ReadRouteSelector("start");

            Console.WriteLine("Input target integer. 0 means harvest point; n > 0 means purchase point of circle n - 1.");
            int targetSelector = ReadRouteSelector("target");

            return (startSelector, targetSelector);
        }

        private static int ReadRouteSelector(string selectorName)
        {
            string? input = Console.ReadLine();
            string? normalizedInput = input?.Trim().TrimStart('\uFEFF');
            if (!int.TryParse(normalizedInput, out int selector))
            {
                throw new InvalidOperationException($"Route {selectorName} selector must be one integer.");
            }

            return selector;
        }

        private static string ResolveWritableRouteDxfPath(string requestedOutputPath)
        {
            if (CanOverwriteFile(requestedOutputPath))
            {
                return requestedOutputPath;
            }

            string directory = Path.GetDirectoryName(requestedOutputPath)!;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(requestedOutputPath);
            string extension = Path.GetExtension(requestedOutputPath);

            for (int index = 0; index < 100; index++)
            {
                string suffix = index == 0
                    ? DateTime.Now.ToString("yyyyMMdd_HHmmss")
                    : $"{DateTime.Now:yyyyMMdd_HHmmss}_{index}";
                string candidatePath = Path.Combine(
                    directory,
                    $"{fileNameWithoutExtension}_{suffix}{extension}");

                if (CanOverwriteFile(candidatePath))
                {
                    Console.WriteLine($"Requested DXF file is locked, write to: {candidatePath}");
                    return candidatePath;
                }
            }

            throw new IOException($"Cannot find writable DXF output path for {requestedOutputPath}.");
        }

        private static bool CanOverwriteFile(string path)
        {
            if (!File.Exists(path))
            {
                return true;
            }

            try
            {
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static int ResolveRouteSelectorToCudaPointIndex(
            int selector,
            IReadOnlyList<CircleGenerator.DerivedPoint> points,
            IReadOnlyList<CircleGenerator.PurchaseAssignment> purchaseAssignments)
        {
            if (selector == 0)
            {
                return FindPointIndexByRole(points, CircleGenerator.HarvestPointCode);
            }

            if (selector < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(selector), "Route selector cannot be negative.");
            }

            int circleIndex = selector - 1;
            CircleGenerator.PurchaseAssignment? assignment = null;
            foreach (CircleGenerator.PurchaseAssignment item in purchaseAssignments)
            {
                if (item.CircleIndex == circleIndex)
                {
                    assignment = item;
                    break;
                }
            }

            if (assignment is null)
            {
                throw new InvalidOperationException(
                    $"Circle index {circleIndex} does not have a purchase point assignment.");
            }

            return FindPointIndexByPosition(
                points,
                assignment.Value.PurchaseCircleIndex,
                assignment.Value.PurchasePointIndex);
        }

        private static int FindPointIndexByRole(
            IReadOnlyList<CircleGenerator.DerivedPoint> points,
            int roleCode)
        {
            int foundIndex = -1;
            for (int index = 0; index < points.Count; index++)
            {
                if (points[index].PointType % roleCode != 0)
                {
                    continue;
                }

                if (foundIndex != -1)
                {
                    throw new InvalidOperationException($"More than one CUDA point has role {roleCode}.");
                }

                foundIndex = index;
            }

            if (foundIndex == -1)
            {
                throw new InvalidOperationException($"No CUDA point has role {roleCode}.");
            }

            return foundIndex;
        }

        private static int FindPointIndexByPosition(
            IReadOnlyList<CircleGenerator.DerivedPoint> points,
            int circleIndex,
            int pointIndex)
        {
            for (int index = 0; index < points.Count; index++)
            {
                CircleGenerator.DerivedPoint point = points[index];
                if (point.CircleIndex == circleIndex && point.PointIndex == pointIndex)
                {
                    return index;
                }
            }

            throw new InvalidOperationException(
                $"CUDA point ({circleIndex}, {pointIndex}) does not exist.");
        }

        internal static int[] ReadRouteRow(
            string routePath,
            int startTradingRow,
            int tradingPointCount,
            int pointCount)
        {
            string fullRoutePath = Path.GetFullPath(routePath);
            if (!File.Exists(fullRoutePath))
            {
                throw new FileNotFoundException("Route file does not exist.", fullRoutePath);
            }

            long expectedLength = checked((long)tradingPointCount * pointCount * sizeof(int));
            FileInfo fileInfo = new(fullRoutePath);
            if (fileInfo.Length != expectedLength)
            {
                throw new InvalidOperationException(
                    $"Route file length {fileInfo.Length} does not match expected length {expectedLength}.");
            }

            int[] row = new int[pointCount];
            long rowOffset = checked((long)startTradingRow * pointCount * sizeof(int));

            using FileStream stream = File.OpenRead(fullRoutePath);
            stream.Seek(rowOffset, SeekOrigin.Begin);
            using BinaryReader reader = new(stream);
            for (int index = 0; index < row.Length; index++)
            {
                row[index] = reader.ReadInt32();
            }

            return row;
        }

        internal static List<int> BuildRoutePointIndexes(
            IReadOnlyList<int> routeRow,
            int startPointIndex,
            int targetPointIndex)
        {
            List<int> reversedRoute = new List<int>();
            HashSet<int> visited = new HashSet<int>();
            int currentPointIndex = targetPointIndex;

            for (int step = 0; step < routeRow.Count; step++)
            {
                if (currentPointIndex < 0 || currentPointIndex >= routeRow.Count)
                {
                    throw new InvalidOperationException(
                        $"Route backtracking point index {currentPointIndex} is outside CUDA point range [0, {routeRow.Count - 1}].");
                }

                if (!visited.Add(currentPointIndex))
                {
                    throw new InvalidOperationException(
                        $"Route backtracking found a cycle at CUDA point {currentPointIndex}.");
                }

                reversedRoute.Add(currentPointIndex);
                int previousPointIndex = routeRow[currentPointIndex];

                if (previousPointIndex == -1)
                {
                    throw new InvalidOperationException(
                        $"Target CUDA point {targetPointIndex} is unreachable from start CUDA point {startPointIndex}.");
                }

                if (previousPointIndex == currentPointIndex)
                {
                    if (currentPointIndex != startPointIndex)
                    {
                        throw new InvalidOperationException(
                            $"Route backtracking stopped at CUDA point {currentPointIndex}, but expected start CUDA point {startPointIndex}.");
                    }

                    break;
                }

                currentPointIndex = previousPointIndex;
            }

            if (reversedRoute[^1] != startPointIndex)
            {
                throw new InvalidOperationException("Route backtracking did not reach the start point.");
            }

            reversedRoute.Reverse();
            return reversedRoute;
        }

        /// <summary>
        /// 根据 Circle.bin 的文件内容生成对应的路线文件路径。
        /// 路线文件名采用 Circle.bin 内容的 SHA256 哈希值加上 Route.bin 后缀。
        /// </summary>
        /// <param name="circlePath">
        /// Circle.bin 的路径；可以是相对路径，也可以是绝对路径。
        /// 方法内部会转成绝对路径，并要求该文件必须已经存在。
        /// </param>
        /// <returns>
        /// 与当前 Circle.bin 内容一一对应的路线文件完整路径。
        /// 例如：{sha256小写十六进制}Route.bin。
        /// </returns>
        /// <remarks>
        /// 这个方法的目的，是让路线文件和圆文件内容绑定。
        /// 只要 Circle.bin 内容发生任何变化，SHA256 就会变化，生成的 Route.bin 文件名也会变化。
        /// 这样可以避免圆数据已经改变，但程序仍然误用旧路线。
        /// </remarks>
        /// <exception cref="FileNotFoundException">
        /// 当 <paramref name="circlePath"/> 指向的 Circle.bin 不存在时抛出。
        /// </exception>
        internal static string BuildRouteFilePath(string circlePath)
        {
            // 把传入路径转成绝对路径，避免当前工作目录变化导致同一个相对路径指向不同文件。
            string fullCirclePath = Path.GetFullPath(circlePath);

            // 路线文件名依赖 Circle.bin 的实际内容。
            // 如果 Circle.bin 不存在，就无法计算 SHA256，也不能生成可靠的路线文件名。
            if (!File.Exists(fullCirclePath))
            {
                throw new FileNotFoundException($"{CircleGenerator.CircleFileName} does not exist.", fullCirclePath);
            }

            // 打开 Circle.bin 只读流，准备按文件内容计算 SHA256。
            using FileStream stream = File.OpenRead(fullCirclePath);

            // 计算 Circle.bin 文件内容的 SHA256。
            // 注意：这里哈希的是文件内容，不是文件名，也不是路径。
            byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(stream);

            // 把 SHA256 字节转成小写十六进制字符串，并拼接 Route.bin 后缀作为路线文件名。
            string fileName = Convert.ToHexString(hashBytes).ToLowerInvariant() + "Route.bin";

            // 路线文件和 Circle.bin 放在同一个目录下，方便按圆文件所在目录成套管理。
            return Path.Combine(Path.GetDirectoryName(fullCirclePath)!, fileName);
        }

        private static void WriteRouteValues(
            BinaryWriter writer,
            IReadOnlyList<int> routeValues,
            long writtenBefore,
            long totalCount)
        {
            foreach (int routeValue in routeValues)
            {
                writer.Write(routeValue);
            }

            long writtenAfter = checked(writtenBefore + routeValues.Count);
            double percent = totalCount == 0
                ? 100.0
                : writtenAfter * 100.0 / totalCount;

            Console.WriteLine(
                $"Route write progress: {writtenAfter}/{totalCount}, {percent:F2}%.");
        }
    }
}
