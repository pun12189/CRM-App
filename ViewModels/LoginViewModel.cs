using CallMan.Core;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CallMan.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IAuthService _authService;        

        [ObservableProperty] private string _email = "";
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private bool _isLoggingIn;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _resetEmail;
        [ObservableProperty] private bool _isLoginVisible = true;
        [ObservableProperty] private bool _isForgotVisible = false;        

        public LoginViewModel(IAuthService authService) 
        {
            _authService = authService;
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
                var passwordContainer = passwordBox as System.Windows.Controls.PasswordBox;
                string password = passwordContainer?.Password ?? "";

                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(password))
                {
                    ErrorMessage = "Email and Password are required.";
                    return;
                }

                IsLoggingIn = true;
                ErrorMessage = "";

                bool success = await _authService.AuthenticateByEmailAsync(Email, password);

                if (success)
                {
                    if (!LicenseManager.Current.IsFullVersion && LicenseManager.Current.DaysRemaining <= 2)
                    {
                        MessageBox.Show(
                            $"Attention: You are currently executing on a temporary trial phase.\n" +
                            $"Remaining time: {LicenseManager.Current.DaysRemaining} Day(s) left.",
                            "Trial Lifecycle Countdown Alert",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }

                    var mainWindow = App.ServiceProvider.GetRequiredService<MainWindow>();
                    mainWindow.Show();

                    // 2. Set the new window as the actual MainWindow of the app
                    Application.Current.MainWindow = mainWindow;

                    // 3. Now it is safe to close the login window
                    CloseCurrentWindow();

                    // 4. (Optional) Set mode back to default if you want app to close when MainWindow closes
                    Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
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
                IsBusy = false; // Hide Spinner, Enable Button
                IsLoggingIn = false;
            }
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
