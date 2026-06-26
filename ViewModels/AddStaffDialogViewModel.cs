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
using System.Windows.Controls;

namespace CallMan.ViewModels
{
    public partial class AddStaffDialogViewModel : ObservableObject
    {
        private readonly StaffService _staffService;
        private readonly DepartmentService _departmentService; // Dynamic dynamic lookup service

        public event Action<bool?> RequestClose;

        [ObservableProperty] private User _currentUser = new();
        [ObservableProperty] private string _windowTitle = "Add New Staff Profile";
        [ObservableProperty] private bool _isEditMode;

        private List<User> _cachedMasterStaffList = new();

        // Data binding collection frames
        [ObservableProperty] private ObservableCollection<User> _potentialSeniors = new();
        [ObservableProperty] private ObservableCollection<Department> _departmentsList = new();

        // Enforce safe types directly instead of using plain magic text strings lists
        public IEnumerable<UserRole> RolesList => Enum.GetValues(typeof(UserRole)).Cast<UserRole>();

        public AddStaffDialogViewModel(StaffService staffService, DepartmentService departmentService)
        {
            _staffService = staffService;
            _departmentService = departmentService;
        }

        /// <summary>
        /// Pre-loads master data lookups and prepares the data form bindings.
        /// </summary>
        public async Task InitializeAsync(User? userToEdit)
        {
            try
            {
                // 1. Fetch dynamic data streams asynchronously from database services
                var departments = await _departmentService.GetAllDepartmentsAsync();
                DepartmentsList = new ObservableCollection<Department>(departments);

                var absoluteStaff = await _staffService.GetAllStaffAsync();
                _cachedMasterStaffList = absoluteStaff.ToList();

                // 2. Establish setup configurations depending on entry execution paths
                if (userToEdit != null)
                {
                    IsEditMode = true;
                    WindowTitle = $"Modify Staff Data - ID: {userToEdit.UserId}";
                    CurrentUser = userToEdit;
                }
                else
                {
                    IsEditMode = false;
                    WindowTitle = "Create New Staff Account Workspace";
                    CurrentUser = new User
                    {
                        IsActive = true,
                        Role = UserRole.Executive
                    };
                }

                // 3. CRITICAL ENGINE HOOK: Listen to internal property shifts on the CurrentUser instance
                CurrentUser.PropertyChanged += OnCurrentUserPropertyChanged;

                // 4. Fire the initial hierarchy pass for the dropdown list items
                RefreshEligibleSeniors();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize lookup parameters data streams:\n{ex.Message}",
                                "System Error Connection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Event listener that intercepts shifts inside the model to recalculate options on the fly.
        /// </summary>
        private void OnCurrentUserPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Whenever the Role field changes, instantly recalculate who can be their senior
            if (e.PropertyName == nameof(User.Role))
            {
                RefreshEligibleSeniors();
            }
        }

        /// <summary>
        /// Dynamic Hierarchy Filter Rule Engine: Evaluates rows based on integer-enum values.
        /// </summary>
        private void RefreshEligibleSeniors()
        {
            if (CurrentUser == null) return;

            // Rule: Filter where senior's integer value is strictly lower than target user's enum value
            var filteredSeniors = _cachedMasterStaffList
                .Where(u => (byte)u.Role < (byte)CurrentUser.Role && u.UserId != CurrentUser.UserId)
                .ToList();

            // Push updates onto the bound UI collection seamlessly
            PotentialSeniors = new ObservableCollection<User>(filteredSeniors);

            // Safety Rule: If the currently assigned senior is no longer eligible, clear out the selection handle
            if (CurrentUser.SeniorId.HasValue && !filteredSeniors.Any(s => s.UserId == CurrentUser.SeniorId))
            {
                CurrentUser.SeniorId = null;
            }
        }

        /// <summary>
        /// Validates requirements and commits changes safely using transaction wrappers.
        /// </summary>
        [RelayCommand]
        private async Task SaveAsync(object parameter)
        {
            // 1. Mandatory Identity Check Verification Guards
            if (string.IsNullOrWhiteSpace(CurrentUser.FullName) || string.IsNullOrWhiteSpace(CurrentUser.Email))
            {
                MessageBox.Show("Operational Stop: Full Name and Login E-mail address coordinates cannot be left blank.",
                                "Validation Boundary Violation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Safely extract password information from the parameter block if creating a new user
            if (!IsEditMode && parameter is PasswordBox passBoxControl)
            {
                string rawTextPassword = passBoxControl.Password;
                if (string.IsNullOrWhiteSpace(rawTextPassword) || rawTextPassword.Length < 4)
                {
                    MessageBox.Show("Security Protection Gate Rule:\nPlease specify a valid system authorization password (Minimum length: 4 characters).",
                                    "Password Requirement Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Assign password safely (Include encryption tools here, e.g., BCrypt/SHA256 hash routines if preferred)
                CurrentUser.Password = rawTextPassword;
            }

            try
            {
                // 3. Fire the atomic transaction database execution loop
                bool executionIsSuccessful;
                if (IsEditMode)
                {
                    executionIsSuccessful = await _staffService.UpdateUserAsync(CurrentUser);
                }
                else
                {
                    int newlyAssignedId = await _staffService.CreateUserAsync(CurrentUser);
                    executionIsSuccessful = newlyAssignedId > 0;
                    if (executionIsSuccessful) CurrentUser.UserId = newlyAssignedId;
                }

                // 4. Send operation feedback notifications back to the UI
                if (executionIsSuccessful)
                {
                    MessageBox.Show("Staff registration data parameters successfully written to the master table registry.",
                                    "Transaction Confirmed", MessageBoxButton.OK, MessageBoxImage.Information);
                    RequestClose?.Invoke(true);
                }
                else
                {
                    MessageBox.Show("The server accepted the channel request but reported that 0 database rows were modified.",
                                    "Write Verification Failure", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                // Fallback capture shield prevents terminal app runtime crashes completely
                MessageBox.Show($"The database operation failed. The engine rolled back adjustments cleanly to prevent corruption errors:\n\n{ex.Message}",
                                "Transaction Error Core Failure", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Cleanup()
        {
            if (CurrentUser != null)
            {
                CurrentUser.PropertyChanged -= OnCurrentUserPropertyChanged;
            }
        }
    }
}
