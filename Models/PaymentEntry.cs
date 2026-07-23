using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class PaymentEntry : ObservableObject
    {
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

        // Helper for UI Display
        public string DisplayDate => PaymentDate.ToString("dd MMM yyyy");
    }
}
