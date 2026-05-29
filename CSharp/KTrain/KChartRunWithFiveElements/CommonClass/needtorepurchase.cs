using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonClass
{
    public class needtorepurchase
    {
        public decimal priceX { get; set; }
        public DateTime dateTimeApply { get; set; }
        public int repurchaseSuccess { get; set; }
        public decimal repurchasePrice { get; set; }
        public decimal BTCValue { get; set; }
    }
    public class debettobuycontact
    {
        //  public decimal priceX { get; set; }
        public int debetToBuyContactIndex { get; set; }
        public int repurchaseSuccess { get; set; }
        public decimal repurchasePrice { get; set; }
        public decimal BTCValue { get; set; }
    }
}
