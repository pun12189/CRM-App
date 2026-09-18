using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Core;
using Tijori.Dialogs;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class UserManagementViewModel : ObservableObject
    {
        private readonly IDialogService _dialogService;
        private readonly StaffService _staffService;
        private readonly AdminSettingsViewModel _adminSettingsViewModel;

        [ObservableProperty] private ObservableCollection<User> _usersList = new();

        public UserManagementViewModel(IDialogService dialogService, StaffService staffService, AdminSettingsViewModel adminSettingsViewModel)
        {
            _dialogService = dialogService;
            _staffService = staffService;
            _adminSettingsViewModel = adminSettingsViewModel;
            _ = LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                var allUsers = await _staffService.GetAllStaffAsync();
                UsersList = new ObservableCollection<User>(allUsers);
            }
            catch (ApplicationException appEx)
            {
                // Intercepts our cleanly wrapped custom exception messages safely
                MessageBox.Show(appEx.Message, "Database Restriction", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                // Ultimate fallback safety net: intercepts unexpected critical infrastructure errors (e.g., Server Offline)
                MessageBox.Show($"A critical communication error occurred while saving to the server:\n\n{ex.Message}",
                                "System Connection Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        [RelayCommand]
        private async Task OpenAddStaffDialog()
        {
            // Pass 'null' for a new staff member
            var result = await _dialogService.ShowAddStaffWindow(null);
            if (result == true)
            {
                await LoadData(); // Refresh table after adding
            }
        }

        [RelayCommand]
        private async Task EditUser(User user)
        {
            // Pass existing user to the dialog for editing
            var result = await _dialogService.ShowAddStaffWindow(user);
            if (result == true)
            {
                await LoadData();
            }
        }

        [RelayCommand]
        private async Task DeleteUser(User user)
        {
            if (user == null) return;

            if (user.Role == UserRole.Admin)
            {
                MessageBox.Show("Administrator accounts cannot be deactivated or deleted.",
                                "Action Denied", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            string promptMessage =
                $"Manage staff removal for: {user.FullName} ({user.Role})\n\n" +
                "• Click [Yes] to DEACTIVATE (Recommended - keeps transaction & ledger history).\n" +
                "• Click [No] to PERMANENTLY DELETE (Hard delete from the database).\n" +
                "• Click [Cancel] to abort.";

            MessageBoxResult choice = MessageBox.Show(
                promptMessage,
                "Deactivate or Permanently Delete?",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (choice == MessageBoxResult.Cancel) return;

            try
            {
                bool success = false;

                if (choice == MessageBoxResult.Yes)
                {
                    // 1. Soft Delete / Deactivate (Sets IsActive = 0)
                    success = await _staffService.SoftDeleteUserAsync(user);
                }
                else if (choice == MessageBoxResult.No)
                {
                    // Double check before permanent removal
                    var confirmHardDelete = MessageBox.Show(
                        $"Are you absolutely sure you want to PERMANENTLY delete {user.FullName}?\nThis action cannot be undone.",
                        "Confirm Permanent Deletion",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Error);

                    if (confirmHardDelete != MessageBoxResult.OK) return;

                    // 2. Hard Delete (Physical DELETE from database)
                    success = await _staffService.DeleteUserAsync(user.UserId);
                }

                if (success)
                {
                    UsersList.Remove(user);
                }
            }
            catch (InvalidOperationException opEx)
            {
                MessageBox.Show(opEx.Message, "Security Policy", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Operation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task ShowStaffDetails(User selectedUser)
        {
            if (selectedUser == null) return;

            // Tell AdminSettingsViewModel to swap CurrentSettingView to StaffDetailsViewModel
            await _adminSettingsViewModel.OpenStaffDetailsAsync(selectedUser);
        }

        [RelayCommand]
        private async Task ImportStaff()
        {
            var vm = App.ServiceProvider.GetRequiredService<ImportViewModel>();
            await vm.InitializeAsync(ImportType.Staff);
            var dialogWindow = new ImportView { DataContext = vm };
            // No need for a close event here since the ImportViewModel can directly call LoadStaff() after a successful import
            vm.RequestClose += (result) =>
            {
                dialogWindow.DialogResult = result;
                dialogWindow.Close();
            };

            if (dialogWindow.ShowDialog() == true)
            {
                // Re-run the query to show the new lead in the DataGrid
                await LoadData();
            }
        }
    }
}
