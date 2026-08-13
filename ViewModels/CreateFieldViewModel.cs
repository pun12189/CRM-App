using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Models;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class CreateFieldViewModel : ObservableObject
    {
        private readonly CustomFieldService _fieldService;
        public event Action<bool>? RequestClose;

        [ObservableProperty] private string _moduleType = "Lead";
        [ObservableProperty] private CustomFieldDefinition _newField = new();
        [ObservableProperty] private CustomFieldDefinition? _selectedChipField;

        // COLUMN 1: CHIP COLLECTIONS
        [ObservableProperty] private ObservableCollection<CustomFieldDefinition> _standardModelChips = new();
        [ObservableProperty] private ObservableCollection<CustomFieldDefinition> _customFieldChips = new();

        // COLUMN 2: CONFIGURED REGISTERED FIELDS LIST
        [ObservableProperty] private ObservableCollection<CustomFieldDefinition> _registeredFieldsList = new();

        // FORM PROPERTIES
        [ObservableProperty] private string _newValueOptionText = string.Empty;
        [ObservableProperty] private ObservableCollection<string> _filteredModelProperties = new();
        [ObservableProperty] private string? _selectedModelPropertyName;
        [ObservableProperty] private bool _isEditMode;

        // TIER RADIO STATES
        [ObservableProperty] private bool _isTier2Selected;
        [ObservableProperty] private bool _isTier3Selected = true;

        public ObservableCollection<string> AvailableControlTypes { get; } = new()
        {
            "Textbox", "TextArea", "DropdownSingle", "DropdownMultiple", "CalendarClock"
        };

        public string DialogTitle => (IsEditMode, NewField.FieldTier) switch
        {
            (true, 1) => $"Rename Mandatory Field ({ModuleType})",
            (true, _) => $"Edit Field ({ModuleType})",
            (false, 2) => $"Add Model Field ({ModuleType})",
            _ => $"Manage & Custom Fields ({ModuleType})"
        };

        public bool IsFieldNameEditable => !IsEditMode && NewField.FieldTier == 3;

        public bool IsControlTypeEditable => NewField.FieldTier == 3;

        /// <summary>
        /// User-friendly notice explaining how settings take effect.
        /// </summary>
        public string ModuleInfoNotice => ModuleType switch
        {
            "Order" or "Orders" or "Purchase" or "Purchases" =>
                "These fields are used for importing Excel sheets and will not affect your standard screen forms.",
            _ =>
                "Changes will apply immediately across your forms."
        };

        public bool CanSubmitCustomField =>
    !string.IsNullOrWhiteSpace(NewField?.FieldName) &&
    !string.IsNullOrWhiteSpace(NewField?.DisplayLabel);

        public CreateFieldViewModel(CustomFieldService fieldService)
        {
            _fieldService = fieldService;
            NewField.SeedValueOptionsList = new ObservableCollection<string>();
        }

        public async Task InitializeAsync(string moduleType)
        {
            ModuleType = moduleType;
            NewField.ModuleType = moduleType;
            await LoadFieldsAsync();
            UpdateFilteredPropertiesList();
            OnPropertyChanged(nameof(ModuleInfoNotice));
        }

        partial void OnIsEditModeChanged(bool value)
        {
            OnPropertyChanged(nameof(IsFieldNameEditable));
            OnPropertyChanged(nameof(IsControlTypeEditable));
        }

        partial void OnNewFieldChanged(CustomFieldDefinition? oldValue, CustomFieldDefinition newValue)
        {
            if (oldValue != null)
            {
                oldValue.PropertyChanged -= OnNewFieldPropertyChanged;
            }

            if (newValue != null)
            {
                newValue.PropertyChanged += OnNewFieldPropertyChanged;
            }

            RefreshCanSubmit();
        }

        private void OnNewFieldPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CustomFieldDefinition.FieldName) ||
                e.PropertyName == nameof(CustomFieldDefinition.DisplayLabel))
            {
                RefreshCanSubmit();
            }
        }

        private void RefreshCanSubmit()
        {
            OnPropertyChanged(nameof(CanSubmitCustomField));
            SubmitCustomFieldCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// SINGLE SOURCE OF TRUTH: Defines standard Tier 2 model properties per module.
        /// </summary>
        private static List<string> GetStandardModelProperties(string moduleType)
        {
            return moduleType switch
            {
                "Lead" => new List<string>
        {
            "Email", "AltPhone", "CompanyName", "AddressLine",
            "Pincode", "City", "District", "State", "Country",
            "BestTimeToTalk", "DOB", "Anniversary",

            // 🌟 MISSING DROPDOWNS & METADATA
            "DivisionId", "LeadSourceId", "LeadTagIds", "LeadLabelIds"
        },

                "Customer" => new List<string>
        {
            "Email", "AltPhone", "CompanyName", "AddressLine",
            "Pincode", "City", "District", "State", "Country",
            "WorkingArea", "MonthlyTarget", "BestTimeToTalk", "DOB", "Anniversary",

            // 🌟 MISSING DROPDOWNS & METADATA
            "DivisionId", "LeadSourceId", "LeadTagIds", "LeadLabelIds"
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
            };
        }

        /// <summary>
        /// Loads database records and dynamically maps un-added Tier 2 properties to Chips.
        /// </summary>
        public async Task LoadFieldsAsync()
        {
            // 1. Fetch active saved custom/standard field definitions from DB
            var activeFields = (await _fieldService.GetFieldsByModuleAsync(ModuleType)).ToList();

            // 2. Fetch master property catalog for this module using our single source helper
            var fullTier2List = GetStandardModelProperties(ModuleType);

            // 3. Filter out Tier 2 properties that ARE ALREADY registered in DB
            var activeFieldNames = activeFields.Select(x => x.FieldName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var unaddedTier2Chips = fullTier2List
                .Where(prop => !activeFieldNames.Contains(prop))
                .Select(prop => new CustomFieldDefinition
                {
                    FieldName = prop,
                    DisplayLabel = SplitCamelCase(prop),
                    ModuleType = ModuleType,
                    FieldTier = 2,
                    FieldType = "Textbox",
                    IsVisible = true,
                    IsRequired = false
                })
                .ToList();

            App.Current.Dispatcher.Invoke(() =>
            {
                // Top Chips: ONLY Un-added Standard Model Properties
                StandardModelChips = new ObservableCollection<CustomFieldDefinition>(unaddedTier2Chips);

                // Bottom Chips: User Created Tier 3 Custom Fields
                CustomFieldChips = new ObservableCollection<CustomFieldDefinition>(
                    activeFields.Where(x => x.FieldTier == 3)
                );

                // Column 2 Registry DataGrid: ALL Active Registered Fields
                RegisteredFieldsList = new ObservableCollection<CustomFieldDefinition>(activeFields);
            });
        }

        /// <summary>
        /// Populates property dropdown pickers without duplicating list declarations.
        /// </summary>
        private void UpdateFilteredPropertiesList()
        {
            OnPropertyChanged(nameof(DialogTitle));

            List<string> properties = NewField.FieldTier switch
            {
                // TIER 1: MANDATORY CORE FIELDS
                1 => ModuleType switch
                {
                    "Lead" or "Customer" => new List<string> { "CustomerName", "Phone", "LeadHolder" },
                    "Product" => new List<string> { "Name" },
                    "Vendor" => new List<string> { "CompanyName", "Phone" },
                    "Staff" => new List<string> { "FullName", "Email", "Role", "UserName" },
                    "Order" => new List<string> { "LeadId", "OrderDate", "TotalAmount", "ProductId", "Quantity", "UnitPrice" },
                    "Purchase" => new List<string> { "PoNumber", "VendorId", "OrderDate", "TotalAmount", "ProductId", "Quantity", "UnitPrice" },
                    _ => new List<string>()
                },

                // TIER 2: REUSES CENTRAL METHOD (NO DUPLICATION!)
                2 => GetStandardModelProperties(ModuleType),

                _ => new List<string>()
            };

            FilteredModelProperties = new ObservableCollection<string>(properties);
        }

        partial void OnIsTier2SelectedChanged(bool value)
        {
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

        partial void OnSelectedModelPropertyNameChanged(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                NewField.FieldName = value;

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
        private void SelectFieldForEdit(CustomFieldDefinition field)
        {
            if (field == null) return;

            string defaultControlType = field.FieldName switch
            {
                "DOB" or "Anniversary" => "CalendarClock",
                "DivisionId" or "LeadLabelIds" => "DropdownMultiple",
                "LeadSourceId" or "LeadTagIds" => "DropdownSingle",
                _ => "Textbox"
            };

            IsEditMode = true;
            SelectedChipField = field;

            NewField = new CustomFieldDefinition
            {
                FieldId = field.FieldId,
                FieldName = field.FieldName,
                DisplayLabel = field.DisplayLabel,
                FieldType = field.FieldType,
                ModuleType = ModuleType,
                FieldTier = field.FieldTier,
                IsVisible = field.IsVisible,
                IsRequired = field.IsRequired,
                IsFilter = field.IsFilter,
                IsAdmin = field.IsAdmin,
                InPdf = field.InPdf,
                SeedValueOptionsList = new ObservableCollection<string>(field.SeedValueOptionsList ?? new())
            };

            IsTier2Selected = field.FieldTier == 2;
            IsTier3Selected = field.FieldTier == 3;

            OnPropertyChanged(nameof(DialogTitle));
            OnPropertyChanged(nameof(IsFieldNameEditable));
            OnPropertyChanged(nameof(IsControlTypeEditable)); // 👈 NOTIFY PERMISSION
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

        /// <summary>
        /// Saves the form configuration and reloads both chip lists and the DataGrid.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSubmitCustomField))]
        private async Task SubmitCustomField()
        {
            if (string.IsNullOrWhiteSpace(NewField.FieldName)) return;

            NewField.ModuleType = ModuleType;

            // Tier 1 Mandatory Fields MUST always be required and visible
            if (NewField.FieldTier == 1)
            {
                NewField.IsRequired = true;
                NewField.IsVisible = true;
            }

            if (string.IsNullOrWhiteSpace(NewField.DisplayLabel))
            {
                NewField.DisplayLabel = SplitCamelCase(NewField.FieldName);
            }

            bool isSaved = await _fieldService.SaveCustomFieldAsync(NewField);
            if (isSaved)
            {
                ResetForm();
                await LoadFieldsAsync(); // 👈 Refreshes all lists instantly!
            }
        }

        /// <summary>
        /// Instantly updates single toggle flag changes directly from Column 2 DataGrid checkboxes.
        /// </summary>
        [RelayCommand]
        private async Task UpdateFieldFlags(CustomFieldDefinition field)
        {
            if (field == null) return;
            await _fieldService.SaveCustomFieldAsync(field);
        }

        /// <summary>
        /// Deletes a field (Protected against Tier 1 Mandatory Fields).
        /// </summary>
        [RelayCommand]
        private async Task DeleteField(CustomFieldDefinition field)
        {
            if (field == null) return;

            // Guard Tier 1 Mandatory System Fields
            if (field.FieldTier == 1)
            {
                MessageBox.Show("Mandatory system fields cannot be deleted.", "Action Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete field '{field.EffectiveLabel}'?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                bool isDeleted = await _fieldService.DeleteCustomFieldAsync(field.FieldId);
                if (isDeleted)
                {
                    if (NewField?.FieldId == field.FieldId) ResetForm();
                    await LoadFieldsAsync(); // 👈 Refreshes lists instantly!
                }
            }
        }

        private void ResetForm()
        {
            IsEditMode = false;
            SelectedChipField = null;
            NewField = new CustomFieldDefinition
            {
                ModuleType = ModuleType,
                FieldTier = 3,
                FieldType = "Textbox",
                IsVisible = true,
                SeedValueOptionsList = new ObservableCollection<string>()
            };

            IsTier3Selected = true;
            IsTier2Selected = false;

            OnPropertyChanged(nameof(DialogTitle));
            OnPropertyChanged(nameof(IsFieldNameEditable));
        }

        [RelayCommand]
        private void CloseDialog()
        {
            RequestClose?.Invoke(true);
        }
    }
}