using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tijori.Models.Enums;

namespace Tijori.Services
{
    public class UserDashboardSettings
    {
        public GlobalDashboardViewMode ProductViewMode { get; set; } = GlobalDashboardViewMode.Cards;
        public GlobalDashboardViewMode AllTimeDataViewMode { get; set; } = GlobalDashboardViewMode.Cards;
        public GlobalDashboardViewMode OrderViewMode { get; set; } = GlobalDashboardViewMode.Cards;
    }

    public static class UserPreferencesService
    {
        private static readonly string FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Tijori"
        );
        private static readonly string FilePath = Path.Combine(FolderPath, "dashboard_preferences.json");

        public static UserDashboardSettings LoadDashboardPreferences()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize<UserDashboardSettings>(json) ?? new UserDashboardSettings();
                }
            }
            catch { }

            return new UserDashboardSettings();
        }

        public static void SaveDashboardPreferences(UserDashboardSettings settings)
        {
            try
            {
                if (!Directory.Exists(FolderPath))
                {
                    Directory.CreateDirectory(FolderPath);
                }

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}
