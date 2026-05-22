using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
using MySqlX.XDevAPI;
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
        private readonly IUserSession _session;

        public event Action<bool>? RequestClose;

        [ObservableProperty] private Order _selectedOrder;
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private string _method = "Cash";

        public List<string> Methods { get; } = new() { "Cash", "GPay", "Cheque", "RTGS" };

        public AddPaymentViewModel(Order order, LeadService service, IUserSession session)
        {
            _selectedOrder = order;
            _service = service;
            _session = session;
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
                Remarks = "Payment Received"
            };

            var historyEntry = new LeadHistoryEntry
            {
                Message = "Payment Received",
                Content = $"Received {Amount:C} via {Method} for Order #{SelectedOrder.OrderId}",
                UpdatedByContent = "record a payment",
                NextFollowUpDate = DateTime.Now,
                UpdatedBy = _session.CurrentUser,
                LogDate = DateTime.Now,
                IsPriority = true
            };

            await _service.RecordPaymentAsync(p, historyEntry);
            RequestClose?.Invoke(true);
        }
    }
}
