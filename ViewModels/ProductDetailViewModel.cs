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
    public partial class ProductDetailViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly int _currentDivisionId = 1;

        [ObservableProperty] private Product _newProduct;
        [ObservableProperty] private ObservableCollection<Category> _categories;
        [ObservableProperty] private ProductBatch _targetBatch;

        // Validation Display Properties
        [ObservableProperty] private string _validationErrorMessage = string.Empty;
        [ObservableProperty] private bool _isValidationErrorVisible;

        public ProductDetailViewModel(ProductService service, ObservableCollection<Category> categories, Product product)
        {
            _productService = service;
            _categories = categories;
            _newProduct = product;

            // FIX: Ensure the first category is selectable if no category is set
            if (_newProduct.CategoryId == 0 && _categories.Any())
            {
                _newProduct.CategoryId = _categories.First().Id;
            }

            InitializeForm();
        }

        private void InitializeForm()
        {
            NewProduct = new Product
            {
                DivisionId = _currentDivisionId,
                Unit = "Pcs",
                TrackCost = true
            };

            TargetBatch = new ProductBatch
            {
                DivisionId = _currentDivisionId
            };

            IsValidationErrorVisible = false;
        }

        // Intercepts input on the full Product Name field to auto-generate a sanitized code format
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

            // Sanitizes inputs: "Alloy Rim 26 Inch" -> "ALLOY-RIM-26-INCH"
            string sanitized = fullName.Trim()
                                       .Replace(" ", "-")
                                       .Replace("--", "-");
            NewProduct.ShortName = sanitized.ToUpper();
        }

        [RelayCommand]
        private void ClearForm()
        {
            InitializeForm();
        }

        private void ShowError(string message)
        {
            ValidationErrorMessage = message;
            IsValidationErrorVisible = true;
        }

        [RelayCommand]
        private async Task Save(Window window)
        {
            if (await _productService.UpsertProductWithBatchAsync(NewProduct, TargetBatch))
            {
                window.DialogResult = true;
                window.Close();
            }
        }

        [RelayCommand]
        private async Task SaveProductAssembly()
        {
            IsValidationErrorVisible = false;

            // 1. Core Field Validations
            if (string.IsNullOrWhiteSpace(NewProduct.Name) ||
                string.IsNullOrWhiteSpace(NewProduct.SKU) ||
                string.IsNullOrWhiteSpace(NewProduct.BrandName) ||
                string.IsNullOrWhiteSpace(TargetBatch.BatchNumber))
            {
                ShowError("Please complete all mandatory fields marked with an asterisk (*).");
                return;
            }

            if (TargetBatch.QuantityReceived <= 0 || TargetBatch.MinimumSellingPrice <= 0)
            {
                ShowError("Quantity received and Minimum Selling Price must be greater than zero.");
                return;
            }

            try
            {
                // 2. Check for Unique Batch Number across the Active Division
                bool isDuplicateBatch = await _productService.IsBatchNumberDuplicateAsync(TargetBatch.BatchNumber, _currentDivisionId);
                if (isDuplicateBatch)
                {
                    ShowError($"Batch code '{TargetBatch.BatchNumber}' is already registered in this division inventory branch.");
                    return;
                }

                // 3. Map values from initial entry configuration into parent row aggregates
                NewProduct.InitialStock = TargetBatch.QuantityReceived;
                NewProduct.RemainingStock = TargetBatch.QuantityReceived;
                NewProduct.CostPrice = TargetBatch.MinimumSellingPrice; // Set base WAC cost baseline

                TargetBatch.CurrentStock = TargetBatch.QuantityReceived;

                // 4. Save to Database
                bool isSaved = await _productService.UpsertProductWithBatchAsync(NewProduct, TargetBatch);
                if (isSaved)
                {
                    // Success! Reset form for next inventory entry
                    InitializeForm();
                }
            }
            catch (Exception ex)
            {
                ShowError($"System execution fault: {ex.Message}");
            }
        }
    }
}
