using CommonClass;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DalOfAddress
{
    public class tradevalue
    {
        public static bool Insert(TradeValueItem data, MySqlTransaction tran, MySqlConnection con)
        {
            bool success;
            int rowNum;
            if (exit(data, tran, con))
            {
                rowNum = 0;
            }
            else
            {
                rowNum = InsertItem(data, tran, con);
            }
            if (rowNum == 1)
            {
                success = true;
            }
            else
            {
                success = false;
            }
            return success;
        }

        private static int InsertItem(TradeValueItem data, MySqlTransaction tran, MySqlConnection con)
        {
            int rowNum;
            string sQL = @"INSERT INTO tradevalue(sellPrice,
buyPrice,
baseHourRecord,
tradeSuccess
) VALUES (@sellPrice,
@buyPrice,
@baseHourRecord,
@tradeSuccess);";
            // long moneycount;
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {
                command.Parameters.AddWithValue("@sellPrice", data.sellPrice);
                command.Parameters.AddWithValue("@buyPrice", data.buyPrice);
                command.Parameters.AddWithValue("@baseHourRecord", data.baseHourRecord);
                command.Parameters.AddWithValue("@tradeSuccess", data.tradeSuccess);
                rowNum = command.ExecuteNonQuery();
            }
            return rowNum;
        }

        private static bool exit(TradeValueItem data, MySqlTransaction tran, MySqlConnection con)
        {
            var sQL = $"SELECT * FROM tradevalue WHERE sellPrice=@sellPrice AND buyPrice=@buyPrice AND baseHourRecord=@baseHourRecord;";
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {
                command.Parameters.AddWithValue("@sellPrice", data.sellPrice);
                command.Parameters.AddWithValue("@buyPrice", data.buyPrice);
                command.Parameters.AddWithValue("@baseHourRecord", data.baseHourRecord);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        internal static List<TradeValueItem> GetAll(MySqlConnection con, MySqlTransaction tran)
        {
            List<TradeValueItem> result = new List<TradeValueItem>();
            var sQL = $"SELECT sellPrice,buyPrice,tradeSuccess,baseHourRecord FROM tradevalue ORDER BY baseHourRecord ASC,sellPrice ASC;";
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new TradeValueItem()
                        {
                            sellPrice = Convert.ToDecimal(reader["sellPrice"]),
                            buyPrice = Convert.ToDecimal(reader["buyPrice"]),
                            tradeSuccess = Convert.ToInt32(reader["tradeSuccess"]),
                            baseHourRecord = Convert.ToDateTime(reader["baseHourRecord"])
                        }); ;
                        //return true;
                    }
                }
            }
            return result;
        }

        public static List<TradeValueItem> GetAll()
        {
            List<TradeValueItem> result;
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    result = GetAll(con, tran);
                }
            }
            return result;
        }

        internal static void GetLimiteValue(MySqlTransaction tran, MySqlConnection con, out DateTime minDateTime, out DateTime maxDateTime)
        {
            var allItem = GetAll(con, tran);
            if (allItem.Count < 50)
            {
                minDateTime = DateTime.Now.AddDays(2);
                maxDateTime = DateTime.Now.AddDays(-2);
            }
            else
            {
                minDateTime = allItem[0].baseHourRecord;
                maxDateTime = allItem[allItem.Count - 1].baseHourRecord;
            }
        }

        public static void GetLimiteValue(out DateTime minDateTime, out DateTime maxDateTime)
        { 
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    GetLimiteValue(tran, con, out minDateTime, out maxDateTime);
                }
            } 
        }
    }
}
