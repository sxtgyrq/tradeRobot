using CommonClass;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DalOfAddress
{
    public class ContactAdviseDAL
    {

        public static bool Insert(contactadvise data)
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

        private static int InsertItem(contactadvise data, MySqlTransaction tran, MySqlConnection con)
        {
            int rowNum;
            string sQL = @"INSERT INTO contactadvise(recordTime,
isSingleLong,
isCrossLong,
isSingleShort,
isCrossShort
) VALUES (@recordTime,
@isSingleLong,
@isCrossLong,
@isSingleShort,
@isCrossShort);";
            // long moneycount;
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {
                command.Parameters.AddWithValue("@recordTime", data.recordTime);
                command.Parameters.AddWithValue("@isSingleLong", data.isSingleLong);
                command.Parameters.AddWithValue("@isCrossLong", data.isCrossLong);
                command.Parameters.AddWithValue("@isSingleShort", data.isSingleShort);
                command.Parameters.AddWithValue("@isCrossShort", data.isCrossShort); 
                rowNum = command.ExecuteNonQuery();
            }
            return rowNum;
        }

        private static bool exit(contactadvise data, MySqlTransaction tran, MySqlConnection con)
        {
            var sQL = $"SELECT recordTime FROM contactadvise WHERE recordTime='{data.recordTime.ToString("yyyy-MM-dd HH:mm:ss")}'";
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
