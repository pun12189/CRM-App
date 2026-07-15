using CallMan.Dialogs;
using CallMan.Interfaces;
using CallMan.Services;
using CallMan.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.Core
{
    public class ActionSecurityGuard : IActionSecurityGuard
    {
        private readonly IGlobalSettingsService _securityRepo;
        private readonly ITwoFactorService _2faService;
        private readonly IUserSession _session;
        private readonly StaffService _staffService;

        public ActionSecurityGuard(IGlobalSettingsService securityRepo, ITwoFactorService faService, IUserSession session, StaffService staffService)
        {
            _securityRepo = securityRepo;
            _2faService = faService;
            _session = session;
            _staffService = staffService;
        }

        /// <summary>
        /// Evaluates company security policies globally and runs security challenges automatically.
        /// </summary>
        public async Task<bool> IsActionAuthorizedAsync()
        {
            // 1. Immediately drop out if the system-wide global 2FA policy switch is False
            bool isGlobal2FAEnabled = await _securityRepo.GetMaster2FAStatusAsync();
            if (!isGlobal2FAEnabled) return true;

            // 2. Immediately drop out if the acting operational user profile is an Admin
            var currentUser = await _staffService.GetUserByEmailAsync(_session.CurrentUserEmail);
            if (currentUser != null && currentUser.Role == Models.Enums.UserRole.Admin) return true; // Assuming 1 = Admin

            // Fetch the Admin structural secret row values securely over local LAN database instance
            string adminSecret = await _staffService.GetAdminSecretKeyAsync();
            if (string.IsNullOrEmpty(adminSecret))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        "System Action Aborted:\n" +
                        "The Global Security Switch is active, but the System Administrator has not configured their personal 2FA Key.\n\n" +
                        "Please request the Admin to complete onboarding setup first.",
                        "Security Policy Failure - Tijori",
                        MessageBoxButton.OK,
                        MessageBoxImage.Stop
                    );
                });

                return false; // Halted: Blocks operation safely over LAN environment
            }

            // 3. Intercept: Run dialog interface challenges on the visual presentation main thread
            return Application.Current.Dispatcher.Invoke(() =>
            {
                var dialogViewModel = new AdminVerificationViewModel(_2faService, _securityRepo, adminSecret);
                var dialogWindow = new AdminVerificationDialog()
                {
                    Owner = Application.Current.MainWindow,
                    DataContext = dialogViewModel
                };

                dialogWindow.ShowDialog();
                return dialogViewModel.IsAuthorized;
            });
        }
    }
}
