using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Models;
using Tijori.Services;
using Tijori.Views;

namespace Tijori.ViewModels
{
    public partial class StockRegisterViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly LeadService _leadService;     // For Matured Customers
        private readonly VendorService _vendorService;
        private readonly OrderService _orderService;
        private readonly PurchaseService _purchaseService;

        [ObservableProperty]
        private string _headerTitleText = "Stock Register Ledger";

        public string SelectedItemDisplayName => SelectedProduct != null ? SelectedProduct.Name : "Select an Item from Filter to view report";

        [ObservableProperty] private Product? _selectedProduct;

        partial void OnSelectedProductChanged(Product? value)
        {
            SelectedBatch = null;
            OnPropertyChanged(nameof(IsBatchSelectionEnabled));
            OnPropertyChanged(nameof(SelectedItemDisplayName)); // Notify UI banner

            // Re-filter child collections based on chosen product
            FilterBatches();
            FilterCustomersAndVendors();
        }

        // 2. Selected Batch
        [ObservableProperty] private ProductBatch? _selectedBatch;

        partial void OnSelectedBatchChanged(ProductBatch? value)
        {
            FilterCustomersAndVendors();
        }

        public bool IsBatchSelectionEnabled => SelectedProduct != null;

        // 3. Selected Customer
        [ObservableProperty] private Lead? _selectedCustomer;

        // 4. Selected Vendor
        [ObservableProperty] private Vendor? _selectedVendor;

        // 5. Date Range
        [ObservableProperty]
        private DateTime _fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        [ObservableProperty]
        private DateTime _toDate = DateTime.Now;

        // 6. Location Filter
        [ObservableProperty]
        private string? _locationFilter;

        private List<Product> _allProducts = new();
        private List<ProductBatch> _allBatches = new();
        private List<Lead> _allMaturedCustomers = new();
        private List<Vendor> _allVendors = new();

        // --- Filter Collections ---
        [ObservableProperty] private ObservableCollection<Product> _availableItems = new();
        [ObservableProperty] private ObservableCollection<ProductBatch> _filteredBatches = new();
        [ObservableProperty] private ObservableCollection<Lead> _filteredCustomers = new();
        [ObservableProperty] private ObservableCollection<Vendor> _filteredVendors = new();

        // Top bar indicator
        [ObservableProperty] private bool _isFilterActive;

        [ObservableProperty]
        private decimal _openingBalanceQty = 0;

        [ObservableProperty]
        private ObservableCollection<StockRegisterRow> _stockRegisterRows = new();

        [ObservableProperty]
        private decimal _totalReceivedQty;

        [ObservableProperty]
        private decimal _totalReceivedValue;

        [ObservableProperty]
        private decimal _totalIssuedQty;

        [ObservableProperty]
        private decimal _totalIssuedValue;

        [ObservableProperty] private bool _exportAsExcel = true;
        [ObservableProperty] private bool _exportAsPdf = false;
        [ObservableProperty] private bool _exportAsCsv = false;

        // --- Column Selection Options ---
        [ObservableProperty] private bool _includeBillNo = true;
        [ObservableProperty] private bool _includeDate = true;
        [ObservableProperty] private bool _includeType = true;
        [ObservableProperty] private bool _includeDescription = true;
        [ObservableProperty] private bool _includeBatchNumber = true;
        [ObservableProperty] private bool _includeQuantity = true;
        [ObservableProperty] private bool _includeValue = true;
        [ObservableProperty] private bool _includeBalanceQuantity = true;

        public StockRegisterViewModel(ProductService productService, LeadService leadService, VendorService vendorService, OrderService orderService, PurchaseService purchaseService)
        {
            _productService = productService;
            _leadService = leadService;
            _vendorService = vendorService;
            _orderService = orderService;
            _purchaseService = purchaseService;
        }

        public async Task InitializeAsync()
        {
            // 1. Load Master Products
            var products = await _productService.GetAllProductsAsync();
            _allProducts = products?.ToList() ?? new List<Product>();
            AvailableItems = new ObservableCollection<Product>(_allProducts);

            // 2. Load Master Batches
            var batches = await _productService.GetAllBatchesAsync();
            _allBatches = batches?.ToList() ?? new List<ProductBatch>();

            // 3. Load Customers (Leads where status == Matured)
            var leads = await _leadService.GetAllLeadsWithLatestUpdateAsync();
            _allMaturedCustomers = leads?.Where(l => l.Status?.Equals("Matured", StringComparison.OrdinalIgnoreCase) == true).ToList()
                                   ?? new List<Lead>();
            FilteredCustomers = new ObservableCollection<Lead>(_allMaturedCustomers);

            // 4. Load Master Vendors
            var vendors = await _vendorService.GetAllVendorsAsync();
            _allVendors = vendors?.ToList() ?? new List<Vendor>();
            FilteredVendors = new ObservableCollection<Vendor>(_allVendors);
        }

