using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using Tijori.Core;
using Tijori.Dialogs;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class AllOrdersViewModel : ObservableObject, IDashboardFilterable
    {
        private readonly LeadService _service;
        private readonly IDialogService _dialogService;
        private readonly OrderService _orderService;
        private readonly CategoryService _categoryService;
        private readonly IUserSession _userSession;
        private readonly IOrderHistoryService _orderHistoryService;
        private readonly IActionSecurityGuard _actionSecurityGuard;
        private readonly InvoiceService _invoiceService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty] private ObservableCollection<Order> _allOrders = new();
        [ObservableProperty] private bool _isLoading;

        // Summary properties for the Header
        [ObservableProperty] private decimal _totalOrderVolume;
        [ObservableProperty] private int _totalOrderCount;
        [ObservableProperty] private decimal _totalPaymentsReceived;
        [ObservableProperty] private decimal _totalOutstandingBalance;

        [ObservableProperty] private bool _isCounterPanelExpanded = false;
        [ObservableProperty] private CustomerStats _customerStats = new();
        [ObservableProperty] private bool _workspaceViewIsActive;

        // --- ACCUMULATED SPECIALIZED METRIC COUNTERS ---
        [ObservableProperty] private int _newOrdersCount;
        [ObservableProperty] private int _repeatOrdersCount;
        [ObservableProperty] private int _paidOrdersCount;
        [ObservableProperty] private int _unpaidOrdersCount;
        [ObservableProperty] private int _partiallyPaidOrdersCount;
        [ObservableProperty] private int _pendingStageOrdersCount;
        private ICollectionView _ordersCollection;

        [ObservableProperty]
        private string _searchText = string.Empty;

        // This is what the DataGrid actually binds to now
        public ICollectionView OrderCollection => _ordersCollection;

        private bool _isInitialized;

        [ObservableProperty]
        private object _tabsDataContext;

        public AllOrdersViewModel(LeadService service, IDialogService dialogService, OrderService orderService, CategoryService categoryService, IUserSession userSession, IOrderHistoryService orderHistoryService, IActionSecurityGuard actionSecurityGuard, InvoiceService invoiceService, IServiceProvider serviceProvider)
        {
            _service = service;
            _dialogService = dialogService;
            _orderService = orderService;
            _categoryService = categoryService;
            _serviceProvider = serviceProvider;
            _invoiceService = invoiceService;
            _orderHistoryService = orderHistoryService;
            _userSession = userSession;
            _actionSecurityGuard = actionSecurityGuard;

            // Initial Load
            _ = LoadInitialDataAsync();
        }        

        /// <summary>
        /// Core execution engine to segment and group order statuses.
        /// Invoke this routine whenever refreshing the master data arrays.
        /// </summary>
        public void CalculateSegmentedOrderCounters(IEnumerable<Order> allOrders)
        {
            if (allOrders == null || !allOrders.Any()) return;

            // 1. Group by Customer (LeadId) to distinguish New vs Repeat Business
            var customerOrderGroups = allOrders
                .GroupBy(o => o.LeadId)
                .ToDictionary(g => g.Key, g => g.OrderBy(o => o.OrderDate).ToList());

            int tempNewCount = 0;
            int tempRepeatCount = 0;

            foreach (var group in customerOrderGroups.Values)
            {
                // First order in their history lifecycle is categorized as New Order channel
                tempNewCount++;

                // Any subsequent orders beyond their first invoice are considered Repeat purchases
                if (group.Count > 1)
                {
                    tempRepeatCount += (group.Count - 1);
                }
            }

            NewOrdersCount = tempNewCount;
            RepeatOrdersCount = tempRepeatCount;

            // 2. Financial settlement status segment counters matching your exact rules
            // Rule: Paid = Fully Paid, Unpaid/Pending = No payment activity footprint recorded
            PaidOrdersCount = allOrders.Count(o => o.PaymentStatus.ToLower() == "Paid".ToLower());
            PartiallyPaidOrdersCount = allOrders.Count(o => o.PaymentStatus.ToLower() == "Partially Paid".ToLower());
            UnpaidOrdersCount = allOrders.Count(o => o.PaymentStatus.ToLower() == "Pending".ToLower() || o.PaymentStatus.ToLower() == "Unpaid".ToLower());

            // Stage representation anchor matching your business lifecycle workflow properties
            PendingStageOrdersCount = UnpaidOrdersCount;
        }

        private async Task LoadInitialDataAsync()
        {
            IsLoading = true;
            try
            {
                if (_isInitialized) return;
                var data = await _service.GetAllOrdersWithCustomerNamesAsync();

                if (_isInitialized) return;
                // Update collection on UI thread
                AllOrders = new ObservableCollection<Order>(data);

                _ordersCollection = CollectionViewSource.GetDefaultView(AllOrders);

                // 4. Re-apply your search filter logic
                _ordersCollection.Filter = FilterOrders;

                // 5. Notify the UI to refresh the table
                OnPropertyChanged(nameof(OrderCollection));

                await RecalculateLedgerAnalytics(); // Recalculate analytics whenever orders are loaded
                CalculateSegmentedOrderCounters(AllOrders); // Calculate segmented order counters

                // Calculate Summaries
                TotalOrderCount = AllOrders.Count;
                TotalOrderVolume = AllOrders.Sum(x => x.TotalAmount);

                CustomerStats = await _service.GetCustomerFinancialSummaryAsync(1);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task LoadAllOrdersAsync()
        {
            _isInitialized = false;
            await LoadInitialDataAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            _ordersCollection?.Refresh();
        }

        private bool FilterOrders(object obj)
        {
            if (obj is not Order order) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            // Search across multiple fields: Name, Phone, City, and Company
            return order.FormattedOrderId.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   (order.FirmName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (order.Status?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (order.PaymentStatus?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (order.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (order.LeadHolder?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (order.ProcessedBy?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (order.CustomerName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (order.ProformaNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (order.InvoiceNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (order.OrderType?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (order.Items?.Any(d => d.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                   (order.Items?.Any(d => d.BatchNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ?? false);                   
        }

        [RelayCommand]
        private async Task ViewOrderDetails(Order selectedOrder)
        {
            if (selectedOrder == null) return;

            // 1. Fetch the Lead object for this order (since Order contains LeadId)
            var lead = await _service.GetLeadByIdAsync(selectedOrder.LeadId);

            if (lead != null)
            {
                // 2. Open the Order Details Popup 
                // We use the same ShowOrderWindow logic we built for the Customer Ledger
                _dialogService.ShowOrderWindow(lead);
            }
        }

        [RelayCommand]
        private async Task AddNewOrder()
        {
            // 1. Open Dialog to create new order
            var result = await _dialogService.ShowGlobalNewOrderDialog();

            if (result == true)
            {
                await LoadAllOrdersAsync(); // Refresh list after adding
            }
        }

        [RelayCommand]
        private async Task RefreshData()
        {
            await LoadAllOrdersAsync();
        }

        [RelayCommand]
        private async Task ImportOrders()
        {
            var vm = _serviceProvider.GetRequiredService<ImportViewModel>();
            await vm.InitializeAsync(ImportType.Order);
            var dialogWindow = new ImportView { DataContext = vm };
            // No need for a close event here since the ImportViewModel can directly call LoadOrders() after a successful import
            vm.RequestClose += (result) =>
            {
                dialogWindow.DialogResult = result;
                dialogWindow.Close();
            };

            if (dialogWindow.ShowDialog() == true)
            {
                // Re-run the query to show the new lead in the DataGrid
                await LoadAllOrdersAsync();
            }
        }

        /// <summary>
        /// Call this routine every time you reload your orders ledger list from the database
        /// </summary>
        private async Task RecalculateLedgerAnalytics()
        {
            var data = await _service.GetMaturedLedgerAsync();
            var mleads = new ObservableCollection<Lead>(data);
            TotalOutstandingBalance = mleads.Sum(x => x.TotalBalanceDue);
            TotalPaymentsReceived = mleads.Sum(x => x.TotalPaidAmount);
        }

        public async void ApplyDashboardFilter(DashboardFilter? filter, DashboardTargetView target)
        {
            _isInitialized = true;

            try
            {
                // 1. Pull the data from your new repository method
                var retrievedOrders = await _service.GetOrdersByDashboardContextAsync(target, filter);

                // 2. Clear old collections and populate without breaking references
                AllOrders.Clear();
                foreach (var order in retrievedOrders)
                {
                    AllOrders.Add(order);
                }

                // 3. Reset and refresh the collection view to bind seamlessly to the DataGrid
                _ordersCollection = CollectionViewSource.GetDefaultView(AllOrders);
                _ordersCollection.Refresh();

                // 4. Force WPF notification event pass
                OnPropertyChanged(nameof(OrderCollection));

                await RecalculateLedgerAnalytics(); // Recalculate analytics whenever orders are loaded
                CalculateSegmentedOrderCounters(AllOrders); // Calculate segmented order counters

                // Calculate Summaries
                TotalOrderCount = AllOrders.Count;
                TotalOrderVolume = AllOrders.Sum(x => x.TotalAmount);

                CustomerStats = await _service.GetCustomerFinancialSummaryAsync(1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Orders Dashboard Drilldown Sync Failure: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ShowOrderDetails(Order selectedOrder)
        {
            LoadingService.Show("Loading view... Please wait.");
            if (selectedOrder == null) return;

            dynamic profileVm = _serviceProvider.GetRequiredService<OrderDetailsViewModel>();

            await profileVm.InitializeAsync(this, selectedOrder);

            this.TabsDataContext = profileVm;
            WorkspaceViewIsActive = true; // Swaps grid out for profile workspace view layout instantly
        }

        [RelayCommand]
        public void HideLeadWorkspace()
        {
            WorkspaceViewIsActive = false;
        }

        [RelayCommand]
        private async Task PrintTaxInvoiceAsync(Order selectedOrder)
        {
            try
            {
                var invoiceData = await _invoiceService.GetOrderInvoiceDataAsync(selectedOrder.OrderId);
                if (invoiceData == null)
                {
                    MessageBox.Show("Unable to load invoice data for Order #" + selectedOrder.FormattedOrderId, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                double printableWidth = 793.7;
                var doc = _invoiceService.CreateTaxInvoiceDocument(invoiceData, printableWidth);

                var previewWin = new PrintPreviewWindow
                {
                    Owner = Application.Current.MainWindow
                };

                previewWin.LoadFlowDocument(doc, $"Invoice Preview - {invoiceData.InvoiceNumber}");
                previewWin.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating invoice: {ex.Message}", "Invoice Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
