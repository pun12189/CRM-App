using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class WorkOrderBomItem : ObservableObject
    {
        [ObservableProperty] private int _workOrderBomId;
        [ObservableProperty] private int _workOrderId;
        [ObservableProperty] private int _rawMaterialProductId;
        [ObservableProperty] private string _rawMaterialName = string.Empty;
        [ObservableProperty] private string _rawMaterialCode = string.Empty;
        [ObservableProperty] private string _phase = "Phase A";
        [ObservableProperty] private decimal _percentageValue;
        [ObservableProperty] private decimal _calculatedQuantity; // Target Required Qty (Kg/Ltr)
        [ObservableProperty] private decimal _actualDispensedQuantity; // Actual Weighed Qty
        [ObservableProperty] private string _unit = "Kg";
        [ObservableProperty] private string? _remarks;
        [ObservableProperty] private int _sequenceOrder = 1;
        [ObservableProperty] private bool _isDispensed;
    }
}