        // --- Filter Dialog Commands ---
        [RelayCommand]
        private async Task OpenFilterDialog()
        {
            await DialogHost.Show(new StockRegisterFilterDialogView { DataContext = this }, "StockRegisterDialogHost");
        }

        private void FilterBatches()
        {
            FilteredBatches.Clear();

            if (SelectedProduct == null) return;

            // Filter batches belonging to selected Product Id
            var matchingBatches = _allBatches.Where(b => b.ProductId == SelectedProduct.ProductId);
            foreach (var batch in matchingBatches)
            {
                FilteredBatches.Add(batch);
            }
        }

        private void FilterCustomersAndVendors()
        {
            // Reset customer & vendor choices if previous selection is filtered out
            FilteredCustomers = new ObservableCollection<Lead>(
                _allMaturedCustomers.Where(c => MatchLocation(c.City))
            );

            FilteredVendors = new ObservableCollection<Vendor>(
                _allVendors.Where(v => MatchLocation(v.Address))
            );
        }

        private bool MatchLocation(string? city)
        {
            if (string.IsNullOrWhiteSpace(LocationFilter)) return true;
            return city != null && city.Contains(LocationFilter, StringComparison.OrdinalIgnoreCase);
        }

        [RelayCommand]
        private async Task ApplyFilter()
        {
            IsFilterActive = SelectedProduct != null
                          || SelectedBatch != null
                          || SelectedCustomer != null
                          || SelectedVendor != null
                          || !string.IsNullOrWhiteSpace(LocationFilter);

            // Fetch Report Data from Database
            await GenerateLedgerReportAsync();

            if (DialogHost.IsDialogOpen("StockRegisterDialogHost"))
            {
                DialogHost.Close("StockRegisterDialogHost");
            }
        }

        [RelayCommand]
        private void DiscardFilterDialog()
        {
            SelectedProduct = null;
            SelectedBatch = null;
            SelectedCustomer = null;
            SelectedVendor = null;
            LocationFilter = string.Empty;
            FromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            ToDate = DateTime.Now;
            IsFilterActive = false;

            if (DialogHost.IsDialogOpen("StockRegisterDialogHost"))
            {
                DialogHost.Close("StockRegisterDialogHost");
            }
        }

        [RelayCommand]
        private void ClearFilters()
        {
            SelectedProduct = null;
            SelectedBatch = null;
            SelectedCustomer = null;
            SelectedVendor = null;
            LocationFilter = string.Empty;
            FromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            ToDate = DateTime.Now;
            IsFilterActive = false;

            if (DialogHost.IsDialogOpen("StockRegisterDialogHost"))
            {
                DialogHost.Close("StockRegisterDialogHost");
            }
        }

        private void ResetSummaryMetrics()
        {
            TotalReceivedQty = 0;
            TotalReceivedValue = 0;
            TotalIssuedQty = 0;
            TotalIssuedValue = 0;
        }

