namespace KChartRunWithFiveElements
{
    internal static class SampleKLines
    {
        public static IReadOnlyDictionary<string, IReadOnlyList<KLine>> BuildAllSamples()
        {
            return new Dictionary<string, IReadOnlyList<KLine>>
            {
                ["金"] = BuildMetalSample(),
                ["水"] = BuildWaterSample(),
                ["土"] = BuildEarthSample(),
                ["火"] = BuildFireSample(),
                ["木"] = BuildWoodSample()
            };
        }

        public static IReadOnlyList<KLine> BuildMetalSample()
        {
            List<KLine> lines = BuildBaseWindow();
            lines[23] = CreateLine(23, 100m, 104m, 100m, 103m);
            AddTargetLine(lines);
            return lines;
        }

        private static IReadOnlyList<KLine> BuildWaterSample()
        {
            List<KLine> lines = BuildBaseWindow();
            lines[23] = CreateLine(23, 100m, 100m, 96m, 97m);
            AddTargetLine(lines);
            return lines;
        }

        private static IReadOnlyList<KLine> BuildEarthSample()
        {
            List<KLine> lines = BuildBaseWindow();
            AddTargetLine(lines);
            return lines;
        }

        private static IReadOnlyList<KLine> BuildFireSample()
        {
            List<KLine> lines = BuildBaseWindow();
            lines[5] = CreateLine(5, 100m, 104m, 99m, 100m);
            AddTargetLine(lines);
            return lines;
        }

        private static IReadOnlyList<KLine> BuildWoodSample()
        {
            List<KLine> lines = BuildBaseWindow();
            lines[5] = CreateLine(5, 100m, 101m, 96m, 100m);
            AddTargetLine(lines);
            return lines;
        }

        private static List<KLine> BuildBaseWindow()
        {
            List<KLine> lines = new List<KLine>();
            for (int index = 0; index < FiveElementClassifier.WindowSize; index++)
            {
                lines.Add(CreateLine(index, 100m, 101m, 99m, 100m));
            }

            return lines;
        }

        private static void AddTargetLine(List<KLine> lines)
        {
            lines.Add(CreateLine(FiveElementClassifier.WindowSize, 100m, 101m, 99m, 100m));
        }

        private static KLine CreateLine(
            int index,
            decimal openValue,
            decimal highValue,
            decimal lowValue,
            decimal closeValue)
        {
            return new KLine(
                new DateTime(2026, 1, 1, 0, 0, 0).AddHours(index),
                openValue,
                highValue,
                lowValue,
                closeValue,
                1m);
        }
    }
}
