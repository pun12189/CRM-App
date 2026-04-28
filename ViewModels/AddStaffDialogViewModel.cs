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

namespace CallMan.ViewModels
{
    public partial class AddStaffDialogViewModel : ObservableObject
    {
        private readonly LeadService _leadService;
        public event Action<bool?> RequestClose;

        [ObservableProperty] private User _currentUser = new();
        [ObservableProperty] private string _windowTitle = "Add New Staff";
        [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private ObservableCollection<User> _potentialSeniors = new();

        public List<string> Roles { get; } = new() { "Executive", "Team Leader", "Sub-Admin" };

        public AddStaffDialogViewModel(LeadService leadService)
        {
            _leadService = leadService;
        }

        public async void Initialize(User? userToEdit)
        {
            // Load Potential Seniors for the dropdown
            var all = await _leadService.GetAllUsersAsync();
            PotentialSeniors = new ObservableCollection<User>(all.Where(u => u.Role != "Executive"));

            if (userToEdit != null)
            {
                IsEditMode = true;
                WindowTitle = "Edit Staff Member";
                CurrentUser = userToEdit; // Map fields as needed
            }
            else
            {
                IsEditMode = false;
                WindowTitle = "Add New Staff";
                CurrentUser = new User { IsActive = true, Role = "Executive" };
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            // Add Password from PasswordBox via code-behind or helper if needed
            bool success = IsEditMode
                ? await _leadService.UpdateUserAsync(CurrentUser)
                : await _leadService.CreateUserAsync(CurrentUser) > 0;

            if (success) RequestClose?.Invoke(true);
        }
    }
}
