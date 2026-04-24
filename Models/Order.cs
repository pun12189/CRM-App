using CommunityToolkit.Mvvm.ComponentModel;
using Mysqlx.Crud;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class Order : ObservableObject
    {
        [ObservableProperty] private int _orderId;
        [ObservableProperty] private int _leadId;
        [ObservableProperty] private DateTime _orderDate = DateTime.Now;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private string _status = "Pending"; // Pending, Partially Paid, Fully Paid
        [ObservableProperty] private string? _description;
        [ObservableProperty] private string? _customerName;

        // UI Helper for a summary string
        public string OrderSummary => $"Order #{OrderId} - {OrderDate:dd MMM yyyy}";
    }
}
