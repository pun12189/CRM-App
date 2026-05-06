using CommunityToolkit.Mvvm.ComponentModel;
using Mysqlx.Crud;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;

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
        [ObservableProperty] private string? _processedBy;
        [ObservableProperty] private string? _proformaNumber;
        [ObservableProperty] private string? _invoiceNumber;
        [ObservableProperty] private ObservableCollection<OrderItem> _items = new();

        public decimal GrandTotal => Items.Sum(x => x.SubTotal);

        // UI Helper for a summary string
        public string OrderSummary => $"Order #{OrderId} - {OrderDate:dd MMM yyyy}";
    }
}
