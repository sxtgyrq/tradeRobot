using CommonClass;
using MySql.Data.MySqlClient;

namespace DalOfAddress
{
    public class DALfiveMinutesRecord
    {
        public static bool Insert(dataItem_5M data)
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
                                var nextItem = new dataItem_5M()
                                {
                                    dateTime = data.dateTime.AddMinutes(5)
                                };
                                var previous = new dataItem_5M()
                                {
                                    dateTime = data.dateTime.AddMinutes(-5)
                                };
                                if (exit(nextItem, tran, con) || exit(previous, tran, con))
                                {
                                    rowNum = InsertItem(data, tran, con);
                                }
                                else
                                {
                                    rowNum = 0;
                                }
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

        private static int InsertItem(dataItem_5M data, MySqlTransaction tran, MySqlConnection con)
        {
            int rowNum;
            string sQL = @"INSERT INTO fiveminutesrecord(recordDateTime,
openValue,
highValue,
lowValue,
closeValue,
volumeValue
) VALUES (@recordDateTime,
@openValue,
@highValue,
@lowValue,
@closeValue,
@volumeValue);";
            // long moneycount;
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {
                command.Parameters.AddWithValue("@recordDateTime", data.dateTime);
                command.Parameters.AddWithValue("@openValue", data.openValue);
                command.Parameters.AddWithValue("@highValue", data.highValue);
                command.Parameters.AddWithValue("@lowValue", data.lowValue);
                command.Parameters.AddWithValue("@closeValue", data.closeValue);
                command.Parameters.AddWithValue("@volumeValue", data.volumeValue);
                rowNum = command.ExecuteNonQuery();
            }
            return rowNum;
        }

        private static int updateItem(dataItem_5M data, MySqlTransaction tran, MySqlConnection con)
        {
            int rowNum;
            string sQL = @"UPDATE fiveminutesrecord SET openValue=@openValue,highValue=@highValue,lowValue=@lowValue,closeValue=@closeValue,volumeValue=@volumeValue WHERE recordDateTime=@recordDateTime";
            //                // long moneycount;
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {
                command.Parameters.AddWithValue("@openValue", data.openValue);
                command.Parameters.AddWithValue("@highValue", data.highValue);
                command.Parameters.AddWithValue("@lowValue", data.lowValue);
                command.Parameters.AddWithValue("@closeValue", data.closeValue);
                command.Parameters.AddWithValue("@volumeValue", data.volumeValue);
                command.Parameters.AddWithValue("@recordDateTime", data.dateTime);
                rowNum = command.ExecuteNonQuery();
            }
            return rowNum;
        }

        private static bool exit(dataItem_5M data, MySqlTransaction tran, MySqlConnection con)
        {
            var sQL = $"SELECT recordDateTime FROM fiveminutesrecord WHERE recordDateTime='{data.dateTime.ToString("yyyy-MM-dd HH:mm:ss")}'";
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

        public static bool Update(dataItem_5M data)
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
                                rowNum = updateItem(data, tran, con);
                            }
                            else
                            {
                                rowNum = 0;
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

        public static dataItem Get(dataItem data)
        {
            bool success;
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    try
                    {
                        if (data == null) { return null; }
                        else
                        {
                            var sQL = $"SELECT * FROM fiveminutesrecord WHERE recordDateTime='{data.dateTime.ToString("yyyy-MM-dd HH:mm:ss")}'";
                            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                            {

                                using (var reader = command.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        data.openValue = Convert.ToDecimal(reader["openValue"]);
                                        data.highValue = Convert.ToDecimal(reader["highValue"]);
                                        data.lowValue = Convert.ToDecimal(reader["lowValue"]);
                                        data.closeValue = Convert.ToDecimal(reader["closeValue"]);
                                        data.volumeValue = Convert.ToDecimal(reader["volumeValue"]);
                                    }
                                    else
                                    {
                                        data = null;
                                    }
                                }
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

            return data;

        }

        public static dataItem Get(DateTime inputDate, MySqlTransaction tran, MySqlConnection con)
        {
            dataItem data = new dataItem()
            {
                dateTime = inputDate
            };
            var sQL = $"SELECT * FROM fiveminutesrecord WHERE recordDateTime='{data.dateTime.ToString("yyyy-MM-dd HH:mm:ss")}'";
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        data.openValue = Convert.ToDecimal(reader["openValue"]);
                        data.highValue = Convert.ToDecimal(reader["highValue"]);
                        data.lowValue = Convert.ToDecimal(reader["lowValue"]);
                        data.closeValue = Convert.ToDecimal(reader["closeValue"]);
                        data.volumeValue = Convert.ToDecimal(reader["volumeValue"]);
                    }
                    else
                    {
                        data = null;
                    }
                }
            }
            return data;
        }

