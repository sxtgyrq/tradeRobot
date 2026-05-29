using System.Reflection;
using System.Runtime.ExceptionServices;
using KChartRunWithFiveElements;

namespace TestFiveElement;

internal class HelperMethodCoverageTests
{
    [Test]
    public void FiveElementPrivateHelpers_ClassifyWindowAndRelationPairsDirectly()
    {
        IReadOnlyList<KLine> metalLines = SampleKLines.BuildMetalSample();
        decimal lowerBound = metalLines[0].OpenValue * 45m / 46m;
        decimal upperBound = metalLines[0].OpenValue * 46m / 45m;

        FiveElement classified = InvokePrivate<FiveElement>(
            typeof(FiveElementClassifier),
            "ClassifyWindow",
            metalLines,
            0,
            lowerBound,
            upperBound,
            metalLines[23].CloseValue);
        bool generating = InvokePrivate<bool>(
            typeof(FiveElementRelationCalculator),
            "IsGeneratingPair",
            FiveElement.Metal,
            FiveElement.Water);
        bool restraining = InvokePrivate<bool>(
            typeof(FiveElementRelationCalculator),
            "IsRestrainingPair",
            FiveElement.Metal,
            FiveElement.Wood);
        bool unrelatedGenerating = InvokePrivate<bool>(
            typeof(FiveElementRelationCalculator),
            "IsGeneratingPair",
            FiveElement.Metal,
            FiveElement.Wood);

        Assert.Multiple(() =>
        {
            Assert.That(classified, Is.EqualTo(FiveElement.Metal));
            Assert.That(generating, Is.True);
            Assert.That(restraining, Is.True);
            Assert.That(unrelatedGenerating, Is.False);
        });
    }

    [Test]
    public void KLineCsvReaderPrivateHelpers_ParseHeadersColumnsNumbersAndQuotedCsv()
    {
        string normalized = InvokePrivate<string>(
            typeof(KLineCsvReader),
            "NormalizeHeaderName",
            "\uFEFFOpen_Value");
        string[] split = InvokePrivate<string[]>(
            typeof(KLineCsvReader),
            "SplitCsvLine",
            "\"2026-01-01 00:00:00\",100,105,95,101,\"1,000\"");
        string unquoted = InvokePrivate<string>(
            typeof(KLineCsvReader),
            "Unquote",
            "\"a\"\"b\"");
        decimal parsed = InvokePrivate<decimal>(
            typeof(KLineCsvReader),
            "ParseDecimal",
            "12.5",
            7,
            "open");
        bool looksLikeHeader = InvokePrivate<bool>(
            typeof(KLineCsvReader),
            "LooksLikeHeader",
            (object)new[] { "dateTime", "open_value", "high" });
        Dictionary<string, int> headerMap = InvokePrivate<Dictionary<string, int>>(
            typeof(KLineCsvReader),
            "BuildHeaderMap",
            (object)new[] { "dateTime", "open", "high", "low", "close", "volume" });
        string closeColumn = InvokePrivate<string>(
            typeof(KLineCsvReader),
            "GetColumn",
            (object)new[] { "2026-01-01", "100", "105", "95", "101", "1" },
            headerMap,
            3,
            "close",
            4);
        KLine parsedLine = InvokePrivate<KLine>(
            typeof(KLineCsvReader),
            "ParseKLine",
            (object)new[] { "2026-01-01 00:00:00", "100", "105", "95", "101", "7" },
            null,
            1);

        Assert.Multiple(() =>
        {
            Assert.That(normalized, Is.EqualTo("openvalue"));
            Assert.That(split, Is.EqualTo(new[] { "2026-01-01 00:00:00", "100", "105", "95", "101", "1,000" }));
            Assert.That(unquoted, Is.EqualTo("a\"b"));
            Assert.That(parsed, Is.EqualTo(12.5m));
            Assert.That(looksLikeHeader, Is.True);
            Assert.That(headerMap["datetime"], Is.EqualTo(0));
            Assert.That(headerMap["close"], Is.EqualTo(4));
            Assert.That(closeColumn, Is.EqualTo("101"));
            Assert.That(parsedLine.CloseValue, Is.EqualTo(101m));
            Assert.Throws<InvalidOperationException>(() =>
                InvokePrivate<decimal>(typeof(KLineCsvReader), "ParseDecimal", "bad", 2, "open"));
            Assert.Throws<InvalidOperationException>(() =>
                InvokePrivate<string>(typeof(KLineCsvReader), "GetColumn", (object)new[] { "100" }, headerMap, 2, "close", 4));
        });
    }

