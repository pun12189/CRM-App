using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
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

        [ObservableProperty] private bool _isDialogOpen;

        [ObservableProperty] private ObservableCollection<Category> _allCategories = new();
        [ObservableProperty] private ObservableCollection<ItemClassification> _availableClassifications = new();

        // Dialog state bindings
        [ObservableProperty] private string _dialogTitle = "Add Root Category";
        [ObservableProperty] private string _dialogIcon = "FolderPlus";
        [ObservableProperty] private string _submitButtonText = "CREATE";
        [ObservableProperty] private string _formCategoryName = string.Empty;
        [ObservableProperty] private ItemClassification _formCategoryType = ItemClassification.FinishedGood;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasParent))]
        [NotifyPropertyChangedFor(nameof(ParentDisplayName))]
        private Category? _selectedParent;

        [ObservableProperty] private Category? _editingCategory;

        public bool HasParent => SelectedParent != null;
        public string ParentDisplayName => SelectedParent?.CategoryName ?? string.Empty;

        public ManageCategoriesViewModel(CategoryService service)
        {
            _service = service;

            // Populate Enum options for dropdown
            AvailableClassifications = new ObservableCollection<ItemClassification>(Enum.GetValues<ItemClassification>());
            _ = LoadCategories();
        }

        public async Task LoadCategories()
        {
            var rawList = (await _service.GetAllCategoriesAsync()).ToList();

            var lookup = rawList
                .Where(c => c.ParentId.HasValue && c.ParentId.Value > 0)
                .GroupBy(c => c.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.CategoryName).ToList());

            var rootNodes = rawList
                .Where(c => c.ParentId == null || c.ParentId == 0 || !rawList.Any(p => p.Id == c.ParentId))
                .OrderBy(c => c.CategoryName)
                .ToList();

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
            });
        }

        [RelayCommand]
        private async Task OpenAddRootDialog()
        {
            EditingCategory = null;
            SelectedParent = null;
            FormCategoryName = string.Empty;
            FormCategoryType = ItemClassification.FinishedGood;

            DialogTitle = "Add Root Category";
            DialogIcon = "FolderPlus";
            SubmitButtonText = "CREATE ROOT";

            IsDialogOpen = true;
        }

        [RelayCommand]
        private async Task OpenAddSubCategoryDialog(Category parentCategory)
        {
            if (parentCategory == null) return;

            EditingCategory = null;
            SelectedParent = parentCategory;
            FormCategoryName = string.Empty;
            FormCategoryType = parentCategory.CategoryType; // Auto-inherit classification

            DialogTitle = $"Add Sub-Category under '{parentCategory.CategoryName}'";
            DialogIcon = "SubdirectoryArrowRight";
            SubmitButtonText = "ADD SUB-CATEGORY";

            IsDialogOpen = true;
        }

        [RelayCommand]
        private async Task OpenEditDialog(Category category)
        {
            if (category == null) return;

            EditingCategory = category;

            if (category.ParentId.HasValue && category.ParentId.Value > 0)
            {
                SelectedParent = AllCategories.FirstOrDefault(c => c.Id == category.ParentId.Value);
            }
            else
            {
                SelectedParent = null;
            }

            FormCategoryName = category.CategoryName;
            FormCategoryType = category.CategoryType;

            DialogTitle = $"Edit '{category.CategoryName}'";
            DialogIcon = "Pencil";
            SubmitButtonText = "UPDATE";

            IsDialogOpen = true;
        }

        [RelayCommand]
        private void CloseDialog()
        {
            IsDialogOpen = false;
        }

        [RelayCommand]
        private async Task SaveCategory()
        {
            if (string.IsNullOrWhiteSpace(FormCategoryName))
            {
                MessageBox.Show("Please enter a category name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var category = new Category
            {
                Id = EditingCategory?.Id ?? 0,
                CategoryName = FormCategoryName.Trim(),
                ParentId = SelectedParent?.Id,
                CategoryType = FormCategoryType,
                HierarchyLevel = SelectedParent != null ? SelectedParent.HierarchyLevel + 1 : 0
            };

            await _service.UpsertCategoryAsync(category);

            // Close the dialog and refresh data
            IsDialogOpen = false;
            await LoadCategories();
        }

        [RelayCommand]
        private async Task Delete(Category category)
        {
            if (category == null) return;

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
