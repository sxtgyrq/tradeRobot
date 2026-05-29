namespace KChartRunWithFiveElements
{
    /// <summary>
    /// 一次路径效应生成的合约结果。
    /// 一条交易点到交易点的路径，可能连续路过多个真实圆；
    /// 每一段连续同圆圆弧最多生成一张合约单，这些合约单统一放在 Orders 中。
    /// </summary>
    internal sealed class RouteContractOrderResult
    {
        public RouteContractOrderResult(
            TradingPointAccount sourceAccount,
            TradingPointAccount targetAccount,
            ConsoleMain.KChartRouteInfo route,
            IReadOnlyList<ContractOrder> orders)
        {
            SourceAccount = sourceAccount ?? throw new ArgumentNullException(nameof(sourceAccount));
            TargetAccount = targetAccount ?? throw new ArgumentNullException(nameof(targetAccount));
            Route = route ?? throw new ArgumentNullException(nameof(route));
            Orders = orders?.ToArray() ?? throw new ArgumentNullException(nameof(orders));
        }

        public TradingPointAccount SourceAccount { get; }

        public TradingPointAccount TargetAccount { get; }

        /// <summary>
        /// 兼容早期“收割点 -> 采购点”命名。现货/合约撮合时，SourceAccount 不一定是真正的收割点。
        /// </summary>
        public TradingPointAccount HarvestAccount
        {
            get
            {
                return SourceAccount;
            }
        }

        /// <summary>
        /// 兼容早期“收割点 -> 采购点”命名。现货/合约撮合时，TargetAccount 不一定是真正的采购点。
        /// </summary>
        public TradingPointAccount PurchaseAccount
        {
            get
            {
                return TargetAccount;
            }
        }

        public ConsoleMain.KChartRouteInfo Route { get; }

        /// <summary>
        /// 本次路径实际新增的合约单；没有路过可开单圆弧时可以为空。
        /// </summary>
        public IReadOnlyList<ContractOrder> Orders { get; }

        public int OrderCount
        {
            get
            {
                return Orders.Count;
            }
        }
    }
}
