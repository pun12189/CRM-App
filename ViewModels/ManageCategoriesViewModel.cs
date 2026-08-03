using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;

namespace Tijori.ViewModels
{
    public partial class ManageCategoriesViewModel : ObservableObject
    {
        private readonly CategoryService _service;

        [ObservableProperty] private ObservableCollection<Category> _allCategories = new();
        [ObservableProperty] private string _newCategoryName;
        [ObservableProperty] private Category _selectedParent;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubmitButtonContent))]
        [NotifyPropertyChangedFor(nameof(IsEditMode))]
        private Category? _editingCategory;

        // Logic to change button text based on state
        public string SubmitButtonContent => EditingCategory == null ? "ADD NEW" : "UPDATE";

        // Logic to show/hide the Clear button
        public bool IsEditMode => EditingCategory != null;

        public ManageCategoriesViewModel(CategoryService service)
        {
            _service = service;
            _ = LoadCategories();
        }

        [RelayCommand]
        private void ClearForm()
        {
            EditingCategory = null;
            NewCategoryName = string.Empty;
            SelectedParent = null;
        }

        private async Task LoadCategories()
        {
            var list = await _service.GetAllCategoriesAsync();
            // The service should return CategoryName and ParentName using a SQL Join
            AllCategories = new ObservableCollection<Category>(list);
        }

        [RelayCommand]
        private async Task SaveCategory()
        {
            if (string.IsNullOrWhiteSpace(NewCategoryName)) return;

            var category = new Category
            {
                Id = EditingCategory?.Id ?? 0, // 0 for new, ID for edit
                CategoryName = NewCategoryName,
                ParentId = SelectedParent?.Id
            };

            await _service.UpsertCategoryAsync(category);

            // Clear Form and Refresh
            NewCategoryName = string.Empty;
            SelectedParent = null;
            EditingCategory = null;
            await LoadCategories();
        }

        [RelayCommand]
        private void Edit(Category category)
        {
            EditingCategory = category;
            NewCategoryName = category.CategoryName;
            // Find the parent in the collection to select it in the ComboBox
            SelectedParent = AllCategories.FirstOrDefault(x => x.Id == category.ParentId);
        }

        [RelayCommand]
        private async void Delete(Category category)
        {
            if (category == null) return;

            MessageBoxResult confirm = MessageBox.Show($"Are you sure you want to delete '{category.CategoryName}'?", $"Delete {category.CategoryName}", MessageBoxButton.YesNo);
            if (confirm == MessageBoxResult.Yes)
            {
                await _service.DeleteCategoryAsync(category.Id);
                await LoadCategories();
            }
        }
    }
}
