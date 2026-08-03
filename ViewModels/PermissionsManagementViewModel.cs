using Tijori.Dialogs;
using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.ViewModels
{
    public partial class PermissionsManagementViewModel : ObservableObject
    {
        private readonly PermissionService _permissionService;

        [ObservableProperty] private ObservableCollection<RoleSummary> _roleSummaries = new();

        public PermissionsManagementViewModel(PermissionService permissionService)
        {
            _permissionService = permissionService;
            _ = InitializeDashboardDataAsync();
        }

        public async Task InitializeDashboardDataAsync()
        {
            var summaryData = await _permissionService.GetRoleSummariesAsync();
            RoleSummaries = new ObservableCollection<RoleSummary>(summaryData);
        }

        [RelayCommand]
        private void LaunchMatrixEditor(RoleSummary selection)
        {
            if (selection == null || !selection.CanEdit) return;

            // Resolve host visual frames to properly attach overlay child owners modal structures
            var activeMainWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                   ?? Application.Current.MainWindow;

            var vm = new PermissionEditorViewModel(_permissionService, selection.Role);

            var editorPopup = new PermissionEditorWindow()
            {
                Owner = activeMainWindow,
                DataContext = vm,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            // Execution halts here until the user submits changes or hits close
            bool? results = editorPopup.ShowDialog();

            if (results == true)
            {
                // Refresh data states to reflect changes instantly if anything modified
                _ = InitializeDashboardDataAsync();
            }
        }
    }
}
