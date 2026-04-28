using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.ViewModels
{
    public partial class AdminSettingsViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty] private object? _currentSettingView;
        [ObservableProperty] private bool _isMainGridVisible = true;

        public AdminSettingsViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        [RelayCommand]
        private void NavigateToSetting(string settingType)
        {
            IsMainGridVisible = false;
            switch (settingType)
            {
                case "Staff":
                    CurrentSettingView = _serviceProvider.GetRequiredService<UserManagementViewModel>();
                    break;
                case "Permissions":
                    // CurrentSettingView = _serviceProvider.GetRequiredService<PermissionsViewModel>();
                    break;
                // Add cases for Fetch Inquiries, Workflows, etc.
                default:
                    IsMainGridVisible = true;
                    break;
            }
        }

        [RelayCommand]
        private void BackToGrid()
        {
            CurrentSettingView = null;
            IsMainGridVisible = true;
        }
    }
}
