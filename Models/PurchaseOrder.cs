using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class PurchaseOrder : ObservableObject
    {
        [ObservableProperty] private int _purchaseOrderId;
        [ObservableProperty] private string _poNumber = string.Empty; // e.g., PO-2026-0001
        [ObservableProperty] private int _vendorId;
        [ObservableProperty] private DateTime _orderDate = DateTime.Today;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private string _orderStatus = "Draft"; // Draft, Ordered, Received, Cancelled
        [ObservableProperty] private string _createdBy = "Admin";

        // Code-linked display property populated by the Service layer join query
        [ObservableProperty] private string _vendorName = string.Empty;
    }
}
