using Tijori.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class BusinessCategory : ObservableObject
    {
        [ObservableProperty] private int _categoryId;
        [ObservableProperty] private string _categoryName = string.Empty;
        [ObservableProperty] private CategoryContext _targetContext;

        // Commercial & Risk Parameters (Exclusive to Leads/Customers)
        [ObservableProperty] private decimal _mspDiscountPercentage;
        [ObservableProperty] private decimal _creditLimitAmount;
        [ObservableProperty] private int _creditGraceDays;
        [ObservableProperty] private int _settlementModel; // 0=Bill-to-Bill, 1=Accumulated, 2=Dual

        [ObservableProperty] private bool _isSystemDefined;
    }
}
