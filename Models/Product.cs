using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class Product : ObservableObject
    {
        public int ProductId { get; set; }
        public int DivisionId { get; set; }
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _shortName = string.Empty;
        [ObservableProperty] private string _sKU = string.Empty;
        [ObservableProperty] private string _unit = "Pcs";
        [ObservableProperty] private int _categoryId;
        [ObservableProperty] private string _categoryName = string.Empty; // For Display

        [ObservableProperty] private string _manufacturer = string.Empty;
        [ObservableProperty] private string _packaging = string.Empty;

        // Stock
        [ObservableProperty] private int _remainingStock;
        [ObservableProperty] private int _initialStock;

        // Costing
        [ObservableProperty] private decimal _mRP;
        [ObservableProperty] private decimal _costPrice;
        [ObservableProperty] private decimal _sellingPrice;
        [ObservableProperty] private decimal _gstPercent;

        [ObservableProperty] private bool _trackCost = true;

        // Calculated Property
        public decimal TotalCost => SellingPrice + (SellingPrice * (GstPercent / 100));

        // Refresh UI when components change
        partial void OnSellingPriceChanged(decimal value) => OnPropertyChanged(nameof(TotalCost));
        partial void OnGstPercentChanged(decimal value) => OnPropertyChanged(nameof(TotalCost));
    }
}
