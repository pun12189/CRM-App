using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class UserManagementViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly IDialogService _dialogService;

        [ObservableProperty] private ObservableCollection<User> _usersList = new();

        public UserManagementViewModel(LeadService service, IDialogService dialogService)
        {
            _dialogService = dialogService;
            _service = service;
            _ = LoadData();
        }

        private async Task LoadData()
        {
            var allUsers = await _service.GetAllUsersAsync();
            UsersList = new ObservableCollection<User>(allUsers);
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

            // Warning about Data Integrity
            string message = $"Permanently deleting {user.FullName} will remove all their records. " +
                             "It is recommended to simply 'Deactivate' them instead. Proceed with Delete?";

            MessageBoxResult isConfirmed = MessageBox.Show(message, "Permanent Deletion Warning", MessageBoxButton.OKCancel);

            if (isConfirmed == MessageBoxResult.OK)
            {
                bool success = await _service.DeleteUserAsync(user.UserId);
                if (success)
                {
                    UsersList.Remove(user);
                }
            }
        }
    }
}
