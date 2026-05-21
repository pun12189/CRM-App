using CommunityToolkit.Mvvm.ComponentModel;
using Mysqlx.Crud;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;

namespace CallMan.Models
{
    public partial class Order : ObservableObject
    {
        [ObservableProperty] private int _orderId;
        [ObservableProperty] private int _divisionId;
        [ObservableProperty] private int _leadId;
        [ObservableProperty] private DateTime _orderDate = DateTime.Now;
        [ObservableProperty] private decimal _totalAmount; // This holds the final snapshot figure
        [ObservableProperty] private string _status = "Pending"; // Pending, Partially Paid, Fully Paid
        [ObservableProperty] private string? _description;
        [ObservableProperty] private string? _customerName;
        [ObservableProperty] private string? _processedBy;
        [ObservableProperty] private string? _proformaNumber;
        [ObservableProperty] private string? _invoiceNumber;
        [ObservableProperty] private decimal _totalCostAmount;

        // Sub-Collections
        [ObservableProperty] private ObservableCollection<OrderItem> _items = new();
        [ObservableProperty] private ObservableCollection<ExtraCharge> _extraCharges = new(); // NEW: Hooked into total calculations

        public Order()
        {
            // Listen to collections adding/removing items to keep grand totals dynamic live on screen
            Items.CollectionChanged += (s, e) => { OnPropertyChanged(nameof(GrandTotal)); HookItemChanges(e); };
            ExtraCharges.CollectionChanged += (s, e) => { OnPropertyChanged(nameof(GrandTotal)); HookChargeChanges(e); };
        }

        // Live calculation combining product costs and auxiliary charges/discounts
        public decimal GrandTotal
        {
            get
            {
                decimal itemsSum = Items.Sum(x => x.Total);
                decimal chargesSum = ExtraCharges.Sum(x => x.TotalCharge);
                return itemsSum + chargesSum;
            }
        }

        public string OrderSummary => $"Order #{OrderId} - {OrderDate:dd MMM yyyy}";

        private void HookItemChanges(NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (OrderItem item in e.NewItems)
                    item.PropertyChanged += (s, ev) => OnPropertyChanged(nameof(GrandTotal));
        }

        private void HookChargeChanges(NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ExtraCharge charge in e.NewItems)
                    charge.PropertyChanged += (s, ev) => OnPropertyChanged(nameof(GrandTotal));
        }
    }
}
