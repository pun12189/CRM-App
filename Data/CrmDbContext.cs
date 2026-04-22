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

        public CrmDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Centralized method to get a connection
        public IDbConnection CreateConnection()
            => new MySqlConnection(_connectionString);

        // You can add common DB logic here later, 
        // like HealthChecks or Migrations.
    }
}
