using CallMan.Data;
using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
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

                // 2. Build local app data safe folder structures tree strings
                string appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string localBackupDir = Path.Combine(appDataRoot, "SofricONE", "TIJORI_Backups");
                string tempStagingDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BackupStaging");

                if (!Directory.Exists(localBackupDir)) Directory.CreateDirectory(localBackupDir);
                if (!Directory.Exists(tempStagingDir)) Directory.CreateDirectory(tempStagingDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string sqlFilePath = Path.Combine(tempStagingDir, $"tijori_{timestamp}.sql");
                string zipFileName = $"Backup_Tijori_{timestamp}.zip";
                string zipStagingPath = Path.Combine(tempStagingDir, zipFileName);

                // 3. Run streaming backup execution pass
                using (var cmd = new MySqlCommand())
                {
                    using (var mb = new MySqlBackup(cmd))
                    {
                        cmd.Connection = conn;
                        mb.ExportToFile(sqlFilePath);
                    }
                }

                // 4. Encapsulate inside compressed zip folder file structure
                using (var archive = ZipFile.Open(zipStagingPath, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(sqlFilePath, Path.GetFileName(sqlFilePath));
                }
                File.Delete(sqlFilePath); // Wipe out large sql string format instantly

                string backupType = "Local";

                // 5. Evaluate dynamic distribution route targeting conditions
                if (forceEmailDelivery && !string.IsNullOrWhiteSpace(destinationEmail))
                {
                    backupType = "Email";
                    await _emailService.SendEmailAsync(
                        recipientEmail: destinationEmail, // Uses the custom text typed inside the window text box control
                        subject: $"TIJORI Manual Backup Request - {DateTime.Today:dd/MM/yyyy}",
                        body: $"Manual application closing backup successfully produced by user '{currentUser}' on workstation terminal '{Environment.MachineName}'.",
                        attachments: new List<string> { zipStagingPath }
                    );
                    File.Delete(zipStagingPath); // Clean temporary staging directory file remnants
                }
                else
                {
                    // Default Fallback: Relocate compression package file structure securely to LocalAppData folder tree
                    string finalDestinationPath = Path.Combine(localBackupDir, zipFileName);
                    File.Move(zipStagingPath, finalDestinationPath);
                }

                // 6. Log successful execution record parameters back up to the master table
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
            catch (Exception)
            {
                try
                {
                    using var conn = _dbContext.CreateConnection();
                    await conn.ExecuteAsync(@"
                INSERT INTO SystemBackupLog (BackupDate, TriggeredByUser, MachineName, BackupType, Status)
                VALUES (NOW(), @User, @Machine, 'None', 'Failed');",
                        new { User = currentUser, Machine = Environment.MachineName });
                }
                catch { /* Master server fully disconnected */ }
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
