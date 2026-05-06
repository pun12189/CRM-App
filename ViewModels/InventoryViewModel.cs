using CallMan.Dialogs;
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
    public partial class InventoryViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;

        [ObservableProperty] private ObservableCollection<Product> _allProducts = new();
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private Product _currentProduct = new();
        [ObservableProperty] private string _searchText = string.Empty;

        public string SubmitButtonText => CurrentProduct.ProductId == 0 ? "ADD PRODUCT" : "UPDATE PRODUCT";

        public InventoryViewModel(ProductService productService, CategoryService categoryService)
        {
            _productService = productService;
            _categoryService = categoryService;
            _ = LoadInitialData();
        }

        // Advanced Search Logic
        public IEnumerable<Product> FilteredProducts => string.IsNullOrWhiteSpace(SearchText)
            ? AllProducts
            : AllProducts.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     p.SKU.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredProducts));

        private async Task LoadInitialData()
        {
            var products = await _productService.GetAllProductsAsync();
            AllProducts = new ObservableCollection<Product>(products);

            var categories = await _categoryService.GetAllCategoriesAsync();
            Categories = new ObservableCollection<Category>(categories);
            OnPropertyChanged(nameof(FilteredProducts));
        }

        [RelayCommand]
        private async Task Save()
        {
            if (await _productService.UpsertProductAsync(CurrentProduct))
            {
                await LoadInitialData();
                Clear();
            }
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
    }
}
