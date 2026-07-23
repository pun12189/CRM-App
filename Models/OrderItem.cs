using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CallMan.Models
{
    public partial class OrderItem : ObservableObject
    {
        // Links
        public int ProductId { get; set; }
        [ObservableProperty] private int _batchId; // NEW: Crucial for accurate batch stock deductions

        // Form Display Context Fields
        [ObservableProperty] private string _productName;
        [ObservableProperty] private string _batchNumber;
        [ObservableProperty] private DateTime? _expiryDate;// NEW: Displayed inline (e.g. "LOT-2026-A")

        // Quantities & Calculations
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private decimal _unitPrice;
        [ObservableProperty] private decimal _gstPercent;
        [ObservableProperty] private decimal _costPrice;

        // Alerts WPF UI to recalculate calculated totals whenever variables shift
        partial void OnQuantityChanged(int value) => NotifyCalculations();
        partial void OnUnitPriceChanged(decimal value) => NotifyCalculations();
        partial void OnGstPercentChanged(decimal value) => NotifyCalculations();
        partial void OnCostPriceChanged(decimal value) => NotifyCalculations();

        private void NotifyCalculations()
        {
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(GstAmount));
            OnPropertyChanged(nameof(Total));
        }

        // Clean Mathematical Properties
        public decimal SubTotal => Quantity * UnitPrice;
        public decimal GstAmount => SubTotal * (GstPercent / 100);
        public decimal Total => SubTotal + GstAmount;
    }
}
