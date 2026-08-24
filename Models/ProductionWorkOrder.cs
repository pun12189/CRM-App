using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class ProductionWorkOrder : ObservableObject
    {
        [ObservableProperty] private int _workOrderId;
        [ObservableProperty] private string _batchNumber = string.Empty;
        [ObservableProperty] private int _orderId;
        [ObservableProperty] private int _orderItemId;
        [ObservableProperty] private int _customerId;
        [ObservableProperty] private string _customerName = string.Empty;
        [ObservableProperty] private string _brandName = string.Empty;
        [ObservableProperty] private int? _productId;
        [ObservableProperty] private string _productName = string.Empty;
        [ObservableProperty] private decimal _batchSize;
        [ObservableProperty] private string _unit = "Pcs";
        [ObservableProperty] private string _currentStage = "Dispensing";
        [ObservableProperty] private DateTime _mfgDate = DateTime.Today;
        [ObservableProperty] private DateTime _expiryDate = DateTime.Today.AddYears(2);
        [ObservableProperty] private string? _productionNotes;
        [ObservableProperty] private DateTime _createdAt = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<WorkOrderStage> _stages = new();
    }
}
