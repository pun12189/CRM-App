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
using Tijori.Core;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class AddStaffDialogViewModel : ObservableObject
    {
        private readonly StaffService _staffService;
        private readonly DepartmentService _departmentService;
        private readonly CustomFieldService _customFieldService;

        public event Action<bool?>? RequestClose;

        [ObservableProperty] private User _currentUser = new();
        [ObservableProperty] private string _windowTitle = "Add New Staff Profile";
        [ObservableProperty] private bool _isEditMode;

        [ObservableProperty] private string _validationErrorMessage = string.Empty;
        [ObservableProperty] private bool _isValidationErrorVisible;

        private List<User> _cachedMasterStaffList = new();

        // Data binding collection frames
        [ObservableProperty] private ObservableCollection<User> _potentialSeniors = new();
        [ObservableProperty] private ObservableCollection<Department> _departmentsList = new();

        // Custom Fields Engine Properties
        [ObservableProperty] private ModuleFieldConfigMap _fieldConfigMap = new(new List<CustomFieldDefinition>());
        [ObservableProperty] private ObservableCollection<CustomFieldInputValue> _dynamicStaffFields = new();

        public IEnumerable<UserRole> RolesList => Enum.GetValues(typeof(UserRole)).Cast<UserRole>();

        public AddStaffDialogViewModel(
            StaffService staffService,
            DepartmentService departmentService,
            CustomFieldService customFieldService)
        {
            _staffService = staffService;
            _departmentService = departmentService;
            _customFieldService = customFieldService;
        }

        public async Task InitializeAsync(User? userToEdit)
        {
            try
            {
                // 1. Fetch dynamic data lookups
                var departments = await _departmentService.GetAllDepartmentsAsync();
                DepartmentsList = new ObservableCollection<Department>(departments);

                var absoluteStaff = await _staffService.GetAllStaffAsync();
                _cachedMasterStaffList = absoluteStaff.ToList();

                // 2. Hydrate Custom Field Configurations
                await GetCustomFields();

                // 3. Configure Entry Execution Paths
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

                CurrentUser.PropertyChanged += OnCurrentUserPropertyChanged;
                RefreshEligibleSeniors();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to initialize lookup parameters: {ex.Message}");
            }
        }

        private async Task GetCustomFields()
        {
            // 1. Fetch field definitions for Staff module
            var fieldDefinitions = (await _customFieldService.GetFieldsByModuleAsync("Staff")).ToList();

            // 2. Config Map for Tier 1 & Tier 2 dynamic hints and visibility
            FieldConfigMap = new ModuleFieldConfigMap(fieldDefinitions);

            // 3. Fetch saved Tier 3 custom field values from DB if editing an existing staff member
            Dictionary<int, string> savedValues = (IsEditMode && CurrentUser.UserId > 0)
                ? await _customFieldService.GetEntityCustomFieldValuesAsync(CurrentUser.UserId, "Staff")
                : new Dictionary<int, string>();

            App.Current.Dispatcher.Invoke(() =>
            {
                DynamicStaffFields.Clear();

                // 4. Hydrate Tier 3 dynamic custom fields with saved values
                foreach (var f in fieldDefinitions.Where(x => x.IsVisible && x.FieldTier == 3))
                {
                    // Try to pull previously saved value for this FieldId
                    savedValues.TryGetValue(f.FieldId, out string? initialValue);

                    DynamicStaffFields.Add(new CustomFieldInputValue
                    {
                        FieldId = f.FieldId,
                        FieldName = f.FieldName,
                        DisplayLabel = f.DisplayLabel,
                        FieldType = f.FieldType,
                        FieldTier = f.FieldTier,
                        IsRequired = f.IsRequired,
                        FieldValue = initialValue ?? string.Empty, // 👈 HYDRATES SAVED VALUE IN EDIT MODE
                        OptionsList = f.SeedValueOptionsList ?? new ObservableCollection<string>()
                    });
                }
            });
        }

        private void OnCurrentUserPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(User.Role))
            {
                RefreshEligibleSeniors();
            }
        }

        private void RefreshEligibleSeniors()
        {
            if (CurrentUser == null) return;

            var filteredSeniors = _cachedMasterStaffList
                .Where(u => (byte)u.Role < (byte)CurrentUser.Role && u.UserId != CurrentUser.UserId)
                .ToList();

            PotentialSeniors = new ObservableCollection<User>(filteredSeniors);

            if (CurrentUser.SeniorId.HasValue && !filteredSeniors.Any(s => s.UserId == CurrentUser.SeniorId))
            {
                CurrentUser.SeniorId = null;
            }
        }

        private void ShowError(string message)
        {
            ValidationErrorMessage = message;
            IsValidationErrorVisible = true;
        }

        [RelayCommand]
        private async Task SaveAsync(object parameter)
        {
            IsValidationErrorVisible = false;

            // 1. TIER 1 MANDATORY VALIDATIONS
            if (string.IsNullOrWhiteSpace(CurrentUser.FullName))
            {
                ShowError($"{FieldConfigMap.GetLabel("FullName", "Full Name")} is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentUser.Email))
            {
                ShowError($"{FieldConfigMap.GetLabel("Email", "Email ID")} is required.");
                return;
            }

            // 2. TIER 2 DYNAMIC MODEL FIELD VALIDATIONS
            if (FieldConfigMap.GetIsRequired("Phone") && string.IsNullOrWhiteSpace(CurrentUser.Phone))
            {
                ShowError($"{FieldConfigMap.GetLabel("Phone", "Phone Number")} is required.");
                return;
            }

            if (FieldConfigMap.GetIsRequired("DepartmentId") && (CurrentUser.DepartmentId == 0))
            {
                ShowError($"{FieldConfigMap.GetLabel("DepartmentId", "Department Location")} is required.");
                return;
            }

            if (FieldConfigMap.GetIsRequired("SeniorId") && !CurrentUser.SeniorId.HasValue)
            {
                ShowError($"{FieldConfigMap.GetLabel("SeniorId", "Senior / Team Leader")} is required.");
                return;
            }

            // 3. TIER 3 DYNAMIC CUSTOM FIELDS VALIDATION
            foreach (var customField in DynamicStaffFields)
            {
                if (customField.IsRequired && string.IsNullOrWhiteSpace(customField.FieldValue))
                {
                    ShowError($"{customField.EffectiveLabel} is required.");
                    return;
                }
            }

            // 4. PASSWORD SECURITY CHECK
            if (!IsEditMode && parameter is PasswordBox passBoxControl)
            {
                string rawTextPassword = passBoxControl.Password;
                if (string.IsNullOrWhiteSpace(rawTextPassword) || rawTextPassword.Length < 4)
                {
                    ShowError("Specify a valid system login password (Minimum length: 4 characters).");
                    return;
                }

                CurrentUser.Password = BCrypt.Net.BCrypt.HashPassword(rawTextPassword);
            }

            try
            {
                int targetUserId = CurrentUser.UserId;
                bool executionIsSuccessful = false;

                if (IsEditMode)
                {
                    executionIsSuccessful = await _staffService.UpdateUserAsync(CurrentUser);
                }
                else
                {
                    targetUserId = await _staffService.CreateUserAsync(CurrentUser);
                    executionIsSuccessful = targetUserId > 0;
                    if (executionIsSuccessful) CurrentUser.UserId = targetUserId;
                }

                if (executionIsSuccessful)
                {
                    // 5. PERSIST TIER 3 DYNAMIC CUSTOM FIELD VALUES TO DATABASE
                    var customValues = DynamicStaffFields
                        .Select(cf => new KeyValuePair<int, string>(cf.FieldId, cf.FieldValue ?? string.Empty));

                    await _customFieldService.SaveEntityCustomFieldValuesAsync(targetUserId, "Staff", customValues);

                    RequestClose?.Invoke(true);
                }
                else
                {
                    ShowError("Failed to write staff profile to database.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Transaction fault: {ex.Message}");
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
