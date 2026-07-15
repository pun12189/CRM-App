using CallMan.Core;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CallMan.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using QRCoder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CallMan.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;
        private readonly ITwoFactorService _2faService;
        private readonly StaffService _userRepository;
        private readonly IGlobalSettingsService _settingsRepository;

        [ObservableProperty] private string _email = "";
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private bool _isLoggingIn;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _resetEmail;
        [ObservableProperty] private bool _isLoginVisible = true;
        [ObservableProperty] private bool _isForgotVisible = false;
        [ObservableProperty] private bool _isTwoFactorVisible = false;
        [ObservableProperty] private bool _isRegistrationVisible = false;
        [ObservableProperty] private string _twoFactorCode;
        [ObservableProperty] private BitmapImage _qrCodeSource;

        private User _currentUser;
        private string _tempSecret;

        public LoginViewModel(IAuthService authService, ITwoFactorService twoFactorService, StaffService userRepository, IGlobalSettingsService settingsRepository) 
        {
            _authService = authService;
            _2faService = twoFactorService;
            _userRepository = userRepository;
            _settingsRepository = settingsRepository;
        }

        [RelayCommand]
        private async Task Login(object passwordBox)
        {
            await LicenseManager.RefreshCacheAsync();

            // Check your model's native properties directly
            if (LicenseManager.Current.IsExpired)
            {
                MessageBox.Show(
                    "Your software application evaluation trial phase has expired.\n\n" +
                    "Please contact administration to activate the Full Enterprise Version.",
                    "Trial License Expired - Tijori",
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop
                );

                // STOPS EXECUTION IN ITS TRACKS: Prevents login routing and credentials checks
                return;
            }

            if (IsBusy) return; // Prevent double-clicking

            IsBusy = true; // Show Spinner, Disable Button

            try
            {
                // =================================================================
                // PHASE 1: STANDARD CREDENTIAL IDENTIFICATION
                // =================================================================
                if (IsLoginVisible)
                {
                    var passwordContainer = passwordBox as System.Windows.Controls.PasswordBox;
                    string password = passwordContainer?.Password ?? "";

                    if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(password))
                    {
                        ErrorMessage = "Email and Password are required.";
                        IsBusy = false;
                        return;
                    }

                    IsLoggingIn = true;
                    bool success = await _authService.AuthenticateByEmailAsync(Email, password);

                    if (success)
                    {
                        _currentUser = await _userRepository.GetUserByEmailAsync(Email);
                        bool global2FA = await _settingsRepository.GetMaster2FAStatusAsync();

                        if (global2FA)
                        {
                            // Intercept: If 2FA is mandatory but this employee has no secret key yet
                            if (!_currentUser.IsTwoFactorEnabled || string.IsNullOrEmpty(_currentUser.TwoFactorSecret))
                            {
                                var setup = _2faService.GenerateSetupInfo(_currentUser.Email);
                                _tempSecret = setup.secretKey;
                                QrCodeSource = GenerateQrCodeBytes(setup.qrCodeUri);

                                IsLoginVisible = false;
                                IsRegistrationVisible = true;
                            }
                            else
                            {
                                // Returning user with active 2FA secret
                                IsLoginVisible = false;
                                IsTwoFactorVisible = true;
                            }
                            TwoFactorCode = string.Empty;
                        }
                        else
                        {
                            FinalizeSuccessRoute();
                        }
                    }
                    else
                    {
                        ErrorMessage = "Invalid credentials.";
                    }
                }
                // =================================================================
                // PHASE 2: NEW USER ONBOARDING REGISTRATION (ON THEIR OWN TERMINAL)
                // =================================================================
                else if (IsRegistrationVisible)
                {
                    if (string.IsNullOrWhiteSpace(TwoFactorCode) || TwoFactorCode.Length != 6)
                    {
                        ErrorMessage = "Enter valid 6-digit code.";
                        IsBusy = false;
                        return;
                    }

                    if (_2faService.VerifyCode(_tempSecret, TwoFactorCode))
                    {
                        // 1. Save the generated keys permanently to the database
                        await _userRepository.UpdateUser2FAStatusAsync(_currentUser.UserId, true, _tempSecret);

                        // 2. CRITICAL FIX: Update the local variable instance flags in memory!
                        _currentUser.IsTwoFactorEnabled = true; // Or true, matching your model data type
                        _currentUser.TwoFactorSecret = _tempSecret;

                        // 3. Launch application dashboard
                        FinalizeSuccessRoute();
                    }
                    else
                    {
                        ErrorMessage = "Verification failed. Re-scan the barcode token.";
                        TwoFactorCode = string.Empty;
                    }
                }
                // =================================================================
                // PHASE 3: STANDARD LOGINS CHALLENGE RESPONSE
                // =================================================================
                else if (IsTwoFactorVisible)
                {
                    if (_2faService.VerifyCode(_currentUser.TwoFactorSecret, TwoFactorCode))
                    {
                        FinalizeSuccessRoute();
                    }
                    else
                    {
                        ErrorMessage = "Invalid code context.";
                        TwoFactorCode = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Connection failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
                IsLoggingIn = false;
            }
        }

        [RelayCommand]
        private void RevertToCredentials()
        {
            _currentUser = null;
            _tempSecret = null;
            TwoFactorCode = string.Empty;
            ErrorMessage = string.Empty;
            IsTwoFactorVisible = false;
            IsRegistrationVisible = false;
            IsLoginVisible = true;
        }

        private BitmapImage GenerateQrCodeBytes(string uri)
        {
            using (QRCodeGenerator qrGen = new QRCodeGenerator())
            using (QRCodeData data = qrGen.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q))
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

        private void FinalizeSuccessRoute()
        {
            if (!LicenseManager.Current.IsFullVersion && LicenseManager.Current.DaysRemaining <= 2)
            {
                MessageBox.Show($"Attention: You are currently executing on a temporary trial phase.\nRemaining time: {LicenseManager.Current.DaysRemaining} Day(s) left.", "Trial Lifecycle Countdown Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            var main = App.ServiceProvider.GetRequiredService<MainWindow>();
            main.Show();
            Application.Current.MainWindow = main;
            CloseCurrentWindow();
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        [RelayCommand]
        private void Cancel2FA()
        {
            ResetToStepOne();
        }

        private void ResetToStepOne()
        {
            _currentUser = null;
            TwoFactorCode = string.Empty;
            ErrorMessage = string.Empty;
            IsTwoFactorVisible = false;
            IsLoginVisible = true;
        }

        private void FinalizeAuthorizationPipeline()
        {
            if (!LicenseManager.Current.IsFullVersion && LicenseManager.Current.DaysRemaining <= 2)
            {
                MessageBox.Show(
                    $"Attention: You are currently executing on a temporary trial phase.\n" +
                    $"Remaining time: {LicenseManager.Current.DaysRemaining} Day(s) left.",
                    "Trial Lifecycle Countdown Alert",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var mainWindow = App.ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            Application.Current.MainWindow = mainWindow;
            CloseCurrentWindow();
            Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        [RelayCommand]
        private async void SendReset(object obj)
        {
            if (string.IsNullOrWhiteSpace(ResetEmail))
            {
                ErrorMessage = "Email is required to send your temporary password.";
                return; 
            }

            ErrorMessage = "";
            IsBusy = true; // Show your professional spinner!
            var success = await _authService.ResetPasswordAsync(ResetEmail);
            IsBusy = false;

            if (success)
            {
                if (MessageBox.Show("A temporary password has been sent to your email.") == MessageBoxResult.OK)
                {
                    IsLoginVisible = !IsLoginVisible;
                    IsForgotVisible = !IsForgotVisible;
                    ErrorMessage = ""; // Clear any old errors when switching
                }
            }
            else
            {
                ErrorMessage = "Email not found in our records.";
            }
        }

        [RelayCommand]
        private void SwitchView(object obj)
        {
            IsLoginVisible = !IsLoginVisible;
            IsForgotVisible = !IsForgotVisible;
            ErrorMessage = ""; // Clear any old errors when switching
        }

        [RelayCommand]
        private void Exit()
        {
            Application.Current.Shutdown();
        }

        private void CloseCurrentWindow()
        {
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w is LoginView);
            window?.Close();
        }
    }
}
