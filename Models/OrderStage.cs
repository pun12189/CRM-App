using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class OrderStage : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty] private string _stageName;
        [ObservableProperty] private string _description;
        [ObservableProperty] private int _sequenceOrder;
        [ObservableProperty] private string _hexColor = "#757575";
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private bool _deductStock = false;
        [ObservableProperty] private bool _isCancellationStage = false;
    }
}
