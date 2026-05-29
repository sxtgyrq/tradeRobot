using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonClass
{
    public class dataItemBase
    {
        public decimal openValue { get; set; }
        public decimal highValue { get; set; }
        public decimal lowValue { get; set; }
        public decimal closeValue { get; set; }
        public decimal volumeValue { get; set; }

        protected DateTime dateTime_Value;


        public string dateTimeStr
        {
            get
            {
                return this.dateTime_Value.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        //  "closeValue":4,"volumeValue":5,"dateTime":"2024-11-21T22:26:00"}
    }
    public class dataItem : dataItemBase
    {

        public DateTime dateTime
        {
            get
            {
                return this.dateTime_Value;
            }
            set
            {
                this.dateTime_Value = new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0);
            }
        }
        //  "closeValue":4,"volumeValue":5,"dateTime":"2024-11-21T22:26:00"}
    }

    public class dataItem_5M : dataItemBase
    {
        public DateTime dateTime
        {
            get
            {
                return this.dateTime_Value;
            }
            set
            {
                this.dateTime_Value = new DateTime(value.Year, value.Month, value.Day, value.Hour, (value.Minute / 5) * 5, 0);
            }
        }
    }
}
