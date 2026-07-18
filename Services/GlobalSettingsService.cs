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

        /// <summary>
        /// Actively locks down the application configuration policy state.
        /// If policy is disabled, it wipes out old configurations to keep data secure.
        /// </summary>
        public async Task SaveGlobal2FAPolicyAsync(bool isEnabled, string adminSecret = null)
        {
            using (IDbConnection db = _dbContext.CreateConnection())
            {
                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        // 1. Update Global Master Toggle Switch Rule state
                        string settingsSql = @"INSERT INTO GlobalSettings (SettingKey, SettingValue) 
                                           VALUES ('IsMaster2FAEnabled', @Val) 
                                           ON DUPLICATE KEY UPDATE SettingValue = @Val;";
                        await db.ExecuteAsync(settingsSql, new { Val = isEnabled.ToString() }, transaction);

                        // 2. Manage Admin secret context state (Role = 1 represents the System Administrator profile)
                        if (isEnabled && !string.IsNullOrEmpty(adminSecret))
                        {
                            string updateAdminSql = @"UPDATE Users 
                                                 SET IsTwoFactorEnabled = 1, TwoFactorSecret = @Secret 
                                                 WHERE Role = 0 AND IsActive = 1;";
                            await db.ExecuteAsync(updateAdminSql, new { Secret = adminSecret }, transaction);
                        }
                        else
                        {
                            // Explicitly render previous secret key configurations null and void by clearing them out
                            string wipeAdminSql = @"UPDATE Users 
                                               SET IsTwoFactorEnabled = 0, TwoFactorSecret = NULL 
                                               WHERE Role = 0;";
                            await db.ExecuteAsync(wipeAdminSql, transaction);
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
