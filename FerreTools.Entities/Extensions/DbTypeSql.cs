using System.Data;

namespace FerreTools.Entities.Extensions
{
    public class DbTypeSql
    {
        public SqlDbType SqlDbType { get; set; }
        public object Value { get; set; }

        public DbTypeSql(SqlDbType SqlDbType, object Value)
        {
            this.SqlDbType = SqlDbType;
            this.Value = Value;
        }
    }
}
