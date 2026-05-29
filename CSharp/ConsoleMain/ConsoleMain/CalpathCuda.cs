using System.Runtime.InteropServices;

namespace ConsoleMain
{
    internal static class CalpathCuda
    {
        public const string DllPath = @"E:\Project\tradeRobot\sxtgyrq\tradeRobot\CUDA\Calpath\x64\Debug\Calpath.dll";

        public static bool IsAvailable => File.Exists(DllPath);

        public static bool TryGetParallelStartPointRecommendation(
            int cudaPointLength,
            out int fullLoadCount,
            out int recommendedCount)
        {
            fullLoadCount = 0;
            recommendedCount = 0;

            if (!IsAvailable)
            {
                return false;
            }

            try
            {
                int status = CalpathGetParallelStartPointRecommendation(
                    cudaPointLength,
                    out fullLoadCount,
                    out recommendedCount);

                return status == 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }

        public static int AcceptPoints(
            IReadOnlyList<CircleGenerator.DerivedPoint> points,
            IReadOnlyList<int> pathPointCounts,
            int[] cudaPoints,
            int[] lastFP,
            int[] connect,
            IReadOnlyList<int> tradingPoints,
            int harvestPointCount,
            int purchasePointCount,
            int calIndexStarted,
            int batchStartPointCount)
        {
            CircleGenerator.ValidateCudaInputPackage(
                points,
                pathPointCounts,
                cudaPoints,
                connect,
                tradingPoints,
                harvestPointCount,
                purchasePointCount,
                calIndexStarted,
                batchStartPointCount,
                lastFP);

            return AcceptValidatedPoints(cudaPoints, lastFP, connect);
        }

        private static int AcceptValidatedPoints(int[] cudaPoints, int[] lastFP, int[] connect)
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

            if (!IsAvailable)
            {
                throw new FileNotFoundException("Calpath CUDA DLL does not exist.", DllPath);
            }

            return CalpathAcceptPoints(cudaPoints, cudaPoints.Length, lastFP, lastFP.Length, connect);
        }

        [DllImport(DllPath, EntryPoint = "Calpath_AcceptPoints", CallingConvention = CallingConvention.Cdecl)]
        private static extern int CalpathAcceptPoints(
            [In] int[] cudaPoints,
            int length,
            [In, Out] int[] lastFP,
            int lastFPLength,
            [In] int[] connect);

        [DllImport(DllPath, EntryPoint = "Calpath_GetParallelStartPointRecommendation", CallingConvention = CallingConvention.Cdecl)]
        private static extern int CalpathGetParallelStartPointRecommendation(
            int cudaPointLength,
            out int fullLoadCount,
            out int recommendedCount);
    }
}
