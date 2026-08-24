using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;

namespace Tijori.ViewModels
{
    public partial class ManageCategoriesViewModel : ObservableObject
    {
        private readonly CategoryService _service;

        // Collections
        [ObservableProperty] private ObservableCollection<Category> _allCategories = new();
        [ObservableProperty] private ObservableCollection<Category> _potentialParentCategories = new();
        [ObservableProperty] private ObservableCollection<ItemClassification> _availableClassifications = new();

        // Form Bindings
        [ObservableProperty] private string _newCategoryName = string.Empty;
        [ObservableProperty] private Category? _selectedParent;
        [ObservableProperty] private ItemClassification _selectedCategoryType = ItemClassification.FinishedGood;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubmitButtonContent))]
        [NotifyPropertyChangedFor(nameof(IsEditMode))]
        [NotifyPropertyChangedFor(nameof(FormHeaderTitle))]
        private Category? _editingCategory;

        public string SubmitButtonContent => EditingCategory == null ? "ADD NEW" : "UPDATE";
        public bool IsEditMode => EditingCategory != null;
        public string FormHeaderTitle => EditingCategory == null ? "Add New Category" : $"Edit Category: {EditingCategory.CategoryName}";

        public ManageCategoriesViewModel(CategoryService service)
        {
            _service = service;

            // Populate Enum options for dropdown
            AvailableClassifications = new ObservableCollection<ItemClassification>(
                Enum.GetValues<ItemClassification>()
            );

            _ = LoadCategories();
        }

        // Auto-inherit CategoryType when selecting a Parent category
        partial void OnSelectedParentChanged(Category? value)
        {
            // Only auto-inherit from parent if we are NOT in Edit Mode
            if (!IsEditMode && value != null)
            {
                SelectedCategoryType = value.CategoryType;
            }
        }

        public async Task LoadCategories()
        {
            var rawList = (await _service.GetAllCategoriesAsync()).ToList();

            // 1. Group children by ParentId for fast lookup
            var lookup = rawList
                .Where(c => c.ParentId.HasValue && c.ParentId.Value > 0)
                .GroupBy(c => c.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.CategoryName).ToList());

            // 2. Identify top-level root nodes
            var rootNodes = rawList
                .Where(c => c.ParentId == null || c.ParentId == 0 || !rawList.Any(p => p.Id == c.ParentId))
                .OrderBy(c => c.CategoryName)
                .ToList();

            // 3. Recursive traversal to flatten the tree in exact parent-child order
            var structuredList = new List<Category>();

            void AppendChildren(Category parent, int currentLevel)
            {
                parent.HierarchyLevel = currentLevel;
                structuredList.Add(parent);

                if (lookup.TryGetValue(parent.Id, out var children))
                {
                    foreach (var child in children)
                    {
                        AppendChildren(child, currentLevel + 1);
                    }
                }
            }

            foreach (var root in rootNodes)
            {
                AppendChildren(root, 0);
            }

            App.Current.Dispatcher.Invoke(() =>
            {
                AllCategories = new ObservableCollection<Category>(structuredList);
                RefreshParentDropdown(rawList);
            });
        }

        private void RefreshParentDropdown(IEnumerable<Category> list)
        {
            // Exclude the currently edited category and prevent self-referencing
            var available = list
                .Where(c => !IsEditMode || c.Id != EditingCategory?.Id)
                .OrderBy(c => c.CategoryName);

            PotentialParentCategories = new ObservableCollection<Category>(available);
        }

        [RelayCommand]
        private async Task SaveCategory()
        {
            if (string.IsNullOrWhiteSpace(NewCategoryName))
            {
                MessageBox.Show("Please enter a category name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var category = new Category
            {
                Id = EditingCategory?.Id ?? 0,
                CategoryName = NewCategoryName.Trim(),
                ParentId = SelectedParent?.Id,
                CategoryType = SelectedCategoryType,
                HierarchyLevel = SelectedParent != null ? SelectedParent.HierarchyLevel + 1 : 0
            };

            await _service.UpsertCategoryAsync(category);

            ClearForm();
            await LoadCategories();
        }

        [RelayCommand]
        private void Edit(Category category)
        {
            if (category == null) return;

            EditingCategory = category;
            NewCategoryName = category.CategoryName;

            RefreshParentDropdown(AllCategories);
            SelectedParent = PotentialParentCategories.FirstOrDefault(x => x.Id == category.ParentId);

            SelectedCategoryType = category.CategoryType;
        }

        [RelayCommand]
        private void ClearForm()
        {
            EditingCategory = null;
            NewCategoryName = string.Empty;
            SelectedParent = null;
            SelectedCategoryType = ItemClassification.FinishedGood;
            RefreshParentDropdown(AllCategories);
        }

        [RelayCommand]
        private async Task Delete(Category category)
        {
            if (category == null) return;

            // Check if this category has subcategories
            bool hasChildren = AllCategories.Any(c => c.ParentId == category.Id);
            if (hasChildren)
            {
                MessageBox.Show($"Cannot delete '{category.CategoryName}' because it has subcategories. Delete or move the subcategories first.",
                                "Operation Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show($"Are you sure you want to delete '{category.CategoryName}'?", $"Delete {category.CategoryName}", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                await _service.DeleteCategoryAsync(category.Id);
                await LoadCategories();
            }
        }
    }
}
