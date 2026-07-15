using CallMan.Data;
using CallMan.Interfaces;
using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class GlobalSettingsService : IGlobalSettingsService
    {
        private readonly CrmDbContext _dbContext;

        public GlobalSettingsService(CrmDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> GetMaster2FAStatusAsync()
        {
            using (IDbConnection db = _dbContext.CreateConnection())
            {
                string sql = "SELECT SettingValue FROM GlobalSettings WHERE SettingKey = 'IsMaster2FAEnabled';";
                var result = await db.ExecuteScalarAsync<string>(sql);
                return bool.TryParse(result, out bool isEnabled) && isEnabled;
            }
        }

        public async Task UpdateMaster2FAStatusAsync(bool isEnabled)
        {
            using (IDbConnection db = _dbContext.CreateConnection())
            {
                string sql = @"INSERT INTO GlobalSettings (SettingKey, SettingValue) 
                           VALUES ('IsMaster2FAEnabled', @Val) 
                           ON DUPLICATE KEY UPDATE SettingValue = @Val;";
                await db.ExecuteAsync(sql, new { Val = isEnabled.ToString() });
            }
        }
    }
}
