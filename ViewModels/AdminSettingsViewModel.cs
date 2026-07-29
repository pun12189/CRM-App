using CallMan.Dialogs;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using QRCoder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace CallMan.ViewModels
{
    public partial class AdminSettingsViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly LicenseService _licenseService;
        private readonly StaffService _staffService;
        private readonly IUserSession _userSession;
        private readonly IDialogService _dialogService;
        private readonly IGlobalSettingsService _settingsRepository;
        private readonly ITwoFactorService _twoFactorService;

        [ObservableProperty] private bool _isMaster2FAEnabled;
        [ObservableProperty] private bool _isRegistrationPending = false;
        [ObservableProperty] private string _verificationCodeInput;
        [ObservableProperty] private string _securityErrorMessage;
        [ObservableProperty] private BitmapImage _adminQrCodeSource;        

        [ObservableProperty] private object? _currentSettingView;
        [ObservableProperty] private bool _isMainGridVisible = true;
        [ObservableProperty] private string _backButtonContent = "↑ Back to Settings Dashboard";

        // Track sub-level depth (e.g., Level 1: Staff Directory, Level 2: Staff Details)
        private bool _isInSubDetailView = false;

        [ObservableProperty] private bool _toggleOnlineServices;
        [ObservableProperty] private bool _isToggleVisible;        

        private string _newGeneratedSecret;

        public AdminSettingsViewModel(IServiceProvider serviceProvider, LicenseService licenseService, IGlobalSettingsService settingsRepository, ITwoFactorService twoFactorService, IUserSession userSession, IDialogService dialogService, StaffService staffService )
        {
            _serviceProvider = serviceProvider;
            _licenseService = licenseService;
            _settingsRepository = settingsRepository;
            _twoFactorService = twoFactorService;
            _dialogService = dialogService;
            _userSession = userSession;
            _staffService = staffService;
            ToggleOnlineServices = Core.LicenseManager.Current.IsOnlineServicesEnabled;
            IsToggleVisible = Core.LicenseManager.Current.IsLocalDatabase;

            Task.Run(async () => IsMaster2FAEnabled = await _settingsRepository.GetMaster2FAStatusAsync());
        }

        [RelayCommand]
        private async Task NavigateToSetting(string settingType)
        {
            IsMainGridVisible = false;
            switch (settingType)
            {
                case "Staff":
                    CurrentSettingView = new UserManagementViewModel(_dialogService, _staffService, this);
                    _isInSubDetailView = false;
                    break;
                case "CProfile":
                    CurrentSettingView = _serviceProvider.GetRequiredService<CompanyProfileViewModel>();
                    break;
                case "Category":
                    CurrentSettingView = _serviceProvider.GetRequiredService<CategorySettingsViewModel>();
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
                case "Permissions":
                    CurrentSettingView = _serviceProvider.GetRequiredService<PermissionsManagementViewModel>();
                    break;
                case "Schemes":
                    CurrentSettingView = _serviceProvider.GetRequiredService<SchemeManagementViewModel>();
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
                // Add cases for Fetch Inquiries, Workflows, etc.
                default:
                    IsMainGridVisible = true;
                    break;
            }

            await Task.CompletedTask;
        }

        [RelayCommand]
        private void BackToGrid()
        {
            if (_isInSubDetailView)
            {
                // If in Level 2 (Staff Details), step back to Level 1 (Staff Directory)
                BackToStaffDirectory();
            }
            else
            {
                // If in Level 1 (Staff Directory), step back to Main Settings Grid
                CurrentSettingView = null;
                IsMainGridVisible = true;
            }
        }

        public async Task OpenStaffDetailsAsync(User user)
        {
            var detailsVm = _serviceProvider.GetRequiredService<StaffDetailsViewModel>();

            // Wire up inner back arrow if clicked inside StaffDetailsView
            detailsVm.OnNavigateBackRequested += () => BackToStaffDirectory();

            await detailsVm.InitializeAsync(user);

            // Swap CurrentSettingView to the Staff Details VM
            IsMainGridVisible = false; // <--- ADD THIS LINE!
            CurrentSettingView = detailsVm;
            _isInSubDetailView = true;
            BackButtonContent = "↑ Back to Staff Directory";
        }

        // Step back from Details -> Directory
        public void BackToStaffDirectory()
        {
            var staffVm = new UserManagementViewModel(_dialogService, _staffService, this);
            IsMainGridVisible = false; // Keep ContentControl visible for Staff Grid
            CurrentSettingView = staffVm;
            _isInSubDetailView = false;
            BackButtonContent = "↑ Back to Settings Dashboard";
        }

        [RelayCommand]
        private async Task ProcessPolicyToggle()
        {
            SecurityErrorMessage = string.Empty;

            if (IsMaster2FAEnabled)
            {
                // Flow: Admin wants to switch it ON. Don't save yet! 
                // Generate a brand new, clean base secret token on the spot.
                var adminUser = _userSession.CurrentUser + "@" + _userSession.UserRole;
                var setup = _twoFactorService.GenerateSetupInfo(adminUser ?? "admin@tijori");

                _newGeneratedSecret = setup.secretKey;
                AdminQrCodeSource = GenerateQrCodeVisual(setup.qrCodeUri);

                // Present the registration validation box container frame
                IsRegistrationPending = true;
            }
            else
            {
                // Flow: Admin wants to turn it OFF. Wipes out the data immediately.
                await _settingsRepository.SaveGlobal2FAPolicyAsync(false);
                IsRegistrationPending = false;
                AdminQrCodeSource = null;
                _newGeneratedSecret = null;
            }
        }

        [RelayCommand]
        private async Task ConfirmAdminRegistration()
        {
            SecurityErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(VerificationCodeInput) || VerificationCodeInput.Length != 6)
            {
                SecurityErrorMessage = "Enter a valid 6-digit confirmation code.";
                return;
            }

            // Validate the newly scanned secret sequence context matches
            bool codeMatch = _twoFactorService.VerifyAdminCode(_newGeneratedSecret, VerificationCodeInput);
            if (codeMatch)
            {
                // Commit structural policies and lock secret securely into database parameters
                await _settingsRepository.SaveGlobal2FAPolicyAsync(true, _newGeneratedSecret);
                IsRegistrationPending = false;
                VerificationCodeInput = string.Empty;
                AdminQrCodeSource = null; // Clean registration graphical buffer layout
            }
            else
            {
                SecurityErrorMessage = "Invalid validation code token. Please try again.";
                VerificationCodeInput = string.Empty;
            }
        }

        [RelayCommand]
        private async Task CancelRegistrationChange()
        {
            // Revert UI component settings status indicators cleanly
            IsMaster2FAEnabled = false;
            IsRegistrationPending = false;
            AdminQrCodeSource = null;
            _newGeneratedSecret = null;
            VerificationCodeInput = string.Empty;
            SecurityErrorMessage = string.Empty;

            await _settingsRepository.SaveGlobal2FAPolicyAsync(false);
        }

        private BitmapImage GenerateQrCodeVisual(string url)
        {
            using (QRCodeGenerator qrGen = new QRCodeGenerator())
            using (QRCodeData data = qrGen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q))
            using (PngByteQRCode qr = new PngByteQRCode(data))
            {
                byte[] bytes = qr.GetGraphic(20);
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
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
