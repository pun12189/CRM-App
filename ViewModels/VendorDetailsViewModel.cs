using Tijori.Core;
using Tijori.Dialogs;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.ViewModels
{
    public partial class VendorDetailsViewModel : ObservableObject
    {
        private readonly VendorService _vendorService;
        private readonly PurchaseService _purchaseService;
        private readonly CategoryService _categoryService;
        private readonly IUserSession _userSession;
        private readonly IActionSecurityGuard _securityGuard;
        private readonly ProductService _productService;

        public event Action? OnNavigateBackRequested;

        [ObservableProperty] private Vendor _vendor = new();
        [ObservableProperty] private int _selectedTabIndex = 0;

        // Rating & Delivery Metrics
        [ObservableProperty] private double _delayRating = 5.0;
        [ObservableProperty] private double _onTimeDeliveryRate = 100.0;
        [ObservableProperty] private int _avgDelayDays = 0;
        [ObservableProperty] private int _totalOrdersProcessed = 0;
        [ObservableProperty] private int _delayedOrdersCount = 0;

        // Financial Summaries
        [ObservableProperty] private decimal _totalPurchasesYtd = 0;
        [ObservableProperty] private decimal _outstandingPayable = 0;
        [ObservableProperty] private DateTime? _lastPurchaseDate;

        // Tab Data Collections
        [ObservableProperty] private ObservableCollection<PurchaseOrder> _vendorOrders = new();
        [ObservableProperty] private ObservableCollection<VendorProductLinkDisplay> _vendorProducts = new();

        [ObservableProperty]
        private ObservableCollection<VendorActivityItem> _vendorActivities = new();

        public bool HasActivities => VendorActivities != null && VendorActivities.Any();

        [ObservableProperty] private ObservableCollection<UploadedDocumentRow> _unifiedDocumentsCollection = new();

        // Dropdown lookup source for the upload dialog header section
        [ObservableProperty] private ObservableCollection<BusinessCategory> _availableDocumentCategories = new();
        [ObservableProperty] private BusinessCategory? _selectedUploadCategory;

        [ObservableProperty] private string _documentCountSummaryText = "0 Files Total";

        public VendorDetailsViewModel(VendorService vendorService, PurchaseService purchaseService, CategoryService categoryService, IUserSession userSession, IActionSecurityGuard securityGuard, ProductService productService)
        {
            _vendorService = vendorService;
            _purchaseService = purchaseService;
            _categoryService = categoryService;
            _userSession = userSession;
            _securityGuard = securityGuard;
            _productService = productService;
        }

        public async Task InitializeAsync(Vendor selectedVendor)
        {
            Vendor = selectedVendor;
            await LoadVendorDataAsync();
            LoadVendorActivities();
        }

        partial void OnSelectedTabIndexChanged(int value)
        {
            if (value == 1)
            {
                _ = LoadUnifiedDocumentsWorkspaceAsync(Vendor.VendorId, "Vendors");
            }
        }

        private void LoadVendorActivities()
        {
            VendorActivities.Clear();

            // Populate activity items based on PO status history
            foreach (var po in VendorOrders)
            {
                VendorActivities.Add(new VendorActivityItem
                {
                    Title = $"Purchase Order Created ({po.PoNumber})",
                    Description = $"PO drafted for total amount ₹{po.TotalAmount:N2}",
                    Timestamp = po.OrderDate,
                    CreatedBy = po.CreatedBy,
                    IconKind = "FileDocumentPlusOutline",
                    IconColor = "#0284C7"
                });

                if (po.OrderStatus == "Received" && po.ActualDeliveryDate.HasValue)
                {
                    VendorActivities.Add(new VendorActivityItem
                    {
                        Title = $"Goods Received ({po.PoNumber})",
                        Description = po.IsDelayed
                            ? $"Stock fulfilled into inventory ({po.DelayInDays} Days Late)"
                            : "Stock fulfilled on time into inventory",
                        Timestamp = po.ActualDeliveryDate.Value,
                        CreatedBy = "System",
                        IconKind = po.IsDelayed ? "AlertCircleOutline" : "CheckCircleOutline",
                        IconColor = po.IsDelayed ? "#DC2626" : "#16A34A"
                    });
                }
            }

            // Sort activities by newest date first
            var sorted = VendorActivities.OrderByDescending(a => a.Timestamp).ToList();
            VendorActivities = new ObservableCollection<VendorActivityItem>(sorted);

            OnPropertyChanged(nameof(HasActivities));
        }

        /// <summary>
        /// Invoke this inside ShowLeadWorkspace to cleanly build the single document grid.
        /// Pass "lead" or "customer" as the activeModule string parameter context.
        /// </summary>
        public async Task LoadUnifiedDocumentsWorkspaceAsync(int entityId, string activeModule)
        {
            var categoriesList = await _categoryService.GetCategoriesByModulesAsync(activeModule);

            // 2. Fetch all files currently uploaded for this specific profile ID            

            var filesList = await _categoryService.GetFilesByProfileIdAsync(activeModule, entityId);
            App.Current.Dispatcher.Invoke(() =>
            {
                AvailableDocumentCategories = new ObservableCollection<BusinessCategory>(categoriesList);
                SelectedUploadCategory = AvailableDocumentCategories.FirstOrDefault();

                UnifiedDocumentsCollection.Clear();
                foreach (var file in filesList)
                {
                    UnifiedDocumentsCollection.Add(file);
                }

                DocumentCountSummaryText = $"{filesList.Count()} Total Document Attachments Registered";
            });
        }

        [RelayCommand]
        public async Task LoadVendorDataAsync()
        {
            if (Vendor == null || Vendor.VendorId == 0) return;

            // 1. Fetch Purchase Orders
            var orders = await _purchaseService.GetOrdersByVendorIdAsync(Vendor.VendorId);
            VendorOrders = new ObservableCollection<PurchaseOrder>(orders);

            // 2. Compute Ratings & Performance Metrics
            CalculateVendorMetrics(orders);

            // 2. Fetch Linked Products
            var products = await _vendorService.GetProductsByVendorIdAsync(Vendor.VendorId);
            VendorProducts = new ObservableCollection<VendorProductLinkDisplay>(products);
        }

        private void CalculateVendorMetrics(System.Collections.Generic.IEnumerable<PurchaseOrder> orders)
        {
            TotalOrdersProcessed = orders.Count();

            if (TotalOrdersProcessed > 0)
            {
                TotalPurchasesYtd = orders.Sum(o => o.TotalAmount);
                LastPurchaseDate = orders.Max(o => o.OrderDate);
            }

            var receivedOrders = orders.Where(o => o.OrderStatus == "Received").ToList();

            if (!receivedOrders.Any())
            {
                DelayRating = 5.0;
                OnTimeDeliveryRate = 100.0;
                AvgDelayDays = 0;
                DelayedOrdersCount = 0;
                return;
            }

            DelayedOrdersCount = receivedOrders.Count(o => o.IsDelayed);
            int totalDelayDays = receivedOrders.Sum(o => o.DelayInDays);

            OnTimeDeliveryRate = Math.Round(((double)(receivedOrders.Count - DelayedOrdersCount) / receivedOrders.Count) * 100.0, 1);
            AvgDelayDays = (int)Math.Ceiling((double)totalDelayDays / receivedOrders.Count);

            // Rating penalty formula: starts at 5.0, deducts based on delay frequency & avg days late
            double delayPenalty = (AvgDelayDays * 0.4) + ((double)DelayedOrdersCount / receivedOrders.Count * 1.5);
            DelayRating = Math.Round(Math.Max(1.0, Math.Min(5.0, 5.0 - delayPenalty)), 1);
        }

        [RelayCommand]
        private void NavigateBack()
        {
            OnNavigateBackRequested?.Invoke();
        }

        #region TAB 4: DOCUMENTS COMMANDS

        [RelayCommand]
        private async Task UploadDocumentAsync()
        {
            if (Vendor == null || SelectedUploadCategory == null)
            {
                MessageBox.Show("Please choose a target Document Category from the dropdown selector first.", "Context Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true, // Bulk multi-uploads to a single category made simple!
                Filter = "Compliance Formats|*.pdf;*.jpg;*.jpeg;*.png;*.xlsx;*.docx"
            };

            if (fileDialog.ShowDialog() == true)
            {
                var success = await _categoryService.UploadDocumentAsync(fileDialog.FileNames, "Vendors", SelectedUploadCategory, Vendor.VendorId, _userSession.CurrentUser);

                if (success)
                {
                    MessageBox.Show("File(s) uploaded successfully!", "Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("File upload failed. Please check the logs for details.", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // Refresh grid matrix instantly
                await LoadUnifiedDocumentsWorkspaceAsync(Vendor.VendorId, "Vendors");
            }
        }

        [RelayCommand]
        private async Task DeleteDocumentFile(UploadedDocumentRow selectedRow)
        {
            if (selectedRow == null) return;

            var result = MessageBox.Show($"Are you sure you want to permanently delete '{selectedRow.FileName}'?", "Confirm Purge", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                // 1. Clean up physical host disk block
                if (System.IO.File.Exists(selectedRow.StoragePath))
                {
                    System.IO.File.Delete(selectedRow.StoragePath);
                }

                // 2. Clear out database pointer record
                await _categoryService.DeleteDocumentRecordAsync(selectedRow.DocumentId);

                // 3. Refresh display layout
                await LoadUnifiedDocumentsWorkspaceAsync(Vendor.VendorId, "Vendors");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while purging file instance: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ReplaceDocumentFile(UploadedDocumentRow selectedRow)
        {
            if (selectedRow == null || Vendor == null) return;

            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Supported Files|*.pdf;*.jpg;*.jpeg;*.png;*.xlsx;*.docx",
                Title = $"Replace Document: {selectedRow.FileName}"
            };

            if (fileDialog.ShowDialog() == true)
            {
                try
                {
                    // 1. Delete the old physical file to prevent disk bloat
                    if (System.IO.File.Exists(selectedRow.StoragePath))
                    {
                        System.IO.File.Delete(selectedRow.StoragePath);
                    }

                    // 2. Write the new file instance exactly to the same vault directory layout path
                    string newLocalPath = fileDialog.FileName;
                    string extension = System.IO.Path.GetExtension(newLocalPath);
                    string cleanName = System.IO.Path.GetFileName(newLocalPath);

                    string targetDir = System.IO.Path.GetDirectoryName(selectedRow.StoragePath)!;
                    string dynamicStoragePath = System.IO.Path.Combine(targetDir, $"{Guid.NewGuid()}_{cleanName}");

                    System.IO.File.Copy(newLocalPath, dynamicStoragePath, true);

                    // 3. Update the tracking row properties inside MySQL server records mapping
                    await _categoryService.ReplaceUploadDocumentAsync(cleanName, dynamicStoragePath, _userSession.CurrentUser, selectedRow.DocumentId);

                    // 4. Instantly refresh the UI matrix grid list
                    await LoadUnifiedDocumentsWorkspaceAsync(Vendor.VendorId, "Vendors");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to replace document attachment: {ex.Message}", "IO Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task DownloadDocumentFile(UploadedDocumentRow selectedRow)
        {
            bool accessGranted = await _securityGuard.IsActionAuthorizedAsync();
            if (!accessGranted) return; // Halt execution path immediately

            if (selectedRow == null || string.IsNullOrEmpty(selectedRow.StoragePath)) return;

            if (!System.IO.File.Exists(selectedRow.StoragePath))
            {
                MessageBox.Show("The source document file could not be discovered on server storage paths.", "File Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Initialize native save dialog frame
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = selectedRow.FileName, // Prefills original filename automatically
                Filter = $"File Extension (*{System.IO.Path.GetExtension(selectedRow.StoragePath)})|*{System.IO.Path.GetExtension(selectedRow.StoragePath)}",
                Title = "Download Document Reference Copy As"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.Copy(selectedRow.StoragePath, saveDialog.FileName, true);
                    MessageBox.Show("File copied and saved locally successfully!", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not export document file copy: {ex.Message}", "Download Fault", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        [RelayCommand]
        private async Task LinkNewProductAsync()
        {
            if (Vendor == null || Vendor.VendorId == 0) return;

            // Fetch master catalog items to select from
            var allProducts = await _productService.GetAllProductsAsync();

            var dialogVm = new LinkVendorProductDialogViewModel(Vendor.VendorId, allProducts);
            var dialog = new LinkVendorProductDialogWindow
            {
                DataContext = dialogVm,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && dialogVm.SelectedProduct != null)
            {
                bool success = await _vendorService.SaveVendorProductLinkAsync(
                    Vendor.VendorId,
                    dialogVm.SelectedProduct.ProductId,
                    dialogVm.SupplierSku,
                    dialogVm.PurchasePrice
                );

                if (success)
                {
                    // Refresh vendor products list
                    var updatedProducts = await _vendorService.GetProductsByVendorIdAsync(Vendor.VendorId);
                    VendorProducts = new ObservableCollection<VendorProductLinkDisplay>(updatedProducts);
                }
                else
                {
                    MessageBox.Show("Failed to link product to vendor.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task EditProductLinkAsync(VendorProductLinkDisplay? item)
        {
            if (item == null || Vendor == null) return;

            var allProducts = await _productService.GetAllProductsAsync();

            var dialogVm = new LinkVendorProductDialogViewModel(Vendor.VendorId, allProducts, item);
            var dialog = new LinkVendorProductDialogWindow
            {
                DataContext = dialogVm,
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && dialogVm.SelectedProduct != null)
            {
                bool success = await _vendorService.SaveVendorProductLinkAsync(
                    Vendor.VendorId,
                    dialogVm.SelectedProduct.ProductId,
                    dialogVm.SupplierSku,
                    dialogVm.PurchasePrice
                );

                if (success)
                {
                    var updatedProducts = await _vendorService.GetProductsByVendorIdAsync(Vendor.VendorId);
                    VendorProducts = new ObservableCollection<VendorProductLinkDisplay>(updatedProducts);
                }
            }
        }
    }
}
