using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
        private async void NavigateToSetting(string settingType)
        {
            IsMainGridVisible = false;
            switch (settingType)
            {
                case "Staff":
                    CurrentSettingView = _serviceProvider.GetRequiredService<UserManagementViewModel>();
                    break;
                case "CProfile":
                    CurrentSettingView = _serviceProvider.GetRequiredService<CompanyProfileViewModel>();
                    break;
                case "Category":
                    CurrentSettingView = _serviceProvider.GetRequiredService<ManageCategoriesViewModel>();
                    break;
                case "OrderStages":
                    CurrentSettingView = _serviceProvider.GetRequiredService<OrderStagesViewModel>();
                    break;
                case "Departments":
                    CurrentSettingView = _serviceProvider.GetRequiredService<DepartmentsViewModel>();
                    break;
                case "Dead Reasons":                     
                case "Followup Stages":                    
                case "Mature Stages":                    
                case "Lead Tags":                    
                case "Lead Source":                    
                case "Lead Labels":
                    var genericVM = _serviceProvider.GetRequiredService<GenericSettingsViewModel>();

                    // Initialize it for the specific type (e.g., "Dead Reasons")
                    await genericVM.Initialize(settingType);

                    // Set it as the current view
                    CurrentSettingView = genericVM;
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

        private async void CommonNavigateGeneric(string name)
        {
            var genericVM = _serviceProvider.GetRequiredService<GenericSettingsViewModel>();

            // Initialize it for the specific type (e.g., "Dead Reasons")
            await genericVM.Initialize(name);

            // Set it as the current view
            CurrentSettingView = genericVM;
        }
    }
}
