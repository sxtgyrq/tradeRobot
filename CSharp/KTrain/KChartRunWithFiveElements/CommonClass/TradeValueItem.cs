using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonClass
{
    public class TradeValueItem
    {
        public decimal sellPrice { get; set; }
        public decimal buyPrice { get; set; }
        /// <summary>
        /// 0，代表失败，1代表成功
        /// </summary>
        public int tradeSuccess { get; set; }
        public DateTime baseHourRecord { get; set; }

    }
}
