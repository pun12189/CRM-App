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
    public partial class OrderViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly IDialogService _dialogService; // Assuming a service to handle popups

        [ObservableProperty] private Lead _currentCustomer;
        [ObservableProperty] private ObservableCollection<Order> _ordersList = new();
        [ObservableProperty] private Order? _selectedOrder;

        public OrderViewModel(Lead customer, LeadService service, IDialogService dialogService)
        {
            _currentCustomer = customer;
            _service = service;
            _dialogService = dialogService;

            // Initialize and Load Data
            Task.Run(async () => await LoadOrders());
        }

        [RelayCommand]
        public async Task LoadOrders()
        {
            var data = await _service.GetOrdersByLeadIdAsync(CurrentCustomer.LeadId);
            App.Current.Dispatcher.Invoke(() =>
            {
                OrdersList = new ObservableCollection<Order>(data);
            });
        }

        [RelayCommand]
        private async Task AddNewOrder()
        {
            // 1. Open Dialog to create new order
            var result = await _dialogService.ShowNewOrderDialog(CurrentCustomer.LeadId);

            if (result == true)
            {
                await LoadOrders(); // Refresh list after adding
            }
        }

        [RelayCommand]
        private async Task CollectPayment(Order order)
        {
            if (order == null) return;

            // 2. Open Dialog to collect payment against this order
            var result = await _dialogService.ShowAddPaymentDialog(order);

            if (result == true)
            {
                await LoadOrders(); // Refresh to see updated "Fully Paid" or "Partially Paid" status
            }
        }
    }
}
