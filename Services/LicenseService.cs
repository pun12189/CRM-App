using CallMan.Data;
using CallMan.Models;
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
    public class LicenseService
    {
        private readonly CrmDbContext _context;
        public LicenseService(CrmDbContext context) => _context = context;
        // Secret cryptographic salt known only to your application source compilation
        private const string SecretSalt = "TIJORIErp2026_SecureSalt!";

        /// <summary>
        /// Retrieves license parameters over the LAN and runs verification algorithms on the stored signature key.
        /// </summary>
        public async Task<LicenseInfo> GetCurrentLicenseStatusAsync()
        {
            using var db = _context.CreateConnection();

            // 1. Silent initialization fallback if the deployment has no token signature records yet
            const string seedSql = @"
                INSERT IGNORE INTO SystemLicense (LicenseId, SystemId, InstallationDate, IsFullVersion)
                VALUES (1, @NewSystemId, CURDATE(), 0);";

            string generatedToken = $"TIJORI-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            await db.ExecuteAsync(seedSql, new { NewSystemId = generatedToken });

            // 2. Query runtime parameters from the central table
            const string querySql = "SELECT SystemId, InstallationDate, ActivationKey, IsFullVersion, IsOnlineServicesEnabled FROM SystemLicense WHERE LicenseId = 1;";
            var row = await db.QueryFirstOrDefaultAsync(querySql);

            var info = new LicenseInfo();
            if (row != null)
            {
                info.SystemId = row.SystemId;
                string storedKey = row.ActivationKey ?? string.Empty;

                // 3. Mathematical Offline Check: confirm the key validates the local SystemId signature
                bool isSignatureValid = VerifyKeyLocally(row.SystemId, storedKey);
                info.IsFullVersion = Convert.ToBoolean(row.IsFullVersion) && isSignatureValid;

                DateTime installDate = Convert.ToDateTime(row.InstallationDate);
                int elapsedDays = (DateTime.Today - installDate).Days;
                info.DaysRemaining = Math.Max(0, info.MaxTrialDays - elapsedDays);

                info.IsOnlineServicesEnabled = Convert.ToBoolean(row.IsOnlineServicesEnabled);
            }

            var builder = new MySqlConnectionStringBuilder(db.ConnectionString);
            string serverHost = builder.Server.Trim().ToLower();

            // Identifies if database lives on localhost or classic local network ranges (192.168.x.x, 10.x.x.x, etc.)
            if (serverHost == "localhost" ||
                serverHost == "127.0.0.1" ||
                serverHost.StartsWith("192.168.") ||
                serverHost.StartsWith("10.") ||
                serverHost.StartsWith("172."))
            {
                info.IsLocalDatabase = true; // Local installation -> Toggle is visible
            }
            else
            {
                info.IsLocalDatabase = false; // Cloud database setup -> Toggle stays hidden
            }

            return info;
        }

        /// <summary>
        /// Validates the provided activation code and saves it to the central database if successful.
        /// </summary>
        public async Task<bool> ActivateSoftwareAsync(string providedKey)
        {
            if (string.IsNullOrWhiteSpace(providedKey)) return false;

            using var db = _context.CreateConnection();
            string systemId = await db.ExecuteScalarAsync<string>("SELECT SystemId FROM SystemLicense WHERE LicenseId = 1;");

            if (string.IsNullOrEmpty(systemId) || !VerifyKeyLocally(systemId, providedKey))
                return false;

            const string updateSql = "UPDATE SystemLicense SET IsFullVersion = 1, ActivationKey = @Key WHERE LicenseId = 1;";
            int affectedRows = await db.ExecuteAsync(updateSql, new { Key = providedKey.Trim().ToUpper() });
            return affectedRows > 0;
        }

        private bool VerifyKeyLocally(string systemId, string providedKey)
        {
            if (string.IsNullOrWhiteSpace(providedKey)) return false;
            string correctKey = GenerateExpectedKey(systemId);
            return string.Equals(providedKey.Trim(), correctKey, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The single computational hashing method used by both the client software (to check validity) 
        /// and your private Key Generator (to issue serial codes).
        /// </summary>
        public static string GenerateExpectedKey(string systemId)
        {
            string rawMatrixInput = $"{systemId.Trim().ToUpper()}-{SecretSalt}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] computedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawMatrixInput));

            StringBuilder hexString = new StringBuilder();
            foreach (var b in computedBytes) hexString.Append(b.ToString("X2"));

            string processedHash = hexString.ToString();
            // Formats characters cleanly into an enterprise serial matrix block (e.g., A1B2-C3D4-E5F6-G7H8)
            return $"{processedHash.Substring(0, 4)}-{processedHash.Substring(4, 4)}-{processedHash.Substring(8, 4)}-{processedHash.Substring(12, 4)}";
        }

        /// <summary>
        /// Saves the updated state of the online services switch configuration down to MySQL over LAN.
        /// </summary>
        public async Task SaveOnlineServicesToggleStateAsync(bool isEnabled)
        {
            const string sql = "UPDATE SystemLicense SET IsOnlineServicesEnabled = @IsEnabled WHERE LicenseId = 1;";
            using var db = _context.CreateConnection();
            await db.ExecuteAsync(sql, new { IsEnabled = isEnabled ? 1 : 0 });
        }
    }
}
