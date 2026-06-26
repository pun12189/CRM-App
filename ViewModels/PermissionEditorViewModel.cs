using CallMan.Models;
using CallMan.Models.Enums;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace CallMan.ViewModels
{
    public partial class PermissionEditorViewModel : ObservableObject
    {
        private readonly PermissionService _permissionService;

        public UserRole TargetRole { get; }
        public string WindowTitle => $"Edit Permissions - {TargetRole}";

        [ObservableProperty] private ObservableCollection<PermissionRow> _permissionRows = new();

        // The master view tracker required for text-filtering rules
        public ICollectionView FilteredRowsView { get; }
        [ObservableProperty] private string _searchText = string.Empty;

        public PermissionEditorViewModel(PermissionService permissionService, UserRole targetRole)
        {
            _permissionService = permissionService;
            TargetRole = targetRole;
            FilteredRowsView = CollectionViewSource.GetDefaultView(PermissionRows);
            FilteredRowsView.Filter = ExecuteSearchFilteringCriteria;
            _ = LoadPermissionsMatrixAsync();
        }

        private async Task LoadPermissionsMatrixAsync()
        {
            var data = await _permissionService.GetMatrixForRoleAsync(TargetRole);

            PermissionRows.Clear();
            foreach (var item in data)
            {
                PermissionRows.Add(item);
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            FilteredRowsView.Refresh();
        }

        private bool ExecuteSearchFilteringCriteria(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            if (item is not PermissionRow row) return false;

            // Perform broad case-insensitive keyword filtering match checks
            return row.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   row.ModuleKey.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        [RelayCommand]
        private async Task SavePermissionsAsync(Window window)
        {
            try
            {
                await _permissionService.SaveMatrixForRoleAsync(TargetRole, PermissionRows);
                MessageBox.Show($"Permissions for {TargetRole} updated successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save permissions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CloseWindow(Window window) => window.Close();
    }
}
