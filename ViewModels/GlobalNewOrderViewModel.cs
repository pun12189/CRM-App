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
    public partial class GlobalNewOrderViewModel : ObservableObject
    {
        private readonly LeadService _service;
        public event Action<bool>? RequestClose;

        [ObservableProperty] private ObservableCollection<Lead> _maturedCustomers = new();
        [ObservableProperty] private Lead? _selectedCustomer;

        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private string _description = string.Empty;

        public GlobalNewOrderViewModel(LeadService service)
        {
            _service = service;
            _ = LoadCustomers();
        }

        private async Task LoadCustomers()
        {
            // Load only matured leads (your active customers)
            var customers = await _service.GetMaturedLedgerAsync();
            MaturedCustomers = new ObservableCollection<Lead>(customers);
        }

        [RelayCommand]
        private async Task SaveOrder()
        {
            if (SelectedCustomer == null || TotalAmount <= 0)
            {
                // You could add a Snackbar or Messagebox here
                return;
            }

            var order = new Order
            {
                LeadId = SelectedCustomer.LeadId,
                TotalAmount = TotalAmount,
                Description = Description,
                OrderDate = DateTime.Now
            };

            await _service.CreateOrderAsync(order);
            RequestClose?.Invoke(true);
        }
    }
}
