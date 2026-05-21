using CallMan.Dialogs;
using CallMan.Models;
using CallMan.Models.Enums;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CallMan.ViewModels
{
    public partial class InventoryViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly int _currentDivisionId = 1;

        [ObservableProperty] private ObservableCollection<Product> _allProducts = new();
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private ObservableCollection<ProductBatch> _selectedProductBatches = new();
        [ObservableProperty] private Product _currentProduct = new();
        // The focused row tracker property
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private string _searchText = string.Empty;

        public string SubmitButtonText => CurrentProduct.ProductId == 0 ? "ADD PRODUCT" : "UPDATE PRODUCT";

        public int TotalUniqueProductsCount => AllProducts.Count();

        /// <summary>
        /// Sum of: Remaining Stock * WAC Cost Price
        /// </summary>
        public decimal GlobalInventoryCostValue => AllProducts.Sum(p => p.RemainingStock * p.CostPrice);

        /// <summary>
        /// Total value of potential tax locked inside your active on-shelf inventory
        /// </summary>
        public decimal GlobalInventoryGstValue => AllProducts.Sum(p => p.RemainingStock * p.CostPrice * (p.GstPercent / 100));

        /// <summary>
        /// Total portfolio asset value including base costs and integrated taxes
        /// </summary>
        public decimal CombinedTotalAssetValue => GlobalInventoryCostValue + GlobalInventoryGstValue;

        public InventoryViewModel(ProductService productService, CategoryService categoryService)
        {
            _productService = productService;
            _categoryService = categoryService;
            _ = LoadInitialData();            
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            // If the user clears the selection, empty the batch grid panel
            if (value == null)
            {
                SelectedProductBatches.Clear();
                return;
            }

            // Fire-and-forget safe async call to pull data on demand
            _ = LoadActiveBatchesAsync(value.ProductId);
        }

        /// <summary>
        /// Call this helper routine inside your data reload method 
        /// right after populating the ProductsCollection to refresh the top gauges.
        /// </summary>
        private void RefreshDashboardMetrics()
        {
            OnPropertyChanged(nameof(TotalUniqueProductsCount));
            OnPropertyChanged(nameof(GlobalInventoryCostValue));
            OnPropertyChanged(nameof(GlobalInventoryGstValue));
            OnPropertyChanged(nameof(CombinedTotalAssetValue));
        }

        // Advanced Search Logic
        public IEnumerable<Product> FilteredProducts => string.IsNullOrWhiteSpace(SearchText)
            ? AllProducts
            : AllProducts.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     p.SKU.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredProducts));

        private async Task LoadInitialData()
        {
            var products = await _productService.GetAllProductsAsync(1);
            AllProducts = new ObservableCollection<Product>(products);

            var categories = await _categoryService.GetAllCategoriesAsync();
            Categories = new ObservableCollection<Category>(categories);
            OnPropertyChanged(nameof(FilteredProducts));

            RefreshDashboardMetrics();
        }

        [RelayCommand]
        private async Task ToggleBatchDetails(Product selectedProduct)
        {
            if (selectedProduct == null) return;

            // Toggle the state directly on the model
            if (selectedProduct.IsExpanded)
            {
                selectedProduct.IsExpanded = false;
            }
            else
            {
                // On Demand Load: Only hit the database query when expanding the panel view layout
                var databaseBatches = await _productService.GetBatchesByProductIdAsync(selectedProduct.ProductId, _currentDivisionId);

                selectedProduct.InnerBatchesCollection.Clear();
                foreach (var b in databaseBatches)
                {
                    selectedProduct.InnerBatchesCollection.Add(b);
                }

                selectedProduct.IsExpanded = true;
            }
        }

        [RelayCommand]
        private async Task SaveChanges(ProductBatch activeEditingBatch)
        {
            if (activeEditingBatch == null) return;

            if (string.IsNullOrWhiteSpace(activeEditingBatch.BatchNumber) || activeEditingBatch.BatchNumber == "NEW-LOT")
            {
                // Replace with your standard UI Alert Box/Snackbar notification hook
                MessageBox.Show("Please enter a valid, unique Batch Number before saving.", "Validation Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var parentProduct = FilteredProducts.FirstOrDefault(p => p.ProductId == activeEditingBatch.ProductId);
            if (parentProduct == null) return;

            try
            {
                bool isDuplicateBatch = await _productService.IsBatchNumberDuplicateAsync(activeEditingBatch.BatchNumber, _currentDivisionId);
                if (isDuplicateBatch)
                {
                    MessageBox.Show($"Batch code '{activeEditingBatch.BatchNumber}' is already registered in this division inventory branch.", "Validation Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // This single execution now takes care of the DB, recalculates WAC/Stocks, and pushes them back up to the parentProduct model properties
                bool success = await _productService.UpsertProductWithBatchAsync(parentProduct, activeEditingBatch);

                if (success)
                {
                    // Simple indicator updates for UI elements not handled inside the database table columns
                    parentProduct.TotalBatchesCount = parentProduct.InnerBatchesCollection.Count;
                    await LoadInitialData(); // Refresh the entire grid to reflect all changes, including recalculated WAC and stock levels
                }
            }
            catch (Exception ex)
            {
                // Sentry logging hook placement boundary
                MessageBox.Show(ex.Message, "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ToggleSelectAll(bool? isChecked)
        {
            if (isChecked == null || FilteredProducts == null) return;

            // Cast the elements of the view to your specific Lead model
            foreach (var item in FilteredProducts.Cast<Product>())
            {
                item.IsSelectedForAction = isChecked.Value;
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (await _productService.UpsertProductWithBatchAsync(CurrentProduct, new ProductBatch()))
            {
                await LoadInitialData();
                Clear();
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadInitialData();
        }

        [RelayCommand]
        private async Task OpenAddProduct()
        {
            // Open window with a fresh product
            await ShowProductDetail(new Product());
        }

        [RelayCommand]
        private async Task Edit(Product p)
        {
            // Open window with existing product data
            await ShowProductDetail(p);
        }

        [RelayCommand]
        private async Task Delete(Product p)
        {
            if (await _productService.DeleteProductAsync(p.ProductId))
                await LoadInitialData();
        }

        [RelayCommand]
        private void Clear()
        {
            CurrentProduct = new Product();
            OnPropertyChanged(nameof(SubmitButtonText));
        }

        private async Task ShowProductDetail(Product product)
        {
            // We pass the Categories and the Product to the detail window
            var detailVm = new ProductDetailViewModel(_productService, Categories, product);
            var window = new ProductDetailWindow { DataContext = detailVm };

            if (window.ShowDialog() == true)
            {
                await LoadInitialData(); // Refresh grid after save
            }
        }

        [RelayCommand]
        private async Task OpenImport()
        {
            var vm = App.ServiceProvider.GetRequiredService<ImportViewModel>();
            await vm.InitializeAsync(ImportType.Product);
            var dialogWindow = new ImportView { DataContext = vm };
            // No need for a close event here since the ImportViewModel can directly call LoadLeads() after a successful import
            vm.RequestClose += (result) =>
            {
                dialogWindow.DialogResult = result;
                dialogWindow.Close();
            };

            if (dialogWindow.ShowDialog() == true)
            {
                // Re-run the query to show the new lead in the DataGrid
                await LoadInitialData();
            }
        }

        private async Task LoadActiveBatchesAsync(int productId)
        {
            try
            {
                // Call your clean standalone retrieval service method
                var batches = await _productService.GetBatchesByProductIdAsync(productId, _currentDivisionId);

                SelectedProductBatches.Clear();
                foreach (var batch in batches)
                {
                    SelectedProductBatches.Add(batch);
                }
            }
            catch (Exception ex)
            {
                // Sentry exception logging boundary setup
                SelectedProductBatches.Clear();
            }
        }

        [RelayCommand]
        private void AddNewBatchRow(Product parentProduct)
        {
            if (parentProduct == null) return;

            // Initialize a clean model blueprint setup
            var newBatchRow = new ProductBatch
            {
                BatchId = 0,                                 // 0 signals INSERT operation to Dapper service
                ProductId = parentProduct.ProductId,         // Link parent relation key
                DivisionId = parentProduct.DivisionId,       // Enforce tenant scoping boundary
                BatchNumber = "NEW-LOT",                     // Placeholder name text
                MfgDate = DateTime.Today,
                ExpiryDate = DateTime.Today.AddYears(1),     // Sensible default placeholder setting
                QuantityReceived = 0,
                CurrentStock = 0,
                MinimumSellingPrice = 0
            };

            // Push the blank fields into the active collection drawer layout view
            parentProduct.InnerBatchesCollection.Add(newBatchRow);
        }
    }
}
