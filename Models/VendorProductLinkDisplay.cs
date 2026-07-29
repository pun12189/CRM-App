using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
namespace CallMan.Models
{
    public partial class VendorProductLinkDisplay : ObservableObject
    {
        [ObservableProperty] private int _vendorId;
        [ObservableProperty] private int _productId;
        [ObservableProperty] private string _productName = string.Empty;
        [ObservableProperty] private string _categoryName = string.Empty;
        [ObservableProperty] private string _supplierSku = string.Empty;
        [ObservableProperty] private decimal _purchasePrice;
        [ObservableProperty] private int _currentStock;
    }
}
