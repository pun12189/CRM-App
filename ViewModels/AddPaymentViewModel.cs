using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.ViewModels
{
    public partial class AddPaymentViewModel : ObservableObject
    {
        private readonly LeadService _service;
        public event Action<bool>? RequestClose;

        [ObservableProperty] private Order _selectedOrder;
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _method = "Cash";

        public List<string> Methods { get; } = new() { "Cash", "GPay", "Cheque", "RTGS" };

        public AddPaymentViewModel(Order order, LeadService service)
        {
            _selectedOrder = order;
            _service = service;
        }

        [RelayCommand]
        private async Task SubmitPayment()
        {
            var p = new PaymentEntry
            {
                OrderId = SelectedOrder.OrderId,
                LeadId = SelectedOrder.LeadId,
                AmountReceived = Amount,
                PaymentMethod = Method,
                Remarks = "Installment Received"
            };

            await _service.RecordPaymentAsync(p);
            RequestClose?.Invoke(true);
        }
    }
}
