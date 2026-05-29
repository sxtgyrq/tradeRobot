using ConsoleMain;
using KChartRunWithFiveElements;

namespace TestFiveElement;

internal class MarketModelTests
{
    [Test]
    public void TradingPointAccount_ConstructsAccountAndKeepsOrderLists()
    {
        KChartTradingPointInfo point = TestHelpers.PurchaseTradingPoint(2);
        TradingPointAccount account = new TradingPointAccount(point, 123, 45.6m);

        Assert.Multiple(() =>
        {
            Assert.That(account.TradingPoint, Is.SameAs(point));
            Assert.That(account.PointKind, Is.EqualTo(KChartTradingPointKind.Purchase));
            Assert.That(account.SatoshiBalance, Is.EqualTo(123));
            Assert.That(account.UBalance, Is.EqualTo(45.6m));
            Assert.That(account.IsBankrupt, Is.False);
            Assert.That(account.ContractOrders, Is.Empty);
            Assert.That(account.SpotOrders, Is.Empty);
        });

        Assert.Throws<ArgumentNullException>(() => new TradingPointAccount(null!, 0, 0m));
    }

    [Test]
    public void TradingPointAccount_EnforcesBalanceSignRules()
    {
        TradingPointAccount harvest = TestHelpers.HarvestAccount(satoshi: 1, u: 0m);
        TradingPointAccount purchase = TestHelpers.PurchaseAccount(ownerCircleIndex: 2, satoshi: 1, u: 0m);

        harvest.UBalance = -100m;

        Assert.Multiple(() =>
        {
            Assert.That(harvest.UBalance, Is.EqualTo(-100m));
            Assert.Throws<InvalidOperationException>(() => harvest.SatoshiBalance = -1);
            Assert.Throws<InvalidOperationException>(() => purchase.SatoshiBalance = -1);
            Assert.Throws<InvalidOperationException>(() => purchase.UBalance = -0.01m);
            Assert.Throws<InvalidOperationException>(() => new TradingPointAccount(TestHelpers.PurchaseTradingPoint(3), 0, -1m));
            Assert.Throws<InvalidOperationException>(() => new TradingPointAccount(TestHelpers.HarvestTradingPoint(), -1, 0m));
        });
    }

    [Test]
    public void SpotOrder_ConstructsAndFollowsTPlusOne()
    {
        TradingPointAccount account = TestHelpers.PurchaseAccount();
        SpotOrder order = TestHelpers.SpotOrder(
            account,
            SpotOrderSide.BuySatoshi,
            satoshiAmount: 200,
            uAmount: 3m,
            createdKLineIndex: 10,
            availableFromKLineIndex: 11,
            fiveElement: FiveElement.Wood,
            price: 150m);

        Assert.Multiple(() =>
        {
            Assert.That(order.Account, Is.SameAs(account));
            Assert.That(order.Side, Is.EqualTo(SpotOrderSide.BuySatoshi));
            Assert.That(order.SatoshiAmount, Is.EqualTo(200));
            Assert.That(order.UAmount, Is.EqualTo(3m));
            Assert.That(order.RemainingSatoshiAmount, Is.EqualTo(200));
            Assert.That(order.RemainingUAmount, Is.EqualTo(3m));
            Assert.That(order.CanTradeAt(10), Is.False);
            Assert.That(order.CanTradeAt(11), Is.True);
            Assert.That(order.FiveElement, Is.EqualTo(FiveElement.Wood));
            Assert.That(order.Price, Is.EqualTo(150m));
            Assert.That(order.IsFilled, Is.False);
        });
    }

    [Test]
    public void SpotOrder_FillAndCancelMutateRemainingAmounts()
    {
        TradingPointAccount account = TestHelpers.PurchaseAccount();
        SpotOrder buyOrder = TestHelpers.SpotOrder(account, SpotOrderSide.BuySatoshi, satoshiAmount: 200, uAmount: 2m);

        buyOrder.Fill(50, 0.5m);

        Assert.Multiple(() =>
        {
            Assert.That(buyOrder.RemainingSatoshiAmount, Is.EqualTo(150));
            Assert.That(buyOrder.RemainingUAmount, Is.EqualTo(1.5m));
            Assert.That(buyOrder.IsFilled, Is.False);
        });

        buyOrder.Cancel();

        Assert.Multiple(() =>
        {
            Assert.That(buyOrder.IsCanceled, Is.True);
            Assert.That(buyOrder.IsFilled, Is.True);
            Assert.That(buyOrder.RemainingSatoshiAmount, Is.EqualTo(0));
            Assert.That(buyOrder.RemainingUAmount, Is.EqualTo(0m));
        });
    }

