using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.ExtendedProperties;
using MySqlX.XDevAPI;
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
    public partial class CreatePoWindowViewModel : ObservableValidator
    {
        private readonly PurchaseService _purchaseService;
        private readonly VendorService _vendorService;
        private readonly ProductService _productService;

        [ObservableProperty] private ObservableCollection<Vendor> _vendorsList = new();

        [ObservableProperty]
        [Required(ErrorMessage = "Selecting a target vendor is mandatory.")]
        [NotifyDataErrorInfo]
        private Vendor? _selectedVendor;

        [ObservableProperty] private ObservableCollection<Product> _productsList = new();
        [ObservableProperty] private Product? _selectedProduct;

        [ObservableProperty] private int _quantityInput = 1;
        [ObservableProperty] private decimal _priceInput = 0.00M;

        [ObservableProperty] private ObservableCollection<PurchaseOrderDetail> _poLines = new();
        [ObservableProperty] private decimal _poTotalAmount;

        [ObservableProperty] private bool _isCustomProductMode;
        [ObservableProperty] private string _customProductName = string.Empty;
        [ObservableProperty] private string _customProductSku = string.Empty;

        public CreatePoWindowViewModel(PurchaseService purchaseService, VendorService vendorService, ProductService productService)
        {
            _purchaseService = purchaseService;
            _vendorService = vendorService;
            _productService = productService;
            _ = LoadDependenciesAsync();
            ValidateAllProperties();
        }

        private async Task LoadDependenciesAsync()
        {
            var vendors = await _vendorService.GetAllVendorsAsync();
            var products = await _productService.GetAllProductsAsync(1);

            App.Current.Dispatcher.Invoke(() =>
            {
                VendorsList = new ObservableCollection<Vendor>(vendors);
                ProductsList = new ObservableCollection<Product>(products);
                SelectedVendor = VendorsList.FirstOrDefault();
            });
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                PriceInput = value.CostPrice;
                QuantityInput = 1; // Default reset on switch to prevent arithmetic carryovers
            }
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName == nameof(SelectedVendor) || e.PropertyName == nameof(HasErrors))
            {
                SavePurchaseOrderCommand.NotifyCanExecuteChanged();
            }
            if (e.PropertyName == nameof(SelectedProduct) || e.PropertyName == nameof(QuantityInput) || e.PropertyName == nameof(PriceInput) || e.PropertyName == nameof(CustomProductName))
            {
                AddItemLineCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        private void ToggleCustomProductMode()
        {
            IsCustomProductMode = !IsCustomProductMode;
            SelectedProduct = null;
            CustomProductName = string.Empty;
            CustomProductSku = string.Empty;
            PriceInput = 0.00M;
            QuantityInput = 1;
        }

        public bool CanAddLine => IsCustomProductMode
            ? (!string.IsNullOrWhiteSpace(CustomProductName) && QuantityInput > 0 && PriceInput > 0)
            : (SelectedProduct != null && QuantityInput > 0 && PriceInput > 0);

        [RelayCommand(CanExecute = nameof(CanAddLine))]
        private void AddItemLine()
        {
            if (IsCustomProductMode)
            {
                // Staging container for custom item entries before final creation loop execution
                PoLines.Add(new PurchaseOrderDetail
                {
                    ProductId = -1, // Temporary negative token flags indicating new product registration requirement
                    ProductName = CustomProductName.Trim(),
                    SupplierSku = string.IsNullOrWhiteSpace(CustomProductSku) ? "CUSTOM-SKU" : CustomProductSku.Trim(),
                    Quantity = QuantityInput,
                    UnitPrice = PriceInput
                });
                ToggleCustomProductMode(); // Return dropdown view state parameters
            }
            else
            {
                var matchingLine = PoLines.FirstOrDefault(l => l.ProductId == SelectedProduct!.ProductId);
                if (matchingLine != null)
                {
                    matchingLine.Quantity += QuantityInput;
                    matchingLine.UnitPrice = PriceInput; // Overwrite if cost changed
                }
                else
                {
                    PoLines.Add(new PurchaseOrderDetail
                    {
                        ProductId = SelectedProduct!.ProductId,
                        ProductName = SelectedProduct.Name,
                        Quantity = QuantityInput,
                        UnitPrice = PriceInput
                    });
                }
            }

            RefreshOrderTotals();
        }

        [RelayCommand]
        private void UpdateGridItemQty(PurchaseOrderDetail line)
        {
            if (line == null) return;
            RefreshOrderTotals();
        }

        [RelayCommand]
        private void DeleteGridItemRow(PurchaseOrderDetail line)
        {
            if (line == null) return;
            PoLines.Remove(line);
            RefreshOrderTotals();
        }

        private void RefreshOrderTotals()
        {
            PoTotalAmount = PoLines.Sum(l => l.TotalAmount);
            SavePurchaseOrderCommand.NotifyCanExecuteChanged();
        }

        public bool CanSavePo(Window currentWindow) => !HasErrors && PoLines.Count > 0 && SelectedVendor != null;

        [RelayCommand(CanExecute = nameof(CanSavePo))]
        private async Task SavePurchaseOrderAsync(Window currentWindow)
        {
            ValidateAllProperties();
            if (HasErrors || PoLines.Count == 0 || SelectedVendor == null) return;

            try
            {
                var finalizedLines = new List<PurchaseOrderDetail>();

                foreach (var line in PoLines)
                {
                    if (line.ProductId == -1) // Custom Ad-hoc item flag caught
                    {
                        // 1. Build the parent schema mapping metrics properties
                        var productPayload = new Product
                        {
                            DivisionId = 1, // Default fallback or active session division configuration
                            Name = line.ProductName,
                            ShortName = line.ProductName,
                            SKU = line.SupplierSku ?? $"SKU-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                            Unit = "PCS",
                            CategoryId = 1, // Map default categorical classification block id
                            InitialStock = line.Quantity,
                            RemainingStock = line.Quantity,
                            CostPrice = line.UnitPrice,
                            SellingPrice = line.UnitPrice * 1.25m,
                            TrackCost = true
                        };

                        // 2. Build the matching initial tracking batch record parameters
                        var batchPayload = new ProductBatch
                        {
                            DivisionId = 1,
                            BatchNumber = $"BAT-{DateTime.Today:yyyyMM}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                            MfgDate = DateTime.Today,
                            ExpiryDate = DateTime.Today.AddYears(2), // Safe industrial lifecycle ceiling default
                            QuantityReceived = line.Quantity,
                            CurrentStock = line.Quantity,
                            MinimumSellingPrice = line.UnitPrice * 1.10m
                        };

                        // 3. Dispatch unified transactional statement blocks down to MySQL 
                        int newlyGeneratedId = await _productService.SaveProductAssemblyAsync(productPayload, batchPayload);

                        // Assign new permanent ID to line details mapping properties
                        line.ProductId = newlyGeneratedId;

                        // Create link between this vendor and the new product code
                        await _vendorService.SaveVendorProductLinkAsync(SelectedVendor.VendorId, newlyGeneratedId, line.SupplierSku ?? "GEN-SKU", line.UnitPrice);
                    }
                    finalizedLines.Add(line);
                }

                // STEP 4: Standard PO generation routine continues...
                var poHeader = new PurchaseOrder
                {
                    PoNumber = $"PO-{DateTime.Today:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                    VendorId = SelectedVendor.VendorId,
                    OrderDate = DateTime.Today,
                    TotalAmount = PoTotalAmount,
                    OrderStatus = "Draft",
                    CreatedBy = "Admin"
                };

                int orderId = await _purchaseService.CreatePurchaseOrderAsync(poHeader, finalizedLines);
                if (orderId > 0 && currentWindow != null)
                {
                    currentWindow.DialogResult = true;
                    currentWindow.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save structural batch elements: {ex.Message}", "Transaction Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CloseWindow(Window currentWindow)
        {
            if (currentWindow != null)
            {
                currentWindow.DialogResult = false;
                currentWindow.Close();
            }
        }
    }
}
