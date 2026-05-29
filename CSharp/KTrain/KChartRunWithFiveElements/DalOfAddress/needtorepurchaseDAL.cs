using CommonClass;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using MySqlX.XDevAPI.Relational;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DalOfAddress
{
    public class needtorepurchaseDAL
    {
        public static List<CommonClass.needtorepurchase> GetAllNeedToRepurchase()
        {
            var result = new List<CommonClass.needtorepurchase>();
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    //  result = GetAll(con, tran);
                    var sQL = $"SELECT * FROM needtorepurchase  WHERE repurchaseSuccess=0;";
                    using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                                result.Add(new needtorepurchase()
                                {
                                    BTCValue = Convert.ToDecimal(reader["BTCValue"]),
                                    dateTimeApply = Convert.ToDateTime(reader["dateTimeApply"]),
                                    priceX = Convert.ToDecimal(reader["priceX"]),
                                    repurchasePrice = Convert.ToDecimal(reader["repurchasePrice"]),
                                    repurchaseSuccess = Convert.ToInt32(reader["repurchasePrice"]),
                                });
                        }
                    }
                }
            }
            return result;
        }

        public static int SetSuccess(needtorepurchase yyItem)
        {
            int rowNumber;
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    //  result = GetAll(con, tran);
                    var sQL = $"UPDATE needtorepurchase SET repurchaseSuccess=1 WHERE dateTimeApply='{yyItem.dateTimeApply.ToString("yyyy-MM-dd HH:mm:ss")}' AND priceX={yyItem.priceX.ToString("f1")} AND repurchaseSuccess=0 AND repurchasePrice={yyItem.repurchasePrice.ToString("f1")} AND BTCValue={yyItem.BTCValue.ToString("f8")}";
                    using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                    {
                        rowNumber = command.ExecuteNonQuery();
                    }
                    if (rowNumber == 1)
                    {
                        tran.Commit();
                    }
                    else
                    {
                        tran.Rollback();
                    }
                }
            }
            return rowNumber;


            //insert into
            /*
             * 
             * INSERT INTO needtorepurchase(priceX,
dateTimeApply,
repurchaseSuccess,
repurchasePrice,
BTCValue
) VALUES(97536.4,'2024-12-11 12:21:39',95416.03,0.002662)
             */
        }
        public static int SetFailed(needtorepurchase yyItem)
        {
            int rowNumber;
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    //  result = GetAll(con, tran);
                    var sQL = $"UPDATE needtorepurchase SET repurchaseSuccess=2 WHERE dateTimeApply='{yyItem.dateTimeApply.ToString("yyyy-MM-dd HH:mm:ss")}' AND priceX={yyItem.priceX.ToString("f1")} AND repurchaseSuccess=0 AND repurchasePrice={yyItem.repurchasePrice.ToString("f1")} AND BTCValue={yyItem.BTCValue.ToString("f8")}";
                    using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                    {
                        rowNumber = command.ExecuteNonQuery();
                    }
                    if (rowNumber == 1)
                    {
                        tran.Commit();
                    }
                    else
                    {
                        tran.Rollback();
                    }
                }
            }
            return rowNumber;


            //insert into
            /*
             * 
             * INSERT INTO needtorepurchase(priceX,
dateTimeApply,
repurchaseSuccess,
repurchasePrice,
BTCValue
) VALUES(97536.4,'2024-12-11 12:21:39',95416.03,0.002662)
             */
        }

        public static int IfNotExitInsert(needtorepurchase insertObj)
        {
            // var result = new List<CommonClass.needtorepurchase>();
            int row = 0;
            bool exit = false;
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    //  result = GetAll(con, tran);
                    var sQL = $"SELECT * FROM needtorepurchase  WHERE dateTimeApply='{insertObj.dateTimeApply.ToString("yyyy-MM-dd HH:mm:ss")}';";
                    using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                exit = true;
                            }
                            else
                            {
                                exit = false;
                            }
                        }
                    }
                    if (!exit)
                    {
                        sQL = $"INSERT INTO needtorepurchase (priceX, dateTimeApply, repurchaseSuccess, repurchasePrice, BTCValue) VALUES (@priceX,@dateTimeApply,@repurchaseSuccess,@repurchasePrice,@BTCValue);";
                        using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                        {
                            //@priceX, @dateTimeApply, @repurchaseSuccess, @repurchasePrice, @BTCValue
                            command.Parameters.AddWithValue("@priceX", insertObj.priceX);
                            command.Parameters.AddWithValue("@dateTimeApply", insertObj.dateTimeApply);
                            command.Parameters.AddWithValue("@repurchaseSuccess", insertObj.repurchaseSuccess);
                            command.Parameters.AddWithValue("@repurchasePrice", insertObj.repurchasePrice);
                            command.Parameters.AddWithValue("@BTCValue", insertObj.BTCValue);
                            if (command.ExecuteNonQuery() == 1)
                            {
                                row = 1;
                                tran.Commit();
                            }
                        }
                    }
                }
            }

            return row;
        }
    }

    public class debettobuycontactDAL
    {
        public static List<CommonClass.debettobuycontact> GetAllNeedToRepurchase()
        {
            var result = new List<CommonClass.debettobuycontact>();
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    //  result = GetAll(con, tran);
                    var sQL = $"SELECT * FROM debettobuycontact  WHERE repurchaseSuccess=0;";
                    using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                                result.Add(new debettobuycontact()
                                {
                                    BTCValue = Convert.ToDecimal(reader["BTCValue"]),
                                    debetToBuyContactIndex = Convert.ToInt32(reader["debetToBuyContactIndex"]),
                                    //  priceX = Convert.ToDecimal(reader["priceX"]),
                                    repurchasePrice = Convert.ToDecimal(reader["repurchasePrice"]),
                                    repurchaseSuccess = Convert.ToInt32(reader["repurchasePrice"]),
                                });
                        }
                    }
                }
            }
            return result;
        }

        public static int SetSuccess(debettobuycontact yyItem)
        {
            int rowNumber;
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    //  result = GetAll(con, tran);
                    var sQL = $"UPDATE debettobuycontact SET repurchaseSuccess=1 WHERE debetToBuyContactIndex={yyItem.debetToBuyContactIndex} AND repurchaseSuccess=0 AND repurchasePrice={yyItem.repurchasePrice.ToString("f1")} AND BTCValue={yyItem.BTCValue.ToString("f8")}";
                    using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                    {
                        rowNumber = command.ExecuteNonQuery();
                    }
                    if (rowNumber == 1)
                    {
                        tran.Commit();
                    }
                    else
                    {
                        tran.Rollback();
                    }
                }
            }
            return rowNumber;


            //insert into
            /*
             * 
             * INSERT INTO needtorepurchase(priceX,
dateTimeApply,
repurchaseSuccess,
repurchasePrice,
BTCValue
) VALUES(97536.4,'2024-12-11 12:21:39',95416.03,0.002662)
             */
        }
        public static int SetFailed(debettobuycontact yyItem)
        {
            int rowNumber;
            using (MySqlConnection con = new MySqlConnection(Connection.ConnectionStr))
            {
                con.Open();
                using (MySqlTransaction tran = con.BeginTransaction())
                {
                    //  result = GetAll(con, tran);
                    var sQL = $"UPDATE debettobuycontact SET repurchaseSuccess=2 WHERE debetToBuyContactIndex={yyItem.debetToBuyContactIndex} AND repurchaseSuccess=0 AND repurchasePrice={yyItem.repurchasePrice.ToString("f1")} AND BTCValue={yyItem.BTCValue.ToString("f8")}";
                    using (MySqlCommand command = new MySqlCommand(sQL, con, tran))
                    {
                        rowNumber = command.ExecuteNonQuery();
                    }
                    if (rowNumber == 1)
                    {
                        tran.Commit();
                    }
                    else
                    {
                        tran.Rollback();
                    }
                }
            }
            return rowNumber;


            //insert into
            /*
             * 
             * INSERT INTO needtorepurchase(priceX,
dateTimeApply,
repurchaseSuccess,
repurchasePrice,
BTCValue
) VALUES(97536.4,'2024-12-11 12:21:39',95416.03,0.002662)
             */
        }
    }
}
