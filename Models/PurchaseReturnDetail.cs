using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class PurchaseReturnDetail : ObservableObject
    {
        [ObservableProperty] private int _prDetailId;
        [ObservableProperty] private int _purchaseReturnId;
        [ObservableProperty] private int _productId;
        [ObservableProperty] private string? _batchNumber;
        [ObservableProperty] private int _quantity;
        [ObservableProperty] private decimal _unitPrice;
        [ObservableProperty] private decimal _taxPercent;
        [ObservableProperty] private decimal _taxAmount;
        [ObservableProperty] private decimal _totalAmount;

        [ObservableProperty] private string _productName = string.Empty;
        [ObservableProperty] private int _maxAvailableQty;
    }
}
