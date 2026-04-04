using System.Runtime.InteropServices;

namespace ConsoleMain
{
    internal static class CalpathCuda
    {
        public const string DllPath = @"E:\Project\tradeRobot\sxtgyrq\tradeRobot\CUDA\Calpath\x64\Debug\Calpath.dll";

        public static bool IsAvailable => File.Exists(DllPath);

        public static int AcceptPoints(int[] cudaPoints)
        {
            return AcceptPoints(cudaPoints, Array.Empty<int>());
        }

        public static int AcceptPoints(int[] cudaPoints, int[] lastFP, int[] connect)
        {
            ArgumentNullException.ThrowIfNull(cudaPoints);
            ArgumentNullException.ThrowIfNull(lastFP);

            if (cudaPoints.Length % 3 != 0)
            {
                throw new ArgumentException(
                    "CUDA point array length must be a multiple of 3.",
                    nameof(cudaPoints));
            }

            if (cudaPoints.Length == 0)
            {
                return 0;
            }

            if (!IsAvailable)
            {
                throw new FileNotFoundException("Calpath CUDA DLL does not exist.", DllPath);
            }

            return CalpathAcceptPoints(cudaPoints, cudaPoints.Length, lastFP, lastFP.Length);
        }

        [DllImport(DllPath, EntryPoint = "Calpath_AcceptPoints", CallingConvention = CallingConvention.Cdecl)]
        private static extern int CalpathAcceptPoints(
            int[] cudaPoints,
            int length,
            int[] lastFP,
            int lastFPLength);
    }
}
