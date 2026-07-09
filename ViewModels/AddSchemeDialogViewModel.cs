using CallMan.Models;
using CallMan.Models.Enums;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class AddSchemeDialogViewModel : ObservableObject
    {
        private readonly CategoryService _categoryService;
        private readonly SchemeService _schemeService;

        [ObservableProperty] private PromotionalScheme _scheme = new();
        [ObservableProperty] private ObservableCollection<CategoryCheckboxItem> _customerCategoriesChecklist = new();
        [ObservableProperty] private int _selectedTabScopeIndex = 0;
        [ObservableProperty] private int _selectedRewardTypeIndex = 0; // 0 = % Rebate, 1 = Fixed Value, 2 = Gift Item
        [ObservableProperty] private int _selectedStaffCalculationModelIndex = 0;

        [ObservableProperty] private string _rewardValueString = "0";
        [ObservableProperty] private bool _isInstantDiscountSelected = true;
        [ObservableProperty] private bool _isWalletCashbackSelected = false;

        public bool IsNumericReward => SelectedRewardTypeIndex == 0 || SelectedRewardTypeIndex == 1;
        public bool IsGiftReward => SelectedRewardTypeIndex == 2;

        public List<int> GetSelectedCategoryIds() =>
           CustomerCategoriesChecklist.Where(x => x.IsSelected).Select(x => x.CategoryId).ToList();

        public AddSchemeDialogViewModel(CategoryService categoryService, SchemeService schemeService)
        {
            _categoryService = categoryService;
            _schemeService = schemeService;

            Scheme.StartDate = DateTime.Today;
            Scheme.EndDate = DateTime.Today.AddMonths(1);
            Scheme.IsActive = true;

            _ = InitializeCategoriesChecklistAsync();
        }

        public AddSchemeDialogViewModel(PromotionalScheme standardScheme, CategoryService categoryService, SchemeService schemeService)
        {
            _categoryService = categoryService;
            _schemeService = schemeService;

            // Clone or copy object context directly
            Scheme = standardScheme;

            // Dynamically set active tabs and types based on existing data settings
            SelectedTabScopeIndex = (Scheme.TargetScope == SchemeScope.Customer) ? 0 : 1;

            if (Scheme.RewardType == RewardType.Percentage) SelectedRewardTypeIndex = 0;
            else if (Scheme.RewardType == RewardType.FixedAmount) SelectedRewardTypeIndex = 1;
            else SelectedRewardTypeIndex = 2;

            RewardValueString = Scheme.RewardValue.ToString();

            if (Scheme.TargetScope == SchemeScope.Customer)
            {
                IsInstantDiscountSelected = Scheme.RedemptionMode == RedemptionMode.InstantDiscount;
                IsWalletCashbackSelected = Scheme.RedemptionMode == RedemptionMode.WalletCashback;
            }

            _ = InitializeCategoriesChecklistAsync(Scheme.SchemeId);
        }

        private async Task InitializeCategoriesChecklistAsync(int? existingSchemeId = null)
        {
            // 1. Fetch available customer and lead category profiles from the database
            var customerTiers = await _categoryService.GetCategoriesByContextAsync(CategoryContext.Customers);
            var leadTiers = await _categoryService.GetCategoriesByContextAsync(CategoryContext.Leads);
            var allTiers = customerTiers.Concat(leadTiers);

            // 2. Fetch already linked categories if we are editing an existing scheme
            List<int> linkedCategoryIds = new();
            if (existingSchemeId.HasValue)
            {
                linkedCategoryIds = await _schemeService.GetCategoryIdsLinkedToSchemeAsync(existingSchemeId.Value);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                CustomerCategoriesChecklist.Clear();
                foreach (var tier in allTiers)
                {
                    CustomerCategoriesChecklist.Add(new CategoryCheckboxItem
                    {
                        CategoryId = tier.CategoryId,
                        CategoryName = tier.CategoryName,
                        IsSelected = linkedCategoryIds.Contains(tier.CategoryId)
                    });
                }
            });
        }       

        partial void OnSelectedRewardTypeIndexChanged(int value)
        {
            if (value == 0) Scheme.RewardType = RewardType.Percentage;
            else if (value == 1) Scheme.RewardType = RewardType.FixedAmount;
            else Scheme.RewardType = RewardType.GiftItem;

            // Reset string representation on type modification toggle
            RewardValueString = "0";

            OnPropertyChanged(nameof(IsNumericReward));
            OnPropertyChanged(nameof(IsGiftReward));
        }

        // Handles strict numeric constraint validations dynamically on user entry loops
        partial void OnRewardValueStringChanged(string value)
        {
            if (decimal.TryParse(value, out decimal parsedValue))
            {
                // Strict rule constraint block: If Percentage Mode is active, clamp it to 100
                if (SelectedRewardTypeIndex == 0 && parsedValue > 100)
                {
                    _rewardValueString = "100";
                    OnPropertyChanged(nameof(RewardValueString));
                    Scheme.RewardValue = 100;
                    return;
                }
                Scheme.RewardValue = parsedValue;
            }
        }

        partial void OnSelectedTabScopeIndexChanged(int value)
        {
            Scheme.TargetScope = (value == 0) ? SchemeScope.Customer : SchemeScope.Staff;
        }

        [RelayCommand]
        private void SaveScheme(Window window)
        {
            if (string.IsNullOrWhiteSpace(Scheme.Title))
            {
                MessageBox.Show("Please assign a valid title name for this campaign strategy.", "Validation Breakout", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Map structural configurations down to target model objects before final save mapping
            Scheme.TargetScope = (SelectedTabScopeIndex == 0) ? SchemeScope.Customer : SchemeScope.Staff;

            if (Scheme.TargetScope == SchemeScope.Customer)
            {
                Scheme.RedemptionMode = IsInstantDiscountSelected ? RedemptionMode.InstantDiscount : RedemptionMode.WalletCashback;
            }
            else
            {
                Scheme.RedemptionMode = RedemptionMode.StaffClaim;
            }

            if (window != null) window.DialogResult = true;
        }
    }
}