        private async Task GenerateLedgerReportAsync()
        {
            if (SelectedProduct == null)
            {
                StockRegisterRows.Clear();
                OpeningBalanceQty = 0;
                ResetSummaryMetrics();
                return;
            }

            int productId = SelectedProduct.ProductId;
            int? selectedBatchId = SelectedBatch?.BatchId;
            int? selectedLeadId = SelectedCustomer?.LeadId;
            int? selectedVendorId = SelectedVendor?.VendorId;
            string? location = string.IsNullOrWhiteSpace(LocationFilter) ? null : LocationFilter.Trim();

            // 1. Fetch filtered transactions directly from DB
            var inwardList = await _purchaseService.GetStockInwardFilteredAsync(productId, selectedVendorId, location);
            var outwardList = await _orderService.GetStockOutwardFilteredAsync(productId, selectedBatchId, selectedLeadId, location);

            // Filter inwardList by selected batch if a batch filter is applied
            if (SelectedBatch != null)
            {
                inwardList = inwardList.Where(inw => inw.BatchNumber == SelectedBatch.BatchNumber).ToList();
            }

            // 2. COMPUTE OPENING BALANCE AT FromDate
            decimal baseStock = 0;

            if (SelectedBatch == null)
            {
                // When viewing whole product: Base stock is the product's original initial stock when created
                baseStock = SelectedProduct.InitialStock; // Use InitialStock, NOT RemainingStock
            }
            else
            {
                // When viewing a specific batch: Batch starts at 0 until its receipt PO transaction occurs
                baseStock = 0;
            }

            // Calculate movements strictly PRIOR to FromDate
            decimal inwardBeforeFromDate = inwardList
                .Where(x => x.TransactionDate.Date < FromDate.Date)
                .Sum(x => x.Quantity);

            decimal outwardBeforeFromDate = outwardList
                .Where(x => x.TransactionDate.Date < FromDate.Date)
                .Sum(x => x.Quantity);

            // True Opening Balance on FromDate
            OpeningBalanceQty = baseStock + inwardBeforeFromDate - outwardBeforeFromDate;

            // 3. FILTER MOVEMENTS WITHIN DATE RANGE (FromDate to ToDate)
            var ledgerTimeline = new List<StockRegisterRow>();

            // Add Purchases occurring within range
            foreach (var inw in inwardList.Where(x => x.TransactionDate.Date >= FromDate.Date && x.TransactionDate.Date <= ToDate.Date))
            {
                ledgerTimeline.Add(new StockRegisterRow
                {
                    BillNo = inw.BillNo,
                    TransactionDate = inw.TransactionDate,
                    VoucherType = "Purchase",
                    Description = inw.VendorName,
                    BatchNumber = inw.BatchNumber,
                    Quantity = inw.Quantity,
                    Value = inw.TotalAmount,
                    MovementType = "Inward"
                });
            }

            // Add Sales occurring within range
            foreach (var outw in outwardList.Where(x => x.TransactionDate.Date >= FromDate.Date && x.TransactionDate.Date <= ToDate.Date))
            {
                ledgerTimeline.Add(new StockRegisterRow
                {
                    BillNo = outw.BillNo,
                    TransactionDate = outw.TransactionDate,
                    VoucherType = "Sale",
                    Description = outw.CustomerName,
                    BatchNumber = outw.BatchNumber,
                    Quantity = outw.Quantity,
                    Value = outw.TotalAmount,
                    MovementType = "Outward"
                });
            }

            // 4. SORT CHRONOLOGICALLY & COMPUTE RUNNING BALANCE
            var sortedTimeline = ledgerTimeline
                .OrderBy(r => r.TransactionDate)
                .ThenBy(r => r.BillNo)
                .ToList();

            decimal currentBalance = OpeningBalanceQty;
            decimal recQty = 0, recVal = 0;
            decimal issQty = 0, issVal = 0;

            foreach (var row in sortedTimeline)
            {
                if (row.MovementType == "Inward")
                {
                    currentBalance += row.Quantity;
                    recQty += row.Quantity;
                    recVal += row.Value;
                }
                else // Outward
                {
                    currentBalance -= row.Quantity;
                    issQty += row.Quantity;
                    issVal += row.Value;
                }

                row.BalanceQuantity = currentBalance;
            }

            // 5. UPDATE UI
            StockRegisterRows = new ObservableCollection<StockRegisterRow>(sortedTimeline);
            TotalReceivedQty = recQty;
            TotalReceivedValue = recVal;
            TotalIssuedQty = issQty;
            TotalIssuedValue = issVal;
        }

        [RelayCommand]
        private async Task OpenExportDialog()
        {
            // Opens ExportDialogView inside MaterialDesign DialogHost
            await DialogHost.Show(new ExportDialogView { DataContext = this }, "StockRegisterDialogHost");
        }

        [RelayCommand]
        private void CloseExportDialog()
        {
            if (DialogHost.IsDialogOpen("StockRegisterDialogHost"))
            {
                DialogHost.Close("StockRegisterDialogHost");
            }
        }

        [RelayCommand]
        private void SelectAllColumns()
        {
            IncludeBillNo = true;
            IncludeDate = true;
            IncludeType = true;
            IncludeDescription = true;
            IncludeBatchNumber = true;
            IncludeQuantity = true;
            IncludeValue = true;
            IncludeBalanceQuantity = true;
        }

        [RelayCommand]
        private void ExecuteExport()
        {
            CloseExportDialog();

            if (ExportAsExcel)
            {
                // Execute ClosedXML or EPPlus Excel exporter
                ExportToExcel();
            }
            else if (ExportAsPdf)
            {
                // Execute MigraDoc / PdfSharp PDF exporter
                ExportToPdf();
            }
            else if (ExportAsCsv)
            {
                // Execute CSV writer
                ExportToCsv();
            }
        }

        private void ExportToExcel()
        {
            // Filter columns based on IncludeBillNo, IncludeDate, etc.
        }

        private void ExportToPdf()
        {
            // Build PDF table with enabled columns
        }

        private void ExportToCsv()
        {
            // Write CSV rows with enabled columns
        }
    }
}
