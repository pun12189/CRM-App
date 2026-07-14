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
        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SofricONE", "dbconfig.json");

        // Globally accessible active connection string cache pointer
        public static string CachedConnectionString { get; private set; } = string.Empty;

        public static string ConnectionHost { get; private set; } = string.Empty;

        public static bool LoadConfiguration()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return false;

                string rawJson = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<DbConfig>(rawJson);

                if (config != null)
                {
                    ConnectionHost = config.Server;
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
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            File.WriteAllText(ConfigPath, rawJson);

            // Instantly refresh localized application memory pointers
            CachedConnectionString = config.ToConnectionString();
        }
    }
}
