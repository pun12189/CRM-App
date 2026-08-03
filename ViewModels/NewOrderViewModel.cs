using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.ViewModels
{
    public partial class NewOrderViewModel : ObservableObject
    {
        private readonly LeadService _service;
        public event Action<bool>? RequestClose;

        [ObservableProperty] private int _leadId;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private string _description = string.Empty;

        public NewOrderViewModel(int leadId, LeadService service)
        {
            _leadId = leadId;
            _service = service;
        }

        [RelayCommand]
        private async Task SaveOrder()
        {
            if (TotalAmount <= 0) return;

            var order = new Order
            {
                LeadId = LeadId,
                TotalAmount = TotalAmount,
                Description = Description,
                OrderDate = DateTime.Now
            };

            await _service.CreateOrderAsync(order);
            RequestClose?.Invoke(true);
        }
    }
}
