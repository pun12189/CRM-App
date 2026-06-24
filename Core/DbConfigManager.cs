using CallMan.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CallMan.Core
{
    public static class DbConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dbconfig.json");

        // Globally accessible active connection string cache pointer
        public static string CachedConnectionString { get; private set; } = string.Empty;

        public static bool LoadConfiguration()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return false;

                string rawJson = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<DbConfig>(rawJson);

                if (config != null)
                {
                    CachedConnectionString = config.ToConnectionString();
                    return true;
                }
            }
            catch
            {
                // Fallback on serialization error
            }
            return false;
        }

        public static void SaveConfiguration(DbConfig config)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string rawJson = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigPath, rawJson);

            // Instantly refresh localized application memory pointers
            CachedConnectionString = config.ToConnectionString();
        }
    }
}
