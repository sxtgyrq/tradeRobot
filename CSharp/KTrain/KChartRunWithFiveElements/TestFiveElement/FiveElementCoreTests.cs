using KChartRunWithFiveElements;

namespace TestFiveElement;

internal class FiveElementCoreTests
{
    [Test]
    public void KLine_ConstructsValidLineAndKeepsValues()
    {
        DateTime time = new DateTime(2026, 1, 1, 8, 0, 0);
        KLine line = new KLine(time, 100m, 110m, 90m, 105m, 12.5m);

        Assert.Multiple(() =>
        {
            Assert.That(line.DateTime, Is.EqualTo(time));
            Assert.That(line.OpenValue, Is.EqualTo(100m));
            Assert.That(line.HighValue, Is.EqualTo(110m));
            Assert.That(line.LowValue, Is.EqualTo(90m));
            Assert.That(line.CloseValue, Is.EqualTo(105m));
            Assert.That(line.VolumeValue, Is.EqualTo(12.5m));
        });
    }

    [Test]
    public void KLine_RejectsInvalidPriceBoundaries()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TestHelpers.Line(open: 0m));
            Assert.Throws<ArgumentException>(() => TestHelpers.Line(open: 100m, high: 90m, low: 110m));
            Assert.Throws<ArgumentException>(() => TestHelpers.Line(open: 80m, high: 110m, low: 90m));
            Assert.Throws<ArgumentException>(() => TestHelpers.Line(open: 100m, high: 110m, low: 90m, close: 120m));
        });
    }

    [Test]
    public void FiveElementDisplay_ReturnsNameForEveryElementAndRejectsUnknown()
    {
        foreach (FiveElement element in Enum.GetValues<FiveElement>())
        {
            string name = FiveElementDisplay.ToChineseName(element);
            Assert.That(name, Is.Not.Null.And.Not.Empty);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FiveElementDisplay.ToChineseName((FiveElement)999));
    }

    [TestCase(FiveElement.Metal, FiveElement.Water, FiveElementRelation.Generating)]
    [TestCase(FiveElement.Water, FiveElement.Metal, FiveElementRelation.Generating)]
    [TestCase(FiveElement.Metal, FiveElement.Wood, FiveElementRelation.Restraining)]
    [TestCase(FiveElement.Wood, FiveElement.Metal, FiveElementRelation.Restraining)]
    [TestCase(FiveElement.Metal, FiveElement.Metal, FiveElementRelation.Unrelated)]
    public void FiveElementRelationCalculator_ReturnsExpectedRelation(
        FiveElement left,
        FiveElement right,
        FiveElementRelation expected)
    {
        Assert.That(FiveElementRelationCalculator.GetRelation(left, right), Is.EqualTo(expected));
    }

    [Test]
    public void FiveElementRelationCalculator_CoversAllGeneratingAndRestrainingPairs()
    {
        (FiveElement Left, FiveElement Right)[] generating =
        {
            (FiveElement.Metal, FiveElement.Water),
            (FiveElement.Water, FiveElement.Wood),
            (FiveElement.Wood, FiveElement.Fire),
            (FiveElement.Fire, FiveElement.Earth),
            (FiveElement.Earth, FiveElement.Metal)
        };
        (FiveElement Left, FiveElement Right)[] restraining =
        {
            (FiveElement.Metal, FiveElement.Wood),
            (FiveElement.Wood, FiveElement.Earth),
            (FiveElement.Earth, FiveElement.Water),
            (FiveElement.Water, FiveElement.Fire),
            (FiveElement.Fire, FiveElement.Metal)
        };

        foreach ((FiveElement left, FiveElement right) in generating)
        {
            Assert.Multiple(() =>
            {
                Assert.That(FiveElementRelationCalculator.GetRelation(left, right), Is.EqualTo(FiveElementRelation.Generating));
                Assert.That(FiveElementRelationCalculator.GetRelation(right, left), Is.EqualTo(FiveElementRelation.Generating));
            });
        }

        foreach ((FiveElement left, FiveElement right) in restraining)
        {
            Assert.Multiple(() =>
            {
                Assert.That(FiveElementRelationCalculator.GetRelation(left, right), Is.EqualTo(FiveElementRelation.Restraining));
                Assert.That(FiveElementRelationCalculator.GetRelation(right, left), Is.EqualTo(FiveElementRelation.Restraining));
            });
        }
    }

    [Test]
    public void SampleKLines_BuildAllSamplesClassifyToAllFiveElements()
    {
        IReadOnlyDictionary<string, IReadOnlyList<KLine>> samples = SampleKLines.BuildAllSamples();
        IReadOnlyList<FiveElement> elements = samples
            .Values
            .Select(lines => FiveElementClassifier.ClassifyNext(lines, 0).Element)
            .ToList();

        Assert.That(elements, Is.EquivalentTo(Enum.GetValues<FiveElement>()));
    }

    [TestCase(nameof(FiveElement.Metal), FiveElement.Metal)]
    [TestCase(nameof(FiveElement.Water), FiveElement.Water)]
    [TestCase(nameof(FiveElement.Earth), FiveElement.Earth)]
    [TestCase(nameof(FiveElement.Fire), FiveElement.Fire)]
    [TestCase(nameof(FiveElement.Wood), FiveElement.Wood)]
    public void FiveElementClassifier_ClassifiesEachRule(string elementName, FiveElement expected)
    {
        IReadOnlyList<KLine> lines = elementName switch
        {
            nameof(FiveElement.Metal) => BuildWindow(lastHigh: 104m, lastLow: 100m, lastClose: 103m),
            nameof(FiveElement.Water) => BuildWindow(lastHigh: 100m, lastLow: 96m, lastClose: 97m),
            nameof(FiveElement.Earth) => BuildWindow(),
            nameof(FiveElement.Fire) => BuildWindow(firstBreakHigh: 104m),
            nameof(FiveElement.Wood) => BuildWindow(firstBreakLow: 96m),
            _ => throw new ArgumentOutOfRangeException(nameof(elementName))
        };

        KLineFiveElementResult result = FiveElementClassifier.ClassifyNext(lines, 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.Element, Is.EqualTo(expected));
            Assert.That(result.WindowStartIndex, Is.EqualTo(0));
            Assert.That(result.WindowEndIndex, Is.EqualTo(23));
            Assert.That(result.TargetIndex, Is.EqualTo(24));
            Assert.That(result.LowerBound, Is.EqualTo(100m * 45m / 46m));
            Assert.That(result.UpperBound, Is.EqualTo(100m * 46m / 45m));
        });
    }

    [Test]
    public void FiveElementClassifier_FinalCloseBreakoutHasPriorityOverEarlierBreakthrough()
    {
        IReadOnlyList<KLine> metalLines = BuildWindow(
            firstBreakLow: 96m,
            lastHigh: 104m,
            lastLow: 100m,
            lastClose: 103m);
        IReadOnlyList<KLine> waterLines = BuildWindow(
            firstBreakHigh: 104m,
            lastHigh: 100m,
            lastLow: 96m,
            lastClose: 97m);

        KLineFiveElementResult metalResult = FiveElementClassifier.ClassifyNext(metalLines, 0);
        KLineFiveElementResult waterResult = FiveElementClassifier.ClassifyNext(waterLines, 0);

        Assert.Multiple(() =>
        {
            Assert.That(metalResult.Element, Is.EqualTo(FiveElement.Metal));
            Assert.That(waterResult.Element, Is.EqualTo(FiveElement.Water));
        });
    }

    [Test]
    public void FiveElementClassifier_ClassifyAllStartsAtTwentyFifthKLine()
    {
        List<KLine> lines = BuildWindow().ToList();
        lines.Add(TestHelpers.Line(hour: 25));

        IReadOnlyList<KLineFiveElementResult> results = FiveElementClassifier.ClassifyAll(lines);

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0].TargetIndex, Is.EqualTo(24));
            Assert.That(results[1].TargetIndex, Is.EqualTo(25));
        });
    }

    [Test]
    public void FiveElementClassifier_RejectsInvalidWindow()
    {
        IReadOnlyList<KLine> lines = BuildWindow();

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => FiveElementClassifier.ClassifyAll(null!));
            Assert.Throws<ArgumentNullException>(() => FiveElementClassifier.ClassifyNext(null!, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => FiveElementClassifier.ClassifyNext(lines, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => FiveElementClassifier.ClassifyNext(lines.Take(24).ToList(), 0));
        });
    }

    [Test]
    public void KLineFiveElementResult_ToStringContainsIndexesAndElement()
    {
        KLineFiveElementResult result = new KLineFiveElementResult(
            1,
            24,
            25,
            new DateTime(2026, 1, 2, 1, 0, 0),
            FiveElement.Fire,
            90m,
            110m);

        string text = result.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("25"));
            Assert.That(text, Does.Contain(nameof(FiveElement.Fire)));
            Assert.That(text, Does.Contain("1-24"));
        });
    }

    [Test]
    public void KLineCsvReader_ReadsHeaderlessAndHeaderCsv()
    {
        string tempDirectory = Directory.CreateTempSubdirectory("five-element-csv").FullName;
        try
        {
            string headerlessPath = Path.Combine(tempDirectory, "headerless.csv");
            File.WriteAllText(
                headerlessPath,
                "2026-01-01 00:00:00,100,110,90,105,12.5" + Environment.NewLine);

            string headerPath = Path.Combine(tempDirectory, "header.csv");
            File.WriteAllText(
                headerPath,
                "close,low,high,open,datetime,volume" + Environment.NewLine +
                "105,90,110,100,2026-01-01 01:00:00,13.5" + Environment.NewLine);

            IReadOnlyList<KLine> headerlessLines = KLineCsvReader.Read(headerlessPath);
            IReadOnlyList<KLine> headerLines = KLineCsvReader.Read(headerPath);

            Assert.Multiple(() =>
            {
                Assert.That(headerlessLines, Has.Count.EqualTo(1));
                Assert.That(headerlessLines[0].CloseValue, Is.EqualTo(105m));
                Assert.That(headerLines, Has.Count.EqualTo(1));
                Assert.That(headerLines[0].DateTime, Is.EqualTo(new DateTime(2026, 1, 1, 1, 0, 0)));
                Assert.That(headerLines[0].VolumeValue, Is.EqualTo(13.5m));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void KLineCsvReader_RejectsMissingFileInvalidDecimalAndMissingColumn()
    {
        string tempDirectory = Directory.CreateTempSubdirectory("five-element-csv-errors").FullName;
        try
        {
            string invalidDecimalPath = Path.Combine(tempDirectory, "invalid-decimal.csv");
            File.WriteAllText(
                invalidDecimalPath,
                "datetime,open,high,low,close" + Environment.NewLine +
                "2026-01-01 00:00:00,not-number,110,90,100" + Environment.NewLine);

            string missingColumnPath = Path.Combine(tempDirectory, "missing-column.csv");
            File.WriteAllText(
                missingColumnPath,
                "datetime,open,high,low" + Environment.NewLine +
                "2026-01-01 00:00:00,100,110,90" + Environment.NewLine);

            Assert.Multiple(() =>
            {
                Assert.Throws<FileNotFoundException>(() => KLineCsvReader.Read(Path.Combine(tempDirectory, "missing.csv")));
                Assert.Throws<InvalidOperationException>(() => KLineCsvReader.Read(invalidDecimalPath));
                Assert.Throws<InvalidOperationException>(() => KLineCsvReader.Read(missingColumnPath));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static IReadOnlyList<KLine> BuildWindow(
        decimal? firstBreakHigh = null,
        decimal? firstBreakLow = null,
        decimal? lastHigh = null,
        decimal? lastLow = null,
        decimal? lastClose = null)
    {
        List<KLine> lines = new List<KLine>();
        for (int index = 0; index < FiveElementClassifier.WindowSize; index++)
        {
            lines.Add(TestHelpers.Line(hour: index));
        }

        if (firstBreakHigh is not null)
        {
            lines[5] = TestHelpers.Line(high: firstBreakHigh.Value, hour: 5);
        }

        if (firstBreakLow is not null)
        {
            lines[5] = TestHelpers.Line(low: firstBreakLow.Value, hour: 5);
        }

        if (lastHigh is not null || lastLow is not null || lastClose is not null)
        {
            lines[23] = TestHelpers.Line(
                high: lastHigh ?? 101m,
                low: lastLow ?? 99m,
                close: lastClose ?? 100m,
                hour: 23);
        }

        lines.Add(TestHelpers.Line(hour: FiveElementClassifier.WindowSize));
        return lines;
    }
}
