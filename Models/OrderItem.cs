using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public decimal SubTotal => (UnitPrice * Quantity) + (UnitPrice * Quantity * (GstPercent / 100));
    }
}
