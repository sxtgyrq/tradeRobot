namespace KChartRunWithFiveElements
{
    internal enum ContractDirection
    {
        Long,
        Short
    }

    internal enum ContractMarginAsset
    {
        Satoshi,
        U
    }

    internal sealed class ContractOrder
    {
        public ContractOrder(
            ConsoleMain.KChartTradingPointInfo sourceTradingPoint,
            ConsoleMain.KChartTradingPointInfo targetTradingPoint,
            int ownerCircleIndex,
            string ownerCircleId,
            int routeCircleIndex,
            string routeCircleId,
            ContractDirection direction,
            ContractMarginAsset marginAsset,
            decimal marginAmount,
            decimal openRatio,
            long arcStepCount,
            decimal arcRadian,
            int pathPointCount,
            int createdKLineIndex,
            int availableFromKLineIndex,
            FiveElement fiveElement,
            decimal price,
            decimal leverage,
            decimal takeProfitPrice,
            decimal liquidationPrice,
            decimal nominalPosition,
            IReadOnlyList<int> routePointIndexes)
        {
            if (sourceTradingPoint is null)
            {
                throw new ArgumentNullException(nameof(sourceTradingPoint));
            }

            if (targetTradingPoint is null)
            {
                throw new ArgumentNullException(nameof(targetTradingPoint));
            }

            if (string.IsNullOrWhiteSpace(ownerCircleId))
            {
                throw new ArgumentException("Owner circle id cannot be empty.", nameof(ownerCircleId));
            }

            if (string.IsNullOrWhiteSpace(routeCircleId))
            {
                throw new ArgumentException("Route circle id cannot be empty.", nameof(routeCircleId));
            }

            if (marginAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(marginAmount), "Margin amount must be greater than 0.");
            }

            if (openRatio <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(openRatio), "Open ratio must be greater than 0.");
            }

            if (arcStepCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arcStepCount), "Arc step count must be greater than 0.");
            }

            if (arcRadian <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arcRadian), "Arc radian must be greater than 0.");
            }

            if (pathPointCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pathPointCount), "Path point count must be greater than 0.");
            }

            if (availableFromKLineIndex <= createdKLineIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(availableFromKLineIndex),
                    "Contract order must follow T+1 rule.");
            }

            if (price <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(price), "Price must be greater than 0.");
            }

            if (leverage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(leverage), "Leverage must be greater than 0.");
            }

            if (takeProfitPrice <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(takeProfitPrice), "Take profit price must be greater than 0.");
            }

            if (liquidationPrice <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(liquidationPrice), "Liquidation price must be greater than 0.");
            }

            if (nominalPosition <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nominalPosition), "Nominal position must be greater than 0.");
            }

            if (routePointIndexes is null || routePointIndexes.Count < 2)
            {
                throw new ArgumentException("Route segment must contain at least two points.", nameof(routePointIndexes));
            }

            OrderId = Guid.NewGuid();
            SourceTradingPoint = sourceTradingPoint;
            TargetTradingPoint = targetTradingPoint;
            OwnerCircleIndex = ownerCircleIndex;
            OwnerCircleId = ownerCircleId;
            RouteCircleIndex = routeCircleIndex;
            RouteCircleId = routeCircleId;
            Direction = direction;
            MarginAsset = marginAsset;
            MarginAmount = marginAmount;
            OpenRatio = openRatio;
            ArcStepCount = arcStepCount;
            ArcRadian = arcRadian;
            PathPointCount = pathPointCount;
            CreatedKLineIndex = createdKLineIndex;
            AvailableFromKLineIndex = availableFromKLineIndex;
            FiveElement = fiveElement;
            Price = price;
            Leverage = leverage;
            TakeProfitPrice = takeProfitPrice;
            LiquidationPrice = liquidationPrice;
            NominalPosition = nominalPosition;
            RemainingNominalPosition = nominalPosition;
            RoutePointIndexes = routePointIndexes.ToArray();
        }

        public Guid OrderId { get; }

        public ConsoleMain.KChartTradingPointInfo SourceTradingPoint { get; }

        public ConsoleMain.KChartTradingPointInfo TargetTradingPoint { get; }

        public int OwnerCircleIndex { get; }

        public string OwnerCircleId { get; }

        public int RouteCircleIndex { get; }

        public string RouteCircleId { get; }

        public ContractDirection Direction { get; }

        public ContractMarginAsset MarginAsset { get; }

        public decimal MarginAmount { get; }

        public decimal OpenRatio { get; }

        public long ArcStepCount { get; }

        public decimal ArcRadian { get; }

        public int PathPointCount { get; }

        public int CreatedKLineIndex { get; }

        public int AvailableFromKLineIndex { get; }

        public FiveElement FiveElement { get; }

        public decimal Price { get; }

        public decimal Leverage { get; }

        public decimal TakeProfitPrice { get; }

        public decimal LiquidationPrice { get; }

        public decimal NominalPosition { get; }

        public decimal RemainingNominalPosition { get; private set; }

        public IReadOnlyList<int> RoutePointIndexes { get; }

        public bool IsFilled
        {
            get
            {
                return RemainingNominalPosition <= 0;
            }
        }

        public bool CanTradeAt(int kLineIndex)
        {
            return kLineIndex >= AvailableFromKLineIndex;
        }

        public void Fill(decimal nominalPosition)
        {
            if (nominalPosition <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nominalPosition));
            }

            if (nominalPosition > RemainingNominalPosition)
            {
                throw new InvalidOperationException("Contract fill nominal position exceeds remaining position.");
            }

            RemainingNominalPosition -= nominalPosition;
        }

        public void Cancel()
        {
            RemainingNominalPosition = 0;
        }
    }
}
