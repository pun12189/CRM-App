using CallMan.Core;
using CallMan.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class DbConfigurationViewModel : ObservableObject
    {
        public event Action<bool>? RequestClose;

        [ObservableProperty] private DbConfig _config = new();
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private string _statusColor = "#475569"; // Slate gray default text color
        [ObservableProperty] private bool _isProcessing;

        // Tracks discovered IP addresses to assist with local server setups
        [ObservableProperty] private ObservableCollection<string> _detectedLocalIps = new();

        public DbConfigurationViewModel()
        {
            DiscoverLocalNetworkInterfaces();
        }

        private void DiscoverLocalNetworkInterfaces()
        {
            try
            {
                DetectedLocalIps.Add("localhost");
                DetectedLocalIps.Add("127.0.0.1");

                // Query the operating system's DNS lookup tables for active local interface connections
                var hostName = Dns.GetHostName();
                var addresses = Dns.GetHostEntry(hostName).AddressList;

                foreach (var ip in addresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork))
                {
                    string cleanIp = ip.ToString();
                    if (!DetectedLocalIps.Contains(cleanIp))
                    {
                        DetectedLocalIps.Add(cleanIp);
                    }
                }
            }
            catch
            {
                // Soft landing for restricted network isolation sandboxes
            }
        }

        [RelayCommand]
        private void SelectDiscoveredIp(string ipAddress)
        {
            if (!string.IsNullOrEmpty(ipAddress))
            {
                Config.Server = ipAddress;
                OnPropertyChanged(nameof(Config));
            }
        }

        [RelayCommand]
        private async Task TestAndSaveConnection()
        {
            if (string.IsNullOrWhiteSpace(Config.Server) || string.IsNullOrWhiteSpace(Config.Database))
            {
                StatusMessage = "❌ Server host address and target database name properties are mandatory.";
                StatusColor = "#DC2626"; // Alert Crimson Red
                return;
            }

            IsProcessing = true;
            StatusMessage = "⚡ Verifying physical connection across the network... Please wait.";
            StatusColor = "#2563EB"; // Processing Royal Blue

            string derivedConnectionString = Config.ToConnectionString();

            bool isConnected = await Task.Run(() =>
            {
                try
                {
                    using var connection = new MySqlConnection(derivedConnectionString);
                    connection.Open();

                    // Execute a low-overhead query statement to confirm read/write operations
                    int checkResult = connection.ExecuteScalar<int>("SELECT 1;");
                    return checkResult == 1;
                }
                catch (Exception ex)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"❌ Connectivity Failed: {ex.Message}";
                        StatusColor = "#DC2626";
                    });
                    return false;
                }
            });

            IsProcessing = false;

            if (isConnected)
            {
                // Save the configuration profile locally if the connectivity test passes
                DbConfigManager.SaveConfiguration(Config);

                MessageBox.Show("Database connection verified successfully. Connection saved.",
                                "Configuration Verified", MessageBoxButton.OK, MessageBoxImage.Information);

                RequestClose?.Invoke(true);
            }
        }
    }
}
