using CommunityToolkit.Mvvm.ComponentModel;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class ServiceOrder : ObservableObject
    {
        [ObservableProperty] private int _orderId;
        [ObservableProperty] private string _orderNumber = string.Empty;
        [ObservableProperty] private int _customerId;
        [ObservableProperty] private string _customerName = string.Empty;
        [ObservableProperty] private DateTime _orderDate = DateTime.Today;
        [ObservableProperty] private DateTime? _deliveryDueDate = DateTime.Today.AddDays(15);
        [ObservableProperty] private string _orderStatus = "Draft";
        [ObservableProperty] private string? _specialInstructions;

        // Financials
        [ObservableProperty] private decimal _subTotalAmount;
        [ObservableProperty] private decimal _taxAmount;
        [ObservableProperty] private decimal _grandTotalAmount;
        [ObservableProperty] private int _batchOrdersCount;

        [ObservableProperty]
        private ObservableCollection<ServiceOrderItem> _items = new();

        public void RecalculateSummary()
        {
            SubTotalAmount = Items.Sum(i => i.LineTotal);
            TaxAmount = Items.Sum(i => i.LineTotal * (i.GstPercent / 100m));
            GrandTotalAmount = SubTotalAmount + TaxAmount;

            OnPropertyChanged(nameof(SubTotalAmount));
            OnPropertyChanged(nameof(TaxAmount));
            OnPropertyChanged(nameof(GrandTotalAmount));
        }
    }
}
