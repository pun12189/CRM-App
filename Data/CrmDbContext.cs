using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Data
{
    public class CrmDbContext
    {
        private readonly string _connectionString;
        private readonly string _activationString;

        public CrmDbContext(string connectionString)
        {
            _connectionString = connectionString;
            _activationString = "Server=82.29.166.165;Port=3309;Uid=root;Pwd=sofric@123;database=tijorirecords;SSLMode=Required;";
        }

        // Centralized method to get a connection
        public IDbConnection CreateConnection()
        {
            var conn = new MySqlConnection(_connectionString);
            conn.Open();

            // Force the session to IST (India Standard Time)
            using (var cmd = new MySqlCommand("SET time_zone = '+05:30';", conn))
            {
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        public IDbConnection Connection()
        {
            var conn = new MySqlConnection(_activationString);
            conn.Open();

            // Force the session to IST (India Standard Time)
            using (var cmd = new MySqlCommand("SET time_zone = '+05:30';", conn))
            {
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        // You can add common DB logic here later, 
        // like HealthChecks or Migrations.
    }
}
