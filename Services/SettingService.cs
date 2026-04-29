using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class SettingService
    {
        private readonly CrmDbContext _context;
        public SettingService(CrmDbContext context) => _context = context;

        // --- GENERIC SETTINGS METHODS ---

        // 1. Get all items from a specific setting table
        public async Task<IEnumerable<SettingItem>> GetSettingsAsync(string tableName)
        {
            using var db = _context.CreateConnection();
            // Use the generic 'Name' property regardless of actual DB column name
            string sql = $@"SELECT Id, {tableName.Replace("Lead", "")}Name as Name, 0 as TotalLeads 
                   FROM {tableName}";

            return await db.QueryAsync<SettingItem>(sql);
        }

        // 2. Add a new setting item
        public async Task<int> CreateSettingAsync(string tableName, string itemName)
        {
            using var db = _context.CreateConnection();
            // Get the specific column name based on the table name
            string colName = $"{tableName.Replace("Lead", "")}Name";

            string sql = $"INSERT INTO {tableName} ({colName}) VALUES (@itemName); SELECT LAST_INSERT_ID();";

            return await db.QuerySingleAsync<int>(sql, new { itemName });
        }

        // 3. Update existing setting item
        public async Task<bool> UpdateSettingAsync(string tableName, SettingItem item)
        {
            using var db = _context.CreateConnection();
            string colName = $"{tableName.Replace("Lead", "")}Name";

            string sql = $"UPDATE {tableName} SET {colName} = @Name WHERE Id = @Id";

            int affected = await db.ExecuteAsync(sql, item);
            return affected > 0;
        }

        // 4. Delete setting item (Hard Delete as seen in image)
        public async Task<bool> DeleteSettingAsync(string tableName, int id)
        {
            using var db = _context.CreateConnection();
            string sql = $"DELETE FROM {tableName} WHERE Id = @id";

            int affected = await db.ExecuteAsync(sql, new { id });
            return affected > 0;
        }
    }
}
