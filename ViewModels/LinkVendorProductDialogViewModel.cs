using CallMan.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class LinkVendorProductDialogViewModel : ObservableValidator
    {
        private readonly int _vendorId;

        [ObservableProperty] private ObservableCollection<Product> _allProducts = new();
        [ObservableProperty] private Product? _selectedProduct;

        [ObservableProperty] private string _supplierSku = string.Empty;

        [ObservableProperty]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
        [NotifyDataErrorInfo]
        private decimal _purchasePrice;

        public bool IsEditMode { get; }

        public LinkVendorProductDialogViewModel(int vendorId, System.Collections.Generic.IEnumerable<Product> availableProducts, VendorProductLinkDisplay? existingLink = null)
        {
            _vendorId = vendorId;
            AllProducts = new ObservableCollection<Product>(availableProducts);

            if (existingLink != null)
            {
                IsEditMode = true;
                SelectedProduct = AllProducts.FirstOrDefault(p => p.ProductId == existingLink.ProductId);
                SupplierSku = existingLink.SupplierSku;
                PurchasePrice = existingLink.PurchasePrice;
            }
            else
            {
                IsEditMode = false;
            }
        }

        [RelayCommand]
        private void Save(Window window)
        {
            ValidateAllProperties();

            if (SelectedProduct == null)
            {
                MessageBox.Show("Please select a product from the list.", "Validation Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (HasErrors || PurchasePrice <= 0)
            {
                MessageBox.Show("Please enter a valid purchase price.", "Validation Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            window.DialogResult = true;
            window.Close();
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
