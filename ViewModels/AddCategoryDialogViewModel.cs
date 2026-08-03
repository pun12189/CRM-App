using Tijori.Models;
using Tijori.Models.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.ViewModels
{
    public partial class AddCategoryDialogViewModel : ObservableObject
    {
        [ObservableProperty] private string _categoryName = string.Empty;
        [ObservableProperty] private bool _applyToAllContexts;
        [ObservableProperty] private decimal _mspDiscountPercentage;
        [ObservableProperty] private decimal _creditLimitAmount;
        [ObservableProperty] private int _creditGraceDays;
        [ObservableProperty] private int _selectedSettlementModelIndex;
        [ObservableProperty] private CategoryContext _activeContextType;

        [ObservableProperty] private ObservableCollection<ModuleCheckboxItem> _moduleCheckboxes = new();

        public bool IsDocumentsContext => ActiveContextType == CategoryContext.Documents;

        public bool IsLeadsOrCustomersContext => ActiveContextType == CategoryContext.Leads || ActiveContextType == CategoryContext.Customers;
        public bool CanShowApplyToAll => ActiveContextType == CategoryContext.Leads || ActiveContextType == CategoryContext.Customers;

        public AddCategoryDialogViewModel(CategoryContext activeContext)
        {
            ActiveContextType = activeContext;

            if (activeContext == CategoryContext.Documents)
            {
                InitializeModuleCheckboxes();
            }
        }

        public AddCategoryDialogViewModel(BusinessCategory existingCategory, List<string>? linkedModules = null)
        {
            ActiveContextType = existingCategory.TargetContext;
            CategoryName = existingCategory.CategoryName;
            MspDiscountPercentage = existingCategory.MspDiscountPercentage;
            CreditLimitAmount = existingCategory.CreditLimitAmount;
            CreditGraceDays = existingCategory.CreditGraceDays;
            SelectedSettlementModelIndex = existingCategory.SettlementModel;

            // Pass the saved database links list to check the boxes on load
            if (ActiveContextType == CategoryContext.Documents && linkedModules != null)
            {
                InitializeModuleCheckboxes(linkedModules);
            }
        }

        private void InitializeModuleCheckboxes(List<string> previouslySavedModules = null)
        {
            var availableModules = new List<string> { "Lead", "Customer", "Vendors", "Purchase", "Orders", "Staff" };

            ModuleCheckboxes.Clear();
            foreach (var module in availableModules)
            {
                ModuleCheckboxes.Add(new ModuleCheckboxItem
                {
                    ModuleName = module,
                    IsSelected = previouslySavedModules != null && previouslySavedModules.Contains(module)
                });
            }
        }

        [RelayCommand]
        private void Save(Window window)
        {
            if (window != null) window.DialogResult = true;
        }

        public BusinessCategory GetConfiguredCategory(CategoryContext context)
        {
            var category = new BusinessCategory
            {
                CategoryName = this.CategoryName,
                TargetContext = context,
                MspDiscountPercentage = this.MspDiscountPercentage,
                CreditLimitAmount = this.CreditLimitAmount,
                CreditGraceDays = this.CreditGraceDays,
                SettlementModel = this.SelectedSettlementModelIndex
            };
            
            return category;
        }

        public List<string> GetSelectedModulesList()
        {
            return ModuleCheckboxes.Where(x => x.IsSelected).Select(x => x.ModuleName).ToList();
        }
    }
}
