using CallMan.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.ViewModels
{
    public partial class AdminVerificationViewModel : ObservableObject
    {
        private readonly ITwoFactorService _2faService;
        private readonly IGlobalSettingsService _securityRepo;
        private readonly string _adminSecret;

        [ObservableProperty] private string _adminCode;
        [ObservableProperty] private string _errorMessage;

        // Tracks the outcome state explicitly for the caller
        public bool IsAuthorized { get; private set; } = false;

        public AdminVerificationViewModel(ITwoFactorService faService, IGlobalSettingsService securityRepo, string adminSecret)
        {
            _2faService = faService;
            _securityRepo = securityRepo;
            _adminSecret = adminSecret;
        }

        [RelayCommand]
        private void Confirm()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(AdminCode) || AdminCode.Length != 6)
            {
                ErrorMessage = "Please enter a valid 6-digit code.";
                return;
            }

            // Validate the code matching matrix natively via our core service layer
            bool isValid = _2faService.VerifyAdminCode(_adminSecret, AdminCode);
            if (isValid)
            {
                IsAuthorized = true;
                CloseDialogRequested?.Invoke(this, true);
            }
            else
            {
                ErrorMessage = "Invalid authorization code. Access denied.";
                AdminCode = string.Empty;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            IsAuthorized = false;
            CloseDialogRequested?.Invoke(this, false);
        }

        // Event handler engine hook to pass execution command states cleanly to View code-behind
        public event EventHandler<bool> CloseDialogRequested;
    }
}
