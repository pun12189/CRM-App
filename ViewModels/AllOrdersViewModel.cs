using CallMan.Interfaces;
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
    public partial class AllOrdersViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly IDialogService _dialogService;

        [ObservableProperty] private ObservableCollection<Order> _allOrders = new();
        [ObservableProperty] private bool _isLoading;

        // Summary properties for the Header
        [ObservableProperty] private decimal _totalOrderVolume;
        [ObservableProperty] private int _totalOrderCount;

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
    }
}
