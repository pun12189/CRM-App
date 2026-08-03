using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class PaymentEntry : ObservableObject
    {
        public Action? OnSelectionChanged { get; set; }

        [ObservableProperty] private bool _isSelected;

        partial void OnIsSelectedChanged(bool value)
        {
            OnSelectionChanged?.Invoke();
        }

        [ObservableProperty]
        private int _paymentId;

        [ObservableProperty] private int _divisionId;

        [ObservableProperty]
        private int _leadId;

        [ObservableProperty]
        private int _orderId;

        [ObservableProperty]
        private decimal _totalOrderValue;

        [ObservableProperty]
        private decimal _amountReceived;

        [ObservableProperty]
        public decimal _balanceAmount;

        [ObservableProperty]
        private DateTime _paymentDate = DateTime.Now;

        [ObservableProperty]
        private string? _paymentMethod; // Cash, Check, GPay, etc.

        [ObservableProperty]
        private string? _remarks;

        [ObservableProperty]
        private int _userId;

        // Helper for UI Display
        public string DisplayDate => PaymentDate.ToString("dd MMM yyyy");
        public string FormattedOrderId => $"ORD{OrderId:D8}";

        public string CustomerName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string OrderType { get; set; } = "New"; // "New" or "Repeat"
        public string TransactionType { get; set; } = "Credit";
        public string RecordedBy { get; set; } = string.Empty;
    }
}
