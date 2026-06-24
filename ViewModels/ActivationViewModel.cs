using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class ActivationViewModel : ObservableObject
    {
        private readonly LicenseService _licenseService;
        public event Action? RequestClose;

        [ObservableProperty] private string _challengeSystemId = string.Empty;
        [ObservableProperty] private string _enteredSerialResponseKey = string.Empty;
        [ObservableProperty] private string _errorNotificationMessage = string.Empty;
        [ObservableProperty] private bool _isActionExecuting;

        public ActivationViewModel(LicenseService licenseService)
        {
            _licenseService = licenseService;

            // Retrieve token parameters straight from local memory cache pointers instantly
            ChallengeSystemId = Core.LicenseManager.Current.SystemId;
        }

        [RelayCommand]
        private async Task SubmitLicenseRegistration()
        {
            if (string.IsNullOrWhiteSpace(EnteredSerialResponseKey))
            {
                ErrorNotificationMessage = "Please enter your activation license serial key.";
                return;
            }

            IsActionExecuting = true;
            ErrorNotificationMessage = string.Empty;

            // Submit input parameters across network pipelines safely
            bool success = await _licenseService.ActivateSoftwareAsync(EnteredSerialResponseKey);

            if (success)
            {
                // Force an immediate synchronous cache refresh to update the local computer instance
                await Core.LicenseManager.RefreshCacheAsync();
                IsActionExecuting = false;

                MessageBox.Show(
                    "Tijori has been activated successfully across your network deployment!\n\n" +
                    "You can finish your active operation safely. The platform will now execute a graceful restart sequence to fully load your premium modules.",
                    "Activation Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                ExecuteGracefulSystemRestart();
            }
            else
            {
                ErrorNotificationMessage = "Invalid serial signature response code. Please try again or check with your administrator.";
                IsActionExecuting = false;
            }
        }

        private void ExecuteGracefulSystemRestart()
        {
            // Spawn a parallel duplicate runtime assembly application instance thread
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);

            // Shut down current interface operations smoothly without corrupting open database pipelines
            Application.Current.Shutdown();
        }
    }
}
