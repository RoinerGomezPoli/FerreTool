using FerreTools.Entities.Extensions;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Transactions;

namespace FerreTools.Data.Connection
{
    public static class connection
    {
        private static TransactionScope transaction { get; set; }
        private static string GetConnectionString()
        {
            string appsetting = ConfigurationManager.AppSettings["defaultConnectionString"].ToString();
            ConnectionStringSettings connectionStringSettings = ConfigurationManager.ConnectionStrings[appsetting];
            string connectionstring = connectionStringSettings.ConnectionString;
            return connectionstring;
        }
        private static DataSet Execute(string sentence, Dictionary<string, object> parameters)
        {
            // Crear conexión
            //beginTransaction(true);
            string stringConnection = GetConnectionString();
            DataSet data = new DataSet();
            SqlConnection connection = new SqlConnection(stringConnection);
            connection.Open();
            SqlCommand command = new SqlCommand(sentence, connection);
            PrepareParameters(command, parameters);
            SqlDataAdapter adapter = new SqlDataAdapter(command);
            try
            {
                adapter.Fill(data);
                connection.Close();
            }
            catch (Exception ex)
            {
                connection.Close();
                disposeTransaction();
                registerException(ex.Message);
            }
            return data;
        }

        private static void PrepareParameters(SqlCommand command, Dictionary<string, object> parameters)
        {
            foreach (var key in parameters)
            {
                SqlParameter parameter = command.CreateParameter();
                parameter.Direction = ParameterDirection.Input;
                parameter.ParameterName = key.Key;
                object value = key.Value;
                if (value != null)
                {
                    if (key.Value.GetType() == typeof(DbTypeSql))
                    {
                        DbTypeSql dbTypeApi = (DbTypeSql)value;
                        value = dbTypeApi.Value;
                        parameter.SqlDbType = dbTypeApi.SqlDbType;
                    }
                }
                parameter.SqlValue = (value == null ? Convert.DBNull : value);
                command.Parameters.Add(parameter);
            }
        }

        public static DataTable GetDataTable(string sentence, Dictionary<string, object> parameters)
        {
            DataSet result = Execute(sentence, parameters);
            return result.Tables[0];
        }

        public static IEnumerable<T> GetList<T>(string sentence, Dictionary<string, object> parameters) where T : new()
        {
            DataSet result = Execute(sentence, parameters);
            IEnumerable<T> response = result.Tables[0].Rows.OfType<DataRow>().Select(s => s.AsEntity<T>());
            return response;
        }

        public static T ExecuteEscalar<T>(string sentence, Dictionary<string, object> parameters)
        {
            DataSet result = Execute(sentence, parameters);
            object value = result.Tables[result.Tables.Count - 1].Rows[0][0];
            var type = typeof(T);
            T response = default(T);
            if (type == typeof(float))
                response = (T)(object)Convert.ToSingle(value);
            else if (type == typeof(int))
                response = (T)(object)Convert.ToInt32(value);
            else if (type == typeof(long))
                response = (T)(object)Convert.ToInt64(value);
            else if (type == typeof(decimal))
                response = (T)(object)Convert.ToDecimal(value);
            else if (type == typeof(string))
                response = (T)(object)Convert.ToString(value);
            else if (type == typeof(bool))
                response = (T)(object)Convert.ToBoolean(value);
            else if (type == typeof(DateTime))
                response = (T)(object)Convert.ToDateTime(value);
            return response;
        }

        public static void ExecuteNonQuery(string sentence, Dictionary<string, object> parameters)
        {
            DataSet result = Execute(sentence, parameters);
        }
        public static int ExecuteNonQueryDt(string sentence, Dictionary<string, object> parameters)
        {
            var code = 0;
            DataSet result = Execute(sentence, parameters);
            if (result.Tables.Count > 0)
            {
                DataTable dt = result.Tables[0];
                if (dt.Rows.Count > 0)
                {
                    code = Convert.ToInt32(dt.Rows[0]["CodAuditoria"].ToString());
                }

            }
            return code;
        }

