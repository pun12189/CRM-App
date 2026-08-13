using Tijori.Core;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Services;
using Tijori.Views;
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

namespace Tijori.ViewModels
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
#if RELEASE
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
#endif
            if (IsBusy) return;
            IsBusy = true;
            IsLoggingIn = true;
            ErrorMessage = string.Empty;

            try
            {
                var passwordContainer = passwordBox as System.Windows.Controls.PasswordBox;
                string password = passwordContainer?.Password ?? "";

                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(password))
                {
                    ErrorMessage = "Username/Email and Password are required.";
                    return;
                }

                // Simple, single-step credential check
                bool success = await _authService.AuthenticateByEmailAsync(Email, password);

                if (success)
                {
                    // Bypasses all 2FA interception screens entirely and opens the app shell dashboard
                    FinalizeSuccessRoute();
                }
                else
                {
                    ErrorMessage = "Invalid credentials.";
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
