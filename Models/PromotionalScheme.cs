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
    public partial class PromotionalScheme : ObservableObject
    {
        [ObservableProperty] private int _schemeId;
        [ObservableProperty] private string _title = string.Empty;
        [ObservableProperty] private SchemeScope _targetScope = SchemeScope.Customer;

        // Date Boundaries (Defaults to a 1-month range from today)
        [ObservableProperty] private DateTime _startDate = DateTime.Today;
        [ObservableProperty] private DateTime _endDate = DateTime.Today.AddMonths(1);
        [ObservableProperty] private bool _isActive = true;

        // Target Threshold Controls
        [ObservableProperty] private decimal _minimumOrderThreshold;

        // Reward Definition Structs
        [ObservableProperty] private RewardType _rewardType = RewardType.Percentage;
        [ObservableProperty] private decimal _rewardValue;
        [ObservableProperty] private string _giftItemName = string.Empty;
        [ObservableProperty] private RedemptionMode _redemptionMode = RedemptionMode.InstantDiscount;

        public bool IsExpired => DateTime.Today > EndDate;

        // Relation Links Tracking Collections
        public ObservableCollection<int> TargetCategoryIds { get; set; } = new();
        public ObservableCollection<int> TargetStaffUserIds { get; set; } = new();
    }
}
