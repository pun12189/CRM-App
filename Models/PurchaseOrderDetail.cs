using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class PurchaseOrderDetail : ObservableObject
    {
        [ObservableProperty] private int _poDetailId;
        [ObservableProperty] private int _purchaseOrderId;
        [ObservableProperty] private int _productId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalCost))]
        private int _quantity;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalCost))]
        private decimal _unitPrice;

        [ObservableProperty] private string? _supplierSku;

        // Dynamic calculated property for binding inside items grids
        public decimal TotalCost => Quantity * UnitPrice;

        // Code-linked display property populated by the Service layer join query
        [ObservableProperty] private string _productName = string.Empty;
    }
}
