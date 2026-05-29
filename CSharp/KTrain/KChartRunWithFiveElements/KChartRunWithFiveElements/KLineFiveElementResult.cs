namespace KChartRunWithFiveElements
{
    internal sealed class KLineFiveElementResult
    {
        public KLineFiveElementResult(
            int windowStartIndex,
            int windowEndIndex,
            int targetIndex,
            DateTime targetDateTime,
            FiveElement element,
            decimal lowerBound,
            decimal upperBound)
        {
            WindowStartIndex = windowStartIndex;
            WindowEndIndex = windowEndIndex;
            TargetIndex = targetIndex;
            TargetDateTime = targetDateTime;
            Element = element;
            LowerBound = lowerBound;
            UpperBound = upperBound;
        }

        public int WindowStartIndex { get; }

        public int WindowEndIndex { get; }

        public int TargetIndex { get; }

        public DateTime TargetDateTime { get; }

        public FiveElement Element { get; }

        public decimal LowerBound { get; }

        public decimal UpperBound { get; }

        public override string ToString()
        {
            return $"{TargetIndex} {TargetDateTime:yyyy-MM-dd HH:mm:ss} 五行={FiveElementDisplay.ToChineseName(Element)}({Element}), 窗口={WindowStartIndex}-{WindowEndIndex}, 下限={LowerBound:F8}, 上限={UpperBound:F8}";
        }
    }
}
