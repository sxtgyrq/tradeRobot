using CommonClass;
using ConsoleMain;
using KChartRunWithFiveElements;

namespace TestFiveElement;

internal class ProgramBusinessTests
{
    [Test]
    public void CreateTradingPointAccounts_CreatesHarvestAndPurchaseInitialBalances()
    {
        IReadOnlyList<KChartTradingPointInfo> tradingPoints =
        [
            TestHelpers.HarvestTradingPoint(tradingPointIndex: 0, cudaPointIndex: 0),
            TestHelpers.PurchaseTradingPoint(ownerCircleIndex: 1, tradingPointIndex: 1, cudaPointIndex: 1),
        ];

        List<TradingPointAccount> accounts = TestHelpers.InvokeProgram<List<TradingPointAccount>>(
            "CreateTradingPointAccounts",
            tradingPoints);

        Assert.Multiple(() =>
        {
            Assert.That(accounts, Has.Count.EqualTo(2));
            Assert.That(accounts[0].PointKind, Is.EqualTo(KChartTradingPointKind.Harvest));
            Assert.That(accounts[0].SatoshiBalance, Is.EqualTo(210_000_000_000L));
            Assert.That(accounts[0].UBalance, Is.EqualTo(1000m));
            Assert.That(accounts[1].PointKind, Is.EqualTo(KChartTradingPointKind.Purchase));
            Assert.That(accounts[1].SatoshiBalance, Is.EqualTo(0L));
            Assert.That(accounts[1].UBalance, Is.EqualTo(1000m));
        });
    }

    [Test]
    public void BuildCircleAccountMap_MapsPurchaseOwnerCircleAndRejectsInvalidOrDuplicate()
    {
        TradingPointAccount first = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        TradingPointAccount second = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, tradingPointIndex: 2, cudaPointIndex: 2);

        Dictionary<int, TradingPointAccount> map = TestHelpers.InvokeProgram<Dictionary<int, TradingPointAccount>>(
            "BuildCircleAccountMap",
            new List<TradingPointAccount> { first, second });

