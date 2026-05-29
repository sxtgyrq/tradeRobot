namespace KChartRunWithFiveElements
{
    internal static class FiveElementClassifier
    {
        public const int WindowSize = 24;

        public static IReadOnlyList<KLineFiveElementResult> ClassifyAll(IReadOnlyList<KLine> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            List<KLineFiveElementResult> results = new List<KLineFiveElementResult>();
            for (int targetIndex = WindowSize; targetIndex < lines.Count; targetIndex++)
            {
                int windowStartIndex = targetIndex - WindowSize;
                results.Add(ClassifyNext(lines, windowStartIndex));
            }

            return results;
        }

        public static KLineFiveElementResult ClassifyNext(
            IReadOnlyList<KLine> lines,
            int windowStartIndex)
        {
            ArgumentNullException.ThrowIfNull(lines);

            if (windowStartIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(windowStartIndex), "窗口起点不能为负数。");
            }

            int windowEndIndex = windowStartIndex + WindowSize - 1;
            int targetIndex = windowStartIndex + WindowSize;
            if (targetIndex >= lines.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(windowStartIndex),
                    $"从 {windowStartIndex} 开始需要 24 根窗口 K 线和第 25 根目标 K 线。当前 K 线数量：{lines.Count}。");
            }

            KLine first = lines[windowStartIndex];
            decimal lowerBound = first.OpenValue * 45m / 46m;
            decimal upperBound = first.OpenValue * 46m / 45m;
            KLine lastWindowLine = lines[windowEndIndex];

            FiveElement element = ClassifyWindow(
                lines,
                windowStartIndex,
                lowerBound,
                upperBound,
                lastWindowLine.CloseValue);

            return new KLineFiveElementResult(
                windowStartIndex,
                windowEndIndex,
                targetIndex,
                lines[targetIndex].DateTime,
                element,
                lowerBound,
                upperBound);
        }

        private static FiveElement ClassifyWindow(
            IReadOnlyList<KLine> lines,
            int windowStartIndex,
            decimal lowerBound,
            decimal upperBound,
            decimal lastCloseValue)
        {
            bool hasBreakthrough = false;
            bool firstBreakthroughTouchesUpper = false;
            decimal lowestValue = decimal.MaxValue;
            decimal highestValue = decimal.MinValue;

            for (int index = windowStartIndex; index < windowStartIndex + WindowSize; index++)
            {
                KLine line = lines[index];
                lowestValue = Math.Min(lowestValue, line.LowValue);
                highestValue = Math.Max(highestValue, line.HighValue);

                bool breaksUpper = line.HighValue > upperBound;
                bool breaksLower = line.LowValue < lowerBound;
                if (!hasBreakthrough && (breaksUpper || breaksLower))
                {
                    hasBreakthrough = true;
                    firstBreakthroughTouchesUpper = breaksUpper;
                }
            }

            // 判定顺序必须和需求一致：最终收盘突破优先判为金或水。
            if (lastCloseValue > upperBound)
            {
                return FiveElement.Metal;
            }

            if (lastCloseValue < lowerBound)
            {
                return FiveElement.Water;
            }

            // 最终收盘没有突破时，若整个窗口都在上下边界内，则判为土。
            if (lowestValue >= lowerBound && highestValue <= upperBound)
            {
                return FiveElement.Earth;
            }

            if (!hasBreakthrough)
            {
                throw new InvalidOperationException("窗口既不是土属性，又没有找到突破 K 线，五行判定逻辑不完整。");
            }

            // 最终收盘在边界内，但窗口中曾突破；第一根突破上限为火，否则为木。
            return firstBreakthroughTouchesUpper
                ? FiveElement.Fire
                : FiveElement.Wood;
        }
    }
}