    [Test]
    public void SampleKLinesPrivateBuilders_CreateExpectedWindowsForEveryElement()
    {
        IReadOnlyList<KLine> water = InvokePrivate<IReadOnlyList<KLine>>(typeof(SampleKLines), "BuildWaterSample");
        IReadOnlyList<KLine> earth = InvokePrivate<IReadOnlyList<KLine>>(typeof(SampleKLines), "BuildEarthSample");
        IReadOnlyList<KLine> fire = InvokePrivate<IReadOnlyList<KLine>>(typeof(SampleKLines), "BuildFireSample");
        IReadOnlyList<KLine> wood = InvokePrivate<IReadOnlyList<KLine>>(typeof(SampleKLines), "BuildWoodSample");
        List<KLine> baseWindow = InvokePrivate<List<KLine>>(typeof(SampleKLines), "BuildBaseWindow");
        KLine line = InvokePrivate<KLine>(typeof(SampleKLines), "CreateLine", 3, 100m, 105m, 95m, 101m);

        InvokePrivate<object?>(typeof(SampleKLines), "AddTargetLine", baseWindow);

        Assert.Multiple(() =>
        {
            Assert.That(FiveElementClassifier.ClassifyNext(water, 0).Element, Is.EqualTo(FiveElement.Water));
            Assert.That(FiveElementClassifier.ClassifyNext(earth, 0).Element, Is.EqualTo(FiveElement.Earth));
            Assert.That(FiveElementClassifier.ClassifyNext(fire, 0).Element, Is.EqualTo(FiveElement.Fire));
            Assert.That(FiveElementClassifier.ClassifyNext(wood, 0).Element, Is.EqualTo(FiveElement.Wood));
            Assert.That(baseWindow, Has.Count.EqualTo(25));
            Assert.That(line.DateTime, Is.EqualTo(new DateTime(2026, 1, 1, 3, 0, 0)));
            Assert.That(line.CloseValue, Is.EqualTo(101m));
        });
    }

    [Test]
    public void TradingPointAccount_UpdateTradingPointRefreshesBindingAndKeepsBalanceRules()
    {
        TradingPointAccount account = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 50, u: 20m);
        ConsoleMain.KChartTradingPointInfo replacement = TestHelpers.PurchaseTradingPoint(
            ownerCircleIndex: 1,
            tradingPointIndex: 9,
            cudaPointIndex: 99,
            pointCircleIndex: 2,
            pointIndex: 7);
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: -1m);

        account.UpdateTradingPoint(replacement);

        Assert.Multiple(() =>
        {
            Assert.That(account.TradingPoint, Is.SameAs(replacement));
            Assert.That(account.SatoshiBalance, Is.EqualTo(50));
            Assert.That(account.UBalance, Is.EqualTo(20m));
            Assert.Throws<ArgumentNullException>(() => account.UpdateTradingPoint(null!));
            Assert.Throws<InvalidOperationException>(() => harvest.UpdateTradingPoint(replacement));
        });
    }

    private static T InvokePrivate<T>(Type type, string methodName, params object?[] args)
    {
        MethodInfo[] methods = type
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(item => item.Name == methodName && item.GetParameters().Length == args.Length)
            .ToArray();

        if (methods.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one private static method named {methodName} with {args.Length} parameters on {type.Name}, found {methods.Length}.");
        }

        try
        {
            return (T)methods[0].Invoke(null, args)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