        public static bool BulkCopy(DataTable data, string table)
        {
            data.TableName = table;
            SqlBulkCopy bulkcopy = new SqlBulkCopy(GetConnectionString());
            data.Columns.OfType<DataColumn>().ToList().ForEach(f => bulkcopy.ColumnMappings.Add(f.ColumnName, f.ColumnName));
            try
            {
                bulkcopy.BatchSize = 10000;
                bulkcopy.BulkCopyTimeout = 0;
                bulkcopy.DestinationTableName = data.TableName;
                bulkcopy.WriteToServer(data);
                bulkcopy.Close();
            }
            catch (Exception ex)
            {
                registerException(ex.Message);
                return false;
            }
            return true;
        }

        public static void InsertEntity<T>(T entity, string table = "") where T : class
        {
            string campo = "";
            string value = "";
            string tableName = (table == "") ? entity.GetType().Name : table;
            PropertyInfo[] properties = entity.GetType().GetProperties();
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            properties.ToList().ForEach(f =>
            {
                CustomAttributeData attribute = f.CustomAttributes.Where(w => w.AttributeType == typeof(NoIncludeColumnSqlAttribute)).FirstOrDefault();
                if (attribute != null)
                {
                    return;
                }
                parameters.Add($"@{f.Name}", f.GetValue(entity));
                campo += ((campo == "") ? "" : ",") + $"{f.Name}";
                value += ((value == "") ? "" : ",") + $"@{f.Name}";
            });
            ExecuteNonQuery($"INSERT INTO {tableName}({campo}) VALUES({value})", parameters);
        }

        public static DateTime GetServerDate()
        {
            DateTime serverDate = ExecuteEscalar<DateTime>("SELECT GETDATE()", new Dictionary<string, object>());
            return serverDate;
        }
        public static DataTable QuerySql(string Query)
        {
            try
            {

                string stringConnection = GetConnectionString();
                DataTable data = new DataTable();
                SqlConnection connection = new SqlConnection(stringConnection);
                connection.Open();
                SqlCommand command = new SqlCommand(Query, connection);
                SqlDataAdapter adapter = new SqlDataAdapter(command);
                try
                {
                    adapter.Fill(data);
                    connection.Close();
                }
                catch (Exception ex)
                {
                    connection.Close();
                    registerException(ex.Message);
                }
                return data;

            }
            catch (Exception ex)
            {
                registerException(ex.Message);
            }

            return null;
        }
        public static DataTable StoreProcedure(string name, Dictionary<string, object> parameters)
        {
            try
            {
                //beginTransaction((Transaction.Current == null && startTransactionProcedure(name)));
                return ExecSP(name, parameters);
            }
            catch (Exception ex)
            {
                DataTable error = new DataTable("Error");
                error.Columns.Add("CatchError");
                error.Rows.Add(ex.Message);
                registerException(ex.Message);
                return error;
            }
        }

        internal static DataTable ExecSP(string sStoredProcedure, Dictionary<string, object> dParams)
        {
            string stringConnection = GetConnectionString();
            DataTable data = new DataTable();
            SqlConnection connection = new SqlConnection(stringConnection);
            SqlCommand command = new SqlCommand();
            command.CommandText = sStoredProcedure;
            command.CommandType = CommandType.StoredProcedure;
            command.Connection = connection;
            SqlDataAdapter adapter = new SqlDataAdapter(command);
            PrepareParameters(command, dParams);
            try
            {
                adapter.Fill(data);
                return data;
            }
            catch (Exception ex)
            {
                registerException(ex.Message);
                return data;
            }
        }
        private static void registerException(string error)
        {
            try
            {
                string path = AppDomain.CurrentDomain.BaseDirectory;
                System.IO.StreamWriter wr = File.AppendText(path + "\\logCuentasMedica");
                string line = "\n[Data base]\n[" + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + "][Conection]\t" + error + "\r\n" +
                    "-----------------------------------------------------------------------------------------------------------------------------------------";
                wr.WriteLine(line);
                wr.Close();
            }
            catch
            {
            }
        }

