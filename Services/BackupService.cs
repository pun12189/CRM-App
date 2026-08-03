using Tijori.Data;
using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services
{
    public class BackupService
    {
        private readonly CrmDbContext _dbContext;
        private readonly EmailService _emailService;

        public BackupService(CrmDbContext dbContext, EmailService emailService)
        {
            _dbContext = dbContext;
            _emailService = emailService;
        }

        /// <summary>
        /// Retrieves the timestamp and operator name of the absolute last successful backup run.
        /// </summary>
        public async Task<(DateTime? Date, string User)> GetLastBackupDetailsAsync()
        {
            try
            {
                using var conn = _dbContext.CreateConnection();
                const string sql = @"
                    SELECT BackupDate, TriggeredByUser 
                    FROM SystemBackupLog 
                    WHERE Status = 'Success' 
                    ORDER BY LogId DESC LIMIT 1;";

                var result = await conn.QueryFirstOrDefaultAsync(sql);
                if (result == null || result.BackupDate == DateTime.MinValue)
                {
                    return (null, "None");
                }
                return (result.BackupDate, result.TriggeredByUser);
            }
            catch (Exception)
            {
                return (null, "Unknown (Database Offline)");
            }
        }

        /// <summary>
        /// Executes a forced backup sequence based on explicit close-dialog inputs.
        /// </summary>
        public async Task ProcessManualExitBackupAsync(string currentUser, bool forceEmailDelivery, string destinationEmail)
        {
            try
            {
                using var conn = (MySqlConnection)_dbContext.CreateConnection();

                // 1. Gather current auto-increment keys state snapshot metrics
                const string stateSql = @"
            SELECT COALESCE(MAX(LeadId), 0) FROM Leads;
            SELECT COALESCE(MAX(HistoryId), 0) FROM LeadHistory;
            SELECT COALESCE(MAX(OrderId), 0) FROM Orders;";

                using var multi = await conn.QueryMultipleAsync(stateSql);
                int currentMaxLead = await multi.ReadFirstAsync<int>();
                int currentMaxHistory = await multi.ReadFirstAsync<int>();
                int currentMaxOrder = await multi.ReadFirstAsync<int>();

                // 2. Provision file system folder structural trees strings
                string appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string localBackupDir = Path.Combine(appDataRoot, "SofricONE", "TIJORI_Backups");
                string tempStagingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BackupStaging");

                if (!Directory.Exists(localBackupDir)) Directory.CreateDirectory(localBackupDir);
                if (!Directory.Exists(tempStagingDir)) Directory.CreateDirectory(tempStagingDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string sqlFilePath = Path.Combine(tempStagingDir, $"tijori_{timestamp}.sql");
                string zipFileName = $"Backup_TIJORI_{timestamp}.zip";
                string zipStagingPath = Path.Combine(tempStagingDir, zipFileName);

                // 3. Run primary memory dump execution streaming pass
                using (var cmd = new MySqlCommand())
                {
                    using (var mb = new MySqlBackup(cmd))
                    {
                        cmd.Connection = conn;
                        mb.ExportToFile(sqlFilePath);
                    }
                }

                // 4. Encapsulate data inside compressed zip folder frame structures
                using (var archive = ZipFile.Open(zipStagingPath, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(sqlFilePath, Path.GetFileName(sqlFilePath));
                }
                File.Delete(sqlFilePath); // Safe un-locked deletion target line

                string backupType = "Local";

                // 5. Evaluate cloud sync email distribution channel triggers
                if (forceEmailDelivery && !string.IsNullOrWhiteSpace(destinationEmail))
                {
                    backupType = "Local+Email";

                    await _emailService.SendEmailAsync(
                        recipientEmail: destinationEmail,
                        subject: $"TIJORI Manual Backup Request - {DateTime.Today:dd/MM/yyyy}",
                        body: $"Manual requested exit snapshot processed by user '{currentUser}' on workstation terminal '{Environment.MachineName}'.",
                        attachments: new List<string> { zipStagingPath }
                    );

                    // ====================================================================
                    // CRITICAL DEFENSIVE PATCH: ISOLATED TEMPORARY FILE CLEANUP CONTAINER
                    // ====================================================================
                    try
                    {
                        if (File.Exists(zipStagingPath))
                        {
                            File.Delete(zipStagingPath);
                        }
                    }
                    catch (Exception)
                    {
                        // Defer cleanup. If .NET has a temporary stream lock on the file handle, 
                        // we catch it silently here so the main execution block can log 'Success'.
                    }
                }
                else
                {
                    // Default Fallback: Relocate backup package safely straight into user AppData
                    string finalDestinationPath = Path.Combine(localBackupDir, zipFileName);
                    File.Move(zipStagingPath, finalDestinationPath);
                }

                // 6. COMMIT REFRESHED SYSTEM STATE SNAPSHOT VALUES TO THE LOG RECORD
                const string logSql = @"
            INSERT INTO SystemBackupLog (BackupDate, TriggeredByUser, MachineName, BackupType, Status, MaxLeadId, MaxHistoryId, MaxOrderId)
            VALUES (NOW(), @User, @Machine, @Type, 'Success', @MaxLead, @MaxHist, @MaxOrd);";

                await conn.ExecuteAsync(logSql, new
                {
                    User = currentUser,
                    Machine = Environment.MachineName,
                    Type = backupType,
                    MaxLead = currentMaxLead,
                    MaxHist = currentMaxHistory,
                    MaxOrd = currentMaxOrder
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine($"BackupService.ProcessManualExitBackupAsync failed: {e.Message}");
                // Global exception fallback recovery trap
                try
                {
                    using var fallbackConn = _dbContext.CreateConnection();
                    await fallbackConn.ExecuteAsync(@"
                INSERT INTO SystemBackupLog (BackupDate, TriggeredByUser, MachineName, BackupType, Status)
                VALUES (NOW(), @User, @Machine, 'None', 'Failed');",
                        new { User = currentUser, Machine = Environment.MachineName });
                }
                catch { /* Master MySQL server unreachable */ }
            }
        }

        /// <summary>
        /// Checks if the email settings exist in the database table.
        /// </summary>
        public async Task<bool> CheckEmailSettingsExistAsync()
        {
            try
            {
                using var conn = _dbContext.CreateConnection();
                // Adjust the table name 'EmailSettings' if it differs in your actual schema
                const string sql = "SELECT COUNT(*) FROM EmailSettings LIMIT 1;";
                int count = await conn.ExecuteScalarAsync<int>(sql);
                return count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
