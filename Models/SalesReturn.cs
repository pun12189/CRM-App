using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class SalesReturn : ObservableObject
    {
        [ObservableProperty] private int _salesReturnId;
        [ObservableProperty] private string _creditNoteNo = string.Empty;
        [ObservableProperty] private int? _customerId;
        [ObservableProperty] private int? _orderId;
        [ObservableProperty] private DateTime _returnDate = DateTime.Today;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private decimal _taxAmount;
        [ObservableProperty] private string? _reason;
        [ObservableProperty] private string _status = "Completed";
        [ObservableProperty] private string _createdBy = "Admin";
        [ObservableProperty] private DateTime _createdAt = DateTime.Now;

        [ObservableProperty] private string _customerName = string.Empty;
        [ObservableProperty] private string _orderNumber = string.Empty;
    }
}
