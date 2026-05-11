using CommunityToolkit.Mvvm.ComponentModel;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Models
{
    public partial class ExtraCharge : ObservableObject
    {
        [ObservableProperty] private string _name;
        [ObservableProperty] private string _action; // "Add (+)" or "Subtract (-)"
        [ObservableProperty] private decimal _value;
        [ObservableProperty] private decimal _gstPercent;
        public ObservableCollection<string> ActionOptions { get; } = new() { "Add (+)", "Subtract (-)" };
        public ObservableCollection<decimal> GstOptions { get; } = new() { 0, 5, 18, 28 };

        public decimal TotalCharge
        {
            get
            {
                decimal baseWithGst = Value * (1 + (GstPercent / 100));
                return Action == "Add (+)" ? baseWithGst : -baseWithGst;
            }
        }
    }
}
