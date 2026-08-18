using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class MasterFormulationViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly MasterFormulationService _formulationService;

        [ObservableProperty] private MasterFormulation _currentFormulation = new();
        [ObservableProperty] private ObservableCollection<MasterFormulation> _formulationsList = new();
        [ObservableProperty] private ObservableCollection<Product> _finishedProductsList = new();
        [ObservableProperty] private ObservableCollection<Product> _rawMaterialsList = new();

        [ObservableProperty] private Product? _selectedRawMaterialToAdd;
        [ObservableProperty] private decimal _newIngredientPercentage = 0m;
        [ObservableProperty] private string _newIngredientPhase = "Phase A";
        [ObservableProperty] private bool _isEditMode;

        // Fast Item Inline Addition State
        [ObservableProperty] private bool _isQuickAddProductOpen;
        [ObservableProperty] private string _quickProductName = string.Empty;
        [ObservableProperty] private string _quickProductCode = string.Empty;
        [ObservableProperty] private string _quickProductUnit = "Kg";
        [ObservableProperty] private decimal _quickProductCost;
        [ObservableProperty] private decimal _quickProductPercentage;

        [ObservableProperty] private string _saveButtonText = "Save Formulation Master";

        public MasterFormulationViewModel(ProductService productService, MasterFormulationService formulationService)
        {
            _productService = productService;
            _formulationService = formulationService;
            _ = InitializeDataAsync();
        }

        public async Task InitializeDataAsync()
        {
            await LoadProductsAsync();
            await LoadFormulationsListAsync();
        }

        public async Task LoadProductsAsync()
        {
            var allProducts = await _productService.GetAllProductsAsync();

            App.Current.Dispatcher.Invoke(() =>
            {
                // 1. Finished Goods (Matches any custom category marked as FinishedGood)
                FinishedProductsList = new ObservableCollection<Product>(
                    allProducts.Where(p => p.CategoryType == ItemClassification.FinishedGood)
                );

                // 2. Raw Materials, Excipients & Chemicals (Matches any category marked as RawMaterial)
                RawMaterialsList = new ObservableCollection<Product>(
                    allProducts.Where(p => p.CategoryType == ItemClassification.RawMaterial)
                );
            });
        }

        private async Task LoadFormulationsListAsync()
        {
            var list = await _formulationService.GetAllFormulationsAsync();
            App.Current.Dispatcher.Invoke(() =>
            {
                FormulationsList = new ObservableCollection<MasterFormulation>(list);
            });
        }

        [RelayCommand]
        private void AddIngredientRow(Product? selectedRawMaterial)
        {
            if (selectedRawMaterial == null) return;

            // 1. Prevent duplicate ingredient entries
            if (CurrentFormulation.Items.Any(x => x.RawMaterialProductId == selectedRawMaterial.ProductId))
            {
                MessageBox.Show($"'{selectedRawMaterial.Name}' is already added to the formulation.",
                                "Duplicate Item", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Create the row item
            var newItem = new MasterFormulationItem
            {
                RawMaterialProductId = selectedRawMaterial.ProductId,
                RawMaterialName = selectedRawMaterial.Name,
                RawMaterialCode = selectedRawMaterial.ShortName ?? string.Empty,
                Unit = selectedRawMaterial.Unit ?? "Kg",
                PercentageValue = NewIngredientPercentage
            };

            // 3. Attach change listener
            newItem.PropertyChanged += OnIngredientPropertyChanged;

            CurrentFormulation.Items.Add(newItem);
            CurrentFormulation.NotifyTotalsChanged();

            // 4. Clear picker selection if using a property binding
            SelectedRawMaterialToAdd = null;
        }

        [RelayCommand]
        private void RemoveIngredientRow(MasterFormulationItem? item)
        {
            if (item == null || !CurrentFormulation.Items.Contains(item)) return;

            // Detach event listener to avoid memory leaks
            item.PropertyChanged -= OnIngredientPropertyChanged;

            CurrentFormulation.Items.Remove(item);
            CurrentFormulation.NotifyTotalsChanged();
        }

        // Reusable event handler for percentage updates
        private void OnIngredientPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MasterFormulationItem.PercentageValue))
            {
                CurrentFormulation.NotifyTotalsChanged();
            }
        }

        // ==========================================
        // INLINE PRODUCT CREATION FOR UNLISTED ITEMS
        // ==========================================
        [RelayCommand]
        private void OpenQuickAddProduct()
        {
            QuickProductName = string.Empty;
            QuickProductCode = string.Empty;
            QuickProductUnit = "Kg";
            QuickProductCost = 0m;
            QuickProductPercentage = 0m;
            IsQuickAddProductOpen = true;
        }

        [RelayCommand]
        private async Task SubmitQuickAddProductAsync()
        {
            if (string.IsNullOrWhiteSpace(QuickProductName))
            {
                MessageBox.Show("Please enter product/chemical name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Create product in main inventory
            var newProduct = new Product
            {
                Name = QuickProductName.Trim(),
                ShortName = string.IsNullOrWhiteSpace(QuickProductCode) ? $"RM-{DateTime.UtcNow.Ticks % 100000}" : QuickProductCode.Trim(),
                CategoryName = "Raw Material",
                Unit = QuickProductUnit,
                CostPrice = QuickProductCost,
                RemainingStock = 0
            };

            int newId = await _productService.SaveProductAssemblyAsync(newProduct);
            newProduct.ProductId = newId;

            // 2. Add to local RawMaterialsList dropdown
            RawMaterialsList.Add(newProduct);

            // 3. Immediately append to formulation
            var formulationItem = new MasterFormulationItem
            {
                RawMaterialProductId = newProduct.ProductId,
                RawMaterialName = newProduct.Name,
                RawMaterialCode = newProduct.ShortName,
                Unit = newProduct.Unit,
                PercentageValue = QuickProductPercentage
            };

            formulationItem.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MasterFormulationItem.PercentageValue))
                {
                    CurrentFormulation.NotifyTotalsChanged();
                }
            };

            CurrentFormulation.Items.Add(formulationItem);
            CurrentFormulation.NotifyTotalsChanged();

            IsQuickAddProductOpen = false;
        }

        // ==========================================
        // 🌟 SAVE / UPDATE FORMULATION COMMAND
        // ==========================================
        [RelayCommand]
        private async Task SaveFormulationAsync()
        {
            // 1. Recipe Name is required
            if (string.IsNullOrWhiteSpace(CurrentFormulation.FormulationName))
            {
                MessageBox.Show("Please enter a formulation / recipe name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Ingredients list cannot be empty
            if (!CurrentFormulation.Items.Any())
            {
                MessageBox.Show("Please add at least one ingredient to the formulation.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3. Formula normalization check (<= 100%)
            if (!CurrentFormulation.IsValidFormula)
            {
                MessageBox.Show($"Total active ingredients ({CurrentFormulation.TotalIngredientsPercentage:N2}%) cannot exceed 100%.",
                                "Formula Limit Exceeded", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                await _formulationService.SaveFormulationAsync(CurrentFormulation);

                MessageBox.Show("Master Formulation saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadFormulationsListAsync();
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving formulation: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==========================================
        // 🌟 EDIT / LOAD EXISTING FORMULATION
        // ==========================================
        [RelayCommand]
        private async Task EditFormulationAsync(MasterFormulation? item)
        {
            if (item == null) return;

            var fullRecipe = await _formulationService.GetFormulationByIdAsync(item.FormulationId);
            if (fullRecipe == null) return;

            // Wire up PropertyChanged for all loaded items
            foreach (var line in fullRecipe.Items)
            {
                line.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MasterFormulationItem.PercentageValue))
                    {
                        CurrentFormulation.NotifyTotalsChanged();
                    }
                };
            }

            CurrentFormulation = fullRecipe;
            CurrentFormulation.NotifyTotalsChanged();
            IsEditMode = true;
            SaveButtonText = "Update Formulation Master";
        }

        // ==========================================
        // 🌟 DELETE FORMULATION COMMAND
        // ==========================================
        [RelayCommand]
        private async Task DeleteFormulationAsync(MasterFormulation? item)
        {
            if (item == null) return;

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete formulation '{item.FormulationName}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                await _formulationService.DeleteFormulationAsync(item.FormulationId);
                await LoadFormulationsListAsync();

                if (CurrentFormulation.FormulationId == item.FormulationId)
                {
                    ResetForm();
                }
            }
        }

        // ==========================================
        // 🌟 RESET / NEW FORM
        // ==========================================
        [RelayCommand]
        private void ResetForm()
        {
            CurrentFormulation = new MasterFormulation();
            CurrentFormulation.NotifyTotalsChanged();
            IsEditMode = false;
            SaveButtonText = "Save Formulation Master";
            SelectedRawMaterialToAdd = null;
            NewIngredientPercentage = 0m;
        }
    }
}
