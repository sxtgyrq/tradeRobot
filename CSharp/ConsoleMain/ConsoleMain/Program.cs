namespace ConsoleMain
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //string outputPath = args.Length > 0
            //    ? args[0]
            //    : Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);

            CircleGenerator.GenerateCircle();
            CircleGenerator.DrawToDxf();
            CircleGenerator.GeneratedPointData pointData = CircleGenerator.GenerateDerivedPointData();
            IReadOnlyList<CircleGenerator.DerivedPoint> points = pointData.Points;
            points = points.OrderBy(item => item.CircleIndex).ThenBy(item => item.PointIndex).ThenBy(item => item.PointType).ToList();
            foreach (CircleGenerator.DerivedPoint point in points)
            {
                Console.WriteLine(point);
            }

            int circleCount = CircleGenerator.DefaultCircleCount;
            int harvestPointCount = CountPointsByCode(points, CircleGenerator.HarvestPointCode);
            int firstPathPointCount = CountPointsByCode(points, CircleGenerator.FirstPathPointCode);
            int terminalLinkPointCount = CountPointsByCode(points, CircleGenerator.TerminalLinkPointCode);
            int ordinaryLinkPointCount = CountPointsByCode(points, CircleGenerator.OrdinaryLinkPointCode);
            int purchasePointCount = CountPointsByCode(points, CircleGenerator.PurchasePointCode);

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
            Console.WriteLine("Press Enter to start CUDA calculation.");

            // parallelStartPointCount 表示一次 CUDA 并行计算多少个起点。
            int parallelStartPointCount = 3;
            Console.WriteLine($"输入并行计算的数量,数量要小于 purchasePointCount+harvestPointCount");
            if (int.TryParse(Console.ReadLine(), out parallelStartPointCount))
            { }
            else
            {
                parallelStartPointCount = 3;
            }
            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(points);
            int[] connect = CircleGenerator.BuildConnectArray(points, pointData.OrdinaryConnections);
            CircleGenerator.ValidateCudaPointConnectRatio(cudaPoints, connect);
            Console.WriteLine($"Connect int length: {connect.Length}");

            Console.WriteLine($"CUDA points int length: {cudaPoints.Length}");

            if (cudaPoints.Length != connect.Length * 3)
            {
                throw new InvalidOperationException(
                    $"CUDA points length {cudaPoints.Length} does not match connect length {connect.Length} * 3.");
            }

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
            Console.WriteLine($"Trading point count: {tradingPoints.Count}");

            if (CalpathCuda.IsAvailable)
            {
                int calIndexStarted = 0;
                while (calIndexStarted < tradingPoints.Count)
                {
                    parallelStartPointCount = Math.Min(parallelStartPointCount, tradingPoints.Count - calIndexStarted);
                    List<int> lastFP = new List<int>(tradingPoints.Count * parallelStartPointCount);//这里用于存储最后一个地址。
                                                                                                    //    List<int> LastRecordResultForSave = new List<int>(fpItems.Count * mCalCount);

                    for (int i = calIndexStarted; i < calIndexStarted + parallelStartPointCount; i++)
                    {
                        for (int j = 0; j < tradingPoints.Count; j++)
                        {
                            var fpItem = tradingPoints[j];
                            if (j == i)
                            {
                                // var stringKey = $"{fpItem.fPCode}{fpItem.Height}";
                                lastFP.Add(tradingPoints[j]);
                            }
                            else
                            {
                                lastFP.Add(-1);
                            }
                        }
                    }
                    int checksum = CalpathCuda.AcceptPoints(cudaPoints, lastFP.ToArray());
                    Console.WriteLine($"CUDA accept checksum: {checksum}");
                    calIndexStarted += parallelStartPointCount;
                }

            }
            else
            {
                Console.WriteLine($"CUDA DLL not found: {CalpathCuda.DllPath}");
            }

            Console.WriteLine($"Generated {CircleGenerator.DefaultCircleCount}");


        }

        private static int CountPointsByCode(
            IEnumerable<CircleGenerator.DerivedPoint> points,
            int pointCode)
        {
            return points.Count(point => point.PointType % pointCode == 0);
        }
    }
}
