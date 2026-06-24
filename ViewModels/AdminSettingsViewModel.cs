using CallMan.Dialogs;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace CallMan.ViewModels
{
    public partial class AdminSettingsViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly LicenseService _licenseService;

        [ObservableProperty] private object? _currentSettingView;
        [ObservableProperty] private bool _isMainGridVisible = true;

        [ObservableProperty] private bool _toggleOnlineServices;
        [ObservableProperty] private bool _isToggleVisible;

        private int _openExpanderIndex = 1; // -1 means all are closed

        public int OpenExpanderIndex
        {
            get => _openExpanderIndex;
            set
            {
                _openExpanderIndex = value;
                OnPropertyChanged();
                // Notify all expanders that the state has changed
                OnPropertyChanged(nameof(IsExpander1Open));
                OnPropertyChanged(nameof(IsExpander2Open));
                OnPropertyChanged(nameof(IsExpander3Open));
                OnPropertyChanged(nameof(IsExpander4Open));
            }
        }

        // Helper properties for the View
        public bool IsExpander1Open => OpenExpanderIndex == 1;
        public bool IsExpander2Open => OpenExpanderIndex == 2;
        public bool IsExpander3Open => OpenExpanderIndex == 3;
        public bool IsExpander4Open => OpenExpanderIndex == 4;

        public AdminSettingsViewModel(IServiceProvider serviceProvider, LicenseService licenseService)
        {
            _serviceProvider = serviceProvider;
            _licenseService = licenseService;

            ToggleOnlineServices = Core.LicenseManager.Current.IsOnlineServicesEnabled;
            IsToggleVisible = Core.LicenseManager.Current.IsLocalDatabase;
        }

        [RelayCommand]
        private async void NavigateToSetting(string settingType)
        {
            IsMainGridVisible = false;
            switch (settingType)
            {
                case "Staff":
                    CurrentSettingView = _serviceProvider.GetRequiredService<UserManagementViewModel>();
                    break;
                case "CProfile":
                    CurrentSettingView = _serviceProvider.GetRequiredService<CompanyProfileViewModel>();
                    break;
                case "Category":
                    CurrentSettingView = _serviceProvider.GetRequiredService<ManageCategoriesViewModel>();
                    break;
                case "OrderStages":
                    CurrentSettingView = _serviceProvider.GetRequiredService<OrderStagesViewModel>();
                    break;
                case "CustomFields":
                    CurrentSettingView = _serviceProvider.GetRequiredService<CustomFieldsViewModel>();
                    break;
                case "Departments":
                    CurrentSettingView = _serviceProvider.GetRequiredService<DepartmentsViewModel>();
                    break;
                case "Logs":
                    CurrentSettingView = _serviceProvider.GetRequiredService<LoginLogsViewModel>();
                    break;
                case "Whatsapp":
                case "CallMan":
                case "ECom":
                case "MargTally":
                    if (!Core.LicenseManager.Current.AreOnlineServicesAllowed)
                    {
                        MessageBox.Show(
                            "This operation requires online communication access permissions.\n\n" +
                            "Please activate your product license and enable 'Online Services' inside the Admin Settings dashboard configuration layout panel.",
                            "Action Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                        BackToGrid();
                        return;
                    }
                    break;
                case "Email":
                    if (!Core.LicenseManager.Current.AreOnlineServicesAllowed)
                    {
                        MessageBox.Show(
                            "This operation requires online communication access permissions.\n\n" +
                            "Please activate your product license and enable 'Online Services' inside the Admin Settings dashboard configuration layout panel.",
                            "Action Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                        BackToGrid();
                        return;
                    }
                    CurrentSettingView = _serviceProvider.GetRequiredService<EmailSettingsViewModel>();
                    break;
                case "Workflows":
                    if (!CallMan.Core.LicenseManager.Current.AreOnlineServicesAllowed)
                    {
                        MessageBox.Show(
                            "This operation requires online communication access permissions.\n\n" +
                            "Please activate your product license and enable 'Online Services' inside the Admin Settings dashboard configuration layout panel.",
                            "Action Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                        BackToGrid();
                        return;
                    }
                    CurrentSettingView = _serviceProvider.GetRequiredService<WorkflowViewModel>();
                    break;
                case "Dead Reasons":                     
                case "Followup Stages":                    
                case "Mature Stages":                    
                case "Lead Tags":                    
                case "Lead Source":                    
                case "Lead Labels":
                    var genericVM = _serviceProvider.GetRequiredService<GenericSettingsViewModel>();

                    // Initialize it for the specific type (e.g., "Dead Reasons")
                    await genericVM.Initialize(settingType);

                    // Set it as the current view
                    CurrentSettingView = genericVM;
                    break;
                case "Permissions":
                    // CurrentSettingView = _serviceProvider.GetRequiredService<PermissionsViewModel>();
                    break;
                // Add cases for Fetch Inquiries, Workflows, etc.
                default:
                    IsMainGridVisible = true;
                    break;
            }
        }

        [RelayCommand]
        private void BackToGrid()
        {
            CurrentSettingView = null;
            IsMainGridVisible = true;
        }

        private async void CommonNavigateGeneric(string name)
        {
            var genericVM = _serviceProvider.GetRequiredService<GenericSettingsViewModel>();

            // Initialize it for the specific type (e.g., "Dead Reasons")
            await genericVM.Initialize(name);

            // Set it as the current view
            CurrentSettingView = genericVM;
        }

        [RelayCommand]
        private async Task UpdateOnlineServicesOption(bool isChecked)
        {
            // 1. Commit the configuration change over the LAN instance registry
            await _licenseService.SaveOnlineServicesToggleStateAsync(isChecked);

            // 2. Force an immediate reload onto the shared in-memory manager
            await Core.LicenseManager.RefreshCacheAsync();

            // 3. Inform user to restart or confirm successful execution state change smoothly
            MessageBox.Show(
                isChecked
                    ? "Online Services Connectivity have been activated successfully."
                    : "Application switched to Offline Mode. External web services are now restricted.",
                "Settings Updated", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void ChangeDatabaseConfig()
        {
            // 1. Instantiate the setup window layout
            var configWindow = new DbConfigurationWindow();

            // 2. Extract the window's ViewModel to seed the current database values
            if (configWindow.DataContext is DbConfigurationViewModel vm)
            {
                try
                {
                    // Read the active configuration file straight from the application folder
                    string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dbconfig.json");

                    if (File.Exists(configPath))
                    {
                        string rawJson = File.ReadAllText(configPath);
                        var currentConfig = JsonSerializer.Deserialize<DbConfig>(rawJson);

                        if (currentConfig != null)
                        {
                            // Populates the fields on the view automatically with their active settings
                            vm.Config = currentConfig;
                        }
                    }
                }
                catch (Exception)
                {
                    // Soft landing fallback: if the file is corrupted, the viewmodel defaults take over
                }
            }

            // 3. Display the populated window modal dialog frame
            bool? dialogResult = configWindow.ShowDialog();

            // 4. If the user completes the validation test execution run successfully
            if (dialogResult == true)
            {
                MessageBox.Show(
                    "Database routing configurations updated successfully!\n\n" +
                    "TIJORI will now perform a graceful restart sequence to fully map your services to the new target server destination.",
                    "Database Migrated", MessageBoxButton.OK, MessageBoxImage.Information);

                ExecuteGracefulSystemRestart();
            }
        }

        private void ExecuteGracefulSystemRestart()
        {
            // Spawn a fresh process instance thread of the application executable
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);

            // Terminate the current instance processes immediately
            Application.Current.Shutdown();
        }
    }
}
