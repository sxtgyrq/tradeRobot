
using CommonClass;
using MySql.Data.MySqlClient;

namespace DalOfAddress
{
    public class ShowAllTheTimePoint
    {
        const int PreviousListCount = 52;
        public static void Find(DateTime minDateTime, DateTime maxDateTime, DataDealWithAndSaveD DataDealWithAndSave)
        {

            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    var allItemOfTrade = DalOfAddress.tradevalue.GetAll(con, tran);
                    for (DateTime timeOfRun = minDateTime; timeOfRun <= maxDateTime; timeOfRun = timeOfRun.AddHours(1))
                    {
                        var hourRecord = DALHourRecord.Get(timeOfRun, tran, con);
                        if (hourRecord == null)
                        {
                            throw new Exception("");
                        }
                        var previousList = DalOfAddress.DALHourRecord.GetPrevious(hourRecord.dateTime, PreviousListCount, tran, con);
                        if (previousList.Count == PreviousListCount)
                        {
                            DataDealWithAndSave(allItemOfTrade, hourRecord, previousList);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    //var all = DalOfAddress.tradevalue.GetAll(con, tran);
                    //for (int i = 0; i < all.Count; i++)
                    //{
                    //    var item = all[i];
                    //    var previousList = DalOfAddress.DALHourRecord.GetPrevious(item.baseHourRecord, PreviousListCount, tran, con);
                    //    if (previousList.Count == PreviousListCount)
                    //    {
                    //        for (var indexOfPList = 0; indexOfPList < previousList.Count; indexOfPList++)
                    //        {
                    //            if (previousList[indexOfPList].dateTime.AddHours(indexOfPList + 1) == item.baseHourRecord) { }
                    //            else
                    //            {
                    //                throw new Exception("");
                    //            }
                    //        }
                    //        WriteMaterial(item, previousList);
                    //    }
                    //    else
                    //    {
                    //        continue;
                    //    }
                    //}
                    // tran.Commit();
                }
            }
        }

        public delegate void DataDealWithAndSaveD(List<TradeValueItem> allItemOfTrade, dataItem hourRecord, List<dataItem> previousList);
    }
}
