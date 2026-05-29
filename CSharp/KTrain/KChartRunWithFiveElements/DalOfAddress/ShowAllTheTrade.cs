using CommonClass;
using MySql.Data.MySqlClient;

namespace DalOfAddress
{
    public class ShowAllTheTrade
    {
        const int PreviousListCount = 52;
        public static void Find(WriteMaterialD WriteMaterial)
        {
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    var all = DalOfAddress.tradevalue.GetAll(con, tran);
                    for (int i = 0; i < all.Count; i++)
                    {
                        var item = all[i];
                        var previousList = DalOfAddress.DALHourRecord.GetPrevious(item.baseHourRecord, PreviousListCount, tran, con);
                        if (previousList.Count == PreviousListCount)
                        {
                            for (var indexOfPList = 0; indexOfPList < previousList.Count; indexOfPList++)
                            {
                                if (previousList[indexOfPList].dateTime.AddHours(indexOfPList + 1) == item.baseHourRecord) { }
                                else
                                {
                                    throw new Exception("");
                                }
                            }
                            WriteMaterial(item, previousList);
                        }
                        else
                        {
                            continue;
                        }
                    }
                    // tran.Commit();
                }
            }
        }

        public delegate void WriteMaterialD(TradeValueItem item, List<dataItem> previousList);
    }
}
