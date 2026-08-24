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
    public partial class ServiceOrderItem : ObservableObject
    {
        [ObservableProperty] private int _orderItemId;
        [ObservableProperty] private int _orderId;

        // Brand relation
        [ObservableProperty] private int? _brandId;
        [ObservableProperty] private string _brandName = string.Empty;

        // Base Product & Formulation
        [ObservableProperty] private int? _productId;
        [ObservableProperty] private string _productName = string.Empty;
        [ObservableProperty] private int? _masterFormulationId;
        [ObservableProperty] private string? _formulationName;

        // Packaging & Physical Attributes
        [ObservableProperty] private string _packagingType = "Bottle";
        [ObservableProperty] private string _packSize = "100 ml";
        [ObservableProperty] private string? _colorShade;
        [ObservableProperty] private string? _fragranceFlavor;
        [ObservableProperty] private string? _containerMaterial;
        [ObservableProperty] private string? _capOrClosureType;

        // Commercials
        [ObservableProperty] private decimal _targetQuantity = 1000m;
        [ObservableProperty] private string _unit = "Pcs";
        [ObservableProperty] private decimal _unitPrice = 0m;
        [ObservableProperty] private decimal _gstPercent = 18m;
        [ObservableProperty] private decimal _lineTotal = 0m;
        [ObservableProperty] private string _productionStatus = "Pending";

        // Guaranteed Embedded BOM Items
        [ObservableProperty]
        private ObservableCollection<ServiceOrderItemBom> _bomItems = new();

        public decimal TotalActiveIngredientsPercentage => BomItems.Sum(x => x.PercentageValue);
        public decimal WaterPercentage => 100m - TotalActiveIngredientsPercentage;
        public decimal TotalBatchWeightKg => BomItems.Sum(x => x.CalculatedQuantity);
        public bool HasValidBOM => BomItems.Count > 0 && TotalActiveIngredientsPercentage <= 100m;

        public void RecalculateLineTotal()
        {
            LineTotal = TargetQuantity * UnitPrice;
            OnPropertyChanged(nameof(LineTotal));
        }

        public void RecalculateBOMQuantities(decimal totalBatchVolumeKg)
        {
            foreach (var item in BomItems)
            {
                item.CalculatedQuantity = Math.Round((item.PercentageValue / 100m) * totalBatchVolumeKg, 3);
            }
            OnPropertyChanged(nameof(TotalActiveIngredientsPercentage));
            OnPropertyChanged(nameof(WaterPercentage));
            OnPropertyChanged(nameof(TotalBatchWeightKg));
            OnPropertyChanged(nameof(HasValidBOM));
        }
    }
}