        public static List<dataItem> GetList(dataItem inputData)
        {
            List<dataItem> datas = new List<dataItem>();
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    try
                    {
                        if (inputData == null) { return datas; }
                        else
                        {
                            var dataParameter = inputData.dateTime.Date;
                            var endData = dataParameter.AddHours(24);
                            var sQL = $"SELECT * FROM fiveminutesrecord WHERE recordDateTime>='{dataParameter.ToString("yyyy-MM-dd HH:mm:ss")}' and recordDateTime<'{endData.ToString("yyyy-MM-dd HH:mm:ss")}' order by recordDateTime";
                            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                            {

                                using (var reader = command.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        datas.Add(new dataItem()
                                        {
                                            openValue = Convert.ToDecimal(reader["openValue"]),
                                            highValue = Convert.ToDecimal(reader["highValue"]),
                                            lowValue = Convert.ToDecimal(reader["lowValue"]),
                                            closeValue = Convert.ToDecimal(reader["closeValue"]),
                                            volumeValue = Convert.ToDecimal(reader["volumeValue"]),
                                            dateTime = Convert.ToDateTime(reader["recordDateTime"])
                                        });
                                    }

                                }
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

            return datas;
        }

        public static List<dataItem_5M> GetAll(MySqlConnection con, MySqlTransaction tran)
        {
            List<dataItem_5M> datas = new List<dataItem_5M>();
            {
                var sQL = $"SELECT * FROM fiveminutesrecord order by recordDateTime";
                using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                {

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            datas.Add(new dataItem_5M()
                            {
                                openValue = Convert.ToDecimal(reader["openValue"]),
                                highValue = Convert.ToDecimal(reader["highValue"]),
                                lowValue = Convert.ToDecimal(reader["lowValue"]),
                                closeValue = Convert.ToDecimal(reader["closeValue"]),
                                volumeValue = Convert.ToDecimal(reader["volumeValue"]),
                                dateTime = Convert.ToDateTime(reader["recordDateTime"])
                            });
                        }

                    }
                }
            }
            return datas;
        }
        public static List<dataItem> GetAfter(dataItem conditionItem, int nextListCount, MySqlTransaction tran, MySqlConnection con)
        {
            var timeLimit = conditionItem.dateTime.AddMinutes(1);
            List<dataItem> datas = new List<dataItem>();
            //SELECT * FROM fiveminutesrecord WHERE recordDateTime>'{timeLimit.ToString("yyyy-MM-dd HH:mm:ss")}' order by recordDateTime ASC  LIMIT 0,48;
            var sQL = $"SELECT * FROM fiveminutesrecord WHERE recordDateTime>'{timeLimit.ToString("yyyy-MM-dd HH:mm:ss")}' order by recordDateTime ASC  LIMIT 0,{nextListCount};";
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        datas.Add(new dataItem()
                        {
                            openValue = Convert.ToDecimal(reader["openValue"]),
                            highValue = Convert.ToDecimal(reader["highValue"]),
                            lowValue = Convert.ToDecimal(reader["lowValue"]),
                            closeValue = Convert.ToDecimal(reader["closeValue"]),
                            volumeValue = Convert.ToDecimal(reader["volumeValue"]),
                            dateTime = Convert.ToDateTime(reader["recordDateTime"])
                        });
                    }

                }
            }
            return datas;
        }

        public static List<dataItem> GetPrevious(DateTime base5MRecord, int previousListCount, MySqlTransaction tran, MySqlConnection con)
        {
            var timeLimit = base5MRecord.AddMinutes(-1);
            List<dataItem> datas = new List<dataItem>();
            //SELECT * FROM hourrecord WHERE recordDateTime>'{timeLimit.ToString("yyyy-MM-dd HH:mm:ss")}' order by recordDateTime ASC  LIMIT 0,48;
            var sQL = $"SELECT * FROM fiveminutesrecord WHERE recordDateTime<'{timeLimit.ToString("yyyy-MM-dd HH:mm:ss")}' order by recordDateTime DESC LIMIT 0,{previousListCount};";
            using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        datas.Add(new dataItem()
                        {
                            openValue = Convert.ToDecimal(reader["openValue"]),
                            highValue = Convert.ToDecimal(reader["highValue"]),
                            lowValue = Convert.ToDecimal(reader["lowValue"]),
                            closeValue = Convert.ToDecimal(reader["closeValue"]),
                            volumeValue = Convert.ToDecimal(reader["volumeValue"]),
                            dateTime = Convert.ToDateTime(reader["recordDateTime"])
                        });
                    }

                }
            }
            return datas;
        }

        public static List<dataItem_5M> GetAll()
        {
            List<dataItem_5M> result;
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
    }
}
