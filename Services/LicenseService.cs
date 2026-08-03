using Tijori.Core;
using Tijori.Data;
using Tijori.Models;
using Tijori.Models.Enums;
using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tijori.Services
{
    public class LicenseService
    {
        private readonly CrmDbContext _context;
        public LicenseService(CrmDbContext context) => _context = context;
        // Secret cryptographic salt known only to your application source compilation
        private const string CryptographicPassphrase = "TIJORIErp2026_SecureKeySystemBundle!";
        private const string StructuralPayloadSalt = "TIJORI_Enterprise_Token_Salt_2026";

        /// <summary>
        /// Retrieves license parameters over the LAN and runs verification algorithms on the stored signature key.
        /// </summary>
        public async Task<LicenseInfo> GetCurrentLicenseStatusAsync()
        {
            using var db = _context.CreateConnection();

            // 1. Silent initialization fallback if the deployment has no token signature records yet
            const string seedSql = @"
                INSERT IGNORE INTO SystemLicense (LicenseId, SystemId, InstallationDate, AllowedTrialDays, IsFullVersion)
                VALUES (1, @NewSystemId, CURDATE(), 7, 0);";

            string generatedToken = $"TIJORI-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            await db.ExecuteAsync(seedSql, new { NewSystemId = generatedToken });

            // 2. Query runtime parameters from the central table
            const string querySql = "SELECT SystemId, InstallationDate, AllowedTrialDays, ActivationKey, IsFullVersion, IsOnlineServicesEnabled FROM SystemLicense WHERE LicenseId = 1;";
            var row = await db.QueryFirstOrDefaultAsync(querySql);

            var info = new LicenseInfo();
            string storedTokenKey = string.Empty;

            if (row != null)
            {
                info.SystemId = row.SystemId;
                storedTokenKey = row.ActivationKey ?? string.Empty;
                info.IsOnlineServicesEnabled = Convert.ToBoolean(row.IsOnlineServicesEnabled);

                // Establish initial trial matrix properties read directly from local data cells
                info.MaxTrialDays = Convert.ToInt32(row.TrialDays);
                DateTime installDate = Convert.ToDateTime(row.InstallationDate);
                int elapsedDays = (DateTime.Today - installDate).Days;
                info.DaysRemaining = Math.Max(0, info.MaxTrialDays - elapsedDays);
            }

            // 3. SECURE OFFLINE TOKEN DECRYPTION GATEWAY
            bool isOfflineValidationSuccessful = false;
            if (!string.IsNullOrWhiteSpace(storedTokenKey))
            {
                var decryptedPayload = DecryptAndParseTokenPayload(storedTokenKey);
                if (decryptedPayload != null && decryptedPayload.TargetSystemId == info.SystemId && VerifyPayloadSignature(decryptedPayload))
                {
                    DateTime expiryDate = DateTime.ParseExact(decryptedPayload.ExpirationDateStr, "yyyy-MM-dd", null);

                    if (DateTime.Today <= expiryDate)
                    {
                        info.IsFullVersion = decryptedPayload.PackageType > 0;
                        info.PackageType = (LicensePackageType)decryptedPayload.PackageType;
                        info.ExpirationDate = expiryDate;
                        info.AllowSoftwareUpdates = decryptedPayload.AllowUpdates;

                        // Parse tracking dates directly according to assigned model parameters
                        if (info.PackageType == LicensePackageType.Trial)
                        {
                            info.MaxTrialDays = decryptedPayload.CustomTrialDays;
                            DateTime installDate = Convert.ToDateTime(row.InstallationDate);
                            int elapsedDays = (DateTime.Today - installDate).Days;
                            info.DaysRemaining = Math.Max(0, info.MaxTrialDays - elapsedDays);
                        }
                        else
                        {
                            info.DaysRemaining = (expiryDate - DateTime.Today).Days;
                        }

                        isOfflineValidationSuccessful = true;
                    }
                }
            }

            if (!isOfflineValidationSuccessful)
            {
                info.IsFullVersion = false;
                info.PackageType = LicensePackageType.Trial;
            }

            // 4. Map LAN routing configurations to identify visibility state behaviors
            var builder = new MySqlConnectionStringBuilder(db.ConnectionString);
            string serverHost = builder.Server.Trim().ToLower();
            info.IsLocalDatabase = (serverHost == "localhost" || serverHost == "127.0.0.1" || serverHost.StartsWith("192.168.") || serverHost.StartsWith("10."));

            // 5. HYBRID BG CHECK: Dispatches server check-ins cleanly out of core UI execution paths
            if (!string.IsNullOrEmpty(info.SystemId))
            {
                _ = Task.Run(() => SafeOnlineCloudSynchronizationCheckAsync(info.SystemId, storedTokenKey));
            }

            return info;
        }

        /// <summary>
        /// Validates the provided activation code and saves it to the central database if successful.
        /// </summary>
        public async Task<bool> ActivateSoftwareAsync(string compressedTokenKey)
        {
            if (string.IsNullOrWhiteSpace(compressedTokenKey)) return false;

            using var db = _context.CreateConnection();
            string localSystemId = await db.ExecuteScalarAsync<string>("SELECT SystemId FROM SystemLicense WHERE LicenseId = 1;");

            if (string.IsNullOrEmpty(localSystemId)) return false;

            var payload = DecryptAndParseTokenPayload(compressedTokenKey);
            if (payload == null || payload.TargetSystemId != localSystemId || !VerifyPayloadSignature(payload))
                return false;

            DateTime expiryDate = DateTime.ParseExact(payload.ExpirationDateStr, "yyyy-MM-dd", null);
            if (DateTime.Today > expiryDate) return false;

            const string updateSql = "UPDATE SystemLicense SET IsFullVersion = @IsFull, ActivationKey = @Key WHERE LicenseId = 1;";
            int affectedRows = await db.ExecuteAsync(updateSql, new
            {
                IsFull = payload.PackageType > 0 ? 1 : 0,
                Key = compressedTokenKey.Trim()
            });

            return affectedRows > 0;
        }

        /// <summary>
        /// Background task that communicates with your cloud VPS server.
        /// If a trial extension or AMC renewal is found, it updates local storage automatically.
        /// </summary>
        private async Task SafeOnlineCloudSynchronizationCheckAsync(string systemId, string activeLocalToken)
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                System.Diagnostics.Debug.WriteLine("[SYNC SKIPPED] Local network adapter reports completely offline.");
                return;
            }            

            try
            {
                using var cloudConn = _context.Connection();

                const string query = "SELECT CurrentActiveToken, AllowedTrialDays, TrialExtensionDays, IsBlocked FROM CustomerActivations WHERE SystemId = @SysId LIMIT 1;";
                var remote = await cloudConn.QueryFirstOrDefaultAsync(query, new { SysId = systemId });

                if (remote != null)
                {
                    using var localDb = _context.CreateConnection();

                    // Handle Remote Deactivation/Revocation (Kill Switch)
                    if (Convert.ToBoolean(remote.IsBlocked))
                    {
                        await localDb.ExecuteAsync("UPDATE SystemLicense SET IsFullVersion = 0, ActivationKey = NULL WHERE LicenseId = 1;");
                        await LicenseManager.RefreshCacheAsync();
                        return;
                    }

                    // PROCESS REMOTE DYNAMIC TRIAL DAYS EXTENSIONS
                    int cloudTrialDays = Convert.ToInt32(remote.AllowedTrialDays) + Convert.ToInt32(remote.TrialExtensionDays);
                    int localTrialDays = await localDb.ExecuteScalarAsync<int>("SELECT AllowedTrialDays FROM SystemLicense WHERE LicenseId = 1;");

                    if (cloudTrialDays != localTrialDays && string.IsNullOrEmpty(activeLocalToken))
                    {
                        await localDb.ExecuteAsync("UPDATE SystemLicense SET AllowedTrialDays = @NewDays WHERE LicenseId = 1;", new { NewDays = cloudTrialDays });
                        await LicenseManager.RefreshCacheAsync();
                        return;
                    }

                    // PROCESS AUTOMATED AMC EXTENSIONS / PACKAGE UPGRADE KEY EXCHANGES
                    string latestCloudToken = remote.CurrentActiveToken;
                    if (!string.IsNullOrEmpty(latestCloudToken) && latestCloudToken != activeLocalToken)
                    {
                        var updatedPayload = DecryptAndParseTokenPayload(latestCloudToken);
                        if (updatedPayload != null && updatedPayload.TargetSystemId == systemId && VerifyPayloadSignature(updatedPayload))
                        {
                            await localDb.ExecuteAsync("UPDATE SystemLicense SET IsFullVersion = @IsFull, ActivationKey = @NewToken WHERE LicenseId = 1;", new
                            {
                                IsFull = updatedPayload.PackageType > 0 ? 1 : 0,
                                NewToken = latestCloudToken
                            });

                            await LicenseManager.RefreshCacheAsync();
                        }
                    }
                }
            }
            catch
            {
                // Network drops fail silently to protect offline usability constraints entirely
                System.Diagnostics.Debug.WriteLine("[HYBRID SECURE LOG] Central license provider lookup skipped.");
            }
        }

        #region Internal Cryptography Engine Utilities

        private LicenseTokenPayload? DecryptAndParseTokenPayload(string cipherText)
        {
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                using var aes = Aes.Create();
                using var sha256 = SHA256.Create();

                aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(CryptographicPassphrase));

                byte[] iv = new byte[16];
                Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, iv.Length, cipherBytes.Length - iv.Length);

                string jsonString = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<LicenseTokenPayload>(jsonString);
            }
            catch
            {
                return null; // Prevents app crashes if users paste corrupted string patterns
            }
        }

        private bool VerifyPayloadSignature(LicenseTokenPayload payload)
        {
            string matrix = $"{payload.TargetSystemId}-{payload.PackageType}-{payload.ExpirationDateStr}-{payload.AllowUpdates}-{payload.CustomTrialDays}-{StructuralPayloadSalt}";
            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(matrix));

            StringBuilder sb = new StringBuilder();
            foreach (var b in hashBytes) sb.Append(b.ToString("X2"));

            return string.Equals(payload.SecuritySignature, sb.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        #endregion        

        public async Task SaveOnlineServicesToggleStateAsync(bool isEnabled)
        {
            const string sql = "UPDATE SystemLicense SET IsOnlineServicesEnabled = @IsEnabled WHERE LicenseId = 1;";
            using var db = _context.CreateConnection();
            await db.ExecuteAsync(sql, new { IsEnabled = isEnabled ? 1 : 0 });
        }
    }
}
