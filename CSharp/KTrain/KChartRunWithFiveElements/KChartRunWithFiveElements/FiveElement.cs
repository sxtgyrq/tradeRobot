namespace KChartRunWithFiveElements
{
    internal enum FiveElement
    {
        Metal,
        Wood,
        Water,
        Fire,
        Earth
    }

    internal static class FiveElementDisplay
    {
        public static string ToChineseName(FiveElement element)
        {
            return element switch
            {
                FiveElement.Metal => "金",
                FiveElement.Wood => "木",
                FiveElement.Water => "水",
                FiveElement.Fire => "火",
                FiveElement.Earth => "土",
                _ => throw new ArgumentOutOfRangeException(nameof(element), element, "未知五行。")
            };
        }
    }
}
