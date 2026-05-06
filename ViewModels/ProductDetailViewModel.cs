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

        [ObservableProperty] private Product _newProduct;
        [ObservableProperty] private ObservableCollection<Category> _categories;

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
        }

        [RelayCommand]
        private async Task Save(Window window)
        {
            if (await _productService.UpsertProductAsync(NewProduct))
            {
                window.DialogResult = true;
                window.Close();
            }
        }
    }
}
