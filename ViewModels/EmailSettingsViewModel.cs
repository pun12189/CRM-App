using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Tijori.ViewModels
{
    public partial class EmailSettingsViewModel : ObservableObject
    {
        private readonly EmailService _emailService;

        [ObservableProperty] private EmailSettings _settings;

        public EmailSettingsViewModel(EmailService emailService)
        {
            _emailService = emailService;
            _ = LoadSettings();
        }

        private async Task LoadSettings()
        {
            var data = await _emailService.GetDefaultSettingsAsync();
            Settings = data ?? new EmailSettings();
        }

        [RelayCommand]
        private async Task SaveSettings(object passwordContainer)
        {
            if (passwordContainer is PasswordBox pwBox)
            {
                // Update the password in the model before saving
                Settings.Password = pwBox.Password;
            }

            bool success = await _emailService.SaveSettingsAsync(Settings);

            if (success)
            {
                MessageBox.Show("Email settings saved successfully!", "SofricONE",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private async Task TestConnection(object passwordContainer)
        {
            try
            {
                var password = ((PasswordBox)passwordContainer).Password;
                if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(Settings.Password))
                {
                    password = Settings.Password; // Use the saved password if none is provided
                }
                else if (string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("Please enter the email password to test the connection");
                    return;
                }

                // Simple attempt to send a 'Hello' email to the sender's own address
                await _emailService.SendEmailAsync(
                    Settings.EmailAddress,
                    "SofricONE - Connection Test",
                    "SMTP Configuration is successful!"
                );

                MessageBox.Show("Test Email Sent Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
