using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Core;
using Tijori.Models;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class AddVendorWindowViewModel : ObservableValidator
    {
        private readonly VendorService _vendorService;
        private readonly CustomFieldService _customFieldService;

        [ObservableProperty] private int _vendorId;
        [ObservableProperty] private string _companyName = string.Empty;
        [ObservableProperty] private string _contactPerson = string.Empty;
        [ObservableProperty] private string _phone = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _gstNumber = string.Empty;
        [ObservableProperty] private string _address = string.Empty;
        [ObservableProperty] private string _status = "Active";

        [ObservableProperty] private string _windowTitle = "Register New Supplier Account";
        [ObservableProperty] private string _headerTitle = "Register New Vendor Account";
        [ObservableProperty] private string _saveButtonText = "Save Vendor";

        [ObservableProperty] private string _validationErrorMessage = string.Empty;
        [ObservableProperty] private bool _isValidationErrorVisible;

        // Custom Fields Engine Properties
        [ObservableProperty] private ModuleFieldConfigMap _fieldConfigMap = new(new List<CustomFieldDefinition>());
        [ObservableProperty] private ObservableCollection<CustomFieldInputValue> _dynamicVendorFields = new();

        public bool IsEditMode { get; }

        public AddVendorWindowViewModel(VendorService vendorService, CustomFieldService customFieldService, Vendor? existingVendor = null)
        {
            _vendorService = vendorService;
            _customFieldService = customFieldService;

            if (existingVendor != null)
            {
                IsEditMode = true;
                WindowTitle = "Update Supplier Details";
                HeaderTitle = "Edit Vendor Account";
                SaveButtonText = "Update Vendor";

                VendorId = existingVendor.VendorId;
                CompanyName = existingVendor.CompanyName ?? string.Empty;
                ContactPerson = existingVendor.ContactPerson ?? string.Empty;
                Phone = existingVendor.Phone ?? string.Empty;
                Email = existingVendor.Email ?? string.Empty;
                GstNumber = existingVendor.GstNumber ?? string.Empty;
                Address = existingVendor.Address ?? string.Empty;
                Status = existingVendor.Status ?? "Active";
            }
            else
            {
                IsEditMode = false;
            }

            _ = InitializeFormAsync();
        }

        private async Task InitializeFormAsync()
        {
            await GetCustomFields();
        }

        private async Task GetCustomFields()
        {
            // 1. Fetch field definitions for Vendor module
            var fieldDefinitions = (await _customFieldService.GetFieldsByModuleAsync("Vendor")).ToList();

            // 2. Config Map for Tier 1 & Tier 2 dynamic hints and visibility
            FieldConfigMap = new ModuleFieldConfigMap(fieldDefinitions);

            // 3. Fetch saved Tier 3 custom field values from DB if editing an existing vendor
            Dictionary<int, string> savedValues = (IsEditMode && VendorId > 0)
                ? await _customFieldService.GetEntityCustomFieldValuesAsync(VendorId, "Vendor")
                : new Dictionary<int, string>();

            App.Current.Dispatcher.Invoke(() =>
            {
                DynamicVendorFields.Clear();

                // 4. Hydrate Tier 3 dynamic custom fields with saved values
                foreach (var f in fieldDefinitions.Where(x => x.IsVisible && x.FieldTier == 3))
                {
                    // Try to pull previously saved value for this FieldId
                    savedValues.TryGetValue(f.FieldId, out string? initialValue);

                    DynamicVendorFields.Add(new CustomFieldInputValue
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

        private void ShowError(string message)
        {
            ValidationErrorMessage = message;
            IsValidationErrorVisible = true;
        }

        [RelayCommand]
        private async Task SaveVendorAsync(Window currentWindow)
        {
            IsValidationErrorVisible = false;

            // 1. TIER 1 MANDATORY VALIDATIONS
            if (string.IsNullOrWhiteSpace(CompanyName))
            {
                ShowError($"{FieldConfigMap.GetLabel("CompanyName", "Company Name")} is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(Phone))
            {
                ShowError($"{FieldConfigMap.GetLabel("Phone", "Phone Number")} is required.");
                return;
            }

            // 2. TIER 2 DYNAMIC MODEL FIELD VALIDATIONS
            if (FieldConfigMap.GetIsRequired("ContactPerson") && string.IsNullOrWhiteSpace(ContactPerson))
            {
                ShowError($"{FieldConfigMap.GetLabel("ContactPerson", "Contact Person Name")} is required.");
                return;
            }

            if (FieldConfigMap.GetIsRequired("Email") && string.IsNullOrWhiteSpace(Email))
            {
                ShowError($"{FieldConfigMap.GetLabel("Email", "Email Address")} is required.");
                return;
            }

            if (FieldConfigMap.GetIsRequired("GstNumber") && string.IsNullOrWhiteSpace(GstNumber))
            {
                ShowError($"{FieldConfigMap.GetLabel("GstNumber", "GSTIN Number")} is required.");
                return;
            }

            if (FieldConfigMap.GetIsRequired("Address") && string.IsNullOrWhiteSpace(Address))
            {
                ShowError($"{FieldConfigMap.GetLabel("Address", "Address Location")} is required.");
                return;
            }

            // 3. TIER 3 DYNAMIC CUSTOM FIELDS VALIDATION
            foreach (var customField in DynamicVendorFields)
            {
                if (customField.IsRequired && string.IsNullOrWhiteSpace(customField.FieldValue))
                {
                    ShowError($"{customField.EffectiveLabel} is required.");
                    return;
                }
            }

            var vendor = new Vendor
            {
                VendorId = VendorId,
                CompanyName = CompanyName.Trim(),
                ContactPerson = string.IsNullOrWhiteSpace(ContactPerson) ? null : ContactPerson.Trim(),
                Phone = Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                GstNumber = string.IsNullOrWhiteSpace(GstNumber) ? null : GstNumber.Trim().ToUpper(),
                Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                Status = Status
            };

            int targetVendorId = VendorId;
            bool success;

            if (IsEditMode)
            {
                success = await _vendorService.UpdateVendorAsync(vendor);
            }
            else
            {
                targetVendorId = await _vendorService.SaveVendorAsync(vendor);
                success = targetVendorId > 0;
            }

            if (success)
            {
                // 4. PERSIST TIER 3 DYNAMIC CUSTOM FIELD VALUES TO DATABASE
                var customValues = DynamicVendorFields
                    .Select(cf => new KeyValuePair<int, string>(cf.FieldId, cf.FieldValue ?? string.Empty));

                await _customFieldService.SaveEntityCustomFieldValuesAsync(targetVendorId, "Vendor", customValues);

                if (currentWindow != null)
                {
                    currentWindow.DialogResult = true;
                    currentWindow.Close();
                }
            }
            else
            {
                ShowError("Failed to save vendor details. Please try again.");
            }
        }

        [RelayCommand]
        private void CloseWindow(Window currentWindow)
        {
            if (currentWindow != null)
            {
                currentWindow.DialogResult = false;
                currentWindow.Close();
            }
        }
    }
}
