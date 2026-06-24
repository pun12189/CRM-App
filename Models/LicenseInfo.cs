using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class LicenseInfo : ObservableObject
    {
        [ObservableProperty] private string _systemId = string.Empty;
        [ObservableProperty] private bool _isFullVersion;
        [ObservableProperty] private int _daysRemaining;
        [ObservableProperty] private int _maxTrialDays = 7; // Set trial configuration duration here

        [ObservableProperty] private bool _isOnlineServicesEnabled;
        [ObservableProperty] private bool _isLocalDatabase;

        // Feature gating flags derived from current subscription thresholds
        public bool IsTrialActive => !IsFullVersion && DaysRemaining > 0;
        public bool IsExpired => !IsFullVersion && DaysRemaining <= 0;

        public string StatusMessage => IsFullVersion
            ? "Full Enterprise Version Active"
            : IsExpired
                ? "Trial Expired - Advanced Operations and Feature Blocks Active"
                : $"Trial Copy: {DaysRemaining} Days Left before activation requirement constraint enforces lock.";

        /// <summary>
        /// Quick-access operational gate checking if online features are currently allowed to execute.
        /// </summary>
        public bool AreOnlineServicesAllowed => IsFullVersion && (IsOnlineServicesEnabled || !IsLocalDatabase);
    }
}
