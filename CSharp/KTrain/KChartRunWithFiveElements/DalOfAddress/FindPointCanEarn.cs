using CommonClass;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Math.EC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DalOfAddress
{
    public class FindPointCanEarn
    {
        public const int NextListCount = 50;//50以后就不变了。这个是常量。策略常量。
        public delegate List<decimal> GetPointCanSetToSellD(dataItem item, List<dataItem> after48);
        public delegate decimal RepurchaseStrategyD(decimal sellPrice);
        public delegate void GetPointD(dataItem item, out List<decimal> pointCanTrade, out List<decimal> pointCanNotTrade);
        public static void Find(ref int sumpoint, ref int successPoint, ref int failPoint, GetPointCanSetToSellD GetPointCanSetToSell, RepurchaseStrategyD RepurchaseStrategy, GetPointD GetPoint)
        {
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    DateTime minDateTime, maxDateTime;
                    tradevalue.GetLimiteValue(tran, con, out minDateTime, out maxDateTime);
                    var all = DalOfAddress.DALHourRecord.GetAll(con, tran);
                    for (int i = 0; i < all.Count; i++)
                    {
                        var item = all[i];
                        if (item.dateTime > minDateTime.AddHours(1) && item.dateTime < maxDateTime.AddHours(-1))
                        {
                            continue;
                        }
                        else
                        {
                            var afterList = DalOfAddress.DALHourRecord.GetAfter(item, NextListCount, tran, con);
                            if (afterList.Count == NextListCount)
                            {
                                List<decimal> SellPointCanTrade = GetPointCanSetToSell(item, afterList);

                                for (int indexOfPoint = 0; indexOfPoint < SellPointCanTrade.Count; indexOfPoint++)
                                {
                                    var sellPrice = Math.Round(SellPointCanTrade[indexOfPoint], 2);
                                    //  var buyPrice =  Math.Round(sellPrice * 35 / 36, 2);
                                    var buyPrice = RepurchaseStrategy(sellPrice);
                                    bool sellSuccess = false;
                                    bool buySuccess = false;
                                    for (int indexOfNextListCount = 0; indexOfNextListCount < NextListCount; indexOfNextListCount++)
                                    {
                                        var nextItem = afterList[indexOfNextListCount];
                                        if ((nextItem.dateTime - item.dateTime).TotalHours != 1 + indexOfNextListCount)
                                        {
                                            Console.WriteLine($"(nextItem.dateTime - item.dateTime).TotalHours={(nextItem.dateTime - item.dateTime).TotalHours}");
                                            throw new Exception("数据有误！");
                                        }
                                        List<decimal> nextPointCanTrade, nextPointCanNotTrade;
                                        GetPoint(nextItem, out nextPointCanTrade, out nextPointCanNotTrade);
                                        for (int indexOfNextPoint = 0; indexOfNextPoint < nextPointCanTrade.Count; indexOfNextPoint++)
                                        {
                                            if (!sellSuccess)
                                            {
                                                //出售比特币过程。即交易的金额要大于挂单的金额。
                                                if (nextPointCanTrade[indexOfNextPoint] > sellPrice)
                                                {
                                                    sellSuccess = true;
                                                    break;
                                                }
                                            }
                                            else if (!buySuccess)
                                            {
                                                if (nextPointCanTrade[indexOfNextPoint] < buyPrice)
                                                {
                                                    buySuccess = true;
                                                    break;
                                                }
                                            }
                                        }
                                        if (sellSuccess && buySuccess)
                                        {
                                            break;
                                        }
                                    }

                                    if (sellSuccess && buySuccess)
                                    {
                                        CommonClass.TradeValueItem p = new TradeValueItem()
                                        {
                                            baseHourRecord = item.dateTime,
                                            sellPrice = sellPrice,
                                            buyPrice = buyPrice,
                                            tradeSuccess = 1
                                        };
                                        var insertSuccess = DalOfAddress.tradevalue.Insert(p, tran, con);
                                        if (insertSuccess)
                                        {
                                            sumpoint++;
                                            successPoint++;
                                        }
                                    }
                                    else
                                    {
                                        CommonClass.TradeValueItem p = new TradeValueItem()
                                        {
                                            baseHourRecord = item.dateTime,
                                            sellPrice = sellPrice,
                                            buyPrice = buyPrice,
                                            tradeSuccess = 0
                                        };
                                        var insertSuccess = DalOfAddress.tradevalue.Insert(p, tran, con);
                                        if (insertSuccess)
                                        {
                                            sumpoint++;
                                            failPoint++;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }
                    }
                    tran.Commit();
                }
            }
        }
    }
}
