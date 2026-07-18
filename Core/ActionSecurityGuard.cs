using CallMan.Dialogs;
using CallMan.Interfaces;
using CallMan.Models.Enums;
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

            // 2. Automatically allow access if the current logged-in employee is the Administrator
            var currentUser = _session;
            if (currentUser != null && currentUser.UserRole == UserRole.Admin.ToString()) return true;

            // 3. Fetch the active Admin secret key context values safely over the LAN database layer
            string adminSecret = await _staffService.GetAdminSecretKeyAsync();
            if (string.IsNullOrEmpty(adminSecret)) return false;

            // 4. Challenge: Present the global authorization modal dialog input window interface components
            return Application.Current.Dispatcher.Invoke(() =>
            {
                var dialogViewModel = new AdminVerificationViewModel(_2faService, _securityRepo, adminSecret);
                var dialogWindow = new AdminVerificationDialog() { Owner = Application.Current.MainWindow, DataContext = dialogViewModel };
                dialogWindow.ShowDialog();
                return dialogViewModel.IsAuthorized;
            });
        }
    }
}
