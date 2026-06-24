using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public class DbConfig
    {
        public string Server { get; set; } = "localhost";
        public uint Port { get; set; } = 3306;
        public string Database { get; set; } = "callmandev";
        public string UserId { get; set; } = "root";
        public string Password { get; set; } = "sofricdev";
        public bool UseSsl { get; set; }

        /// <summary>
        /// Assembles properties dynamically into a secure, production-ready MySQL connection string.
        /// </summary>
        public string ToConnectionString()
        {
            var builder = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder
            {
                Server = this.Server.Trim(),
                Port = this.Port,
                Database = this.Database.Trim(),
                UserID = this.UserId.Trim(),
                Password = this.Password,
                SslMode = this.UseSsl ? MySql.Data.MySqlClient.MySqlSslMode.Required : MySql.Data.MySqlClient.MySqlSslMode.Disabled
            };
            return builder.ConnectionString;
        }
    }
}