        Assert.Multiple(() =>
        {
            Assert.That(map[1], Is.SameAs(first));
            Assert.That(map[2], Is.SameAs(second));
            Assert.Throws<InvalidOperationException>(() =>
                TestHelpers.InvokeProgram("BuildCircleAccountMap", new List<TradingPointAccount>
                {
                    first,
                    TestHelpers.PurchaseAccount(ownerCircleIndex: 1, tradingPointIndex: 3, cudaPointIndex: 3)
                }));
            Assert.Throws<InvalidOperationException>(() =>
                TestHelpers.InvokeProgram("BuildCircleAccountMap", new List<TradingPointAccount>
                {
                    TestHelpers.PurchaseAccount(ownerCircleIndex: -1)
                }));
        });
    }

    [Test]
    public void CalculatePathEffectMultiplier_FollowsFiveElementRelation()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TestHelpers.InvokeProgram<decimal>("CalculatePathEffectMultiplier", FiveElement.Metal, FiveElement.Water), Is.EqualTo(2m));
            Assert.That(TestHelpers.InvokeProgram<decimal>("CalculatePathEffectMultiplier", FiveElement.Metal, FiveElement.Wood), Is.EqualTo(0.5m));
            Assert.That(TestHelpers.InvokeProgram<decimal>("CalculatePathEffectMultiplier", FiveElement.Metal, FiveElement.Metal), Is.EqualTo(1m));
        });
    }

    [Test]
    public void ValidateCircleId_RequiresTwentyFourLowerHexCharacters()
    {
        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => TestHelpers.InvokeProgram("ValidateCircleId", "00000000000000000000000a", "circleId"));
            Assert.Throws<ArgumentException>(() => TestHelpers.InvokeProgram("ValidateCircleId", "", "circleId"));
            Assert.Throws<ArgumentException>(() => TestHelpers.InvokeProgram("ValidateCircleId", "abc", "circleId"));
            Assert.Throws<ArgumentException>(() => TestHelpers.InvokeProgram("ValidateCircleId", "00000000000000000000000A", "circleId"));
            Assert.Throws<ArgumentException>(() => TestHelpers.InvokeProgram("ValidateCircleId", "00000000000000000000000g", "circleId"));
        });
    }

    [Test]
    public void PriceAndSatoshiConversionMethods_AreConsistent()
    {
        decimal satoshiValue = TestHelpers.InvokeProgram<decimal>("ConvertUToSatoshiValue", 200m, 100m);
        decimal uAmount = TestHelpers.InvokeProgram<decimal>("ConvertSatoshiValueToU", satoshiValue, 100m);
        decimal uFromLong = TestHelpers.InvokeProgram<decimal>("ConvertSatoshiToU", 100_000_000L, 100m);
        long floor = TestHelpers.InvokeProgram<long>("FloorToSatoshi", 123.999m);

        Assert.Multiple(() =>
        {
            Assert.That(satoshiValue, Is.EqualTo(200_000_000m));
            Assert.That(uAmount, Is.EqualTo(200m));
            Assert.That(uFromLong, Is.EqualTo(100m));
            Assert.That(floor, Is.EqualTo(123L));
            Assert.Throws<OverflowException>(() => TestHelpers.InvokeProgram("FloorToSatoshi", decimal.MaxValue));
        });
    }

    [Test]
    public void ContractPriceMethods_CalculateTakeProfitAndLiquidation()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TestHelpers.InvokeProgram<decimal>("CalculateTakeProfitPrice", 100m, ContractDirection.Long), Is.EqualTo(100m * 46m / 45m));
            Assert.That(TestHelpers.InvokeProgram<decimal>("CalculateTakeProfitPrice", 100m, ContractDirection.Short), Is.EqualTo(100m * 45m / 46m));
            Assert.That(TestHelpers.InvokeProgram<decimal>("CalculateLiquidationPrice", 100m, ContractDirection.Long, 2m), Is.EqualTo(50m));
            Assert.That(TestHelpers.InvokeProgram<decimal>("CalculateLiquidationPrice", 100m, ContractDirection.Short, 2m), Is.EqualTo(150m));
        });
    }

    [Test]
    public void ArcMethods_CalculateForwardAndWrappedSteps()
    {
        KChartRoutePointInfo point10 = TestHelpers.RoutePoint(0, 1, 10);
        KChartRoutePointInfo point40 = TestHelpers.RoutePoint(1, 1, 40);
        KChartRoutePointInfo point5 = TestHelpers.RoutePoint(2, 1, 5);
        KChartRoutePointInfo otherCircle = TestHelpers.RoutePoint(3, 2, 50);
        KChartRoutePointInfo differentCount = TestHelpers.RoutePoint(4, 1, 60, pathPointCount: 200);

        long normal = TestHelpers.InvokeProgram<long>("CalculateForwardArcStep", point10, point40);
        long wrapped = TestHelpers.InvokeProgram<long>("CalculateForwardArcStep", point40, point5);
        long segment = TestHelpers.InvokeProgram<long>(
            "CalculateArcStepCount",
            new List<KChartRoutePointInfo> { point10, point40, point5 },
            0,
            2);
        decimal radian = TestHelpers.InvokeProgram<decimal>("CalculateArcRadian", 50L, 100);

        Assert.Multiple(() =>
        {
            Assert.That(normal, Is.EqualTo(30));
            Assert.That(wrapped, Is.EqualTo(65));
            Assert.That(segment, Is.EqualTo(95));
            Assert.That(radian, Is.EqualTo(6.2831853071795864769252867666m / 2m));
            Assert.Throws<InvalidOperationException>(() => TestHelpers.InvokeProgram("CalculateForwardArcStep", point10, otherCircle));
            Assert.Throws<InvalidOperationException>(() => TestHelpers.InvokeProgram("CalculateForwardArcStep", point10, differentCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestHelpers.InvokeProgram("CalculateArcRadian", 0L, 100));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestHelpers.InvokeProgram("CalculateArcRadian", 1L, 0));
        });
    }

    [Test]
    public void AddContractOrdersFromRoute_CreatesOneOrderPerContinuousCircleSegment()
    {
        TradingPointAccount source = TestHelpers.HarvestAccount();
        TradingPointAccount target = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        TradingPointAccount circle0 = TestHelpers.PurchaseAccount(ownerCircleIndex: 0, satoshi: 0, u: 1000m, tradingPointIndex: 10, cudaPointIndex: 10);
        TradingPointAccount circle1 = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 1000m, tradingPointIndex: 11, cudaPointIndex: 11);
        Dictionary<int, TradingPointAccount> circleAccounts = new Dictionary<int, TradingPointAccount>
        {
            [0] = circle0,
            [1] = circle1
        };
        KChartRouteInfo route = TestHelpers.Route(
            TestHelpers.RoutePoint(0, 0, 90, signedRadius: 100),
            TestHelpers.RoutePoint(1, 0, 10, signedRadius: 100),
            TestHelpers.RoutePoint(2, 1, 20, signedRadius: -100),
            TestHelpers.RoutePoint(3, 1, 30, signedRadius: -100),
            TestHelpers.RoutePoint(4, 0, 40, signedRadius: 100),
            TestHelpers.RoutePoint(5, 0, 50, signedRadius: 100));
        List<ContractOrder> market = new List<ContractOrder>();

        RouteContractOrderResult result = TestHelpers.InvokeProgram<RouteContractOrderResult>(
            "AddContractOrdersFromRoute",
            source,
            target,
            route,
            circleAccounts,
            source,
            24,
            FiveElement.Fire,
            100m,
            1m,
            market);

        Assert.Multiple(() =>
        {
            Assert.That(result.Orders, Has.Count.EqualTo(3));
            Assert.That(market, Has.Count.EqualTo(3));
            Assert.That(result.Orders[0].OwnerCircleIndex, Is.EqualTo(0));
            Assert.That(result.Orders[0].ArcStepCount, Is.EqualTo(20));
            Assert.That(result.Orders[0].Direction, Is.EqualTo(ContractDirection.Long));
            Assert.That(result.Orders[1].OwnerCircleIndex, Is.EqualTo(1));
            Assert.That(result.Orders[1].Direction, Is.EqualTo(ContractDirection.Short));
            Assert.That(result.Orders[2].OwnerCircleIndex, Is.EqualTo(0));
            Assert.That(circle0.ContractOrders, Has.Count.EqualTo(2));
            Assert.That(circle1.ContractOrders, Has.Count.EqualTo(1));
            Assert.That(source.UBalance, Is.GreaterThan(1000m));
            Assert.That(result.Orders.All(item => item.AvailableFromKLineIndex == 25), Is.True);
        });
    }

    [Test]
    public void AddContractOrdersFromRoute_WithSatoshiMarginKeepsSatoshiTotalUnchanged()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 1000, u: 0m);
        TradingPointAccount target = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        TradingPointAccount circle0 = TestHelpers.PurchaseAccount(ownerCircleIndex: 0, satoshi: 1000, u: 0m, tradingPointIndex: 10, cudaPointIndex: 10);
        Dictionary<int, TradingPointAccount> circleAccounts = new Dictionary<int, TradingPointAccount>
        {
            [0] = circle0
        };
        KChartRouteInfo route = TestHelpers.Route(
            TestHelpers.RoutePoint(0, 0, 0, signedRadius: 100),
            TestHelpers.RoutePoint(1, 0, 50, signedRadius: 100));
        List<TradingPointAccount> accounts = new List<TradingPointAccount> { harvest, target, circle0 };
        List<ContractOrder> market = new List<ContractOrder>();
        long before = TestHelpers.InvokeProgram<long>("CalculateTrackedSatoshiTotal", accounts);

        RouteContractOrderResult result = TestHelpers.InvokeProgram<RouteContractOrderResult>(
            "AddContractOrdersFromRoute",
            harvest,
            target,
            route,
            circleAccounts,
            harvest,
            24,
            FiveElement.Fire,
            100m,
            1m,
            market);
        long after = TestHelpers.InvokeProgram<long>("CalculateTrackedSatoshiTotal", accounts);

        Assert.Multiple(() =>
        {
            Assert.That(result.Orders, Has.Count.EqualTo(1));
            Assert.That(result.Orders[0].MarginAsset, Is.EqualTo(ContractMarginAsset.Satoshi));
            Assert.That(after, Is.EqualTo(before));
            Assert.That(harvest.SatoshiBalance, Is.GreaterThan(1000));
            Assert.That(circle0.SatoshiBalance, Is.LessThan(1000));
        });
    }

    [Test]
    public void AddContractOrdersFromRoute_RejectsMissingCircleAndInvalidMultiplier()
    {
        TradingPointAccount source = TestHelpers.HarvestAccount();
        TradingPointAccount target = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        KChartRouteInfo route = TestHelpers.Route(
            TestHelpers.RoutePoint(0, 0, 0),
            TestHelpers.RoutePoint(1, 0, 10));

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TestHelpers.InvokeProgram(
                "AddContractOrdersFromRoute",
                source,
                target,
                route,
                new Dictionary<int, TradingPointAccount>(),
                source,
                24,
                FiveElement.Fire,
                100m,
                0m,
                new List<ContractOrder>()));

            Assert.Throws<InvalidOperationException>(() => TestHelpers.InvokeProgram(
                "AddContractOrdersFromRoute",
                source,
                target,
                route,
                new Dictionary<int, TradingPointAccount>(),
                source,
                24,
                FiveElement.Fire,
                100m,
                1m,
                new List<ContractOrder>()));
        });
    }

    [Test]
    public void SpotOrderCreation_UsesDeviationAndLocksBalances()
    {
        TradingPointAccount satoshiHeavy = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 100_000_000L, u: 0m);
        TradingPointAccount uHeavy = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 0L, u: 1000m, tradingPointIndex: 2, cudaPointIndex: 2);
        KLine flatLine = TestHelpers.Line(open: 100m, high: 100m, low: 100m, close: 100m);

        SpotOrder? sellOrder = TestHelpers.InvokeProgram<SpotOrder?>(
            "TryCreateSpotOrderFromAssetDeviation",
            satoshiHeavy,
            24,
            flatLine,
            FiveElement.Metal);
        SpotOrder? buyOrder = TestHelpers.InvokeProgram<SpotOrder?>(
            "TryCreateSpotOrderFromAssetDeviation",
            uHeavy,
            24,
            flatLine,
            FiveElement.Water);

        Assert.Multiple(() =>
        {
            Assert.That(sellOrder, Is.Not.Null);
            Assert.That(sellOrder!.Side, Is.EqualTo(SpotOrderSide.SellSatoshi));
            Assert.That(sellOrder.AvailableFromKLineIndex, Is.EqualTo(25));
            Assert.That(sellOrder.FiveElement, Is.EqualTo(FiveElement.Metal));
            Assert.That(satoshiHeavy.SatoshiBalance, Is.LessThan(100_000_000L));

            Assert.That(buyOrder, Is.Not.Null);
            Assert.That(buyOrder!.Side, Is.EqualTo(SpotOrderSide.BuySatoshi));
            Assert.That(buyOrder.AvailableFromKLineIndex, Is.EqualTo(25));
            Assert.That(buyOrder.FiveElement, Is.EqualTo(FiveElement.Water));
            Assert.That(uHeavy.UBalance, Is.LessThan(1000m));
        });
    }

    [Test]
    public void SpotMarket_CreatesOrdersWithoutExecutingCurrentHourOrders()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 210_000_000_000L, u: 1000m);
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 100_000_000L, u: 0m);
        List<TradingPointAccount> accounts = new List<TradingPointAccount> { harvest, purchase };
        List<SpotOrder> spotMarket = new List<SpotOrder>();
        List<ContractOrder> contractMarket = new List<ContractOrder>();

        SpotMarketResult result = TestHelpers.InvokeProgram<SpotMarketResult>(
            "SpotMarket",
            24,
            TestHelpers.Line(open: 100m, high: 100m, low: 100m, close: 100m),
            FiveElement.Earth,
            accounts,
            spotMarket,
            contractMarket);

        Assert.Multiple(() =>
        {
            Assert.That(result.CreatedOrders, Is.Not.Empty);
            Assert.That(spotMarket, Has.Count.EqualTo(result.CreatedOrders.Count));
            Assert.That(result.Trades, Is.Empty);
            Assert.That(result.RouteContractOrderResults, Is.Empty);
            Assert.That(result.CreatedOrders.All(item => item.AvailableFromKLineIndex == 25), Is.True);
        });
    }

    [Test]
    public void MatchingAndMarketEmptyBranches_DoNotNeedRouteFiles()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount();
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        List<TradingPointAccount> accounts = new List<TradingPointAccount> { harvest, purchase };
        List<SpotOrder> spotMarket = new List<SpotOrder>();
        List<ContractOrder> contractMarket = new List<ContractOrder>();
        Dictionary<int, TradingPointAccount> circleAccounts = new Dictionary<int, TradingPointAccount> { [1] = purchase };
        List<RouteContractOrderResult> routeResults = new List<RouteContractOrderResult>();

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => TestHelpers.InvokeProgram("MatchingTrade", 24, TestHelpers.Line(), FiveElement.Metal, accounts, spotMarket, contractMarket));
            Assert.That(TestHelpers.InvokeProgram<List<SpotTrade>>("MatchMatureSpotOrders", 24, TestHelpers.Line(), FiveElement.Metal, harvest, circleAccounts, spotMarket, contractMarket, routeResults), Is.Empty);
            Assert.That(TestHelpers.InvokeProgram<int>("MatchMatureContractOrders", 24, TestHelpers.Line(), FiveElement.Metal, harvest, circleAccounts, contractMarket, routeResults), Is.EqualTo(0));
            Assert.That(TestHelpers.InvokeProgram<int>("SettleLiquidatedContractOrders", 24, TestHelpers.Line(), FiveElement.Metal, harvest, circleAccounts, contractMarket, routeResults), Is.EqualTo(0));
        });
    }

    [Test]
    public void ContractKLineResult_HandlesTakeProfitLiquidationAndBothTouched()
    {
        TradingPointAccount owner = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        ContractOrder longOrder = TestHelpers.ContractOrder(owner, ContractDirection.Long, price: 100m);
        ContractOrder shortOrder = TestHelpers.ContractOrder(owner, ContractDirection.Short, price: 100m);

        object longTakeProfit = TestHelpers.InvokeProgram("GetContractKLineResult", longOrder, TestHelpers.Line(open: 100m, high: 111m, low: 90m, close: 105m))!;
        object longLiquidation = TestHelpers.InvokeProgram("GetContractKLineResult", longOrder, TestHelpers.Line(open: 100m, high: 105m, low: 49m, close: 90m))!;
        object shortTakeProfit = TestHelpers.InvokeProgram("GetContractKLineResult", shortOrder, TestHelpers.Line(open: 100m, high: 120m, low: 89m, close: 95m))!;
        object none = TestHelpers.InvokeProgram("GetContractKLineResult", longOrder, TestHelpers.Line(open: 100m, high: 109m, low: 90m, close: 100m))!;

        Assert.Multiple(() =>
        {
            Assert.That(longTakeProfit.ToString(), Is.EqualTo("TakeProfit"));
            Assert.That(longLiquidation.ToString(), Is.EqualTo("Liquidation"));
            Assert.That(shortTakeProfit.ToString(), Is.EqualTo("TakeProfit"));
            Assert.That(none.ToString(), Is.EqualTo("None"));
            Assert.That(TestHelpers.InvokeProgram<bool>("IsContractTakeProfitTouched", longOrder, TestHelpers.Line(high: 111m)), Is.True);
            Assert.That(TestHelpers.InvokeProgram<bool>("IsContractLiquidationTouched", longOrder, TestHelpers.Line(low: 49m)), Is.True);
            Assert.That(TestHelpers.InvokeProgram<bool>("ShouldPreferTakeProfitWhenBothTouched", longOrder, TestHelpers.Line(open: 100m, high: 111m, low: 49m, close: 101m)), Is.True);
            Assert.That(TestHelpers.InvokeProgram<bool>("ShouldPreferTakeProfitWhenBothTouched", shortOrder, TestHelpers.Line(open: 100m, high: 151m, low: 89m, close: 99m)), Is.True);
        });
    }

    [Test]
    public void SettleLiquidatedContractOrders_ClosesOrderWithoutRouteEffect()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: 10m);
        TradingPointAccount owner = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 0m);
        ContractOrder order = TestHelpers.ContractOrder(owner, ContractDirection.Long, ContractMarginAsset.U, marginAmount: 10m, price: 100m);
        List<ContractOrder> contractMarket = new List<ContractOrder> { order };
        List<RouteContractOrderResult> routeResults = new List<RouteContractOrderResult>();
        Dictionary<int, TradingPointAccount> circleAccounts = new Dictionary<int, TradingPointAccount>
        {
            [1] = owner
        };

        int liquidationCount = TestHelpers.InvokeProgram<int>(
            "SettleLiquidatedContractOrders",
            24,
            TestHelpers.Line(open: 100m, high: 105m, low: 49m, close: 90m),
            FiveElement.Metal,
            harvest,
            circleAccounts,
            contractMarket,
            routeResults);

        Assert.Multiple(() =>
        {
            Assert.That(liquidationCount, Is.EqualTo(1));
            Assert.That(order.IsFilled, Is.True);
            Assert.That(routeResults, Is.Empty);
            Assert.That(harvest.UBalance, Is.EqualTo(10m));
        });
    }

    [Test]
    public void ContractSettlementMethods_ReturnMarginProfitAndOwnerAccount()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: 1010m);
        TradingPointAccount owner = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 0m);
        ContractOrder order = TestHelpers.ContractOrder(owner, ContractDirection.Long, ContractMarginAsset.U, marginAmount: 10m, price: 100m, leverage: 2m);
        Dictionary<int, TradingPointAccount> circleAccounts = new Dictionary<int, TradingPointAccount> { [1] = owner };

        decimal margin = TestHelpers.InvokeProgram<decimal>("CalculateRemainingContractMargin", order);
        decimal marginValue = TestHelpers.InvokeProgram<decimal>("GetRemainingContractMarginValueInSatoshi", order, 100m);
        decimal contractValue = TestHelpers.InvokeProgram<decimal>("GetRemainingContractValueInSatoshi", order, 100m);
        decimal nominalFromSatoshi = TestHelpers.InvokeProgram<decimal>("CalculateContractNominalPositionBySatoshiValue", order, 5_000_000m, 100m);
        TradingPointAccount foundOwner = TestHelpers.InvokeProgram<TradingPointAccount>("GetContractOwnerAccount", order, circleAccounts);

        TestHelpers.InvokeProgram("SettleContractTakeProfit", order, owner, harvest, order.RemainingNominalPosition);

        TradingPointAccount satoshiHarvest = TestHelpers.HarvestAccount(satoshi: 1000, u: 0m);
        TradingPointAccount satoshiOwner = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 0, u: 0m, tradingPointIndex: 2, cudaPointIndex: 2);
        ContractOrder satoshiOrder = TestHelpers.ContractOrder(
            satoshiOwner,
            ContractDirection.Long,
            ContractMarginAsset.Satoshi,
            marginAmount: 100m,
            price: 100m,
            leverage: 2m);
        object? satoshiPayout = TestHelpers.InvokeProgram(
            "SettleContractTakeProfit",
            satoshiOrder,
            satoshiOwner,
            satoshiHarvest,
            satoshiOrder.RemainingNominalPosition);
        TradingPointAccount poorHarvest = TestHelpers.HarvestAccount(satoshi: 50, u: 0m);
        TradingPointAccount poorOwner = TestHelpers.PurchaseAccount(ownerCircleIndex: 3, satoshi: 0, u: 0m, tradingPointIndex: 3, cudaPointIndex: 3);
        ContractOrder poorSatoshiOrder = TestHelpers.ContractOrder(
            poorOwner,
            ContractDirection.Long,
            ContractMarginAsset.Satoshi,
            marginAmount: 100m,
            price: 100m,
            leverage: 2m);

        Assert.Multiple(() =>
        {
            Assert.That(margin, Is.EqualTo(10m));
            Assert.That(marginValue, Is.EqualTo(10_000_000m));
            Assert.That(contractValue, Is.EqualTo(20_000_000m));
            Assert.That(nominalFromSatoshi, Is.EqualTo(5m));
            Assert.That(foundOwner, Is.SameAs(owner));
            Assert.That(owner.UBalance, Is.EqualTo(12m));
            Assert.That(harvest.UBalance, Is.EqualTo(998m));
            Assert.That(order.IsFilled, Is.True);
            Assert.That(satoshiPayout, Is.Not.Null);
            Assert.That(TestHelpers.GetProperty<TradingPointAccount>(satoshiPayout!, "SourceAccount"), Is.SameAs(satoshiHarvest));
            Assert.That(TestHelpers.GetProperty<TradingPointAccount>(satoshiPayout!, "TargetAccount"), Is.SameAs(satoshiOwner));
            Assert.That(TestHelpers.GetProperty<long>(satoshiPayout!, "SatoshiAmount"), Is.EqualTo(120));
            Assert.That(satoshiHarvest.SatoshiBalance, Is.EqualTo(880));
            Assert.That(satoshiOwner.SatoshiBalance, Is.EqualTo(120));
            Assert.Throws<InvalidOperationException>(() => TestHelpers.InvokeProgram(
                "SettleContractTakeProfit",
                poorSatoshiOrder,
                poorOwner,
                poorHarvest,
                poorSatoshiOrder.RemainingNominalPosition));
            Assert.Throws<InvalidOperationException>(() => TestHelpers.InvokeProgram("GetContractOwnerAccount", order, new Dictionary<int, TradingPointAccount>()));
        });
    }

    [Test]
    public void TryExecuteContractTrade_WhenHarvestSatoshiIsInsufficient_DoesNotPartiallySettle()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 200, u: 0m);
        TradingPointAccount longOwner = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 0m);
        TradingPointAccount shortOwner = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 0, u: 0m, tradingPointIndex: 2, cudaPointIndex: 2);
        ContractOrder longOrder = TestHelpers.ContractOrder(
            longOwner,
            ContractDirection.Long,
            ContractMarginAsset.Satoshi,
            marginAmount: 100m,
            price: 100m,
            leverage: 2m);
        ContractOrder shortOrder = TestHelpers.ContractOrder(
            shortOwner,
            ContractDirection.Short,
            ContractMarginAsset.Satoshi,
            marginAmount: 100m,
            price: 100m,
            leverage: 2m);
        Dictionary<int, TradingPointAccount> circleAccounts = new Dictionary<int, TradingPointAccount>
        {
            [1] = longOwner,
            [2] = shortOwner
        };
        List<ContractOrder> contractMarket = new List<ContractOrder> { longOrder, shortOrder };
        List<RouteContractOrderResult> routeResults = new List<RouteContractOrderResult>();

        Assert.Throws<InvalidOperationException>(() => TestHelpers.InvokeProgram(
            "TryExecuteContractTrade",
            longOrder,
            shortOrder,
            110m,
            24,
            FiveElement.Metal,
            harvest,
            circleAccounts,
            contractMarket,
            routeResults));

        Assert.Multiple(() =>
        {
            Assert.That(harvest.SatoshiBalance, Is.EqualTo(200));
            Assert.That(longOwner.SatoshiBalance, Is.EqualTo(0));
            Assert.That(shortOwner.SatoshiBalance, Is.EqualTo(0));
            Assert.That(longOrder.IsFilled, Is.False);
            Assert.That(shortOrder.IsFilled, Is.False);
            Assert.That(routeResults, Is.Empty);
        });
    }

    [Test]
    public void ContractOwnerLookup_UsesOwnerCircleIdWhenCircleIndexChangesAfterMaintenance()
    {
        KChartTradingPointInfo reindexedTradingPoint = TestHelpers.TradingPoint(
            KChartTradingPointKind.Purchase,
            tradingPointIndex: 4,
            cudaPointIndex: 4,
            ownerCircleIndex: 1,
            pointCircleIndex: 1,
            ownerCircleId: TestHelpers.CircleId(9),
            pointCircleId: TestHelpers.CircleId(1));
        TradingPointAccount reindexedOwner = new TradingPointAccount(reindexedTradingPoint, 0, 0m);
        ContractOrder staleIndexOrder = new ContractOrder(
            reindexedOwner.TradingPoint,
            TestHelpers.HarvestTradingPoint(),
            ownerCircleIndex: 9,
            ownerCircleId: TestHelpers.CircleId(9),
            routeCircleIndex: 9,
            routeCircleId: TestHelpers.CircleId(9),
            ContractDirection.Long,
            ContractMarginAsset.U,
            marginAmount: 10m,
            openRatio: 0.1m,
            arcStepCount: 10,
            arcRadian: 0.1m,
            pathPointCount: 100,
            createdKLineIndex: 1,
            availableFromKLineIndex: 2,
            FiveElement.Metal,
            price: 100m,
            leverage: 2m,
            takeProfitPrice: 110m,
            liquidationPrice: 50m,
            nominalPosition: 20m,
            routePointIndexes: new[] { 0, 1 });
        ContractOrder currentIndexOrder = TestHelpers.ContractOrder(reindexedOwner);
        Dictionary<int, TradingPointAccount> circleAccounts = new Dictionary<int, TradingPointAccount>
        {
            [1] = reindexedOwner
        };

        TradingPointAccount foundOwner = TestHelpers.InvokeProgram<TradingPointAccount>(
            "GetContractOwnerAccount",
            staleIndexOrder,
            circleAccounts);
        bool sameOwner = TestHelpers.InvokeProgram<bool>(
            "IsSameContractOwner",
            staleIndexOrder,
            currentIndexOrder);

        Assert.Multiple(() =>
        {
            Assert.That(foundOwner, Is.SameAs(reindexedOwner));
            Assert.That(sameOwner, Is.True);
        });
    }

    [Test]
    public void TransferRemainingContractMarginToHarvest_KeepsPrepaidMarginInHarvest()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 10, u: 0m);
        TradingPointAccount owner = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        ContractOrder order = TestHelpers.ContractOrder(owner, ContractDirection.Long, ContractMarginAsset.Satoshi, marginAmount: 10m);

        TestHelpers.InvokeProgram("TransferRemainingContractMarginToHarvest", order, harvest);

        Assert.That(harvest.SatoshiBalance, Is.EqualTo(10L));
    }

    [Test]
    public void HarvestFeeAndReturnMethods_PayAndReturnSatoshi()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: 0m);
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 150, u: 0m);
        List<SpotOrder> spotMarket = new List<SpotOrder>();
        Dictionary<TradingPointAccount, long> pending = new Dictionary<TradingPointAccount, long>();

        object payment = TestHelpers.InvokeProgram(
            "TryPayHarvestFee",
            purchase,
            harvest,
            100m,
            spotMarket,
            new List<ContractOrder>())!;
        bool isPaid = TestHelpers.GetProperty<bool>(payment, "IsPaid");
        long returnable = TestHelpers.GetProperty<long>(payment, "ReturnableSatoshi");
        List<object> paymentTransfers = TestHelpers.GetProperty<System.Collections.IEnumerable>(payment, "SatoshiTransfers")
            .Cast<object>()
            .ToList();
        TestHelpers.InvokeProgram("AddPendingHarvestReturn", pending, purchase, returnable);
        object returnedTransferResult = TestHelpers.InvokeProgram(
            "ReturnHarvestedSatoshi",
            24,
            new List<TradingPointAccount> { harvest, purchase },
            pending)!;
        List<object> returnedTransfers = ((System.Collections.IEnumerable)returnedTransferResult)
            .Cast<object>()
            .ToList();
        TradingPointAccount weakHarvest = TestHelpers.HarvestAccount(satoshi: 50, u: 0m);
        TradingPointAccount weakPurchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 0, u: 0m, tradingPointIndex: 2, cudaPointIndex: 2);
        Dictionary<TradingPointAccount, long> weakPending = new Dictionary<TradingPointAccount, long>();
        TestHelpers.InvokeProgram("AddPendingHarvestReturn", weakPending, weakPurchase, 100L);

        Assert.Multiple(() =>
        {
            Assert.That(isPaid, Is.True);
            Assert.That(returnable, Is.EqualTo(100));
            Assert.That(paymentTransfers, Has.Count.EqualTo(1));
            Assert.That(TestHelpers.GetProperty<TradingPointAccount>(paymentTransfers[0], "SourceAccount"), Is.SameAs(purchase));
            Assert.That(TestHelpers.GetProperty<TradingPointAccount>(paymentTransfers[0], "TargetAccount"), Is.SameAs(harvest));
            Assert.That(TestHelpers.GetProperty<long>(paymentTransfers[0], "SatoshiAmount"), Is.EqualTo(100));
            Assert.That(returnedTransfers, Has.Count.EqualTo(1));
            Assert.That(TestHelpers.GetProperty<TradingPointAccount>(returnedTransfers[0], "SourceAccount"), Is.SameAs(harvest));
            Assert.That(TestHelpers.GetProperty<TradingPointAccount>(returnedTransfers[0], "TargetAccount"), Is.SameAs(purchase));
            Assert.That(TestHelpers.GetProperty<long>(returnedTransfers[0], "SatoshiAmount"), Is.EqualTo(100));
            Assert.That(harvest.SatoshiBalance, Is.EqualTo(0));
            Assert.That(purchase.SatoshiBalance, Is.EqualTo(150));
            Assert.That(pending, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => TestHelpers.InvokeProgram(
                "ReturnHarvestedSatoshi",
                24,
                new List<TradingPointAccount> { weakHarvest, weakPurchase },
                weakPending));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestHelpers.InvokeProgram("AddPendingHarvestReturn", pending, purchase, 0L));
        });
    }

    [Test]
    public void TryPayHarvestFee_CancelsContractAndRemovesItFromMarketWhenNeeded()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: 1m);
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 0m);
        ContractOrder contractOrder = TestHelpers.ContractOrder(
            purchase,
            ContractDirection.Long,
            ContractMarginAsset.U,
            marginAmount: 1m);
        purchase.ContractOrders.Add(contractOrder);
        List<SpotOrder> spotMarket = new List<SpotOrder>();
        List<ContractOrder> contractMarket = new List<ContractOrder> { contractOrder };

        object payment = TestHelpers.InvokeProgram(
            "TryPayHarvestFee",
            purchase,
            harvest,
            100m,
            spotMarket,
            contractMarket)!;

        Assert.Multiple(() =>
        {
            Assert.That(TestHelpers.GetProperty<bool>(payment, "IsPaid"), Is.True);
            Assert.That(contractOrder.IsFilled, Is.True);
            Assert.That(contractMarket, Is.Empty);
            Assert.That(purchase.UBalance, Is.GreaterThan(0m));
            Assert.That(harvest.UBalance, Is.LessThan(1m));
        });
    }

    [Test]
    public void Harvest_NoopsOutsideFeeHourAndReturnsBankruptOwnerIndexesOnFeeHour()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: 0m);
        TradingPointAccount paidPurchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 100, u: 0m);
        TradingPointAccount bankruptPurchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 0, u: 0m, tradingPointIndex: 2, cudaPointIndex: 2);
        Dictionary<TradingPointAccount, long> pending = new Dictionary<TradingPointAccount, long>();
        List<TradingPointAccount> accounts = new List<TradingPointAccount> { harvest, paidPurchase, bankruptPurchase };

        object noop = TestHelpers.InvokeProgram(
            "Harvest",
            22,
            TestHelpers.Line(close: 100m),
            accounts,
            new List<SpotOrder>(),
            new List<ContractOrder>(),
            pending)!;
        object result = TestHelpers.InvokeProgram(
            "Harvest",
            23,
            TestHelpers.Line(close: 100m),
            accounts,
            new List<SpotOrder>(),
            new List<ContractOrder>(),
            pending)!;

        Assert.Multiple(() =>
        {
            List<object> harvestTransfers = TestHelpers.GetProperty<System.Collections.IEnumerable>(result, "SatoshiTransfers")
                .Cast<object>()
                .ToList();
            Assert.That(TestHelpers.GetProperty<int>(noop, "BankruptCount"), Is.EqualTo(0));
            Assert.That(TestHelpers.GetProperty<int>(result, "BankruptCount"), Is.EqualTo(1));
            Assert.That(TestHelpers.GetProperty<IReadOnlyList<int>>(result, "BankruptOwnerCircleIndexes"), Is.EqualTo(new[] { 2 }));
            Assert.That(harvestTransfers, Has.Count.EqualTo(1));
            Assert.That(TestHelpers.GetProperty<TradingPointAccount>(harvestTransfers[0], "SourceAccount"), Is.SameAs(paidPurchase));
            Assert.That(TestHelpers.GetProperty<TradingPointAccount>(harvestTransfers[0], "TargetAccount"), Is.SameAs(harvest));
            Assert.That(TestHelpers.GetProperty<long>(harvestTransfers[0], "SatoshiAmount"), Is.EqualTo(100));
            Assert.That(bankruptPurchase.IsBankrupt, Is.True);
            Assert.That(pending[paidPurchase], Is.EqualTo(100));
        });
    }

    [Test]
    public void LiquidityInjection_AddsUOnlyWhenHarvestAssetIsBelowThreePercentOnHarvestClose()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: 0m);
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 100_000_000L, u: 0m);
        List<TradingPointAccount> accounts = new List<TradingPointAccount> { harvest, purchase };

        TestHelpers.InvokeProgram("InjectLiquidityToHarvest", 22, TestHelpers.Line(close: 100m), accounts);
        decimal before = harvest.UBalance;
        TestHelpers.InvokeProgram("InjectLiquidityToHarvest", 23, TestHelpers.Line(close: 100m), accounts);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.EqualTo(0m));
            Assert.That(harvest.UBalance, Is.GreaterThan(0m));
        });
    }

    [Test]
    public void CancelAndLiquidateMethods_ReturnLockedAssetsAndClearAccount()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: 5m);
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 10, u: 15m);
        SpotOrder sellOrder = TestHelpers.SpotOrder(purchase, SpotOrderSide.SellSatoshi, satoshiAmount: 30, uAmount: 3m);
        ContractOrder contractOrder = TestHelpers.ContractOrder(purchase, ContractDirection.Long, ContractMarginAsset.U, marginAmount: 5m);
        purchase.SpotOrders.Add(sellOrder);
        purchase.ContractOrders.Add(contractOrder);
        List<SpotOrder> spotMarket = new List<SpotOrder> { sellOrder };
        List<ContractOrder> contractMarket = new List<ContractOrder> { contractOrder };

        TestHelpers.InvokeProgram(
            "LiquidateBankruptAccount",
            purchase,
            harvest,
            spotMarket,
            contractMarket,
            TestHelpers.Line(high: 111m));

        Assert.Multiple(() =>
        {
            Assert.That(purchase.IsBankrupt, Is.True);
            Assert.That(purchase.SatoshiBalance, Is.EqualTo(0));
            Assert.That(purchase.UBalance, Is.EqualTo(0m));
            Assert.That(purchase.SpotOrders, Is.Empty);
            Assert.That(purchase.ContractOrders, Is.Empty);
            Assert.That(spotMarket, Is.Empty);
            Assert.That(contractMarket, Is.Empty);
            Assert.That(harvest.SatoshiBalance, Is.EqualTo(40));
            Assert.That(harvest.UBalance, Is.EqualTo(20m));
        });
    }

    [Test]
    public void LiquidateBankruptAccount_ReturnsBuySpotOrderLockedUWithoutCreatingSatoshi()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: 0m);
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 0m);
        SpotOrder buyOrder = TestHelpers.SpotOrder(
            purchase,
            SpotOrderSide.BuySatoshi,
            satoshiAmount: 100,
            uAmount: 0.0001m,
            price: 100m);
        purchase.SpotOrders.Add(buyOrder);
        List<SpotOrder> spotMarket = new List<SpotOrder> { buyOrder };
        List<ContractOrder> contractMarket = new List<ContractOrder>();

        TestHelpers.InvokeProgram("LiquidateBankruptAccount", purchase, harvest, spotMarket, contractMarket, null);

        Assert.Multiple(() =>
        {
            Assert.That(purchase.IsBankrupt, Is.True);
            Assert.That(buyOrder.IsFilled, Is.True);
            Assert.That(spotMarket, Is.Empty);
            Assert.That(harvest.SatoshiBalance, Is.EqualTo(0));
            Assert.That(harvest.UBalance, Is.EqualTo(0.0001m));
        });
    }

    [Test]
    public void AssetAndPriceHelperMethods_ReturnExpectedValues()
    {
        TradingPointAccount account = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 50_000_000L, u: 50m);
        KLine line = TestHelpers.Line(open: 100m, high: 110m, low: 90m, close: 100m);

        decimal totalAsset = TestHelpers.InvokeProgram<decimal>("CalculateAccountTotalAssetInSatoshi", account, 100m);
        decimal deviation = TestHelpers.InvokeProgram<decimal>("CalculateAssetDeviationPercent", account, 100m);
        decimal targetRatio = TestHelpers.InvokeProgram<decimal>("CalculateTargetBtcRatio", account.TradingPoint.OwnerCircleId);
        decimal flatPrice = TestHelpers.InvokeProgram<decimal>("CreateRandomSpotOrderPrice", TestHelpers.Line(open: 100m, high: 100m, low: 100m, close: 100m));

        Assert.Multiple(() =>
        {
            Assert.That(totalAsset, Is.EqualTo(100_000_000m));
            Assert.That(deviation, Is.EqualTo(Math.Abs(0.5m - targetRatio)));
            Assert.That(targetRatio, Is.InRange(0.10m, 0.89m));
            Assert.That(flatPrice, Is.EqualTo(100m));
            Assert.That(TestHelpers.InvokeProgram<bool>("IsPriceInsideKLine", 90m, line), Is.True);
            Assert.That(TestHelpers.InvokeProgram<bool>("IsPriceInsideKLine", 110m, line), Is.True);
            Assert.That(TestHelpers.InvokeProgram<bool>("IsPriceInsideKLine", 111m, line), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => TestHelpers.InvokeProgram("CalculateAccountTotalAssetInSatoshi", account, 0m));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestHelpers.InvokeProgram("CalculateAssetDeviationPercent", account, 0m));
        });
    }

    [Test]
    public void ConvertToKLines_ConvertsDataItemsAndRoundsDataItemHour()
    {
        dataItem item = new dataItem
        {
            dateTime = new DateTime(2026, 1, 1, 8, 35, 10),
            openValue = 100m,
            highValue = 110m,
            lowValue = 90m,
            closeValue = 105m,
            volumeValue = 12m
        };

        IReadOnlyList<KLine> lines = TestHelpers.InvokeProgram<IReadOnlyList<KLine>>(
            "ConvertToKLines",
            new List<dataItem> { item });

        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Count.EqualTo(1));
            Assert.That(lines[0].DateTime, Is.EqualTo(new DateTime(2026, 1, 1, 8, 0, 0)));
            Assert.That(lines[0].CloseValue, Is.EqualTo(105m));
        });
    }

    [Test]
    public void RebuildTradingPointAccountsAfterCircleMaintenance_PreservesSurvivorsAndInitializesNewAccounts()
    {
        TradingPointAccount oldHarvest = TestHelpers.HarvestAccount(satoshi: 1234, u: 61m);
        TradingPointAccount oldPurchase = TestHelpers.PurchaseAccount(
            ownerCircleIndex: 1,
            satoshi: 789,
            u: 12m,
            tradingPointIndex: 9,
            cudaPointIndex: 9);
        TradingPointAccount deletedPurchase = TestHelpers.PurchaseAccount(
            ownerCircleIndex: 9,
            satoshi: 100,
            u: 15m,
            tradingPointIndex: 10,
            cudaPointIndex: 10);
        SpotOrder deletedSpotOrder = TestHelpers.SpotOrder(
            deletedPurchase,
            SpotOrderSide.SellSatoshi,
            satoshiAmount: 30,
            uAmount: 3m);
        ContractOrder deletedContractOrder = TestHelpers.ContractOrder(
            deletedPurchase,
            ContractDirection.Long,
            ContractMarginAsset.U,
            marginAmount: 5m);
        deletedPurchase.SpotOrders.Add(deletedSpotOrder);
        deletedPurchase.ContractOrders.Add(deletedContractOrder);
        List<SpotOrder> spotMarket = new List<SpotOrder> { deletedSpotOrder };
        List<ContractOrder> contractMarket = new List<ContractOrder> { deletedContractOrder };
        Dictionary<TradingPointAccount, long> oldPending = new Dictionary<TradingPointAccount, long>
        {
            [oldPurchase] = 88,
            [deletedPurchase] = 66
        };
        IReadOnlyList<KChartTradingPointInfo> reloaded =
        [
            TestHelpers.HarvestTradingPoint(tradingPointIndex: 0, cudaPointIndex: 0),
            TestHelpers.PurchaseTradingPoint(ownerCircleIndex: 1, tradingPointIndex: 1, cudaPointIndex: 1),
            TestHelpers.PurchaseTradingPoint(ownerCircleIndex: 2, tradingPointIndex: 2, cudaPointIndex: 2),
        ];
        object?[] args =
        {
            new List<TradingPointAccount> { oldHarvest, oldPurchase, deletedPurchase },
            reloaded,
            spotMarket,
            contractMarket,
            TestHelpers.Line(high: 111m),
            oldPending,
            null
        };

        List<TradingPointAccount> rebuilt = TestHelpers.InvokeProgram<List<TradingPointAccount>>(
            "RebuildTradingPointAccountsAfterCircleMaintenance",
            args);
        Dictionary<TradingPointAccount, long> rebuiltPending = (Dictionary<TradingPointAccount, long>)args[6]!;

        Assert.Multiple(() =>
        {
            Assert.That(rebuilt, Has.Count.EqualTo(3));
            Assert.That(rebuilt[0], Is.SameAs(oldHarvest));
            Assert.That(rebuilt[0].SatoshiBalance, Is.EqualTo(1364));
            Assert.That(rebuilt[0].UBalance, Is.EqualTo(76m));
            Assert.That(rebuilt[1], Is.SameAs(oldPurchase));
            Assert.That(rebuilt[1].SatoshiBalance, Is.EqualTo(789));
            Assert.That(rebuilt[1].UBalance, Is.EqualTo(12m));
            Assert.That(rebuilt[1].TradingPoint.TradingPointIndex, Is.EqualTo(1));
            Assert.That(rebuilt[1].TradingPoint.CudaPointIndex, Is.EqualTo(1));
            Assert.That(rebuilt[2].SatoshiBalance, Is.EqualTo(0));
            Assert.That(rebuilt[2].UBalance, Is.EqualTo(1000m));
            Assert.That(rebuiltPending[rebuilt[1]], Is.EqualTo(88));
            Assert.That(rebuiltPending.ContainsValue(66), Is.False);
            Assert.That(deletedPurchase.IsBankrupt, Is.True);
            Assert.That(deletedPurchase.SatoshiBalance, Is.EqualTo(0));
            Assert.That(deletedPurchase.UBalance, Is.EqualTo(0m));
            Assert.That(deletedPurchase.SpotOrders, Is.Empty);
            Assert.That(deletedPurchase.ContractOrders, Is.Empty);
            Assert.That(spotMarket, Is.Empty);
            Assert.That(contractMarket, Is.Empty);
            Assert.That(oldHarvest.SatoshiBalance, Is.EqualTo(1364));
            Assert.That(oldHarvest.UBalance, Is.EqualTo(76m));
        });
    }

    [Test]
    public void TryExecuteMethods_ValidateEarlyBranchesWithoutRouteFiles()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount();
        TradingPointAccount first = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        TradingPointAccount second = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, tradingPointIndex: 2, cudaPointIndex: 2);
        SpotOrder buy = TestHelpers.SpotOrder(first, SpotOrderSide.BuySatoshi);
        SpotOrder sell = TestHelpers.SpotOrder(second, SpotOrderSide.SellSatoshi);
        ContractOrder filledOrder = TestHelpers.ContractOrder(first);
        filledOrder.Cancel();
        List<ContractOrder> market = new List<ContractOrder>();
        List<RouteContractOrderResult> routeResults = new List<RouteContractOrderResult>();
        TradingPointAccount emptyHarvest = TestHelpers.HarvestAccount(satoshi: 0, u: 0m);
        Dictionary<int, TradingPointAccount> circleAccounts = new Dictionary<int, TradingPointAccount>
        {
            [1] = first,
            [2] = second
        };

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => TestHelpers.InvokeProgram("TryExecuteSpotTrade", buy, buy, harvest, 100m, 24, FiveElement.Metal, circleAccounts, market, routeResults));
            Assert.Throws<ArgumentException>(() => TestHelpers.InvokeProgram("TryExecuteSpotTradeWithHarvestBuyer", buy, harvest, 24, FiveElement.Metal, circleAccounts, market, routeResults));
            Assert.Throws<ArgumentException>(() => TestHelpers.InvokeProgram("TryExecuteSpotTradeWithHarvestSeller", sell, harvest, 24, FiveElement.Metal, circleAccounts, market, routeResults));
            Assert.That(TestHelpers.InvokeProgram<SpotTrade?>("TryExecuteSpotTradeWithHarvestSeller", buy, emptyHarvest, 24, FiveElement.Metal, circleAccounts, market, routeResults), Is.Null);
            Assert.That(TestHelpers.InvokeProgram<bool>("TryExecuteContractTradeWithHarvest", filledOrder, harvest, 24, FiveElement.Metal, circleAccounts, market, routeResults), Is.False);
            Assert.That(TestHelpers.InvokeProgram<RouteContractOrderResult?>("TryAddContractOrdersForMatchingTrade", first, first, 24, FiveElement.Metal, 100m, 1m, harvest, circleAccounts, market), Is.Null);
        });
    }

    [Test]
    public void ConsoleUtilityMethods_RunWithoutExternalServices()
    {
        string tempDirectory = Directory.CreateTempSubdirectory("five-element-runloop").FullName;
        string oldDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = tempDirectory;
            Assert.That(TestHelpers.InvokeProgram<bool>("RunLoop"), Is.True);

            File.WriteAllText(Path.Combine(tempDirectory, "stop.bin"), string.Empty);
            string stopOutput = TestHelpers.CaptureConsole(() =>
                Assert.That(TestHelpers.InvokeProgram<bool>("RunLoop"), Is.False));

            string menu = TestHelpers.CaptureConsole(() => TestHelpers.InvokeProgram("PrintMenu"));
            string sample = TestHelpers.CaptureConsole(() => TestHelpers.InvokeProgram("RunSample"));

            string csvPath = Path.Combine(tempDirectory, "lines.csv");
            List<string> rows = new List<string> { "datetime,open,high,low,close,volume" };
            for (int index = 0; index < 25; index++)
            {
                rows.Add($"{new DateTime(2026, 1, 1, 0, 0, 0).AddHours(index):yyyy-MM-dd HH:mm:ss},100,101,99,100,1");
            }

            File.WriteAllLines(csvPath, rows);
            string csvOutput = TestHelpers.CaptureConsole(
                () => TestHelpers.InvokeProgram("ClassifyCsv"),
                csvPath + Environment.NewLine);

            Assert.Multiple(() =>
            {
                Assert.That(stopOutput, Does.Contain("stop.bin"));
                Assert.That(menu, Does.Contain("SAMPLE"));
                Assert.That(sample, Does.Contain(nameof(FiveElement.Metal)));
                Assert.That(csvOutput, Does.Contain("25"));
            });
        }
        finally
        {
            Environment.CurrentDirectory = oldDirectory;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}
