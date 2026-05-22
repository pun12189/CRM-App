using CallMan.Dialogs;
using CallMan.Interfaces;
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

namespace CallMan.ViewModels
{
    public partial class AllOrdersViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly IDialogService _dialogService;

        [ObservableProperty] private ObservableCollection<Order> _allOrders = new();
        [ObservableProperty] private bool _isLoading;

        // Summary properties for the Header
        [ObservableProperty] private decimal _totalOrderVolume;
        [ObservableProperty] private int _totalOrderCount;
        [ObservableProperty] private decimal _totalPaymentsReceived;
        [ObservableProperty] private decimal _totalOutstandingBalance;

        [ObservableProperty] private bool _isCounterPanelExpanded = true;
        [ObservableProperty] private CustomerStats _customerStats = new();

        // --- ACCUMULATED SPECIALIZED METRIC COUNTERS ---
        [ObservableProperty] private int _newOrdersCount;
        [ObservableProperty] private int _repeatOrdersCount;
        [ObservableProperty] private int _paidOrdersCount;
        [ObservableProperty] private int _unpaidOrdersCount;
        [ObservableProperty] private int _partiallyPaidOrdersCount;
        [ObservableProperty] private int _pendingStageOrdersCount;

        public AllOrdersViewModel(LeadService service, IDialogService dialogService)
        {
            _service = service;
            _dialogService = dialogService;

            // Initial Load
            _ = LoadAllOrdersAsync();
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

        [RelayCommand]
        public async Task LoadAllOrdersAsync()
        {
            IsLoading = true;
            try
            {
                var data = await _service.GetAllOrdersWithCustomerNamesAsync();                

                // Update collection on UI thread
                AllOrders = new ObservableCollection<Order>(data);
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
            var vm = App.ServiceProvider.GetRequiredService<ImportViewModel>();
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
    }
}
