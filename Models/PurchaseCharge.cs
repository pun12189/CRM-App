using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class PurchaseCharge : ObservableObject
    {
        [ObservableProperty] private int _chargeId;
        [ObservableProperty] private int _purchaseOrderId;
        [ObservableProperty] private int _vendorId;
        [ObservableProperty] private string _vendorInvoiceNo = string.Empty;
        [ObservableProperty] private DateTime? _invoiceDate;
        [ObservableProperty] private string _chargeName = string.Empty;
        [ObservableProperty] private string? _hsnCode;
        [ObservableProperty] private string? _companyCode;
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private decimal _sgstPercent;
        [ObservableProperty] private decimal _cgstPercent;
        [ObservableProperty] private decimal _igstPercent;
        [ObservableProperty] private decimal _taxAmount;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private DateTime _createdAt = DateTime.Now;

        // Display Helper
        public decimal GstPercentTotal => SgstPercent + CgstPercent + IgstPercent;
    }
}
