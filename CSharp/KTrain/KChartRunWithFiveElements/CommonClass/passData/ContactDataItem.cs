using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CommonClass.passData
{

    public class CoinType
    {
        public CoinType(string name_)
        {
            this.name = name_;
            this.kaicang = string.Empty;
            this.qiangping = string.Empty;
            this.zhiying = string.Empty;
            this.IsError = true;
        }
        public string kaicang { get; set; }
        public string zhiying { get; set; }
        public string qiangping { get; set; }
        public string pos { get; set; }
        public string MarkPrice { get; set; }

        public string name { get; private set; }
        public bool IsError { get; set; }
    }
    public class ContactDataItem
    {
        public string Account { get; set; }
        public CoinType USDT { get; set; }
        public CoinType USDC { get; set; }
        public CoinType USD { get; set; }
       // public string MarkPrice { get; set; }
        public bool IsError
        {
            get
            {
                return this.USDT.IsError || this.USDC.IsError;
            }
        }
    }

    public class ContactDanger
    {
        public string Account { get; set; }
        public bool LongDanger { get; set; }
        public bool ShortDanger { get; set; }
        public string LongValue { get; set; }
        public string ShortValue { get; set; }
    }
}
