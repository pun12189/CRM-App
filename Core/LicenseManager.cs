using CallMan.Models;
using CallMan.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.Core
{
    public static class LicenseManager
    {
        private static LicenseService? _licenseService;
        private static System.Threading.Timer? _syncBackgroundTimer;

        /// <summary>
        /// Globally accessible, thread-safe memory snapshot configuration cache pointer.
        /// </summary>
        public static LicenseInfo Current { get; private set; } = new();

        /// <summary>
        /// Configures initial parameters on application startup.
        /// </summary>
        public static async Task InitializeAsync(LicenseService licenseService)
        {
            _licenseService = licenseService;

            // Execute absolute initial boot synchronization pull over the LAN pipeline
            await RefreshCacheAsync();

            // Set up background synchronization timer pass to fetch state updates every 5 minutes
            _syncBackgroundTimer = new System.Threading.Timer(async _ =>
            {
                await RefreshCacheAsync();
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public static async Task RefreshCacheAsync()
        {
            if (_licenseService == null) return;

            try
            {
                var deepStateCopy = await _licenseService.GetCurrentLicenseStatusAsync();

                // Dispatches memory references onto the active UI synchronization thread context safely
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Current = deepStateCopy;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LICENSE MANAGER ERROR] Cache refresh failed: {ex.Message}");
            }
        }
    }
}
