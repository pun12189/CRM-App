using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class ServiceOrderItemBom : ObservableObject
    {
        [ObservableProperty] private int _bomItemId;
        [ObservableProperty] private int _orderItemId;
        [ObservableProperty] private int _rawMaterialProductId;
        [ObservableProperty] private string _rawMaterialName = string.Empty;
        [ObservableProperty] private string _rawMaterialCode = string.Empty;
        [ObservableProperty] private string _phase = "Phase A";
        [ObservableProperty] private decimal _percentageValue;
        [ObservableProperty] private decimal _calculatedQuantity;
        [ObservableProperty] private string _unit = "Kg";
        [ObservableProperty] private string? _remarks;
        [ObservableProperty] private int _sequenceOrder = 1;
    }
}
