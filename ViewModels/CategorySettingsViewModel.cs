using CallMan.Dialogs;
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
    public partial class CategorySettingsViewModel : ObservableObject
    {
        private readonly CategoryService _categoryService;

        [ObservableProperty] private CategoryContext _selectedContext = CategoryContext.Items;

        public ManageCategoriesViewModel ItemsManagementVm { get; }

        public ObservableCollection<BusinessCategory> FilteredCategories { get; set; } = new();
        public ObservableCollection<DocumentCategoryDisplay> DocumentCategoriesDisplayCollection { get; set; } = new();

        public CategorySettingsViewModel(CategoryService categoryService, ManageCategoriesViewModel itemsManagementVm)
        {
            _categoryService = categoryService;
            ItemsManagementVm = itemsManagementVm;

            _ = LoadCategoriesAsync();
        }

        partial void OnSelectedContextChanged(CategoryContext value) 
        { 
            _ = LoadCategoriesAsync(); 
        }

        [RelayCommand]
        private async Task LoadCategoriesAsync()
        {
            try
            {
                var data = await _categoryService.GetCategoriesByContextAsync(SelectedContext);

                if (SelectedContext == CategoryContext.Leads || SelectedContext == CategoryContext.Customers)
                {
                    FilteredCategories.Clear();
                    foreach (var item in data)
                    {
                        FilteredCategories.Add(item);
                    }
                }
                else if (SelectedContext == CategoryContext.Documents)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DocumentCategoriesDisplayCollection.Clear();
                        foreach (var cat in data)
                        {
                            // Fetch associated module string keys using the service layer
                            var modulesList = Task.Run(() => _categoryService.GetModulesLinkedToCategoryAsync(cat.CategoryId)).Result;

                            DocumentCategoriesDisplayCollection.Add(new DocumentCategoryDisplay
                            {
                                CategoryId = cat.CategoryId,
                                CategoryName = cat.CategoryName,
                                IsSystemDefined = cat.IsSystemDefined,
                                RawCategory = cat,
                                // Join array components cleanly into a display string (e.g., "Lead, Customer")
                                LinkedModulesDisplay = modulesList.Any() ? string.Join(", ", modulesList) : "None"
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to sync data matrices from local host network: {ex.Message}", "Database Connectivity Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task OpenAddCategory()
        {
            // Opens our unified view dialog window initialized with the currently selected view context
            var dialogVm = new AddCategoryDialogViewModel(SelectedContext);
            var dialogWindow = new AddCategoryDialog { DataContext = dialogVm };

            if (dialogWindow.ShowDialog() == true)
            {
                try
                {
                    // ====================================================================
                    // CONDITION 1: EXCLUSIVE ROUTE FOR DYNAMIC DOCUMENTS MODULE
                    // ====================================================================
                    if (SelectedContext == CategoryContext.Documents)
                    {
                        var documentCategory = dialogVm.GetConfiguredCategory(CategoryContext.Documents);
                        var selectedModulesList = dialogVm.GetSelectedModulesList();

                        // Call our transactional service mapping to save the label along with its checkbox intersections
                        await _categoryService.SaveDocumentCategoryWithLinksAsync(documentCategory, selectedModulesList);
                    }
                    // ====================================================================
                    // CONDITION 2: DUAL LINK SAVING FOR LEADS & CUSTOMERS COMBINED
                    // ====================================================================
                    else if (dialogVm.ApplyToAllContexts && (SelectedContext == CategoryContext.Leads || SelectedContext == CategoryContext.Customers))
                    {
                        var leadCategory = dialogVm.GetConfiguredCategory(CategoryContext.Leads);
                        var customerCategory = dialogVm.GetConfiguredCategory(CategoryContext.Customers);

                        await _categoryService.SaveCategoryAsync(leadCategory);
                        await _categoryService.SaveCategoryAsync(customerCategory);
                    }
                    // ====================================================================
                    // CONDITION 3: STANDARD FLAT SAVING (ITEMS / SINGLE SALES CONTEXT)
                    // ====================================================================
                    else
                    {
                        var singleCategory = dialogVm.GetConfiguredCategory(SelectedContext);
                        await _categoryService.SaveCategoryAsync(singleCategory);
                    }

                    // Refresh the current UI view data layout instantly
                    await LoadCategoriesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to commit category changes to disk ledger: {ex.Message}",
                                    "Write Conflict Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task EditCategory(BusinessCategory selectedCategory)
        {
            if (selectedCategory == null) return;

            // Load the clean unified layout dialog using an edit mode constructor overload
            var dialogVm = new AddCategoryDialogViewModel(selectedCategory);
            var dialogWindow = new AddCategoryDialog { DataContext = dialogVm };

            if (dialogWindow.ShowDialog() == true)
            {
                try
                {
                    bool success = await _categoryService.UpdateCategoryAsync(dialogVm.GetConfiguredCategory(selectedCategory.TargetContext));
                    if (success)
                    {
                        await LoadCategoriesAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to update category alterations: {ex.Message}", "Write Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task EditDocCategory(DocumentCategoryDisplay displayItem)
        {
            if (displayItem == null) return;

            // 1. Fetch currently saved links from the DB to check our checkboxes
            var savedModules = await _categoryService.GetModulesLinkedToCategoryAsync(displayItem.CategoryId);

            // 2. Pass the structural settings to the dialog
            var dialogVm = new AddCategoryDialogViewModel(displayItem.RawCategory, savedModules);
            var dialogWindow = new AddCategoryDialog { DataContext = dialogVm };

            if (dialogWindow.ShowDialog() == true)
            {
                try
                {
                    // Extract the newly checked items list from the dialog view model
                    var checkedModules = dialogVm.GetSelectedModulesList();

                    // 3. Save updates to database
                    await _categoryService.UpdateDocumentCategoryWithLinksAsync(
                        dialogVm.GetConfiguredCategory(CategoryContext.Documents),
                        checkedModules
                    );

                    // Refresh layout grid views
                    await LoadCategoriesAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to modify document settings: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ====================================================================
        // UPDATED COMMAND: CLEAN SIMPLE DELETE PROCESS
        // ====================================================================
        [RelayCommand]
        private async Task DeleteCategory(BusinessCategory selectedCategory)
        {
            if (selectedCategory == null) return;

            var result = MessageBox.Show($"Are you sure you want to permanently delete the category '{selectedCategory.CategoryName}'?",
                                         "Confirm Permanent Deletion",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = await _categoryService.DeleteBusinessCategoryAsync(selectedCategory.CategoryId);
                    if (success)
                    {
                        await LoadCategoriesAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Database deletion error: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }    
}
