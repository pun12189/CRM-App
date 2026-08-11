using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Core;
using Tijori.Models;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class ProductDetailViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly CustomFieldService _customFieldService;
        private readonly int _currentDivisionId = 1;

        [ObservableProperty] private Product _newProduct;
        [ObservableProperty] private ObservableCollection<Category> _categories;
        [ObservableProperty] private ProductBatch _targetBatch;

        // Validation Display Properties
        [ObservableProperty] private string _validationErrorMessage = string.Empty;
        [ObservableProperty] private bool _isValidationErrorVisible;

        [ObservableProperty] private ModuleFieldConfigMap _fieldConfigMap = new(new List<CustomFieldDefinition>());
        [ObservableProperty] private ObservableCollection<CustomFieldInputValue> _dynamicProductFields = new();

        public ProductDetailViewModel(
            ProductService service,
            CustomFieldService customFieldService,
            ObservableCollection<Category> categories,
            Product? product = null)
        {
            _productService = service;
            _customFieldService = customFieldService;
            _categories = categories;

            _newProduct = product ?? new Product
            {
                DivisionId = _currentDivisionId,
                Unit = "Pcs",
                TrackCost = true,
                HasBatchTracking = true // Default to batch-tracked mode
            };

            _targetBatch = new ProductBatch
            {
                DivisionId = _currentDivisionId
            };

            // Select default category if unassigned
            if (_newProduct.CategoryId == 0 && _categories != null && _categories.Any())
            {
                _newProduct.CategoryId = _categories.First().Id;
            }

            _ = LoadInitialDataAsync();
        }

        private async Task LoadInitialDataAsync()
        {
            await GetCustomFields();
        }

        private async Task GetCustomFields()
        {
            // 1. Fetch field definitions for Product module
            var fieldDefinitions = (await _customFieldService.GetFieldsByModuleAsync("Product")).ToList();

            // 2. Populate FieldConfigMap for Section 1 & 2 hints and visibility toggles
            FieldConfigMap = new ModuleFieldConfigMap(fieldDefinitions);

            // 3. Fetch saved Tier 3 custom field values from DB if editing an existing product
            Dictionary<int, string> savedValues = (NewProduct != null && NewProduct.ProductId > 0)
                ? await _customFieldService.GetEntityCustomFieldValuesAsync(NewProduct.ProductId, "Product")
                : new Dictionary<int, string>();

            App.Current.Dispatcher.Invoke(() =>
            {
                DynamicProductFields.Clear();

                // 4. Hydrate Tier 3 dynamic custom fields into Section 3 with saved values
                foreach (var f in fieldDefinitions.Where(x => x.IsVisible && x.FieldTier == 3))
                {
                    // Try to pull previously saved value for this FieldId
                    savedValues.TryGetValue(f.FieldId, out string? initialValue);

                    DynamicProductFields.Add(new CustomFieldInputValue
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

        // Auto-generate sanitized ShortName when Product.Name changes
        partial void OnNewProductChanged(Product value)
        {
            if (value != null)
            {
                value.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(Product.Name))
                    {
                        GenerateShortName(value.Name);
                    }
                };
            }
        }

        private void GenerateShortName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                NewProduct.ShortName = string.Empty;
                return;
            }

            string sanitized = fullName.Trim()
                                       .Replace(" ", "-")
                                       .Replace("--", "-");
            NewProduct.ShortName = sanitized.ToUpper();
        }

        [RelayCommand]
        private void ClearForm()
        {
            NewProduct = new Product
            {
                DivisionId = _currentDivisionId,
                Unit = "Pcs",
                TrackCost = true,
                HasBatchTracking = true,
                CategoryId = _categories.FirstOrDefault()?.Id ?? 0
            };

            TargetBatch = new ProductBatch
            {
                DivisionId = _currentDivisionId
            };

            IsValidationErrorVisible = false;
            ValidationErrorMessage = string.Empty;

            _ = GetCustomFields();
        }

        private void ShowError(string message)
        {
            ValidationErrorMessage = message;
            IsValidationErrorVisible = true;
        }

        [RelayCommand]
        private async Task SaveProductAssembly(Window? window)
        {
            IsValidationErrorVisible = false;

            // 1. MANDATORY TIER 1 VALIDATION
            if (string.IsNullOrWhiteSpace(NewProduct.Name))
            {
                ShowError($"{FieldConfigMap.GetLabel("Name", "Product Name")} is required.");
                return;
            }

            // 2. TIER 2 DYNAMIC VALIDATION
            if (FieldConfigMap.GetIsRequired("SKU") && string.IsNullOrWhiteSpace(NewProduct.SKU))
            {
                ShowError($"{FieldConfigMap.GetLabel("SKU", "SKU")} is required.");
                return;
            }

            if (FieldConfigMap.GetIsRequired("ShortName") && string.IsNullOrWhiteSpace(NewProduct.ShortName))
            {
                ShowError($"{FieldConfigMap.GetLabel("ShortName", "Short Name")} is required.");
                return;
            }

            // 3. TIER 3 DYNAMIC CUSTOM FIELDS VALIDATION
            foreach (var customField in DynamicProductFields)
            {
                if (customField.IsRequired && string.IsNullOrWhiteSpace(customField.FieldValue))
                {
                    ShowError($"{customField.EffectiveLabel} is required.");
                    return;
                }
            }

            // 4. BATCH VS DIRECT TRADER DATE LOGIC
            if (NewProduct.HasBatchTracking)
            {
                // Batch Mode: Dates come from TargetBatch and get pushed to NewProduct
                if (string.IsNullOrWhiteSpace(TargetBatch.BatchNumber))
                {
                    ShowError("Batch Number / Lot Code is required when Batch Tracking is enabled.");
                    return;
                }

                // Check for duplicate batch code if creating a new entry
                if (NewProduct.ProductId == 0)
                {
                    bool isDuplicate = await _productService.IsBatchNumberDuplicateAsync(TargetBatch.BatchNumber, _currentDivisionId);
                    if (isDuplicate)
                    {
                        ShowError($"Batch code '{TargetBatch.BatchNumber}' already exists in this division.");
                        return;
                    }
                }

                NewProduct.MfgDate = TargetBatch.MfgDate;
                NewProduct.ExpiryDate = TargetBatch.ExpiryDate;
            }
            else
            {
                // Direct Trader Mode: Sync dates directly to a default lot row
                if (string.IsNullOrWhiteSpace(TargetBatch.BatchNumber))
                {
                    TargetBatch.BatchNumber = $"DIRECT-{DateTime.Now:yyyyMMddHHmmss}";
                }
                TargetBatch.MfgDate = NewProduct.MfgDate;
                TargetBatch.ExpiryDate = NewProduct.ExpiryDate;
            }

            try
            {
                TargetBatch.CurrentStock = TargetBatch.QuantityReceived;
                TargetBatch.DivisionId = _currentDivisionId;

                NewProduct.InitialStock = TargetBatch.QuantityReceived;
                NewProduct.RemainingStock = TargetBatch.QuantityReceived;
                NewProduct.CostPrice = TargetBatch.MinimumSellingPrice;

                // 5. UPSERT MAIN PRODUCT AND BATCH RECORD
                bool isSaved = await _productService.UpsertProductWithBatchAsync(NewProduct, TargetBatch);

                if (isSaved && NewProduct.ProductId > 0)
                {
                    // 6. PERSIST TIER 3 DYNAMIC CUSTOM FIELD VALUES TO DATABASE
                    var customValues = DynamicProductFields
                        .Select(cf => new KeyValuePair<int, string>(cf.FieldId, cf.FieldValue ?? string.Empty));

                    await _customFieldService.SaveEntityCustomFieldValuesAsync(NewProduct.ProductId, "Product", customValues);

                    if (window != null)
                    {
                        window.DialogResult = true;
                        window.Close();
                    }
                    else
                    {
                        ClearForm();
                    }
                }
                else
                {
                    ShowError("Failed to save product entry.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"System execution fault: {ex.Message}");
            }
        }
    }
}