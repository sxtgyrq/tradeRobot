using System.Globalization;

namespace KChartRunWithFiveElements
{
    internal static class KLineCsvReader
    {
        public static IReadOnlyList<KLine> Read(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("CSV 文件不存在。", fullPath);
            }

            string[] lines = File.ReadAllLines(fullPath);
            List<KLine> result = new List<KLine>();
            Dictionary<string, int>? headerMap = null;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string rawLine = lines[lineIndex].Trim().TrimStart('\uFEFF');
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                string[] columns = SplitCsvLine(rawLine);
                if (headerMap is null && LooksLikeHeader(columns))
                {
                    headerMap = BuildHeaderMap(columns);
                    continue;
                }

                result.Add(ParseKLine(columns, headerMap, lineIndex + 1));
            }

            return result;
        }

        private static KLine ParseKLine(
            string[] columns,
            IReadOnlyDictionary<string, int>? headerMap,
            int lineNumber)
        {
            DateTime dateTime = DateTime.Parse(GetColumn(columns, headerMap, lineNumber, "datetime", 0), CultureInfo.InvariantCulture);
            decimal openValue = ParseDecimal(GetColumn(columns, headerMap, lineNumber, "open", 1), lineNumber, "open");
            decimal highValue = ParseDecimal(GetColumn(columns, headerMap, lineNumber, "high", 2), lineNumber, "high");
            decimal lowValue = ParseDecimal(GetColumn(columns, headerMap, lineNumber, "low", 3), lineNumber, "low");
            decimal closeValue = ParseDecimal(GetColumn(columns, headerMap, lineNumber, "close", 4), lineNumber, "close");
            decimal volumeValue = columns.Length > 5
                ? ParseDecimal(GetColumn(columns, headerMap, lineNumber, "volume", 5), lineNumber, "volume")
                : 0m;

            return new KLine(dateTime, openValue, highValue, lowValue, closeValue, volumeValue);
        }

        private static string GetColumn(
            string[] columns,
            IReadOnlyDictionary<string, int>? headerMap,
            int lineNumber,
            string fieldName,
            int fallbackIndex)
        {
            int index = fallbackIndex;
            if (headerMap is not null)
            {
                if (!headerMap.TryGetValue(fieldName, out index))
                {
                    throw new InvalidOperationException($"CSV 第 {lineNumber} 行缺少字段 {fieldName}。");
                }
            }

            if (index < 0 || index >= columns.Length)
            {
                throw new InvalidOperationException($"CSV 第 {lineNumber} 行字段数量不足，缺少 {fieldName}。");
            }

            return columns[index].Trim();
        }

        private static decimal ParseDecimal(string value, int lineNumber, string fieldName)
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal result))
            {
                throw new InvalidOperationException($"CSV 第 {lineNumber} 行字段 {fieldName} 不是有效数字：{value}");
            }

            return result;
        }

        private static bool LooksLikeHeader(IReadOnlyList<string> columns)
        {
            return columns.Any(column => NormalizeHeaderName(column) is "open" or "openvalue");
        }

        private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> columns)
        {
            Dictionary<string, int> headerMap = new Dictionary<string, int>();
            for (int index = 0; index < columns.Count; index++)
            {
                string normalizedName = NormalizeHeaderName(columns[index]);
                string? fieldName = normalizedName switch
                {
                    "datetime" or "time" or "date" => "datetime",
                    "open" or "openvalue" => "open",
                    "high" or "highvalue" => "high",
                    "low" or "lowvalue" => "low",
                    "close" or "closevalue" => "close",
                    "volume" or "volumevalue" => "volume",
                    _ => null
                };

                if (fieldName is not null)
                {
                    headerMap[fieldName] = index;
                }
            }

            return headerMap;
        }

        private static string NormalizeHeaderName(string value)
        {
            return value.Trim().TrimStart('\uFEFF').Replace("_", string.Empty).ToLowerInvariant();
        }

        private static string[] SplitCsvLine(string line)
        {
            List<string> columns = new List<string>();
            bool inQuote = false;
            int columnStart = 0;

            for (int index = 0; index < line.Length; index++)
            {
                if (line[index] == '"')
                {
                    inQuote = !inQuote;
                    continue;
                }

                if (line[index] == ',' && !inQuote)
                {
                    columns.Add(Unquote(line[columnStart..index]));
                    columnStart = index + 1;
                }
            }

            columns.Add(Unquote(line[columnStart..]));
            return columns.ToArray();
        }

        private static string Unquote(string value)
        {
            string trimmed = value.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            {
                return trimmed[1..^1].Replace("\"\"", "\"");
            }

            return trimmed;
        }
    }
}