    [Test]
    public void SpotOrder_RejectsInvalidFillAndConstructorArguments()
    {
        TradingPointAccount account = TestHelpers.PurchaseAccount();
        SpotOrder order = TestHelpers.SpotOrder(account, SpotOrderSide.SellSatoshi, satoshiAmount: 100, uAmount: 1m);

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new SpotOrder(null!, SpotOrderSide.SellSatoshi, 1, 1m, 0.1m, 1, 2, FiveElement.Metal, 100m));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpotOrder(account, SpotOrderSide.SellSatoshi, 0, 1m, 0.1m, 1, 2, FiveElement.Metal, 100m));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpotOrder(account, SpotOrderSide.SellSatoshi, 1, 0m, 0.1m, 1, 2, FiveElement.Metal, 100m));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpotOrder(account, SpotOrderSide.SellSatoshi, 1, 1m, 0.1m, 1, 1, FiveElement.Metal, 100m));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpotOrder(account, SpotOrderSide.SellSatoshi, 1, 1m, 0.1m, 1, 2, FiveElement.Metal, 0m));
            Assert.Throws<ArgumentOutOfRangeException>(() => order.Fill(0, 1m));
            Assert.Throws<ArgumentOutOfRangeException>(() => order.Fill(1, 0m));
            Assert.Throws<InvalidOperationException>(() => order.Fill(101, 1m));
        });
    }

    [Test]
    public void ContractOrder_ConstructsAndFollowsTPlusOne()
    {
        TradingPointAccount owner = TestHelpers.PurchaseAccount(ownerCircleIndex: 3);
        ContractOrder order = TestHelpers.ContractOrder(
            owner,
            ContractDirection.Short,
            ContractMarginAsset.Satoshi,
            marginAmount: 20m,
            createdKLineIndex: 5,
            availableFromKLineIndex: 6,
            fiveElement: FiveElement.Fire);

        Assert.Multiple(() =>
        {
            Assert.That(order.OwnerCircleIndex, Is.EqualTo(3));
            Assert.That(order.OwnerCircleId, Is.EqualTo(owner.TradingPoint.OwnerCircleId));
            Assert.That(order.RouteCircleIndex, Is.EqualTo(3));
            Assert.That(order.Direction, Is.EqualTo(ContractDirection.Short));
            Assert.That(order.MarginAsset, Is.EqualTo(ContractMarginAsset.Satoshi));
            Assert.That(order.MarginAmount, Is.EqualTo(20m));
            Assert.That(order.NominalPosition, Is.EqualTo(40m));
            Assert.That(order.RemainingNominalPosition, Is.EqualTo(40m));
            Assert.That(order.CanTradeAt(5), Is.False);
            Assert.That(order.CanTradeAt(6), Is.True);
            Assert.That(order.FiveElement, Is.EqualTo(FiveElement.Fire));
            Assert.That(order.IsFilled, Is.False);
        });
    }

    [Test]
    public void ContractOrder_FillAndCancelMutateRemainingPosition()
    {
        TradingPointAccount owner = TestHelpers.PurchaseAccount();
        ContractOrder order = TestHelpers.ContractOrder(owner, marginAmount: 10m);

        order.Fill(5m);

        Assert.Multiple(() =>
        {
            Assert.That(order.RemainingNominalPosition, Is.EqualTo(15m));
            Assert.That(order.IsFilled, Is.False);
        });

        order.Cancel();

        Assert.Multiple(() =>
        {
            Assert.That(order.RemainingNominalPosition, Is.EqualTo(0m));
            Assert.That(order.IsFilled, Is.True);
        });
    }

    [Test]
    public void ContractOrder_RejectsInvalidArguments()
    {
        TradingPointAccount owner = TestHelpers.PurchaseAccount();
        KChartTradingPointInfo source = owner.TradingPoint;
        KChartTradingPointInfo target = TestHelpers.HarvestTradingPoint();

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new ContractOrder(null!, target, 0, TestHelpers.CircleId(0), 0, TestHelpers.CircleId(0), ContractDirection.Long, ContractMarginAsset.U, 1m, 0.1m, 1, 0.1m, 10, 1, 2, FiveElement.Metal, 100m, 2m, 110m, 50m, 2m, new[] { 0, 1 }));
            Assert.Throws<ArgumentNullException>(() => new ContractOrder(source, null!, 0, TestHelpers.CircleId(0), 0, TestHelpers.CircleId(0), ContractDirection.Long, ContractMarginAsset.U, 1m, 0.1m, 1, 0.1m, 10, 1, 2, FiveElement.Metal, 100m, 2m, 110m, 50m, 2m, new[] { 0, 1 }));
            Assert.Throws<ArgumentException>(() => new ContractOrder(source, target, 0, "", 0, TestHelpers.CircleId(0), ContractDirection.Long, ContractMarginAsset.U, 1m, 0.1m, 1, 0.1m, 10, 1, 2, FiveElement.Metal, 100m, 2m, 110m, 50m, 2m, new[] { 0, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContractOrder(source, target, 0, TestHelpers.CircleId(0), 0, TestHelpers.CircleId(0), ContractDirection.Long, ContractMarginAsset.U, 0m, 0.1m, 1, 0.1m, 10, 1, 2, FiveElement.Metal, 100m, 2m, 110m, 50m, 2m, new[] { 0, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContractOrder(source, target, 0, TestHelpers.CircleId(0), 0, TestHelpers.CircleId(0), ContractDirection.Long, ContractMarginAsset.U, 1m, 0m, 1, 0.1m, 10, 1, 2, FiveElement.Metal, 100m, 2m, 110m, 50m, 2m, new[] { 0, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContractOrder(source, target, 0, TestHelpers.CircleId(0), 0, TestHelpers.CircleId(0), ContractDirection.Long, ContractMarginAsset.U, 1m, 0.1m, 0, 0.1m, 10, 1, 2, FiveElement.Metal, 100m, 2m, 110m, 50m, 2m, new[] { 0, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContractOrder(source, target, 0, TestHelpers.CircleId(0), 0, TestHelpers.CircleId(0), ContractDirection.Long, ContractMarginAsset.U, 1m, 0.1m, 1, 0m, 10, 1, 2, FiveElement.Metal, 100m, 2m, 110m, 50m, 2m, new[] { 0, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContractOrder(source, target, 0, TestHelpers.CircleId(0), 0, TestHelpers.CircleId(0), ContractDirection.Long, ContractMarginAsset.U, 1m, 0.1m, 1, 0.1m, 0, 1, 2, FiveElement.Metal, 100m, 2m, 110m, 50m, 2m, new[] { 0, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContractOrder(source, target, 0, TestHelpers.CircleId(0), 0, TestHelpers.CircleId(0), ContractDirection.Long, ContractMarginAsset.U, 1m, 0.1m, 1, 0.1m, 10, 1, 1, FiveElement.Metal, 100m, 2m, 110m, 50m, 2m, new[] { 0, 1 }));
            Assert.Throws<ArgumentException>(() => new ContractOrder(source, target, 0, TestHelpers.CircleId(0), 0, TestHelpers.CircleId(0), ContractDirection.Long, ContractMarginAsset.U, 1m, 0.1m, 1, 0.1m, 10, 1, 2, FiveElement.Metal, 100m, 2m, 110m, 50m, 2m, new[] { 0 }));
        });

        ContractOrder order = TestHelpers.ContractOrder(owner);
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => order.Fill(0m));
            Assert.Throws<InvalidOperationException>(() => order.Fill(order.RemainingNominalPosition + 1m));
        });
    }

    [Test]
    public void RouteContractOrderResult_ExposesSourceTargetAndOrders()
    {
        TradingPointAccount source = TestHelpers.HarvestAccount();
        TradingPointAccount target = TestHelpers.PurchaseAccount();
        ContractOrder order = TestHelpers.ContractOrder(target);
        KChartRouteInfo route = TestHelpers.Route(
            TestHelpers.RoutePoint(0, 0, 0),
            TestHelpers.RoutePoint(1, 0, 10));

        RouteContractOrderResult result = new RouteContractOrderResult(
            source,
            target,
            route,
            new[] { order });

        Assert.Multiple(() =>
        {
            Assert.That(result.SourceAccount, Is.SameAs(source));
            Assert.That(result.HarvestAccount, Is.SameAs(source));
            Assert.That(result.TargetAccount, Is.SameAs(target));
            Assert.That(result.PurchaseAccount, Is.SameAs(target));
            Assert.That(result.Route, Is.SameAs(route));
            Assert.That(result.Orders, Has.Count.EqualTo(1));
            Assert.That(result.OrderCount, Is.EqualTo(1));
        });

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new RouteContractOrderResult(null!, target, route, new[] { order }));
            Assert.Throws<ArgumentNullException>(() => new RouteContractOrderResult(source, null!, route, new[] { order }));
            Assert.Throws<ArgumentNullException>(() => new RouteContractOrderResult(source, target, null!, new[] { order }));
            Assert.Throws<ArgumentNullException>(() => new RouteContractOrderResult(source, target, route, null!));
        });
    }

    [Test]
    public void SpotTradeAndSpotMarketResult_ExposeValuesAndValidateArguments()
    {
        TradingPointAccount seller = TestHelpers.PurchaseAccount(ownerCircleIndex: 1);
        TradingPointAccount buyer = TestHelpers.PurchaseAccount(ownerCircleIndex: 2);
        ContractOrder order = TestHelpers.ContractOrder(seller);
        KChartRouteInfo route = TestHelpers.Route(
            TestHelpers.RoutePoint(0, 1, 0),
            TestHelpers.RoutePoint(1, 1, 10));
        RouteContractOrderResult routeResult = new RouteContractOrderResult(seller, buyer, route, new[] { order });
        SpotTrade trade = new SpotTrade(seller, buyer, 100, 2m, 200m, 8, FiveElement.Earth, routeResult);
        SpotOrder spotOrder = TestHelpers.SpotOrder(seller, SpotOrderSide.SellSatoshi);
        SpotMarketResult marketResult = new SpotMarketResult(
            new[] { trade },
            new[] { spotOrder },
            new[] { routeResult });

        Assert.Multiple(() =>
        {
            Assert.That(trade.Seller, Is.SameAs(seller));
            Assert.That(trade.Buyer, Is.SameAs(buyer));
            Assert.That(trade.SatoshiAmount, Is.EqualTo(100));
            Assert.That(trade.UAmount, Is.EqualTo(2m));
            Assert.That(trade.Price, Is.EqualTo(200m));
            Assert.That(trade.KLineIndex, Is.EqualTo(8));
            Assert.That(trade.FiveElement, Is.EqualTo(FiveElement.Earth));
            Assert.That(trade.RouteContractOrderResult, Is.SameAs(routeResult));
            Assert.That(marketResult.Trades, Has.Count.EqualTo(1));
            Assert.That(marketResult.CreatedOrders, Has.Count.EqualTo(1));
            Assert.That(marketResult.RouteContractOrderResults, Has.Count.EqualTo(1));
        });

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => new SpotTrade(null!, buyer, 1, 1m, 1m, 1, FiveElement.Metal, routeResult));
            Assert.Throws<ArgumentNullException>(() => new SpotTrade(seller, null!, 1, 1m, 1m, 1, FiveElement.Metal, routeResult));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpotTrade(seller, buyer, 0, 1m, 1m, 1, FiveElement.Metal, routeResult));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpotTrade(seller, buyer, 1, 0m, 1m, 1, FiveElement.Metal, routeResult));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpotTrade(seller, buyer, 1, 1m, 0m, 1, FiveElement.Metal, routeResult));
            Assert.Throws<ArgumentNullException>(() => new SpotTrade(seller, buyer, 1, 1m, 1m, 1, FiveElement.Metal, null!));
            Assert.Throws<ArgumentNullException>(() => new SpotMarketResult(null!, Array.Empty<SpotOrder>(), Array.Empty<RouteContractOrderResult>()));
            Assert.Throws<ArgumentNullException>(() => new SpotMarketResult(Array.Empty<SpotTrade>(), null!, Array.Empty<RouteContractOrderResult>()));
            Assert.Throws<ArgumentNullException>(() => new SpotMarketResult(Array.Empty<SpotTrade>(), Array.Empty<SpotOrder>(), null!));
        });
    }
}
