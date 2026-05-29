using CommonClass;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DalOfAddress
{
    public class ContactDetailDAL
    {
        public static bool Insert(contactdetail data)
        {
            bool success;
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    try
                    {
                        if (data == null) { success = false; }
                        else
                        {
                            int rowNum;
                            if (exit(data, tran, con))
                            {
                                rowNum = 0;
                                //  rowNum = updateItem(data, tran, con);
                            }
                            else
                            {
                                rowNum = InsertItem(data, tran, con);
                            }
                            if (rowNum == 1)
                            {
                                tran.Commit();
                                success = true;
                            }
                            else
                            {
                                tran.Rollback();
                                success = false;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        throw e;
                        throw new Exception("新增错误");
                    }
                }
            }

            return success;
        }

        private static int InsertItem(contactdetail data, MySqlTransaction tran, MySqlConnection con)
        {
            int rowNum;
            string sQL = @"INSERT INTO contactdetail(uTime,
avgPx,
adviseTime,
subAcct,
instId,
pnl,
ccy
) VALUES (@uTime,
@avgPx,
@adviseTime,
@subAcct,
@instId,
@pnl,
@ccy);";
            // long moneycount;
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {
                command.Parameters.AddWithValue("@uTime", data.uTime);
                command.Parameters.AddWithValue("@avgPx", data.avgPx);
                command.Parameters.AddWithValue("@adviseTime", data.adviseTime);
                command.Parameters.AddWithValue("@subAcct", data.subAcct);
                command.Parameters.AddWithValue("@instId", data.instId);
                command.Parameters.AddWithValue("@pnl", data.pnl);
                command.Parameters.AddWithValue("@ccy", data.ccy);
                rowNum = command.ExecuteNonQuery();
            }
            return rowNum;
        }

        private static bool exit(contactdetail data, MySqlTransaction tran, MySqlConnection con)
        {
            var sQL = $"SELECT uTime FROM contactdetail WHERE uTime={data.uTime}";
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {

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
    }
}
