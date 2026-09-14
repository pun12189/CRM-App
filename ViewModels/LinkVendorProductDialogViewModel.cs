using Tijori.Models;
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

namespace Tijori.ViewModels
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

        // --- New Procurement & Auto-Reorder Properties ---
        [ObservableProperty]
        [Range(1, 365, ErrorMessage = "Lead time must be at least 1 day.")]
        [NotifyDataErrorInfo]
        private int _leadTimeDays = 3;

        [ObservableProperty]
        [Range(1, 99, ErrorMessage = "Priority must be 1 or greater.")]
        [NotifyDataErrorInfo]
        private int _vendorPriority = 1;

        [ObservableProperty]
        private bool _isPreferredVendor = true;

        public bool IsEditMode { get; }

        partial void OnSelectedProductChanged(Product? value)
        {
            // Only autofill defaults in Add mode so existing link data isn't wiped out during edit
            if (value != null && !IsEditMode)
            {
                SupplierSku = value.ShortName ?? string.Empty;
                PurchasePrice = value.CostPrice;
            }
        }

        public LinkVendorProductDialogViewModel(
            int vendorId,
            IEnumerable<Product> availableProducts,
            VendorProductLinkDisplay? existingLink = null)
        {
            _vendorId = vendorId;
            AllProducts = new ObservableCollection<Product>(availableProducts);

            if (existingLink != null)
            {
                IsEditMode = true;
                SelectedProduct = AllProducts.FirstOrDefault(p => p.ProductId == existingLink.ProductId);
                SupplierSku = existingLink.SupplierSku ?? string.Empty;
                PurchasePrice = existingLink.PurchasePrice;

                // Load existing link parameters (with fallbacks if zero/null)
                LeadTimeDays = existingLink.LeadTimeDays > 0 ? existingLink.LeadTimeDays : 3;
                VendorPriority = existingLink.VendorPriority > 0 ? existingLink.VendorPriority : 1;
                IsPreferredVendor = existingLink.IsPreferredVendor;
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
                MessageBox.Show("Please enter a valid purchase price and ensure all fields are correct.", "Validation Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
