using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonClass
{
    public class EuclideanItem
    {
        public DateTime EDateTime { get; set; }
        public int MaterialHour { get; set; }
        public decimal AdviceSellPrice { get; set; }
        public decimal AdviceBuyPrice { get; set; }

        public decimal AdviceSellMaxPrice { get; set; }
        public decimal AdviceSellMinPrices { get; set; }
        public double EuclideanDelta { get; set; }

        public bool AdviseToSell { get; set; }

        public double PriceCorssValue { get; set; }
        public double WeightCrossValue { get; set; }
        public double WeightMultiplyPriceCrossValue { get; set; }

        public double MaxMinusMin { get; set; }

        public int NumInMH { get; set; }
    }
}
