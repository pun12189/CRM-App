using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class PurchaseOrderDetail : ObservableObject
    {
        [ObservableProperty] private int _poDetailId;
        [ObservableProperty] private int _purchaseOrderId;
        [ObservableProperty] private int _productId;
        [ObservableProperty] private string? _batchNumber;
        [ObservableProperty] private int _quantity;
        [ObservableProperty] private int _freeQuantity;
        [ObservableProperty] private decimal _unitPrice;
        [ObservableProperty] private decimal _mRP;
        [ObservableProperty] private decimal _discountPercent;
        [ObservableProperty] private decimal _taxPercent;
        [ObservableProperty] private decimal _taxAmount;
        [ObservableProperty] private decimal _totalAmount;

        // Join / Display Properties
        [ObservableProperty] private string _productName = string.Empty;
        [ObservableProperty] private string? _supplierSku;

        // Helper for Total Inward Stock
        public int TotalPhysicalUnits => Quantity + FreeQuantity;
    }
}
