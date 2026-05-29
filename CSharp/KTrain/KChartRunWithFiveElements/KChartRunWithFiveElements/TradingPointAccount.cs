namespace KChartRunWithFiveElements
{
    internal sealed class TradingPointAccount
    {
        // 当前账户绑定的交易点。
        // 圆还存在时，账户对象会一直沿用；补圆或重算线路后，只更新这里的 TradingPoint 信息。
        private ConsoleMain.KChartTradingPointInfo _tradingPoint;

        // 聪余额。
        // 系统规则：无论收割点还是采购点，聪都不能为负数。
        private long _satoshiBalance;

        // U 余额。
        // 系统规则：收割点的 U 可以为负数，用于兜底市场流动性；采购点的 U 不能为负数。
        private decimal _uBalance;

        public TradingPointAccount(
            ConsoleMain.KChartTradingPointInfo tradingPoint,
            long satoshiBalance,
            decimal uBalance)
        {
            // 构造时先绑定交易点。
            // 后续设置 UBalance 时，需要通过 PointKind 判断这个账户是不是收割点。
            _tradingPoint = tradingPoint ?? throw new ArgumentNullException(nameof(tradingPoint));

            // 通过属性赋值，而不是直接写字段。
            // 这样构造阶段也会执行“聪不能为负数”的统一校验。
            SatoshiBalance = satoshiBalance;

            // 通过属性赋值，让构造阶段也执行“只有收割点 U 可以为负数”的统一校验。
            UBalance = uBalance;

            // 当前账户持有的合约单引用。
            // 圆存在时账户沿用，这个列表也跟着账户保留。
            ContractOrders = new List<ContractOrder>();

            // 当前账户持有的现货单引用。
            // 圆存在时账户沿用，这个列表也跟着账户保留。
            SpotOrders = new List<SpotOrder>();
        }

        // 账户当前绑定的交易点信息。
        // 对采购点账户来说，OwnerCircleId 是账户身份；只要这个圆还存在，账户对象就继续用。
        public ConsoleMain.KChartTradingPointInfo TradingPoint
        {
            get
            {
                return _tradingPoint;
            }
        }

        // 交易点类型：收割点或采购点。
        // U 余额是否允许为负数，就由这个类型决定。
        public ConsoleMain.KChartTradingPointKind PointKind
        {
            get
            {
                return TradingPoint.PointKind;
            }
        }

        // 聪余额。
        // 硬规则：聪永不允许为负数；如果业务代码扣成负数，说明上层逻辑有漏洞，必须立即报错。
        public long SatoshiBalance
        {
            get
            {
                return _satoshiBalance;
            }

            set
            {
                // 收割点和采购点都不能让聪余额变成负数。
                if (value < 0)
                {
                    throw new InvalidOperationException("Satoshi balance cannot be negative.");
                }

                // 校验通过后才写入字段。
                _satoshiBalance = value;
            }
        }

        // U 余额。
        // 硬规则：收割点可以为负数，采购点不能为负数。
        public decimal UBalance
        {
            get
            {
                return _uBalance;
            }

            set
            {
                // 只有收割点能通过 U 负数来兜底；采购点 U 不足时应破产或走其他清算逻辑。
                if (value < 0 && PointKind != ConsoleMain.KChartTradingPointKind.Harvest)
                {
                    throw new InvalidOperationException("Only harvest account can have negative U balance.");
                }

                // 校验通过后才写入字段。
                _uBalance = value;
            }
        }

        // 采购点是否已经进入破产状态。
        // 收割点不会破产；如果看到收割点被标记破产，说明上层逻辑错误。
        public bool IsBankrupt { get; set; }

        // 当前账户关联的合约单。
        // 圆还存在时账户不换，所以这里的订单引用也会保留。
        public List<ContractOrder> ContractOrders { get; }

        // 当前账户关联的现货单。
        // 圆还存在时账户不换，所以这里的订单引用也会保留。
        public List<SpotOrder> SpotOrders { get; }

        // 更新账户绑定的交易点信息。
        // 用途：补圆或重算线路后，交易点序号、CUDA 点序号、实际路径点位置可能变化；
        // 但只要 OwnerCircleId 还在，账户对象本身继续使用，只更新绑定信息。
        public void UpdateTradingPoint(ConsoleMain.KChartTradingPointInfo tradingPoint)
        {
            // 调用方必须提供新的交易点信息。
            if (tradingPoint is null)
            {
                throw new ArgumentNullException(nameof(tradingPoint));
            }

            // 防御性校验：
            // 如果当前账户已经有负 U，说明它只能是收割点账户；
            // 这时不允许把它更新成采购点，否则会违反“采购点 U 不能为负数”的规则。
            if (_uBalance < 0 && tradingPoint.PointKind != ConsoleMain.KChartTradingPointKind.Harvest)
            {
                throw new InvalidOperationException("Only harvest account can keep negative U balance.");
            }

            // 校验通过后，更新账户绑定的交易点。
            _tradingPoint = tradingPoint;
        }
    }
}
