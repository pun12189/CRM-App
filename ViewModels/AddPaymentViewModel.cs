using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.ViewModels
{
    public partial class AddPaymentViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly IUserSession _session;
        private readonly IOrderHistoryService _orderHistoryService;

        public event Action<bool>? RequestClose;

        [ObservableProperty] private Order _selectedOrder;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RemainingBalance))]
        [NotifyPropertyChangedFor(nameof(IsOverpaid))]
        private decimal _amount;

        [ObservableProperty] private string _method = "Cash";
        [ObservableProperty] private string _remarks = "Payment Received";

        public List<string> Methods { get; } = new() { "Cash", "GPay", "Cheque", "RTGS", "Bank Transfer" };

        #region DYNAMIC BALANCE COMPUTED PROPERTIES

        /// <summary>
        /// Calculates remaining balance live based on GrandTotal/TotalAmount - AmountPaid - Current Entered Amount
        /// </summary>
        public decimal RemainingBalance
        {
            get
            {
                if (SelectedOrder == null) return 0;

                // Uses GrandTotal if available, otherwise falls back to TotalAmount
                decimal targetTotal = SelectedOrder.GrandTotal > 0 ? SelectedOrder.GrandTotal : SelectedOrder.TotalAmount;
                decimal outstandingBeforeThisPayment = targetTotal - SelectedOrder.AmountPaid;

                return outstandingBeforeThisPayment - Amount;
            }
        }

        /// <summary>
        /// Flags if the entered payment exceeds the total outstanding balance
        /// </summary>
        public bool IsOverpaid => RemainingBalance < 0;

        /// <summary>
        /// Resolves the actual invoice value whether the order is itemized (GrandTotal) or flat-amount (TotalAmount).
        /// </summary>
        public decimal TargetInvoiceValue
        {
            get
            {
                if (SelectedOrder == null) return 0;

                // If GrandTotal is > 0 (itemized order), use GrandTotal.
                // Otherwise fallback to TotalAmount (flat order).
                return SelectedOrder.GrandTotal > 0 ? SelectedOrder.GrandTotal : SelectedOrder.TotalAmount;
            }
        }

        #endregion

        public AddPaymentViewModel(Order order, LeadService service, IUserSession session, IOrderHistoryService orderHistoryService)
        {
            _selectedOrder = order;
            _service = service;
            _session = session;
            _orderHistoryService = orderHistoryService;
        }

        [RelayCommand]
        private async Task SubmitPayment()
        {
            // INPUT VALIDATIONS
            if (Amount <= 0)
            {
                MessageBox.Show("Please enter a valid payment amount greater than ₹ 0.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var p = new PaymentEntry
            {
                OrderId = SelectedOrder.OrderId,
                LeadId = SelectedOrder.LeadId,
                DivisionId = SelectedOrder.DivisionId,
                TotalOrderValue = TargetInvoiceValue,
                AmountReceived = Amount,
                BalanceAmount = RemainingBalance,
                PaymentMethod = Method,
                Remarks = string.IsNullOrWhiteSpace(Remarks) ? "Payment Received" : Remarks,
                PaymentDate = DateTime.Now
            };

            var historyEntry = new LeadHistoryEntry
            {
                LeadId = SelectedOrder.LeadId,
                Message = "Payment Received",
                Content = $"Received ₹ {Amount:N2} via {Method} for Order #{SelectedOrder.FormattedOrderId}. Remarks: {p.Remarks}",
                UpdatedByContent = "record a payment",
                NextFollowUpDate = DateTime.Now,
                UpdatedBy = _session.CurrentUser ?? "Admin",
                LogDate = DateTime.Now,
                IsPriority = true
            };

            await _service.RecordPaymentAsync(p, historyEntry);

            var orderHistory = new OrderHistoryEntry
            {
                OrderId = SelectedOrder.OrderId,
                LeadId = SelectedOrder.LeadId,
                ActionTitle = "Payment Received",
                Description = $"Payment of ₹ {Amount:N2} received via {Method}. Remaining Balance: ₹ {RemainingBalance:N2}. Remarks: {p.Remarks}",
                ActionType = "PaymentAdded",
                TransactionAmount = Amount,
                PerformedBy = _session.CurrentUser ?? "Admin",
                LogDate = DateTime.Now,
                IsImportant = false
            };

            await _orderHistoryService.LogActivityAsync(orderHistory);

            // Invoke close dialog event with success flag
            RequestClose?.Invoke(true);
        }
    }
}
