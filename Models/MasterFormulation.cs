using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class MasterFormulation : ObservableObject
    {
        [ObservableProperty] private int _formulationId;
        [ObservableProperty] private int? _finishedProductId;
        [ObservableProperty] private string? _finishedProductName;
        [ObservableProperty] private string _formulationName = string.Empty;
        [ObservableProperty] private decimal _standardBatchSize = 100m;
        [ObservableProperty] private string _standardBatchUnit = "Ltr";
        [ObservableProperty] private string? _instructions;
        [ObservableProperty] private bool _isActive = true;

        [ObservableProperty]
        private ObservableCollection<MasterFormulationItem> _items = new();

        // 🌟 Computed: Total active ingredients %
        public decimal TotalIngredientsPercentage => Items.Sum(i => i.PercentageValue);

        // 🌟 Computed: Remaining % automatically allocated to Water (Aqua / QS)
        public decimal WaterPercentage => 100m - TotalIngredientsPercentage;

        public bool IsValidFormula => TotalIngredientsPercentage <= 100m && TotalIngredientsPercentage >= 0m;

        public void NotifyTotalsChanged()
        {
            OnPropertyChanged(nameof(TotalIngredientsPercentage));
            OnPropertyChanged(nameof(WaterPercentage));
            OnPropertyChanged(nameof(IsValidFormula));
        }
    }
}
