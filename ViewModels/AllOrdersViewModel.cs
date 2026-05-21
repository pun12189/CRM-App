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

        [ObservableProperty] private decimal _totalSalesRevenue;
        [ObservableProperty] private decimal _totalCostOfGoodsSold;
        [ObservableProperty] private decimal _netProfitLossAmount;
        [ObservableProperty] private bool _isOverallProfitable = true;

        public AllOrdersViewModel(LeadService service, IDialogService dialogService)
        {
            _service = service;
            _dialogService = dialogService;

            // Initial Load
            _ = LoadAllOrdersAsync();
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
                RecalculateLedgerAnalytics(AllOrders); // Recalculate analytics whenever orders are loaded

                // Calculate Summaries
                TotalOrderCount = AllOrders.Count;
                TotalOrderVolume = AllOrders.Sum(x => x.TotalAmount);
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
        private void RecalculateLedgerAnalytics(IEnumerable<Order> ordersList)
        {
            TotalSalesRevenue = ordersList.Sum(o => o.TotalAmount); // Base pre-tax sales value
            TotalCostOfGoodsSold = ordersList.Sum(o => o.TotalCostAmount); // Historical accumulated cost footprint

            // Net profit = Revenue - COGS
            NetProfitLossAmount = TotalSalesRevenue - TotalCostOfGoodsSold;
            IsOverallProfitable = NetProfitLossAmount >= 0;
        }
    }
}