        public static void beginTransaction(bool isTransaction)
        {
            if (!isTransaction)
                return;

            if (Transaction.Current != null)
            {
                if (transaction != null)
                    return;
                transaction = new TransactionScope(Transaction.Current);
                return;
            }

            transaction = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.RepeatableRead, Timeout = new TimeSpan(1, 0, 0) });
        }

        public static void endTransaction()
        {
            if (Transaction.Current == null)
                return;
            try
            {
                getTransaction();
                Transaction.Current.Rollback();
                Transaction.Current.Dispose();
                transaction.Dispose();
                transaction = null;
            }
            catch (Exception ex)
            {
                registerException(ex.Message);
            }

        }

        public static void commitTransaction()
        {
            try
            {
                getTransaction();
                transaction.Complete();
                transaction.Dispose();
                transaction = null;
            }
            catch (Exception ex)
            {
                disposeTransaction();
                registerException(ex.Message);
            }

        }

        public static void disposeTransaction()
        {
            endTransaction();
        }

        private static void getTransaction()
        {
            if (Transaction.Current != null)
            {
                if (transaction == null)
                    transaction = new TransactionScope(Transaction.Current);
            }
        }

        private static T AsEntity<T>(this DataRow row) where T : new()
        {
            T obj = new T();
            List<string> columns = row.Table.Columns.OfType<DataColumn>().Select(s => s.ColumnName.Replace("_", "").ToLower()).ToList();
            PropertyInfo[] properties = obj.GetType().GetProperties();
            properties.ToList().ForEach(f =>
            {
                string propertyName = f.Name;
                if (!columns.Contains(propertyName.ToLower()))
                    return;
                int indexColumn = columns.FindIndex(fi => fi == propertyName.Replace("_", "").ToLower());
                string column = row.Table.Columns[indexColumn].ColumnName;
                object value = row[column];
                SetPropertyValue(f, value, obj);
            });
            return obj;
        }
        private static void SetPropertyValue(this PropertyInfo property, object value, object obj)
        {
            Type type = property.PropertyType;
            Type under = Nullable.GetUnderlyingType(type);
            if (under != null)
            {
                if (Convert.IsDBNull(value))
                {
                    property.SetValue(obj, null);
                    return;
                }
                type = under;
            }
            if (property.SetMethod == null)
            {
                return;
            }
            // Asignar valor segun su tipo
            if (type == typeof(int))
                property.SetValue(obj, Convert.ToInt32(((value ?? "0").ToString() == "") ? "0" : (value ?? "0").ToString()));
            else if (type == typeof(bool))
                property.SetValue(obj, Convert.ToBoolean(((value ?? "0").ToString() == "") ? "0" : (value ?? "0").ToString()));
            else if (type == typeof(float))
                property.SetValue(obj, float.Parse(((value ?? "0").ToString() == "") ? "0" : (value ?? "0").ToString()));
            else if (type == typeof(decimal))
                property.SetValue(obj, decimal.Parse(((value ?? "0").ToString() == "") ? "0" : (value ?? "0").ToString()));
            else if (type == typeof(long))
                property.SetValue(obj, long.Parse(((value ?? "0").ToString() == "") ? "0" : (value ?? "0").ToString()));
            else if (type == typeof(DateTime))
            {
                if (under != null)
                {
                    value = ((value ?? "").ToString() == "") ? null : value;
                    if (value != null)
                    {
                        value = Convert.ToDateTime(value);
                    }
                }
                else
                {
                    value = Convert.ToDateTime(value);
                }
                property.SetValue(obj, value);
            }
            else if (type == typeof(string))
                property.SetValue(obj, (Convert.IsDBNull(value) ? null : Convert.ToString(value)));
        }
    }
}
