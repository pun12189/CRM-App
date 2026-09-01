using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class PurchaseReturn : ObservableObject
    {
        [ObservableProperty] private int _purchaseReturnId;
        [ObservableProperty] private string _returnDebitNo = string.Empty;
        [ObservableProperty] private int _vendorId;
        [ObservableProperty] private int? _purchaseOrderId;
        [ObservableProperty] private DateTime _returnDate = DateTime.Today;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private decimal _taxAmount;
        [ObservableProperty] private string? _reason;
        [ObservableProperty] private string _status = "Completed";
        [ObservableProperty] private string _createdBy = "Admin";
        [ObservableProperty] private DateTime _createdAt = DateTime.Now;

        [ObservableProperty] private string _vendorName = string.Empty;
        [ObservableProperty] private string _poNumber = string.Empty;
    }
}
