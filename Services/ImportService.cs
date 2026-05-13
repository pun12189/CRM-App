using CallMan.Data;
using CallMan.Interfaces;
using CallMan.Models.Enums;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class ImportService : IImportService
    {
        private readonly CrmDbContext _context;
        public ImportService(CrmDbContext context) => _context = context;

        public async Task<int> BulkInsertAsync(IEnumerable<dynamic> data, ImportType type)
        {
            if (!data.Any()) return 0;

            using var conn = _context.CreateConnection();
            using var transaction = conn.BeginTransaction();
            try
            {
                var firstRow = data.First() as IDictionary<string, object>;

                // Generate Column Names and Parameter Placeholders
                string columns = string.Join(", ", firstRow.Keys);
                string parameters = string.Join(", ", firstRow.Keys.Select(k => "@" + k));

                // Generate the Update clause for duplicates
                string updates = string.Join(", ", firstRow.Keys.Select(k => $"{k}=VALUES({k})"));

                string tableName = type == ImportType.Product ? "Products" : "Leads";

                string sql = $@"
        INSERT INTO {tableName} ({columns}, CreatedAt) 
        VALUES ({parameters}, NOW()) 
        ON DUPLICATE KEY UPDATE {updates};";

                var affectedRows = await conn.ExecuteAsync(sql, data, transaction);
                transaction.Commit();
                return affectedRows;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return 0;
            }
            
        }
    }
}
