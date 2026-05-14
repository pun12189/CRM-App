using CallMan.Data;
using CallMan.Interfaces;
using CallMan.Models.Enums;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class ImportService : IImportService
    {
        private readonly CrmDbContext _context;
        private readonly IUserSession _session;
        public ImportService(CrmDbContext context, IUserSession session)
        {
            _context = context;
            _session = session;
        }

        public async Task<int> BulkInsertAsync(IEnumerable<dynamic> data, ImportType type)
        {
            if (!data.Any()) return 0;

            using var conn = _context.CreateConnection();
            using var transaction = conn.BeginTransaction();
            try
            {
                var dataList = data.Cast<IDictionary<string, object>>().ToList();
                var firstRow = dataList.First();

                // Generate Column Names and Parameter Placeholders
                string columns = string.Join(", ", firstRow.Keys);
                string parameters = string.Join(", ", firstRow.Keys.Select(k => "@" + k));

                // Generate the Update clause for duplicates
                string updates = string.Join(", ", firstRow.Keys.Select(k => $"{k}=VALUES({k})"));

                string tableName = type == ImportType.Product ? "Products" : "Leads";

                string sql = $@"
            INSERT INTO {tableName} ({columns}, CreatedAt) 
            VALUES ({parameters}, NOW()) 
            ON DUPLICATE KEY UPDATE {updates}, LeadId = LAST_INSERT_ID(LeadId);";

                var affectedRows = await conn.ExecuteAsync(sql, dataList, transaction);

                if (type == ImportType.Lead)
                {
                    await InsertBulkHistory(conn, transaction, dataList);
                }

                transaction.Commit();
                return affectedRows;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return 0;
            }
            
        }

        private async Task InsertBulkHistory(IDbConnection conn, IDbTransaction trans, List<IDictionary<string, object>> dataList)
        {
            // We need to match the Excel data back to the DB to get the LeadIds
            // The most reliable way is by Phone Number since that is usually your Unique/Primary key
            string historySql = $@"
        INSERT INTO LeadHistory (LeadId, LogDate, Message, FollowupStage, UpdatedBy)
        SELECT LeadId, NOW(), '{_session.CurrentUser} uploaded this Lead', 'Lead Imported', @UpdatedBy
        FROM Leads 
        WHERE Phone IN @Phones";

            var phones = dataList.Select(d => d["Phone"].ToString()).ToList();
            var updatedBy = _session.CurrentUser;
            await conn.ExecuteAsync(historySql, new { Phones = phones, UpdatedBy = updatedBy }, trans);
        }
    }
}
