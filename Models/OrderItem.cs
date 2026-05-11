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
        public int ProductId { get; set; }
        [ObservableProperty] private string _productName;
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private decimal _unitPrice;
        [ObservableProperty] private decimal _gstPercent;

        // Calculated Property
        public decimal SubTotal => Quantity * UnitPrice;
        public decimal GstAmount => SubTotal * (GstPercent / 100);
        public decimal Total => SubTotal + GstAmount;
    }
}
