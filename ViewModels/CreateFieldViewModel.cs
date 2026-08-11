using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tijori.Models;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class CreateFieldViewModel : ObservableObject
    {
        private readonly CustomFieldService _fieldService;
        public event Action<bool>? RequestClose;

        [ObservableProperty]
        private CustomFieldDefinition _newField = new();

        [ObservableProperty]
        private string _newValueOptionText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _filteredModelProperties = new();

        [ObservableProperty] private bool _isEditMode;

        // Tier Radio States
        [ObservableProperty] private bool _isTier2Selected;
        [ObservableProperty] private bool _isTier3Selected = true;

        public string DialogTitle => (IsEditMode, NewField.FieldTier) switch
        {
            (true, 1) => $"Rename Mandatory Field ({NewField.ModuleType})",
            (true, _) => $"Edit Field ({NewField.ModuleType})",
            (false, 2) => $"Add Model Field ({NewField.ModuleType})",
            _ => $"Create Custom Field ({NewField.ModuleType})"
        };

        public bool IsFieldNameEditable => !IsEditMode && NewField.FieldTier == 3;

        public CreateFieldViewModel(CustomFieldService fieldService)
        {
            _fieldService = fieldService;
            NewField.SeedValueOptionsList = new ObservableCollection<string>();
        }

        public void InitializeAvailableModelProperties(string moduleType)
        {
            NewField.ModuleType = moduleType;
            UpdateFilteredPropertiesList();
        }

        partial void OnIsTier2SelectedChanged(bool value)
        {
            // FIX: Prevent overwriting IsVisible/IsRequired during Edit Mode!
            if (value && !IsEditMode)
            {
                NewField.FieldTier = 2;
                NewField.IsVisible = true;
                NewField.IsRequired = false;
                UpdateFilteredPropertiesList();
            }
        }

        partial void OnIsTier3SelectedChanged(bool value)
        {
            // FIX: Prevent overwriting existing labels/flags during Edit Mode!
            if (value && !IsEditMode)
            {
                NewField.FieldTier = 3;
                NewField.FieldName = string.Empty;
                NewField.DisplayLabel = string.Empty;
                NewField.FieldType = "Textbox";
                NewField.IsVisible = true;
                NewField.IsRequired = false;
                OnPropertyChanged(nameof(DialogTitle));
                OnPropertyChanged(nameof(IsFieldNameEditable));
            }
        }

        private void UpdateFilteredPropertiesList()
        {
            OnPropertyChanged(nameof(DialogTitle));

            List<string> properties = NewField.FieldTier switch
            {
                // TIER 1: MANDATORY FIELDS
                1 => NewField.ModuleType switch
                {
                    "Lead" or "Customer" => new List<string> { "CustomerName", "Phone", "LeadHolder" },
                    "Product" => new List<string> { "Name" },
                    "Vendor" => new List<string> { "CompanyName", "Phone" },
                    "Staff" => new List<string> { "FullName", "Email", "Role" },
                    "Order" => new List<string>
                    {
                        "LeadId", "OrderDate", "TotalAmount",
                        "ProductId", "Quantity", "UnitPrice"
                    },
                    "Purchase" => new List<string>
                    {
                        "PoNumber", "VendorId", "OrderDate", "TotalAmount",
                        "ProductId", "Quantity", "UnitPrice"
                    },
                    _ => new List<string>()
                },

                // TIER 2: STANDARD MODEL FIELDS
                2 => NewField.ModuleType switch
                {
                    "Lead" => new List<string>
                {
                    "Email", "AltPhone", "CompanyName", "AddressLine",
                    "Pincode", "City", "District", "State", "Country"
                },

                    "Customer" => new List<string>
                {
                    "Email", "AltPhone", "CompanyName", "AddressLine",
                    "Pincode", "City", "District", "State", "Country",
                    "WorkingArea", "MonthlyTarget"
                },

                    "Product" => new List<string>
                {
                    "ShortName", "SKU", "Unit", "CategoryId", "BrandName",
                    "Manufacturer", "Packaging", "InitialStock", "CostPrice",
                    "MRP", "SellingPrice", "GSTPercent", "BatchNumber", "MfgDate", "ExpiryDate"
                },
                    "Vendor" => new List<string> { "ContactPerson", "Email", "GstNumber", "Address", "Status" },
                    "Staff" => new List<string> { "Phone", "DepartmentId", "SeniorId", "MonthlyTarget", "IsActive" },
                    "Order" => new List<string>
                {
                    "InvoiceNumber", "ProformaNumber", "OrderType", "PaymentStatus",
                    "AmountPaid", "ProcessedBy", "LeadHolder", "PreferedTransport",
                    "Status", "Remarks", "Description", "TotalCostAmount", "DivisionId",
                    "BatchId", "BatchNumber", "ExpiryDate", "CostPrice", "GSTPercent",
                    "SubTotal", "GstAmount", "Total", "ChargeName", "ChargeAmount"
                },
                    "Purchase" => new List<string>
                {
                    "ExpectedDeliveryDate", "ActualDeliveryDate", "OrderStatus",
                    "CreatedBy", "SupplierSku", "TotalCost"
                },
                    _ => new List<string>()
                },

                _ => new List<string>()
            };

            FilteredModelProperties = new ObservableCollection<string>(properties);
        }

        [ObservableProperty]
        private string? _selectedModelPropertyName;

        partial void OnSelectedModelPropertyNameChanged(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                NewField.FieldName = value;

                // FIX: Only auto-generate DisplayLabel if it's currently empty AND NOT in Edit Mode
                if (string.IsNullOrWhiteSpace(NewField.DisplayLabel) && !IsEditMode)
                {
                    NewField.DisplayLabel = SplitCamelCase(value);
                }
            }
        }

        private static string SplitCamelCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
        }

        [RelayCommand]
        private void AddValueOption()
        {
            if (string.IsNullOrWhiteSpace(NewValueOptionText)) return;

            var rawItems = NewValueOptionText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in rawItems)
            {
                string cleanValue = item.Trim();
                if (!string.IsNullOrEmpty(cleanValue) && !NewField.SeedValueOptionsList.Contains(cleanValue))
                {
                    NewField.SeedValueOptionsList.Add(cleanValue);
                }
            }

            NewValueOptionText = string.Empty;
        }

        [RelayCommand]
        private void RemoveValueOption(string optionToRemove)
        {
            if (optionToRemove != null && NewField.SeedValueOptionsList.Contains(optionToRemove))
            {
                NewField.SeedValueOptionsList.Remove(optionToRemove);
            }
        }

        [RelayCommand]
        private async Task SubmitCustomField()
        {
            if (string.IsNullOrWhiteSpace(NewField.FieldName)) return;

            // Tier 1 Mandatory Fields are always required and visible
            if (NewField.FieldTier == 1)
            {
                NewField.IsRequired = true;
                NewField.IsVisible = true;
            }

            if (string.IsNullOrWhiteSpace(NewField.DisplayLabel))
            {
                NewField.DisplayLabel = SplitCamelCase(NewField.FieldName);
            }

            // Clear option lists only for non-dropdown types
            if (NewField.FieldType != "DropdownSingle" && NewField.FieldType != "DropdownMultiple")
            {
                NewField.SeedValueOptionsList.Clear();
            }

            bool isSaved = await _fieldService.SaveCustomFieldAsync(NewField);
            if (isSaved)
            {
                RequestClose?.Invoke(true);
            }
        }

        [RelayCommand]
        private void CloseDialog()
        {
            RequestClose?.Invoke(false);
        }
    }
}
