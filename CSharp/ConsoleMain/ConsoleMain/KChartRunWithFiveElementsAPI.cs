using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleMain
{
    public enum KChartTradingPointKind
    {
        /// <summary>
        /// 收割点，全场唯一。
        /// </summary>
        Harvest = 1,

        /// <summary>
        /// 采购点，每个圆分配一个。
        /// </summary>
        Purchase = 2,
    }

    /// <summary>
    /// KChartRunWithFiveElements 使用的交易点信息。
    /// 交易点包括收割点和采购点；采购点归属于某个真实圆，但实际路径点可能落在另一个圆上。
    /// 收割点账户使用虚拟归属 ID (0,0,0)，真实几何落点仍记录在 PointCircleIndex、PointCircleId 和 PointIndex 中。
    /// </summary>
    public sealed class KChartTradingPointInfo
    {
        public KChartTradingPointInfo(
            KChartTradingPointKind pointKind,
            int tradingPointIndex,
            int cudaPointIndex,
            int ownerCircleIndex,
            string ownerCircleId,
            int pointCircleIndex,
            string pointCircleId,
            int pointIndex)
        {
            PointKind = pointKind;
            TradingPointIndex = tradingPointIndex;
            CudaPointIndex = cudaPointIndex;
            OwnerCircleIndex = ownerCircleIndex;
            OwnerCircleId = ValidateCircleId(ownerCircleId, nameof(ownerCircleId));
            PointCircleIndex = pointCircleIndex;
            PointCircleId = ValidateCircleId(pointCircleId, nameof(pointCircleId));
            PointIndex = pointIndex;
        }

        /// <summary>
        /// 交易点类型：收割点或采购点。
        /// </summary>
        public KChartTradingPointKind PointKind { get; }

        /// <summary>
        /// 交易点序号，也就是该交易点在 tradingPoints 列表中的 index。
        /// 读取 Route.bin 时，这个值用于定位“以该交易点为起点”的路线结果行。
        /// </summary>
        public int TradingPointIndex { get; }

        /// <summary>
        /// CUDA 点序号，也就是该交易点在排序后的 cudaPoints 点集中的 index。
        /// 读取某一行 Route.bin 时，这个值用于定位目标点所在列，也可作为路径回溯的起点或终点。
        /// </summary>
        public int CudaPointIndex { get; }

        /// <summary>
        /// 账户归属圆序号。
        /// 对采购点来说，表示系统正在给哪个圆分配采购点；这个圆就是采购点账户的业务归属圆。
        /// 对收割点来说，账户归属不是某个真实圆，而是虚拟的 (0,0,0)，因此该值固定为 -1。
        /// 注意：这个值不一定等于 <see cref="PointCircleIndex"/>，因为采购点可以落在其他圆的路径点上。
        /// </summary>
        public int OwnerCircleIndex { get; }

        /// <summary>
        /// 账户归属圆 ID。
        /// 该字段永远不能为 null 或空字符串，并且必须是 24 位小写十六进制字符串。
        /// 对采购点来说，它表示这个采购点是为哪个圆服务、归哪个圆账户管理。
        /// 采购点的 OwnerCircleId 由归属圆在 Circle.bin 中的 12 字节原始数据转成 24 位小写十六进制字符串。
        /// 对收割点来说，OwnerCircleId 固定为虚拟 ID：000000000000000000000000，即三元组 (0,0,0) 的 12 字节形式。
        /// 收割点实际落在哪个真实圆上，应看 <see cref="PointCircleId"/>。
        /// 如果你要找“当前账户属于哪个圆”，使用这个字段。
        /// </summary>
        public string OwnerCircleId { get; }

        /// <summary>
        /// 路径点所在圆序号，也就是“被选中的那个路径点实际在哪个圆上”的序号。
        /// 对采购点来说，系统会在所有圆的路径点里寻找距离 OwnerCircleIndex 圆心最近的可用路径点；
        /// 找到的这个路径点可能在 OwnerCircleIndex 自己的圆上，也可能在其他圆上。
        /// PointCircleIndex 记录的就是这个最近路径点实际所在的圆序号。
        /// 对收割点来说，它表示收割点实际落在哪个圆上。
        /// </summary>
        public int PointCircleIndex { get; }

        /// <summary>
        /// 路径点所在圆 ID，也就是“被选中的那个路径点实际在哪个圆上”的圆 ID。
        /// 该字段永远不能为 null 或空字符串，并且必须是 24 位小写十六进制字符串。
        /// 该 ID 由路径点所在圆在 Circle.bin 中的 12 字节原始数据转成 24 位小写十六进制字符串。
        /// 对采购点来说，它表示距离业务归属圆圆心最近的采购路径点实际落在哪个圆上。
        /// 对收割点来说，它等于收割点实际所在圆的 ID。
        /// 如果你要根据几何路径点去算路线、取圆上路径点坐标，使用这个字段。
        /// </summary>
        public string PointCircleId { get; }

        /// <summary>
        /// 交易点在 PointCircleIndex 对应圆上的路径点序号。
        /// </summary>
        public int PointIndex { get; }

        internal static string ValidateCircleId(string? circleId, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(circleId))
            {
                throw new ArgumentException("Circle id cannot be null or empty.", parameterName);
            }

            if (circleId.Length != 24)
            {
                throw new ArgumentException("Circle id must be 24 lowercase hexadecimal characters.", parameterName);
            }

            for (int index = 0; index < circleId.Length; index++)
            {
                char c = circleId[index];
                bool isLowerHex =
                    (c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'f');
                if (!isLowerHex)
                {
                    throw new ArgumentException("Circle id must be 24 lowercase hexadecimal characters.", parameterName);
                }
            }

            return circleId;
        }
    }

    public sealed class KChartRoutePointInfo
    {
        public KChartRoutePointInfo(
            int cudaPointIndex,
            int circleIndex,
            string circleId,
            int pointIndex,
            int pointType,
            int pathPointCount,
            int signedRadius)
        {
            CudaPointIndex = cudaPointIndex;
            CircleIndex = circleIndex;
            CircleId = KChartTradingPointInfo.ValidateCircleId(circleId, nameof(circleId));
            PointIndex = pointIndex;
            PointType = pointType;
            PathPointCount = pathPointCount;
            SignedRadius = signedRadius;
        }

        public int CudaPointIndex { get; }

        public int CircleIndex { get; }

        public string CircleId { get; }

        public int PointIndex { get; }

        public int PointType { get; }

        public int PathPointCount { get; }

        public int SignedRadius { get; }
    }

    public sealed class KChartRouteInfo
    {
        public KChartRouteInfo(
            int startTradingPointIndex,
            int targetTradingPointIndex,
            int startCudaPointIndex,
            int targetCudaPointIndex,
            IReadOnlyList<int> routePointIndexes,
            IReadOnlyList<KChartRoutePointInfo> routePoints)
        {
            if (routePointIndexes is null)
            {
                throw new ArgumentNullException(nameof(routePointIndexes));
            }

            if (routePoints is null)
            {
                throw new ArgumentNullException(nameof(routePoints));
            }

            StartTradingPointIndex = startTradingPointIndex;
            TargetTradingPointIndex = targetTradingPointIndex;
            StartCudaPointIndex = startCudaPointIndex;
            TargetCudaPointIndex = targetCudaPointIndex;
            RoutePointIndexes = routePointIndexes.ToArray();
            RoutePoints = routePoints.ToArray();
        }

        public int StartTradingPointIndex { get; }

        public int TargetTradingPointIndex { get; }

        public int StartCudaPointIndex { get; }

        public int TargetCudaPointIndex { get; }

        public IReadOnlyList<int> RoutePointIndexes { get; }

        public IReadOnlyList<KChartRoutePointInfo> RoutePoints { get; }
    }

    public sealed class KChartCircleMaintenanceResult
    {
        public KChartCircleMaintenanceResult(
            string circlePath,
            string routePath,
            int originalCircleCount,
            int removedCircleCount,
            int addedCircleCount,
            int finalCircleCount,
            IReadOnlyList<int> removedCircleIndexes,
            IReadOnlyList<string> addedCircleIds)
        {
            CirclePath = circlePath ?? throw new ArgumentNullException(nameof(circlePath));
            RoutePath = routePath ?? throw new ArgumentNullException(nameof(routePath));
            OriginalCircleCount = originalCircleCount;
            RemovedCircleCount = removedCircleCount;
            AddedCircleCount = addedCircleCount;
            FinalCircleCount = finalCircleCount;
            RemovedCircleIndexes = removedCircleIndexes?.ToArray() ?? throw new ArgumentNullException(nameof(removedCircleIndexes));
            AddedCircleIds = addedCircleIds?.ToArray() ?? throw new ArgumentNullException(nameof(addedCircleIds));
        }

        public string CirclePath { get; }

        public string RoutePath { get; }

        public int OriginalCircleCount { get; }

        public int RemovedCircleCount { get; }

        public int AddedCircleCount { get; }

        public int FinalCircleCount { get; }

        public IReadOnlyList<int> RemovedCircleIndexes { get; }

        public IReadOnlyList<string> AddedCircleIds { get; }
    }

    public class KChartRunWithFiveElementsAPI
    {
        public const int HarvestOwnerCircleIndex = -1;
        public const string HarvestOwnerCircleId = "000000000000000000000000";
        private const int MaintenanceMaxAttemptsPerCircle = 200_000;
        private const int MaintenanceMaxExtraBridgeCircleCount = 10_000;
        private static readonly object RouteLoadCacheLock = new object();
        private static RouteLoadCache? CurrentRouteLoadCache;

        private sealed class RouteLoadCache
        {
            public RouteLoadCache(
                string circlePath,
                string routePath,
                IReadOnlyList<string> circleIds,
                IReadOnlyList<int> signedRadii,
                CircleGenerator.GeneratedPointData pointData,
                IReadOnlyList<CircleGenerator.DerivedPoint> sortedPoints,
                IReadOnlyList<int> tradingPointCudaIndexes,
                int pointCount)
            {
                CirclePath = circlePath;
                RoutePath = routePath;
                CircleIds = circleIds;
                SignedRadii = signedRadii;
                PointData = pointData;
                SortedPoints = sortedPoints;
                TradingPointCudaIndexes = tradingPointCudaIndexes;
                PointCount = pointCount;
            }

            public string CirclePath { get; }

            public string RoutePath { get; }

            public IReadOnlyList<string> CircleIds { get; }

            public IReadOnlyList<int> SignedRadii { get; }

            public CircleGenerator.GeneratedPointData PointData { get; }

            public IReadOnlyList<CircleGenerator.DerivedPoint> SortedPoints { get; }

            public IReadOnlyList<int> TradingPointCudaIndexes { get; }

            public int PointCount { get; }

            public Dictionary<int, int[]> RouteRowsByStartTradingPointIndex { get; } = new Dictionary<int, int[]>();
        }

        /// <summary>
        /// 为 KChartRunWithFiveElements 入口准备圆数据文件。
        /// 如果当前运行目录下没有 Circle.bin，则新建完整圆集合；
        /// 如果 Circle.bin 已存在，则读取已有圆，校验数量、几何唯一性和单一连通分量；
        /// 如果已有数量不足，则在已有圆基础上补齐，且新增圆必须与已有圆集合相交或相切；
        /// 补圆时，每新增一个候选圆都必须保持所有圆属于同一个连通分量；如果候选圆导致连通分量不为一，则删除该候选圆并重新生成。
        /// 如果 Circle.bin 是新建的，或者生成后内容发生变化，则删除旧路线文件，避免继续使用失效路径。
        /// </summary>
        /// <remarks>
        /// 这个方法只负责准备圆文件和清理失效路线文件，不负责实际计算 Route.bin。
        /// 实际路线计算由 <see cref="GenerateCircleAndRoute"/> 或 <see cref="CalculateRouteForExistingCircle"/> 触发。
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// 当已有 Circle.bin 数据非法、重复、数量超过目标数量，或者已有圆集合本身不是单一连通分量时抛出。
        /// </exception>
        public static void GenerateCircle()
        {
            // 当前运行目录下的 Circle.bin 完整路径；KChartRunWithFiveElements 运行时就以这个文件作为圆数据源。
            string circlePath = Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);

            // 记录调用 GenerateCircle 之前 Circle.bin 是否已经存在。
            // 后面要用它判断：这是新建文件，还是在已有文件基础上检查/补圆。
            bool circleFileExistsBefore = File.Exists(circlePath);

            // 如果 Circle.bin 已经存在，就先根据“旧 Circle.bin 内容的 SHA256”算出旧 Route.bin 路径。
            // 如果 Circle.bin 不存在，说明本次可能会新建圆文件，旧路线自然不存在。
            string? routePathBefore = circleFileExistsBefore
                ? Program.BuildRouteFilePath(circlePath)
                : null;

            // 生成或补齐 Circle.bin。
            // 内部规则：
            // 1. 文件不存在：新建到 DefaultCircleCount；
            // 2. 文件存在但数量不足：在已有圆基础上补齐；
            // 3. 文件存在但数量超出：报错，不截断；
            // 4. 已有圆必须是单一连通分量；
            // 5. 补圆过程中每加一个候选圆，都必须仍然保持单一连通分量。
            CircleGenerator.GenerateCircle();

            // 根据“新 Circle.bin 内容的 SHA256”计算新 Route.bin 路径。
            // 如果 Circle.bin 内容变了，SHA256 就会变，对应路线文件名也会变。
            string routePathAfter = Program.BuildRouteFilePath(circlePath);

            // 判断圆文件是否发生了会影响路线的变化。
            // 情况 1：原来没有 Circle.bin，本次新建了，肯定需要清理旧路线；
            // 情况 2：原来有 Circle.bin，但新旧 SHA256 对应的 Route.bin 路径不同，说明圆内容变了。
            bool circleFileChanged =
                !circleFileExistsBefore ||
                !string.Equals(routePathBefore, routePathAfter, StringComparison.OrdinalIgnoreCase);

            // 只有 Circle.bin 新建或内容变化时，才删除旧 Route.bin。
            // 如果圆文件没变，就保留现有路线，避免重复计算路径。
            if (circleFileChanged)
            {
                // 删除失效路线文件。
                // 新建 Circle.bin 时 deleteAllRouteFiles=true，会清理当前目录下旧的路线文件；
                // 已有 Circle.bin 改变时，会删除旧哈希对应路线，保留当前新哈希路线等待后续计算。
                DeleteRouteFilesAfterCircleChanged(
                    Path.GetDirectoryName(Path.GetFullPath(circlePath))!,
                    routePathBefore,
                    routePathAfter,
                deleteAllRouteFiles: !circleFileExistsBefore);
            }
        }

        /// <summary>
        /// 生成或补齐 Circle.bin，并按 ConsoleMain 原有 GENERATE 流程计算路线文件。
        /// 如果当前 Circle.bin 的 SHA256 对应 Route.bin 已经存在，则不重复计算路径。
        /// 如果对应 Route.bin 不存在，则严格校验 CUDA 入参后开始计算路径。
        /// 路径计算前会走原有 CUDA 入参严格校验，包括派生点顺序、唯一性、角色数量、connect、交易点和 lastFP 批次范围。
        /// </summary>
        public static void GenerateCircleAndRoute()
        {
            GenerateCircle();

            string circlePath = Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);
            string routePath = Program.BuildRouteFilePath(circlePath);
            if (File.Exists(routePath))
            {
                Console.WriteLine($"Route file already exists: {routePath}");
                return;
            }

            Program.CalculateRouteForExistingCircleWithoutInput();
        }

        /// <summary>
        /// 针对当前运行目录下已经存在的 Circle.bin 计算路线文件。
        /// 路线文件名使用 Circle.bin 内容的 SHA256 哈希值生成；
        /// 如果当前哈希对应的 Route.bin 已存在，则不重复计算；
        /// 如果不存在，则调用 ConsoleMain 原有路径计算流程，并在进入 CUDA 前执行完整参数校验。
        /// </summary>
        /// <remarks>
        /// 这个方法不会生成或补齐 Circle.bin。
        /// 调用它之前，必须已经通过 <see cref="GenerateCircle"/> 或其他明确流程准备好合法的 Circle.bin。
        /// 它的职责只是：检查当前圆文件是否已有对应路线；没有路线时，复用 ConsoleMain 原有无输入路径计算流程。
        /// </remarks>
        /// <exception cref="FileNotFoundException">
        /// 当前运行目录下不存在 Circle.bin 时抛出。
        /// </exception>
        public static void CalculateRouteForExistingCircle()
        {
            // 当前运行目录下的 Circle.bin 路径。
            // 注意：这个方法只处理“已有圆”，所以不会在这里调用 GenerateCircle()。
            string circlePath = Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);

            // 如果 Circle.bin 不存在，说明调用顺序错误。
            // 这里直接报错，而不是偷偷生成圆，避免隐藏上层流程问题。
            if (!File.Exists(circlePath))
            {
                throw new FileNotFoundException($"Circle file not found: {circlePath}", circlePath);
            }

            // 根据 Circle.bin 的文件内容 SHA256 计算对应 Route.bin 路径。
            // 只要圆文件内容不变，routePath 就稳定；圆文件内容一变，routePath 也会变。
            string routePath = Program.BuildRouteFilePath(circlePath);

            // 如果当前 Circle.bin 哈希对应的路线文件已经存在，说明路线已经计算过。
            // 此时直接返回，避免重复跑 CUDA 路径计算。
            if (File.Exists(routePath))
            {
                Console.WriteLine($"Route file already exists: {routePath}");
                return;
            }

            // 当前 Circle.bin 没有对应 Route.bin，开始计算路线。
            // 这个方法内部会沿用 ConsoleMain 原有流程，并在进入 CUDA 前做完整参数校验。
            Program.CalculateRouteForExistingCircleWithoutInput();
        }

        /// <summary>
        /// 破产后物理删除对应真实圆，随后补齐新圆并重算路线。
        /// 传入的是采购点账户的 OwnerCircleIndex；收割点使用虚拟归属圆，不能传入这里。
        /// 这个方法只在 KChartRunWithFiveElementsAPI 外壳层维护 Circle.bin，不改 ConsoleMain 圆生成核心代码。
        /// </summary>
        public static KChartCircleMaintenanceResult ReplaceBankruptCirclesAndRecalculateRoute(
            IEnumerable<int> bankruptOwnerCircleIndexes)
        {
            if (bankruptOwnerCircleIndexes is null)
            {
                throw new ArgumentNullException(nameof(bankruptOwnerCircleIndexes));
            }

            List<int> indexesToRemove = bankruptOwnerCircleIndexes.ToList();
            if (indexesToRemove.Count == 0)
            {
                string currentCirclePath = Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);
                string currentRoutePath = Program.BuildRouteFilePath(currentCirclePath);
                int currentCircleCount = File.Exists(currentCirclePath)
                    ? GetCircleRecordCount(currentCirclePath)
                    : 0;

                return new KChartCircleMaintenanceResult(
                    currentCirclePath,
                    currentRoutePath,
                    currentCircleCount,
                    0,
                    0,
                    currentCircleCount,
                    Array.Empty<int>(),
                    Array.Empty<string>());
            }

            ValidateBankruptCircleIndexes(indexesToRemove);

            string circlePath = Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);
            if (!File.Exists(circlePath))
            {
                throw new FileNotFoundException($"{CircleGenerator.CircleFileName} does not exist.", circlePath);
            }

            string routePathBefore = Program.BuildRouteFilePath(circlePath);
            List<CircleGenerator.CircleRecord> circles = ReadCircleRecords(circlePath);
            int originalCircleCount = circles.Count;

            foreach (int circleIndex in indexesToRemove)
            {
                if (circleIndex >= originalCircleCount)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(bankruptOwnerCircleIndexes),
                        $"Bankrupt circle index {circleIndex} is outside [0, {originalCircleCount - 1}].");
                }
            }

            foreach (int circleIndex in indexesToRemove.OrderByDescending(item => item))
            {
                circles.RemoveAt(circleIndex);
            }

            List<string> addedCircleIds = new List<string>(indexesToRemove.Count);
            if (circles.Count == 0)
            {
                circles = CircleGenerator.BuildConnectedCircles(originalCircleCount);
                foreach (CircleGenerator.CircleRecord circle in circles)
                {
                    addedCircleIds.Add(BuildCircleId(circle));
                }
            }
            else
            {
                HashSet<CircleGenerator.GeometryKey> geometries = BuildGeometrySetForMaintenance(circles);
                ReconnectAndReplenishCircles(
                    circles,
                    geometries,
                    originalCircleCount,
                    addedCircleIds);
            }

            EnsureSingleConnectedComponentForMaintenance(circles);
            WriteCircleRecords(circlePath, circles);

            string routePathAfter = Program.BuildRouteFilePath(circlePath);
            DeleteRouteFilesAfterCircleChanged(
                Path.GetDirectoryName(Path.GetFullPath(circlePath))!,
                routePathBefore,
                routePathAfter,
                deleteAllRouteFiles: false);

            Program.CalculateRouteForExistingCircleWithoutInput();

            if (!File.Exists(routePathAfter))
            {
                throw new InvalidOperationException(
                    $"Route recalculation did not create expected route file: {routePathAfter}");
            }

            return new KChartCircleMaintenanceResult(
                circlePath,
                routePathAfter,
                originalCircleCount,
                indexesToRemove.Count,
                addedCircleIds.Count,
                circles.Count,
                indexesToRemove.OrderBy(item => item).ToList(),
                addedCircleIds);
        }

        /// <summary>
        /// 从当前 Circle.bin 派生点数据中加载 KChartRunWithFiveElements 需要的交易点列表。
        /// 交易点包括全场唯一收割点和每个圆对应的采购点。
        /// </summary>
        /// <returns>
        /// 交易点信息列表。
        /// 每个元素都包含交易点类型、交易点序号、CUDA 点序号、账户归属圆、实际路径点所在圆以及路径点序号。
        /// </returns>
        /// <remarks>
        /// 收割点使用虚拟账户归属圆：OwnerCircleIndex = -1，OwnerCircleId = 000000000000000000000000。
        /// 采购点的 OwnerCircleIndex 表示“这个采购点归哪个圆账户管理”；
        /// PurchaseCircleIndex / PurchasePointIndex 表示“这个采购点实际落在哪个圆的哪个路径点上”。
        /// 注意：采购点可能落在别的圆上，所以 OwnerCircleIndex 不一定等于 PointCircleIndex。
        /// </remarks>
        public static IReadOnlyList<KChartTradingPointInfo> LoadTradingPoints()
        {
            // 读取当前 Circle.bin 中每个圆的 12 字节 ID。
            // circleIds[index] 与 Circle.bin 里的圆序号一一对应。
            IReadOnlyList<string> circleIds = LoadCircleIds(
                Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName));

            // 重新计算当前 Circle.bin 派生出的所有点。
            // 里面包括收割点、第一路径点、末尾联通点、普通联通点、采购点，以及采购点分配结果。
            CircleGenerator.GeneratedPointData pointData = CircleGenerator.GenerateDerivedPointData();

            // 按 CUDA 入参要求排序派生点。
            // 排序顺序必须与 BuildCudaPointArray 保持一致：circleIndex、pointIndex、pointType 升序。
            IReadOnlyList<CircleGenerator.DerivedPoint> sortedPoints = pointData.Points
                .OrderBy(item => item.CircleIndex)
                .ThenBy(item => item.PointIndex)
                .ThenBy(item => item.PointType)
                .ToList();

            // 建立“路径点位置 -> cudaPoints 点序号”的索引。
            // key 是 (CircleIndex, PointIndex)，value 是该点在 sortedPoints/cudaPoints 中的点序号。
            Dictionary<(int CircleIndex, int PointIndex), int> cudaPointIndexes =
                BuildCudaPointIndexMap(sortedPoints);

            // 把派生点转成 CUDA 使用的 int[] 三元组数组：
            // [circleIndex, pointIndex, pointType, ...]。
            int[] cudaPoints = CircleGenerator.BuildCudaPointArray(sortedPoints);

            // 从 cudaPoints 中找出所有交易点的 CUDA 点序号。
            // 交易点定义：收割点 pointType % 2 == 0，或采购点 pointType % 11 == 0。
            List<int> tradingPointCudaIndexes = Program.BuildTradingPointIndexes(cudaPoints);

            // 建立“CUDA 点序号 -> 交易点序号”的索引。
            // Route.bin 是按交易点序号组织起点行的，所以后续读取路线要用这个序号。
            Dictionary<int, int> tradingPointIndexes =
                BuildTradingPointIndexMap(tradingPointCudaIndexes);

            // 最终返回给 KChartRunWithFiveElements 的交易点列表。
            // 下面会先加入收割点，再加入所有采购点。
            List<KChartTradingPointInfo> tradingPoints = new List<KChartTradingPointInfo>();

            // 第一段：从所有派生点中找收割点。
            // 按需求，收割点全场唯一；这里如果底层派生点异常，后续交易点校验/使用阶段会暴露问题。
            foreach (CircleGenerator.DerivedPoint point in sortedPoints)
            {
                // 收割点标记规则：pointType 能被 HarvestPointCode 整除。
                if (point.PointType % CircleGenerator.HarvestPointCode == 0)
                {
                    // 根据收割点所在的 (CircleIndex, PointIndex)，找到它在 CUDA 点集里的序号。
                    int cudaPointIndex = GetRequiredCudaPointIndex(
                        cudaPointIndexes,
                        point.CircleIndex,
                        point.PointIndex);

                    // 加入收割点交易点信息。
                    // 收割点账户归属圆使用虚拟 (0,0,0) ID；
                    // 但它实际落在哪个真实圆、哪个路径点上，仍然记录 point.CircleIndex 和 point.PointIndex。
                    tradingPoints.Add(new KChartTradingPointInfo(
                        KChartTradingPointKind.Harvest,
                        GetRequiredTradingPointIndex(tradingPointIndexes, cudaPointIndex),
                        cudaPointIndex,
                        HarvestOwnerCircleIndex,
                        HarvestOwnerCircleId,
                        point.CircleIndex,
                        circleIds[point.CircleIndex],
                        point.PointIndex));
                }
            }

            // 第二段：根据采购点分配结果加入采购点。
            // 每个圆必须有一个采购点账户；采购点位置来自 GenerateDerivedPointData 的 PurchaseAssignments。
            foreach (CircleGenerator.PurchaseAssignment assignment in pointData.PurchaseAssignments)
            {
                // 根据采购点实际所在的 (PurchaseCircleIndex, PurchasePointIndex)，找到 CUDA 点序号。
                int cudaPointIndex = GetRequiredCudaPointIndex(
                    cudaPointIndexes,
                    assignment.PurchaseCircleIndex,
                    assignment.PurchasePointIndex);

                // 加入采购点交易点信息。
                // assignment.CircleIndex 是账户归属圆；
                // assignment.PurchaseCircleIndex 是采购点实际所在圆；
                // 这两个值可以不同。
                tradingPoints.Add(new KChartTradingPointInfo(
                    KChartTradingPointKind.Purchase,
                    GetRequiredTradingPointIndex(tradingPointIndexes, cudaPointIndex),
                    cudaPointIndex,
                    assignment.CircleIndex,
                    circleIds[assignment.CircleIndex],
                    assignment.PurchaseCircleIndex,
                    circleIds[assignment.PurchaseCircleIndex],
                    assignment.PurchasePointIndex));
            }

            // 返回收割点 + 采购点列表。
            // 调用方会据此创建 TradingPointAccount，并读取交易点之间的路线。
            return tradingPoints;
        }

        public static IReadOnlyList<KChartRouteInfo> LoadRoutesFromTradingPoint(
            KChartTradingPointInfo startTradingPoint,
            IReadOnlyList<KChartTradingPointInfo> targetTradingPoints)
        {
            if (startTradingPoint is null)
            {
                throw new ArgumentNullException(nameof(startTradingPoint));
            }

            if (targetTradingPoints is null)
            {
                throw new ArgumentNullException(nameof(targetTradingPoints));
            }

            string circlePath = Path.Combine(Environment.CurrentDirectory, CircleGenerator.CircleFileName);
            RouteLoadCache cache = GetOrCreateRouteLoadCache(circlePath);

            ValidateTradingPointAgainstCudaList(
                startTradingPoint,
                cache.TradingPointCudaIndexes,
                nameof(startTradingPoint));

            int[] routeRow = GetOrReadRouteRow(cache, startTradingPoint.TradingPointIndex);

            List<KChartRouteInfo> routes = new List<KChartRouteInfo>(targetTradingPoints.Count);
            foreach (KChartTradingPointInfo targetTradingPoint in targetTradingPoints)
            {
                ValidateTradingPointAgainstCudaList(
                    targetTradingPoint,
                    cache.TradingPointCudaIndexes,
                    nameof(targetTradingPoints));

                List<int> routePointIndexes = Program.BuildRoutePointIndexes(
                    routeRow,
                    startTradingPoint.CudaPointIndex,
                    targetTradingPoint.CudaPointIndex);
                IReadOnlyList<KChartRoutePointInfo> routePoints = BuildRoutePointInfos(
                    routePointIndexes,
                    cache.SortedPoints,
                    cache.PointData.PathPointCounts,
                    cache.CircleIds,
                    cache.SignedRadii);
                routes.Add(new KChartRouteInfo(
                    startTradingPoint.TradingPointIndex,
                    targetTradingPoint.TradingPointIndex,
                    startTradingPoint.CudaPointIndex,
                    targetTradingPoint.CudaPointIndex,
                    routePointIndexes,
                    routePoints));
            }

            return routes;
        }

        private static RouteLoadCache GetOrCreateRouteLoadCache(string circlePath)
        {
            string fullCirclePath = Path.GetFullPath(circlePath);
            string routePath = Program.BuildRouteFilePath(fullCirclePath);

            lock (RouteLoadCacheLock)
            {
                if (CurrentRouteLoadCache is not null &&
                    string.Equals(CurrentRouteLoadCache.CirclePath, fullCirclePath, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(CurrentRouteLoadCache.RoutePath, routePath, StringComparison.OrdinalIgnoreCase))
                {
                    return CurrentRouteLoadCache;
                }

                IReadOnlyList<string> circleIds = LoadCircleIds(fullCirclePath);
                IReadOnlyList<int> signedRadii = LoadCircleSignedRadii(fullCirclePath);
                CircleGenerator.GeneratedPointData pointData = CircleGenerator.GenerateDerivedPointData(fullCirclePath);
                IReadOnlyList<CircleGenerator.DerivedPoint> sortedPoints = pointData.Points
                    .OrderBy(item => item.CircleIndex)
                    .ThenBy(item => item.PointIndex)
                    .ThenBy(item => item.PointType)
                    .ToList();
                CircleGenerator.ValidateCudaPointOrderAndUniqueness(sortedPoints);
                CircleGenerator.ValidateCudaPointBusinessRules(sortedPoints, pointData.PathPointCounts);

                int[] cudaPoints = CircleGenerator.BuildCudaPointArray(sortedPoints);
                int pointCount = cudaPoints.Length / 3;
                List<int> tradingPointCudaIndexes = Program.BuildTradingPointIndexes(cudaPoints);

                CurrentRouteLoadCache = new RouteLoadCache(
                    fullCirclePath,
                    routePath,
                    circleIds,
                    signedRadii,
                    pointData,
                    sortedPoints,
                    tradingPointCudaIndexes,
                    pointCount);

                return CurrentRouteLoadCache;
            }
        }

        private static int[] GetOrReadRouteRow(RouteLoadCache cache, int startTradingPointIndex)
        {
            lock (RouteLoadCacheLock)
            {
                if (!cache.RouteRowsByStartTradingPointIndex.TryGetValue(startTradingPointIndex, out int[]? routeRow))
                {
                    routeRow = Program.ReadRouteRow(
                        cache.RoutePath,
                        startTradingPointIndex,
                        cache.TradingPointCudaIndexes.Count,
                        cache.PointCount);
                    cache.RouteRowsByStartTradingPointIndex.Add(startTradingPointIndex, routeRow);
                }

                return routeRow;
            }
        }

        private static IReadOnlyList<KChartRoutePointInfo> BuildRoutePointInfos(
            IReadOnlyList<int> routePointIndexes,
            IReadOnlyList<CircleGenerator.DerivedPoint> sortedPoints,
            IReadOnlyList<int> pathPointCounts,
            IReadOnlyList<string> circleIds,
            IReadOnlyList<int> signedRadii)
        {
            List<KChartRoutePointInfo> routePoints = new List<KChartRoutePointInfo>(routePointIndexes.Count);

            foreach (int cudaPointIndex in routePointIndexes)
            {
                if (cudaPointIndex < 0 || cudaPointIndex >= sortedPoints.Count)
                {
                    throw new InvalidOperationException(
                        $"Route CUDA point index {cudaPointIndex} is outside [0, {sortedPoints.Count - 1}].");
                }

                CircleGenerator.DerivedPoint point = sortedPoints[cudaPointIndex];
                routePoints.Add(new KChartRoutePointInfo(
                    cudaPointIndex,
                    point.CircleIndex,
                    circleIds[point.CircleIndex],
                    point.PointIndex,
                    point.PointType,
                    pathPointCounts[point.CircleIndex],
                    signedRadii[point.CircleIndex]));
            }

            return routePoints;
        }

        private static void ValidateBankruptCircleIndexes(IReadOnlyList<int> indexesToRemove)
        {
            HashSet<int> seen = new HashSet<int>();
            foreach (int circleIndex in indexesToRemove)
            {
                if (circleIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(indexesToRemove),
                        $"Bankrupt circle index cannot be negative. Actual: {circleIndex}.");
                }

                if (!seen.Add(circleIndex))
                {
                    throw new InvalidOperationException(
                        $"Duplicate bankrupt circle index: {circleIndex}.");
                }
            }
        }

        private static void ReconnectAndReplenishCircles(
            List<CircleGenerator.CircleRecord> circles,
            HashSet<CircleGenerator.GeometryKey> geometries,
            int targetCircleCount,
            List<string> addedCircleIds)
        {
            while (circles.Count < targetCircleCount)
            {
                IReadOnlyList<IReadOnlyList<int>> components = BuildConnectedComponents(circles);
                CircleGenerator.CircleRecord candidate = components.Count > 1
                    ? GenerateRandomCircleConnectingComponents(
                        circles,
                        components,
                        geometries)
                    : GenerateRandomCircleConnectingAnyExistingCircle(circles, geometries);

                AddCircleForMaintenance(candidate, circles, geometries);
                addedCircleIds.Add(BuildCircleId(candidate));
            }

            int extraBridgeCircleCount = 0;
            while (BuildConnectedComponents(circles).Count > 1)
            {
                if (extraBridgeCircleCount >= MaintenanceMaxExtraBridgeCircleCount)
                {
                    throw new InvalidOperationException(
                        $"Failed to reconnect all split circle components after adding {extraBridgeCircleCount} extra bridge circles.");
                }

                IReadOnlyList<IReadOnlyList<int>> components = BuildConnectedComponents(circles);
                CircleGenerator.CircleRecord candidate = GenerateRandomCircleConnectingComponents(
                    circles,
                    components,
                    geometries);
                AddCircleForMaintenance(candidate, circles, geometries);
                addedCircleIds.Add(BuildCircleId(candidate));
                extraBridgeCircleCount++;
            }

            EnsureSingleConnectedComponentForMaintenance(circles);
        }

        private static CircleGenerator.CircleRecord GenerateRandomCircleConnectingComponents(
            IReadOnlyList<CircleGenerator.CircleRecord> circles,
            IReadOnlyList<IReadOnlyList<int>> components,
            HashSet<CircleGenerator.GeometryKey> geometries)
        {
            if (components.Count < 2)
            {
                throw new ArgumentException("At least two components are required to generate a bridge circle.", nameof(components));
            }

            for (int attempt = 0; attempt < MaintenanceMaxAttemptsPerCircle; attempt++)
            {
                int leftComponentIndex = Random.Shared.Next(components.Count);
                int rightComponentIndex = Random.Shared.Next(components.Count - 1);
                if (rightComponentIndex >= leftComponentIndex)
                {
                    rightComponentIndex++;
                }

                IReadOnlyList<int> leftComponent = components[leftComponentIndex];
                IReadOnlyList<int> rightComponent = components[rightComponentIndex];
                CircleGenerator.CircleRecord left = circles[leftComponent[Random.Shared.Next(leftComponent.Count)]];
                CircleGenerator.CircleRecord right = circles[rightComponent[Random.Shared.Next(rightComponent.Count)]];
                CircleGenerator.CircleRecord? candidate = TryGenerateCircleIntersectingTargets(left, right);
                if (candidate is null)
                {
                    continue;
                }

                CircleGenerator.CircleRecord circle = candidate.Value;
                if (geometries.Contains(circle.GeometryKey))
                {
                    continue;
                }

                if (CanConnectToComponent(circle, circles, leftComponent) &&
                    CanConnectToComponent(circle, circles, rightComponent))
                {
                    return circle;
                }
            }

            throw new InvalidOperationException(
                $"Failed to generate a replacement circle that reconnects split components after {MaintenanceMaxAttemptsPerCircle} attempts.");
        }

        private static CircleGenerator.CircleRecord GenerateRandomCircleConnectingAnyExistingCircle(
            IReadOnlyList<CircleGenerator.CircleRecord> circles,
            HashSet<CircleGenerator.GeometryKey> geometries)
        {
            for (int attempt = 0; attempt < MaintenanceMaxAttemptsPerCircle; attempt++)
            {
                CircleGenerator.CircleRecord target = circles[Random.Shared.Next(circles.Count)];
                CircleGenerator.CircleRecord? candidate = TryGenerateCircleIntersectingTargets(target, null);
                if (candidate is null)
                {
                    continue;
                }

                CircleGenerator.CircleRecord circle = candidate.Value;
                if (geometries.Contains(circle.GeometryKey))
                {
                    continue;
                }

                if (CanConnectToAnyExistingCircle(circle, circles))
                {
                    return circle;
                }
            }

            throw new InvalidOperationException(
                $"Failed to generate a connected replacement circle after {MaintenanceMaxAttemptsPerCircle} attempts.");
        }

        private static CircleGenerator.CircleRecord? TryGenerateCircleIntersectingTargets(
            CircleGenerator.CircleRecord firstTarget,
            CircleGenerator.CircleRecord? secondTarget)
        {
            int minCoordinate = -CircleGenerator.MaxX + CircleGenerator.MinRadius;
            int maxCoordinate = CircleGenerator.MaxX - CircleGenerator.MinRadius;
            int a = Random.Shared.Next(minCoordinate, maxCoordinate + 1);
            int b = Random.Shared.Next(minCoordinate, maxCoordinate + 1);

            int radiusLowerBound = CircleGenerator.MinRadius;
            int radiusUpperBound = Math.Min(
                CircleGenerator.MaxRadius,
                Math.Min(
                    CircleGenerator.MaxX - Math.Abs(a),
                    CircleGenerator.MaxY - Math.Abs(b)));

            IntersectRadiusRangeWithTarget(
                a,
                b,
                firstTarget,
                ref radiusLowerBound,
                ref radiusUpperBound);

            if (secondTarget is CircleGenerator.CircleRecord target)
            {
                IntersectRadiusRangeWithTarget(
                    a,
                    b,
                    target,
                    ref radiusLowerBound,
                    ref radiusUpperBound);
            }

            if (radiusLowerBound > radiusUpperBound)
            {
                return null;
            }

            int radius = Random.Shared.Next(radiusLowerBound, radiusUpperBound + 1);
            int signedRadius = Random.Shared.Next(2) == 0 ? radius : -radius;
            CircleGenerator.CircleRecord candidate = new CircleGenerator.CircleRecord(a, b, signedRadius);
            CircleGenerator.ValidateCircle(candidate);
            return candidate;
        }

        private static void IntersectRadiusRangeWithTarget(
            int a,
            int b,
            CircleGenerator.CircleRecord target,
            ref int radiusLowerBound,
            ref int radiusUpperBound)
        {
            long dx = (long)a - target.A;
            long dy = (long)b - target.B;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            int minRadiusForIntersection = (int)Math.Ceiling(Math.Abs(distance - target.Radius));
            int maxRadiusForIntersection = (int)Math.Floor(distance + target.Radius);

            radiusLowerBound = Math.Max(radiusLowerBound, minRadiusForIntersection);
            radiusUpperBound = Math.Min(radiusUpperBound, maxRadiusForIntersection);
        }

        private static void AddCircleForMaintenance(
            CircleGenerator.CircleRecord circle,
            List<CircleGenerator.CircleRecord> circles,
            HashSet<CircleGenerator.GeometryKey> geometries)
        {
            CircleGenerator.ValidateCircle(circle);

            if (!geometries.Add(circle.GeometryKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate geometry circle: ({circle.A}, {circle.B}, {circle.Radius}).");
            }

            if (circles.Count > 0 && !CanConnectToAnyExistingCircle(circle, circles))
            {
                throw new InvalidOperationException(
                    $"Replacement circle does not connect to the existing circle set: ({circle.A}, {circle.B}, {circle.SignedRadius}).");
            }

            circles.Add(circle);
        }

        private static bool CanConnectToAnyExistingCircle(
            CircleGenerator.CircleRecord candidate,
            IReadOnlyList<CircleGenerator.CircleRecord> existingCircles)
        {
            foreach (CircleGenerator.CircleRecord existing in existingCircles)
            {
                if (CircleGenerator.HasCircumferenceIntersectionOrTangency(candidate, existing))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanConnectToComponent(
            CircleGenerator.CircleRecord candidate,
            IReadOnlyList<CircleGenerator.CircleRecord> circles,
            IReadOnlyList<int> component)
        {
            foreach (int circleIndex in component)
            {
                if (CircleGenerator.HasCircumferenceIntersectionOrTangency(candidate, circles[circleIndex]))
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<CircleGenerator.GeometryKey> BuildGeometrySetForMaintenance(
            IReadOnlyList<CircleGenerator.CircleRecord> circles)
        {
            HashSet<CircleGenerator.GeometryKey> geometries = new HashSet<CircleGenerator.GeometryKey>();

            foreach (CircleGenerator.CircleRecord circle in circles)
            {
                CircleGenerator.ValidateCircle(circle);
                if (!geometries.Add(circle.GeometryKey))
                {
                    throw new InvalidOperationException(
                        $"Duplicate geometry circle: ({circle.A}, {circle.B}, {circle.Radius}).");
                }
            }

            return geometries;
        }

        private static void EnsureSingleConnectedComponentForMaintenance(
            IReadOnlyList<CircleGenerator.CircleRecord> circles)
        {
            if (BuildConnectedComponents(circles).Count > 1)
            {
                throw new InvalidOperationException("Circle set must remain one connected component.");
            }
        }

        private static IReadOnlyList<IReadOnlyList<int>> BuildConnectedComponents(
            IReadOnlyList<CircleGenerator.CircleRecord> circles)
        {
            List<IReadOnlyList<int>> components = new List<IReadOnlyList<int>>();
            bool[] visited = new bool[circles.Count];

            for (int start = 0; start < circles.Count; start++)
            {
                if (visited[start])
                {
                    continue;
                }

                List<int> component = new List<int>();
                Queue<int> pending = new Queue<int>();
                visited[start] = true;
                pending.Enqueue(start);

                while (pending.Count > 0)
                {
                    int current = pending.Dequeue();
                    component.Add(current);

                    for (int next = 0; next < circles.Count; next++)
                    {
                        if (visited[next])
                        {
                            continue;
                        }

                        if (!CircleGenerator.HasCircumferenceIntersectionOrTangency(circles[current], circles[next]))
                        {
                            continue;
                        }

                        visited[next] = true;
                        pending.Enqueue(next);
                    }
                }

                components.Add(component);
            }

            return components;
        }

        private static List<CircleGenerator.CircleRecord> ReadCircleRecords(string circlePath)
        {
            string fullPath = Path.GetFullPath(circlePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"{CircleGenerator.CircleFileName} does not exist.", fullPath);
            }

            const int circleRecordSize = sizeof(int) * 3;
            FileInfo fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length % circleRecordSize != 0)
            {
                throw new InvalidOperationException(
                    $"{CircleGenerator.CircleFileName} is corrupted: file length must be a multiple of {circleRecordSize} bytes.");
            }

            List<CircleGenerator.CircleRecord> circles = new List<CircleGenerator.CircleRecord>((int)(fileInfo.Length / circleRecordSize));
            using FileStream stream = File.OpenRead(fullPath);
            using BinaryReader reader = new BinaryReader(stream);

            while (stream.Position < stream.Length)
            {
                circles.Add(new CircleGenerator.CircleRecord(
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32()));
            }

            return circles;
        }

        private static int GetCircleRecordCount(string circlePath)
        {
            string fullPath = Path.GetFullPath(circlePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"{CircleGenerator.CircleFileName} does not exist.", fullPath);
            }

            const int circleRecordSize = sizeof(int) * 3;
            FileInfo fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length % circleRecordSize != 0)
            {
                throw new InvalidOperationException(
                    $"{CircleGenerator.CircleFileName} is corrupted: file length must be a multiple of {circleRecordSize} bytes.");
            }

            return (int)(fileInfo.Length / circleRecordSize);
        }

        private static void WriteCircleRecords(
            string circlePath,
            IReadOnlyList<CircleGenerator.CircleRecord> circles)
        {
            string fullPath = Path.GetFullPath(circlePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            using FileStream stream = File.Create(fullPath);
            using BinaryWriter writer = new BinaryWriter(stream);
            foreach (CircleGenerator.CircleRecord circle in circles)
            {
                writer.Write(circle.A);
                writer.Write(circle.B);
                writer.Write(circle.SignedRadius);
            }
        }

        private static string BuildCircleId(CircleGenerator.CircleRecord circle)
        {
            byte[] bytes = new byte[sizeof(int) * 3];
            BitConverter.GetBytes(circle.A).CopyTo(bytes, 0);
            BitConverter.GetBytes(circle.B).CopyTo(bytes, sizeof(int));
            BitConverter.GetBytes(circle.SignedRadius).CopyTo(bytes, sizeof(int) * 2);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static Dictionary<(int CircleIndex, int PointIndex), int> BuildCudaPointIndexMap(
            IReadOnlyList<CircleGenerator.DerivedPoint> sortedPoints)
        {
            Dictionary<(int CircleIndex, int PointIndex), int> indexes =
                new Dictionary<(int CircleIndex, int PointIndex), int>(sortedPoints.Count);

            for (int index = 0; index < sortedPoints.Count; index++)
            {
                CircleGenerator.DerivedPoint point = sortedPoints[index];
                indexes.Add((point.CircleIndex, point.PointIndex), index);
            }

            return indexes;
        }

        private static Dictionary<int, int> BuildTradingPointIndexMap(IReadOnlyList<int> tradingPointCudaIndexes)
        {
            Dictionary<int, int> indexes = new Dictionary<int, int>(tradingPointCudaIndexes.Count);

            for (int index = 0; index < tradingPointCudaIndexes.Count; index++)
            {
                indexes.Add(tradingPointCudaIndexes[index], index);
            }

            return indexes;
        }

        private static int GetRequiredCudaPointIndex(
            IReadOnlyDictionary<(int CircleIndex, int PointIndex), int> cudaPointIndexes,
            int circleIndex,
            int pointIndex)
        {
            if (!cudaPointIndexes.TryGetValue((circleIndex, pointIndex), out int cudaPointIndex))
            {
                throw new InvalidOperationException(
                    $"CUDA point ({circleIndex}, {pointIndex}) does not exist.");
            }

            return cudaPointIndex;
        }

        private static int GetRequiredTradingPointIndex(
            IReadOnlyDictionary<int, int> tradingPointIndexes,
            int cudaPointIndex)
        {
            if (!tradingPointIndexes.TryGetValue(cudaPointIndex, out int tradingPointIndex))
            {
                throw new InvalidOperationException(
                    $"CUDA point {cudaPointIndex} is not in tradingPoints.");
            }

            return tradingPointIndex;
        }

        private static void ValidateTradingPointAgainstCudaList(
            KChartTradingPointInfo tradingPoint,
            IReadOnlyList<int> tradingPointCudaIndexes,
            string parameterName)
        {
            if (tradingPoint.TradingPointIndex < 0 ||
                tradingPoint.TradingPointIndex >= tradingPointCudaIndexes.Count)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Trading point index {tradingPoint.TradingPointIndex} is outside [0, {tradingPointCudaIndexes.Count - 1}].");
            }

            int actualCudaPointIndex = tradingPointCudaIndexes[tradingPoint.TradingPointIndex];
            if (actualCudaPointIndex != tradingPoint.CudaPointIndex)
            {
                throw new InvalidOperationException(
                    $"Trading point {tradingPoint.TradingPointIndex} expected CUDA point {tradingPoint.CudaPointIndex}, actual {actualCudaPointIndex}.");
            }
        }

        private static IReadOnlyList<string> LoadCircleIds(string circlePath)
        {
            string fullPath = Path.GetFullPath(circlePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"{CircleGenerator.CircleFileName} does not exist.", fullPath);
            }

            const int circleRecordSize = sizeof(int) * 3;
            byte[] bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length % circleRecordSize != 0)
            {
                throw new InvalidOperationException(
                    $"{CircleGenerator.CircleFileName} is corrupted: file length must be a multiple of {circleRecordSize} bytes.");
            }

            List<string> circleIds = new List<string>(bytes.Length / circleRecordSize);
            for (int offset = 0; offset < bytes.Length; offset += circleRecordSize)
            {
                circleIds.Add(Convert.ToHexString(bytes, offset, circleRecordSize).ToLowerInvariant());
            }

            return circleIds;
        }

        private static IReadOnlyList<int> LoadCircleSignedRadii(string circlePath)
        {
            string fullPath = Path.GetFullPath(circlePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"{CircleGenerator.CircleFileName} does not exist.", fullPath);
            }

            const int circleRecordSize = sizeof(int) * 3;
            byte[] bytes = File.ReadAllBytes(fullPath);
            if (bytes.Length % circleRecordSize != 0)
            {
                throw new InvalidOperationException(
                    $"{CircleGenerator.CircleFileName} is corrupted: file length must be a multiple of {circleRecordSize} bytes.");
            }

            List<int> signedRadii = new List<int>(bytes.Length / circleRecordSize);
            for (int offset = 0; offset < bytes.Length; offset += circleRecordSize)
            {
                signedRadii.Add(BitConverter.ToInt32(bytes, offset + sizeof(int) * 2));
            }

            return signedRadii;
        }

        private static void DeleteRouteFilesAfterCircleChanged(
            string directory,
            string? routePathBefore,
            string routePathAfter,
            bool deleteAllRouteFiles)
        {
            if (deleteAllRouteFiles)
            {
                foreach (string routeFile in Directory.EnumerateFiles(directory, "*Route.bin"))
                {
                    DeleteFileIfExists(routeFile);
                }

                foreach (string routeTempFile in Directory.EnumerateFiles(directory, "*Route.bin.tmp"))
                {
                    DeleteFileIfExists(routeTempFile);
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(routePathBefore))
            {
                DeleteFileIfExists(routePathBefore);
                DeleteFileIfExists(routePathBefore + ".tmp");
            }

            DeleteFileIfExists(routePathAfter);
            DeleteFileIfExists(routePathAfter + ".tmp");
        }

        private static void DeleteFileIfExists(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            File.Delete(path);
            Console.WriteLine($"Deleted stale route file: {path}");
        }
    }
}
