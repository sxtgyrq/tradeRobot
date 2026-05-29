namespace KChartRunWithFiveElements
{
    internal enum SpotOrderSide
    {
        BuySatoshi,
        SellSatoshi
    }

    internal sealed class SpotOrder
    {
        public SpotOrder(
            TradingPointAccount account,
            SpotOrderSide side,
            long satoshiAmount,
            decimal uAmount,
            decimal deviationPercent,
            int createdKLineIndex,
            int availableFromKLineIndex,
            FiveElement fiveElement,
            decimal price)
        {
            Account = account ?? throw new ArgumentNullException(nameof(account));
            if (satoshiAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(satoshiAmount));
            }

            if (uAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uAmount));
            }

            if (availableFromKLineIndex <= createdKLineIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(availableFromKLineIndex));
            }

            if (price <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(price));
            }

            OrderId = Guid.NewGuid();
            Account = account;
            Side = side;
            SatoshiAmount = satoshiAmount;
            UAmount = uAmount;
            RemainingSatoshiAmount = satoshiAmount;
            RemainingUAmount = uAmount;
            DeviationPercent = deviationPercent;
            CreatedKLineIndex = createdKLineIndex;
            AvailableFromKLineIndex = availableFromKLineIndex;
            FiveElement = fiveElement;
            Price = price;
        }

        public Guid OrderId { get; }

        public TradingPointAccount Account { get; }

        public SpotOrderSide Side { get; }

        public long SatoshiAmount { get; }

        public decimal UAmount { get; }

        public long RemainingSatoshiAmount { get; private set; }

        public decimal RemainingUAmount { get; private set; }

        public decimal DeviationPercent { get; }

        public int CreatedKLineIndex { get; }

        public int AvailableFromKLineIndex { get; }

        public FiveElement FiveElement { get; }

        public decimal Price { get; }

        public bool IsCanceled { get; private set; }

        public bool IsFilled
        {
            get
            {
                return IsCanceled || RemainingSatoshiAmount <= 0;
            }
        }

        public bool CanTradeAt(int kLineIndex)
        {
            return !IsCanceled && kLineIndex >= AvailableFromKLineIndex;
        }

        public void Fill(long satoshiAmount, decimal uAmount)
        {
            if (satoshiAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(satoshiAmount));
            }

            if (uAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uAmount));
            }

            if (satoshiAmount > RemainingSatoshiAmount)
            {
                throw new InvalidOperationException("Spot fill satoshi amount exceeds remaining amount.");
            }

            if (Side == SpotOrderSide.BuySatoshi && uAmount > RemainingUAmount)
            {
                throw new InvalidOperationException("Spot fill U amount exceeds remaining locked U.");
            }

            RemainingSatoshiAmount -= satoshiAmount;
            if (Side == SpotOrderSide.BuySatoshi)
            {
                RemainingUAmount -= uAmount;
            }

            if (RemainingSatoshiAmount == 0)
            {
                RemainingUAmount = 0;
            }
        }

        public void Cancel()
        {
            IsCanceled = true;
            RemainingSatoshiAmount = 0;
            RemainingUAmount = 0;
        }
    }

    internal sealed class SpotTrade
    {
        public SpotTrade(
            TradingPointAccount seller,
            TradingPointAccount buyer,
            long satoshiAmount,
            decimal uAmount,
            decimal price,
            int kLineIndex,
            FiveElement fiveElement,
            RouteContractOrderResult routeContractOrderResult)
        {
            Seller = seller ?? throw new ArgumentNullException(nameof(seller));
            Buyer = buyer ?? throw new ArgumentNullException(nameof(buyer));
            if (satoshiAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(satoshiAmount));
            }

            if (uAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uAmount));
            }

            if (price <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(price));
            }

            Seller = seller;
            Buyer = buyer;
            SatoshiAmount = satoshiAmount;
            UAmount = uAmount;
            Price = price;
            KLineIndex = kLineIndex;
            FiveElement = fiveElement;
            RouteContractOrderResult = routeContractOrderResult ?? throw new ArgumentNullException(nameof(routeContractOrderResult));
        }

        public TradingPointAccount Seller { get; }

        public TradingPointAccount Buyer { get; }

        public long SatoshiAmount { get; }

        public decimal UAmount { get; }

        public decimal Price { get; }

        public int KLineIndex { get; }

        public FiveElement FiveElement { get; }

        public RouteContractOrderResult RouteContractOrderResult { get; }
    }

    internal sealed class SpotMarketResult
    {
        public SpotMarketResult(
            IReadOnlyList<SpotTrade> trades,
            IReadOnlyList<SpotOrder> createdOrders,
            IReadOnlyList<RouteContractOrderResult> routeContractOrderResults)
        {
            Trades = trades?.ToArray() ?? throw new ArgumentNullException(nameof(trades));
            CreatedOrders = createdOrders?.ToArray() ?? throw new ArgumentNullException(nameof(createdOrders));
            RouteContractOrderResults = routeContractOrderResults?.ToArray() ?? throw new ArgumentNullException(nameof(routeContractOrderResults));
        }

        public IReadOnlyList<SpotTrade> Trades { get; }

        public IReadOnlyList<SpotOrder> CreatedOrders { get; }

        public IReadOnlyList<RouteContractOrderResult> RouteContractOrderResults { get; }
    }
}
