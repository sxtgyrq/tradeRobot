namespace KChartRunWithFiveElements
{
    internal sealed class KLine
    {
        public KLine(
            DateTime dateTime,
            decimal openValue,
            decimal highValue,
            decimal lowValue,
            decimal closeValue,
            decimal volumeValue)
        {
            if (openValue <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(openValue), "开盘价必须大于 0。");
            }

            if (highValue < lowValue)
            {
                throw new ArgumentException("最高价不能小于最低价。");
            }

            if (openValue < lowValue || openValue > highValue)
            {
                throw new ArgumentException("开盘价必须位于最低价和最高价之间。");
            }

            if (closeValue < lowValue || closeValue > highValue)
            {
                throw new ArgumentException("收盘价必须位于最低价和最高价之间。");
            }

            DateTime = dateTime;
            OpenValue = openValue;
            HighValue = highValue;
            LowValue = lowValue;
            CloseValue = closeValue;
            VolumeValue = volumeValue;
        }

        public DateTime DateTime { get; }

        public decimal OpenValue { get; }

        public decimal HighValue { get; }

        public decimal LowValue { get; }

        public decimal CloseValue { get; }

        public decimal VolumeValue { get; }
    }
}
