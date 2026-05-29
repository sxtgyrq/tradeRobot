using System.Runtime.InteropServices;

namespace ConsoleMain
{
    internal static class CalpathCuda
    {
        public const string DllPath = @"E:\Project\tradeRobot\sxtgyrq\tradeRobot\CUDA\Calpath\x64\Debug\Calpath.dll";

        public static bool IsAvailable => File.Exists(DllPath);

        public static int AcceptPoints(int[] cudaPoints, int[] lastFP, int[] connect)
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
    }
}
