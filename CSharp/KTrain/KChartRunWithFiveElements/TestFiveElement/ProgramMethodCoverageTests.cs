using CommonClass;
using ConsoleMain;
using KChartRunWithFiveElements;
using System.Collections;
using System.Reflection;

namespace TestFiveElement;

internal class ProgramMethodCoverageTests
{
    [Test]
    public void PendingHarvestTotalAndSatoshiGuardMethods_CountOnlyLivePositiveReturns()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 1000, u: 0m);
        TradingPointAccount livePurchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 10, u: 0m);
        TradingPointAccount bankruptPurchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 20, u: 0m, tradingPointIndex: 2, cudaPointIndex: 2);
        bankruptPurchase.IsBankrupt = true;

        Dictionary<TradingPointAccount, long> pending = new Dictionary<TradingPointAccount, long>
        {
            [livePurchase] = 30,
            [bankruptPurchase] = 40,
        };
        List<TradingPointAccount> accounts = new List<TradingPointAccount> { harvest, livePurchase, bankruptPurchase };
        long before = TestHelpers.InvokeProgram<long>("CalculateTrackedSatoshiTotal", accounts);

        long total = TestHelpers.InvokeProgram<long>("CalculatePendingHarvestReturnTotal", pending);

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(30));
            Assert.DoesNotThrow(() => TestHelpers.InvokeProgram("EnsureSatoshiTotalUnchanged", "stable", before, accounts));

            livePurchase.SatoshiBalance += 1;
            Assert.Throws<InvalidOperationException>(() =>
                TestHelpers.InvokeProgram("EnsureSatoshiTotalUnchanged", "changed", before, accounts));
        });
    }

    [Test]
    public void PayHarvestFeeFromAvailableBalances_UsesSatoshiBeforeUAndRecordsSatoshiTransfer()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: 0m);
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 40, u: 1m);
        IList transfers = CreateSatoshiTransferList();
        object?[] args =
        {
            purchase,
            harvest,
            100L,
            100m,
            0L,
            transfers
        };

        long remaining = TestHelpers.InvokeProgram<long>("PayHarvestFeeFromAvailableBalances", args);
        long returnableSatoshi = (long)args[4]!;
        object firstTransfer = transfers[0]!;

        Assert.Multiple(() =>
        {
            Assert.That(remaining, Is.EqualTo(0));
            Assert.That(returnableSatoshi, Is.EqualTo(40));
            Assert.That(purchase.SatoshiBalance, Is.EqualTo(0));
            Assert.That(harvest.SatoshiBalance, Is.EqualTo(40));
            Assert.That(purchase.UBalance, Is.EqualTo(1m - 0.00006m));
            Assert.That(harvest.UBalance, Is.EqualTo(0.00006m));
            Assert.That(transfers, Has.Count.EqualTo(1));
            Assert.That(TestHelpers.GetProperty<TradingPointAccount>(firstTransfer, "SourceAccount"), Is.SameAs(purchase));
            Assert.That(TestHelpers.GetProperty<TradingPointAccount>(firstTransfer, "TargetAccount"), Is.SameAs(harvest));
            Assert.That(TestHelpers.GetProperty<long>(firstTransfer, "SatoshiAmount"), Is.EqualTo(40));
        });
    }

    [Test]
    public void HarvestPayableAndRiskHelpers_IncludeOpenOrdersAndMeasureLiquidationDistance()
    {
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 10, u: 1m);
        SpotOrder sellOrder = TestHelpers.SpotOrder(purchase, SpotOrderSide.SellSatoshi, satoshiAmount: 30, uAmount: 0.00003m);
        SpotOrder buyOrder = TestHelpers.SpotOrder(purchase, SpotOrderSide.BuySatoshi, satoshiAmount: 40, uAmount: 0.00004m);
        ContractOrder uContract = TestHelpers.ContractOrder(
            purchase,
            ContractDirection.Long,
            ContractMarginAsset.U,
            marginAmount: 1m,
            price: 100m);
        purchase.SpotOrders.Add(sellOrder);
        purchase.SpotOrders.Add(buyOrder);
        purchase.ContractOrders.Add(uContract);

        decimal payable = TestHelpers.InvokeProgram<decimal>("CalculateHarvestPayableSatoshiValue", purchase, 100m);
        decimal risk = TestHelpers.InvokeProgram<decimal>("CalculateContractLiquidationDistanceRatio", uContract, 100m);

        Assert.Multiple(() =>
        {
            Assert.That(payable, Is.EqualTo(10 + 1_000_000m + 30 + 40 + 1_000_000m));
            Assert.That(risk, Is.EqualTo(0.5m));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TestHelpers.InvokeProgram("CalculateContractLiquidationDistanceRatio", uContract, 0m));
        });
    }

    [Test]
    public void CancelOpenSpotOrders_ReturnsLockedAssetsAndRemovesOrdersFromMarket()
    {
        TradingPointAccount account = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 10, u: 5m);
        SpotOrder sellOrder = TestHelpers.SpotOrder(account, SpotOrderSide.SellSatoshi, satoshiAmount: 30, uAmount: 0.00003m);
        SpotOrder buyOrder = TestHelpers.SpotOrder(account, SpotOrderSide.BuySatoshi, satoshiAmount: 40, uAmount: 2m);
        account.SpotOrders.Add(sellOrder);
        account.SpotOrders.Add(buyOrder);
        List<SpotOrder> market = new List<SpotOrder> { sellOrder, buyOrder };

        TestHelpers.InvokeProgram("CancelOpenSpotOrders", account, market);

        Assert.Multiple(() =>
        {
            Assert.That(account.SatoshiBalance, Is.EqualTo(40));
            Assert.That(account.UBalance, Is.EqualTo(7m));
            Assert.That(sellOrder.IsFilled, Is.True);
            Assert.That(buyOrder.IsFilled, Is.True);
            Assert.That(market, Is.Empty);
        });
    }

    [Test]
    public void CancelOpenContractOrder_ReturnsUMarginOrSatoshiMarginFromHarvest()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 1000, u: 100m);
        TradingPointAccount uAccount = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 0m);
        TradingPointAccount satoshiAccount = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 0, u: 0m, tradingPointIndex: 2, cudaPointIndex: 2);
        ContractOrder uOrder = TestHelpers.ContractOrder(
            uAccount,
            ContractDirection.Long,
            ContractMarginAsset.U,
            marginAmount: 10m);
        ContractOrder satoshiOrder = TestHelpers.ContractOrder(
            satoshiAccount,
            ContractDirection.Short,
            ContractMarginAsset.Satoshi,
            marginAmount: 20m);

        TestHelpers.InvokeProgram("CancelOpenContractOrder", uAccount, harvest, uOrder);
        TestHelpers.InvokeProgram("CancelOpenContractOrder", satoshiAccount, harvest, satoshiOrder);

        Assert.Multiple(() =>
        {
            Assert.That(uOrder.IsFilled, Is.True);
            Assert.That(satoshiOrder.IsFilled, Is.True);
            Assert.That(uAccount.UBalance, Is.EqualTo(10m));
            Assert.That(satoshiAccount.SatoshiBalance, Is.EqualTo(20));
            Assert.That(harvest.UBalance, Is.EqualTo(90m));
            Assert.That(harvest.SatoshiBalance, Is.EqualTo(980));
        });
    }

    [Test]
    public void BankruptOrderSettlementHelpers_MoveOpenSpotAndContractResidualsToHarvest()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 0, u: 0m);
        TradingPointAccount seller = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 0m);
        TradingPointAccount buyer = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 0, u: 0m, tradingPointIndex: 2, cudaPointIndex: 2);
        SpotOrder sellOrder = TestHelpers.SpotOrder(seller, SpotOrderSide.SellSatoshi, satoshiAmount: 30, uAmount: 0.00003m);
        SpotOrder buyOrder = TestHelpers.SpotOrder(buyer, SpotOrderSide.BuySatoshi, satoshiAmount: 40, uAmount: 2m);
        ContractOrder takeProfitOrder = TestHelpers.ContractOrder(seller, ContractDirection.Long, ContractMarginAsset.U, marginAmount: 10m);
        ContractOrder normalOrder = TestHelpers.ContractOrder(buyer, ContractDirection.Short, ContractMarginAsset.U, marginAmount: 5m);

        TestHelpers.InvokeProgram("SettleBankruptSpotOrderToHarvest", sellOrder, harvest);
        TestHelpers.InvokeProgram("SettleBankruptSpotOrderToHarvest", buyOrder, harvest);
        TestHelpers.InvokeProgram("SettleBankruptContractOrderToHarvest", takeProfitOrder, harvest, TestHelpers.Line(high: 111m));
        TestHelpers.InvokeProgram("SettleBankruptContractOrderToHarvest", normalOrder, harvest, null);

        Assert.Multiple(() =>
        {
            Assert.That(harvest.SatoshiBalance, Is.EqualTo(30));
            Assert.That(harvest.UBalance, Is.EqualTo(2m));
            Assert.That(sellOrder.IsFilled, Is.True);
            Assert.That(buyOrder.IsFilled, Is.True);
            Assert.That(takeProfitOrder.IsFilled, Is.True);
            Assert.That(normalOrder.IsFilled, Is.True);
        });
    }

    [Test]
    public void ContractTakeProfitHelpers_PrecheckPayoutAndSettlementMath()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 1000, u: 100m);
        TradingPointAccount owner = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 0m);
        ContractOrder uOrder = TestHelpers.ContractOrder(
            owner,
            ContractDirection.Long,
            ContractMarginAsset.U,
            marginAmount: 10m,
            price: 100m,
            leverage: 2m);
        ContractOrder satoshiOrder = TestHelpers.ContractOrder(
            owner,
            ContractDirection.Long,
            ContractMarginAsset.Satoshi,
            marginAmount: 100m,
            price: 100m,
            leverage: 2m);

        (decimal marginReturn, decimal profit, decimal payout) =
            TestHelpers.InvokeProgram<(decimal MarginReturn, decimal Profit, decimal Payout)>(
                "CalculateContractTakeProfitSettlement",
                uOrder,
                10m);

        Assert.Multiple(() =>
        {
            Assert.That(marginReturn, Is.EqualTo(5m));
            Assert.That(profit, Is.EqualTo(1m));
            Assert.That(payout, Is.EqualTo(6m));
            Assert.DoesNotThrow(() =>
                TestHelpers.InvokeProgram("EnsureHarvestCanPayContractTakeProfit", harvest, new (ContractOrder Order, decimal NominalPosition)[] { (uOrder, 10m), (satoshiOrder, 10m) }));
            Assert.Throws<InvalidOperationException>(() =>
                TestHelpers.InvokeProgram("EnsureHarvestCanPayContractTakeProfit", TestHelpers.HarvestAccount(satoshi: 0, u: 100m), new (ContractOrder Order, decimal NominalPosition)[] { (satoshiOrder, 10m) }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TestHelpers.InvokeProgram("CalculateContractTakeProfitSettlement", uOrder, 0m));
            Assert.Throws<InvalidOperationException>(() =>
                TestHelpers.InvokeProgram("CalculateContractTakeProfitSettlement", uOrder, uOrder.RemainingNominalPosition + 1m));
        });

        TestHelpers.InvokeProgram("SettleContractTakeProfitToHarvest", uOrder, harvest, 10m);
        Assert.That(uOrder.RemainingNominalPosition, Is.EqualTo(10m));
    }

    [Test]
    public void AddContractOrdersForSatoshiTransfers_ValidatesInputAndSkipsEmptyTransfers()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount();
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        List<TradingPointAccount> accounts = new List<TradingPointAccount> { harvest, purchase };
        List<ContractOrder> market = new List<ContractOrder>();

        IReadOnlyList<RouteContractOrderResult> empty = TestHelpers.InvokeProgram<IReadOnlyList<RouteContractOrderResult>>(
            "AddContractOrdersForSatoshiTransfers",
            CreateSatoshiTransferArray(),
            24,
            FiveElement.Metal,
            100m,
            accounts,
            market);

        Assert.Multiple(() =>
        {
            Assert.That(empty, Is.Empty);
            Assert.Throws<ArgumentNullException>(() =>
                TestHelpers.InvokeProgram("AddContractOrdersForSatoshiTransfers", null, 24, FiveElement.Metal, 100m, accounts, market));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TestHelpers.InvokeProgram("AddContractOrdersForSatoshiTransfers", CreateSatoshiTransferArray(), 24, FiveElement.Metal, 0m, accounts, market));
            Assert.Throws<ArgumentNullException>(() =>
                TestHelpers.InvokeProgram("AddContractOrdersForSatoshiTransfers", CreateSatoshiTransferArray(), 24, FiveElement.Metal, 100m, null, market));
            Assert.Throws<ArgumentNullException>(() =>
                TestHelpers.InvokeProgram("AddContractOrdersForSatoshiTransfers", CreateSatoshiTransferArray(), 24, FiveElement.Metal, 100m, accounts, null));
        });
    }

    [Test]
    public void AddSatoshiPayoutPathEffectIfNeeded_SkipsNullPayoutWithoutRouteAccess()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount();
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        Dictionary<int, TradingPointAccount> circleAccounts = new Dictionary<int, TradingPointAccount> { [1] = purchase };
        List<ContractOrder> market = new List<ContractOrder>();
        List<RouteContractOrderResult> results = new List<RouteContractOrderResult>();

        TestHelpers.InvokeProgram(
            "AddSatoshiPayoutPathEffectIfNeeded",
            null,
            24,
            FiveElement.Metal,
            100m,
            harvest,
            circleAccounts,
            market,
            results);

        Assert.Multiple(() =>
        {
            Assert.That(market, Is.Empty);
            Assert.That(results, Is.Empty);
        });
    }

    [Test]
    public void CreateSpotOrdersFromAssetDeviation_CreatesOrdersAndLocksAccountBalances()
    {
        TradingPointAccount satoshiHeavy = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 100_000_000L, u: 0m);
        TradingPointAccount uHeavy = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 0, u: 100m, tradingPointIndex: 2, cudaPointIndex: 2);
        List<TradingPointAccount> purchases = new List<TradingPointAccount> { satoshiHeavy, uHeavy };
        List<SpotOrder> market = new List<SpotOrder>();

        List<SpotOrder> created = TestHelpers.InvokeProgram<List<SpotOrder>>(
            "CreateSpotOrdersFromAssetDeviation",
            24,
            TestHelpers.Line(open: 100m, high: 100m, low: 100m, close: 100m),
            FiveElement.Wood,
            purchases,
            market);

        Assert.Multiple(() =>
        {
            Assert.That(created, Has.Count.EqualTo(2));
            Assert.That(market, Is.EqualTo(created));
            Assert.That(created.All(item => item.AvailableFromKLineIndex == 25), Is.True);
            Assert.That(created.All(item => item.FiveElement == FiveElement.Wood), Is.True);
            Assert.That(satoshiHeavy.SatoshiBalance, Is.LessThan(100_000_000L));
            Assert.That(uHeavy.UBalance, Is.LessThan(100m));
        });
    }

    [Test]
    public void TryCreateHarvestSpotOrderFromBalance_RebalancesHarvestTowardHalfSatoshiHalfU()
    {
        TradingPointAccount satoshiHeavyHarvest = TestHelpers.HarvestAccount(satoshi: 100_000_000L, u: 0m);
        TradingPointAccount uHeavyHarvest = TestHelpers.HarvestAccount(satoshi: 0, u: 100m);
        TradingPointAccount emptyHarvest = TestHelpers.HarvestAccount(satoshi: 0, u: 0m);
        KLine flatLine = TestHelpers.Line(open: 100m, high: 100m, low: 100m, close: 100m);

        SpotOrder? sellOrder = TestHelpers.InvokeProgram<SpotOrder?>(
            "TryCreateHarvestSpotOrderFromBalance",
            satoshiHeavyHarvest,
            24,
            flatLine,
            FiveElement.Fire);
        SpotOrder? buyOrder = TestHelpers.InvokeProgram<SpotOrder?>(
            "TryCreateHarvestSpotOrderFromBalance",
            uHeavyHarvest,
            24,
            flatLine,
            FiveElement.Water);
        SpotOrder? none = TestHelpers.InvokeProgram<SpotOrder?>(
            "TryCreateHarvestSpotOrderFromBalance",
            emptyHarvest,
            24,
            flatLine,
            FiveElement.Earth);

        Assert.Multiple(() =>
        {
            Assert.That(sellOrder, Is.Not.Null);
            Assert.That(sellOrder!.Side, Is.EqualTo(SpotOrderSide.SellSatoshi));
            Assert.That(satoshiHeavyHarvest.SatoshiBalance, Is.LessThan(100_000_000L));
            Assert.That(buyOrder, Is.Not.Null);
            Assert.That(buyOrder!.Side, Is.EqualTo(SpotOrderSide.BuySatoshi));
            Assert.That(uHeavyHarvest.UBalance, Is.LessThan(100m));
            Assert.That(none, Is.Null);
        });
    }

    [Test]
    public void ExecuteDistribution_SkipsBeforeRouteLoadWhenThresholdOrAvailableSatoshiBlocksDistribution()
    {
        TradingPointAccount lowHarvest = TestHelpers.HarvestAccount(satoshi: 100, u: 0m);
        TradingPointAccount richPurchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 1000m);
        List<TradingPointAccount> accounts = new List<TradingPointAccount> { lowHarvest, richPurchase };
        List<ContractOrder> market = new List<ContractOrder>();

        TestHelpers.InvokeProgram(
            "ExecuteDistribution",
            24,
            100m,
            FiveElement.Metal,
            accounts,
            market,
            0L);

        Assert.Multiple(() =>
        {
            Assert.That(lowHarvest.SatoshiBalance, Is.EqualTo(100));
            Assert.That(richPurchase.SatoshiBalance, Is.EqualTo(0));
            Assert.That(market, Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TestHelpers.InvokeProgram("ExecuteDistribution", 24, 0m, FiveElement.Metal, accounts, market, 0L));
            Assert.Throws<InvalidOperationException>(() =>
                TestHelpers.InvokeProgram("ExecuteDistribution", 24, 100m, FiveElement.Metal, new List<TradingPointAccount> { lowHarvest }, market, 0L));
        });
    }

    [Test]
    public void TryCreateContractOrderForSegment_CreatesUOrSatoshiMarginByLargerAssetValue()
    {
        TradingPointAccount source = TestHelpers.HarvestAccount(satoshi: 0, u: 0m);
        TradingPointAccount target = TestHelpers.PurchaseAccount(ownerCircleIndex: 9);
        TradingPointAccount uCircle = TestHelpers.PurchaseAccount(ownerCircleIndex: 1, satoshi: 0, u: 100m, tradingPointIndex: 10, cudaPointIndex: 10);
        TradingPointAccount satoshiCircle = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 1000, u: 0m, tradingPointIndex: 11, cudaPointIndex: 11);
        KChartRouteInfo longRoute = TestHelpers.Route(
            TestHelpers.RoutePoint(0, 1, 0, pathPointCount: 100, signedRadius: 100),
            TestHelpers.RoutePoint(1, 1, 50, pathPointCount: 100, signedRadius: 100));
        KChartRouteInfo shortRoute = TestHelpers.Route(
            TestHelpers.RoutePoint(0, 2, 0, pathPointCount: 100, signedRadius: -100),
            TestHelpers.RoutePoint(1, 2, 50, pathPointCount: 100, signedRadius: -100));

        ContractOrder? uOrder = TestHelpers.InvokeProgram<ContractOrder?>(
            "TryCreateContractOrderForSegment",
            source,
            target,
            uCircle,
            source,
            longRoute,
            0,
            1,
            24,
            FiveElement.Fire,
            100m,
            1m);
        ContractOrder? satoshiOrder = TestHelpers.InvokeProgram<ContractOrder?>(
            "TryCreateContractOrderForSegment",
            source,
            target,
            satoshiCircle,
            source,
            shortRoute,
            0,
            1,
            24,
            FiveElement.Water,
            100m,
            1m);

        Assert.Multiple(() =>
        {
            Assert.That(uOrder, Is.Not.Null);
            Assert.That(uOrder!.Direction, Is.EqualTo(ContractDirection.Long));
            Assert.That(uOrder.MarginAsset, Is.EqualTo(ContractMarginAsset.U));
            Assert.That(uOrder.AvailableFromKLineIndex, Is.EqualTo(25));
            Assert.That(uCircle.UBalance, Is.LessThan(100m));
            Assert.That(source.UBalance, Is.GreaterThan(0m));

            Assert.That(satoshiOrder, Is.Not.Null);
            Assert.That(satoshiOrder!.Direction, Is.EqualTo(ContractDirection.Short));
            Assert.That(satoshiOrder.MarginAsset, Is.EqualTo(ContractMarginAsset.Satoshi));
            Assert.That(satoshiCircle.SatoshiBalance, Is.LessThan(1000));
            Assert.That(source.SatoshiBalance, Is.GreaterThan(0));
        });
    }

    [Test]
    public void RouteDependentHelpers_ReportMissingCircleFileInsteadOfSilentlySucceeding()
    {
        string oldDirectory = Environment.CurrentDirectory;
        string tempDirectory = Directory.CreateTempSubdirectory("five-element-route-missing").FullName;
        try
        {
            Environment.CurrentDirectory = tempDirectory;
            TradingPointAccount harvest = TestHelpers.HarvestAccount();
            TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
            Dictionary<int, TradingPointAccount> circleAccounts = new Dictionary<int, TradingPointAccount> { [1] = purchase };
            List<ContractOrder> contractMarket = new List<ContractOrder>();
            List<RouteContractOrderResult> routeResults = new List<RouteContractOrderResult>();
            SpotOrder buyOrder = TestHelpers.SpotOrder(purchase, SpotOrderSide.BuySatoshi);
            object transfer = CreateSatoshiTransfer(harvest, purchase, 1);

            Assert.Multiple(() =>
            {
                Assert.Throws<FileNotFoundException>(() =>
                    TestHelpers.InvokeProgram("AddContractOrdersForSpotTrade", harvest, purchase, 24, FiveElement.Metal, 100m, 1m, harvest, circleAccounts, contractMarket));
                Assert.Throws<FileNotFoundException>(() =>
                    TestHelpers.InvokeProgram("AddSatoshiPayoutPathEffectIfNeeded", transfer, 24, FiveElement.Metal, 100m, harvest, circleAccounts, contractMarket, routeResults));
                Assert.Throws<FileNotFoundException>(() =>
                    TestHelpers.InvokeProgram("TryExecuteSpotTradeWithHarvestSeller", buyOrder, harvest, 24, FiveElement.Metal, circleAccounts, contractMarket, routeResults));
            });
        }
        finally
        {
            Environment.CurrentDirectory = oldDirectory;
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void CreateTradingPointAccountsAndClearAccountOrderLists_HandleUnknownKindsAndExistingOrders()
    {
        KChartTradingPointInfo unknown = TestHelpers.TradingPoint(
            (KChartTradingPointKind)999,
            0,
            0,
            1,
            1);
        TradingPointAccount account = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        SpotOrder spotOrder = TestHelpers.SpotOrder(account, SpotOrderSide.SellSatoshi);
        ContractOrder contractOrder = TestHelpers.ContractOrder(account);
        account.SpotOrders.Add(spotOrder);
        account.ContractOrders.Add(contractOrder);

        Assert.Throws<InvalidOperationException>(() =>
            TestHelpers.InvokeProgram("CreateTradingPointAccounts", new List<KChartTradingPointInfo> { unknown }));

        TestHelpers.InvokeProgram("ClearAccountOrderLists", new List<TradingPointAccount> { account });

        Assert.Multiple(() =>
        {
            Assert.That(account.SpotOrders, Is.Empty);
            Assert.That(account.ContractOrders, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => TestHelpers.InvokeProgram("ClearAccountOrderLists", new object?[] { null }));
        });
    }

    [Test]
    public void ConvertToKLines_DirectlyMapsDataItemValues()
    {
        dataItem first = new dataItem
        {
            dateTime = new DateTime(2026, 6, 14, 1, 2, 3),
            openValue = 100m,
            highValue = 105m,
            lowValue = 95m,
            closeValue = 101m,
            volumeValue = 7m
        };
        dataItem second = new dataItem
        {
            dateTime = new DateTime(2026, 6, 14, 2, 3, 4),
            openValue = 101m,
            highValue = 106m,
            lowValue = 96m,
            closeValue = 102m,
            volumeValue = 8m
        };

        IReadOnlyList<KLine> lines = TestHelpers.InvokeProgram<IReadOnlyList<KLine>>(
            "ConvertToKLines",
            new List<dataItem> { first, second });

        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Count.EqualTo(2));
            Assert.That(lines[0].DateTime, Is.EqualTo(first.dateTime));
            Assert.That(lines[0].OpenValue, Is.EqualTo(100m));
            Assert.That(lines[1].CloseValue, Is.EqualTo(102m));
            Assert.That(lines[1].VolumeValue, Is.EqualTo(8m));
        });
    }

    private static Type GetSatoshiTransferType()
    {
        return typeof(Program).GetNestedType("SatoshiTransfer", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Program.SatoshiTransfer type was not found.");
    }

    private static IList CreateSatoshiTransferList()
    {
        Type listType = typeof(List<>).MakeGenericType(GetSatoshiTransferType());
        return (IList)Activator.CreateInstance(listType)!;
    }

    private static Array CreateSatoshiTransferArray(params object[] transfers)
    {
        Type transferType = GetSatoshiTransferType();
        Array array = Array.CreateInstance(transferType, transfers.Length);
        for (int index = 0; index < transfers.Length; index++)
        {
            array.SetValue(transfers[index], index);
        }

        return array;
    }

    private static object CreateSatoshiTransfer(
        TradingPointAccount sourceAccount,
        TradingPointAccount targetAccount,
        long satoshiAmount)
    {
        Type transferType = GetSatoshiTransferType();
        ConstructorInfo constructor = transferType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            new[] { typeof(TradingPointAccount), typeof(TradingPointAccount), typeof(long) },
            modifiers: null)
            ?? throw new InvalidOperationException("Program.SatoshiTransfer constructor was not found.");

        return constructor.Invoke(new object[] { sourceAccount, targetAccount, satoshiAmount });
    }
}
