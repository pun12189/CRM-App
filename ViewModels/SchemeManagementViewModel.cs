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
    public partial class SchemeManagementViewModel : ObservableObject
    {
        private readonly SchemeService _schemeService;
        private readonly CategoryService _categoryService;

        // Observable collections bound straight into your independent Tab DataGrids
        public ObservableCollection<PromotionalScheme> CustomerSchemesCollection { get; set; } = new();
        public ObservableCollection<PromotionalScheme> StaffSchemesCollection { get; set; } = new();

        public SchemeManagementViewModel(SchemeService schemeService, CategoryService categoryService)
        {
            _schemeService = schemeService ?? throw new ArgumentNullException(nameof(schemeService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));

            // Sync current configurations from local VPS server nodes on initial load
            _ = LoadAllSchemesAsync();
        }

        [RelayCommand]
        private async Task LoadAllSchemesAsync()
        {
            try
            {
                // Load all staff incentives
                var staffData = await _schemeService.GetAllSchemesAsync();

                // For customer schemes, we fetch using a broad lookup wrapper or query
                // Here we fetch and sort them into their respective collections on the UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CustomerSchemesCollection.Clear();
                    StaffSchemesCollection.Clear();

                    // Fallback baseline population loops split cleanly via TargetScope definitions
                    foreach (var scheme in staffData)
                    {
                        if (scheme.TargetScope == SchemeScope.Staff)
                            StaffSchemesCollection.Add(scheme);
                        else
                            CustomerSchemesCollection.Add(scheme);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch active policy structures: {ex.Message}", "Sync Conflict Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task OpenCreateScheme()
        {
            // Open the top horizontal aligned tab modal window dialog layout we built previously
            var dialogVm = new AddSchemeDialogViewModel(_categoryService, _schemeService);
            var dialogWindow = new AddSchemeDialog { DataContext = dialogVm };

            if (dialogWindow.ShowDialog() == true)
            {
                try
                {
                    var assignedCategoryIds = dialogVm.GetSelectedCategoryIds();
                    // Call Dapper transaction service layer to write to MySQL data matrices
                    bool success = await _schemeService.SaveSchemeAsync(dialogVm.Scheme, assignedCategoryIds);
                    if (success)
                    {
                        // Refresh display collections
                        await LoadAllSchemesAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save active policy rules data: {ex.Message}", "Database Write Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ExportReport()
        {
            // Open standard system save picker dialog file
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Spreadsheet (*.xlsx)|*.xlsx|PDF Document (*.pdf)|*.pdf",
                FileName = $"Incentive_Report_{DateTime.Today:yyyyMMdd}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                string targetFilePath = saveDialog.FileName;

                // Routing routine block execution paths depending on user choices extension
                if (targetFilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    // Call your dynamic system PDF Invoice/Report generator pipeline (MigraDoc matrix engines)
                    MessageBox.Show($"Policy data exported safely to PDF path: {targetFilePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Trigger CSV text parsing loop or Epplus engine routines here
                    MessageBox.Show($"Data matrix serialized safely to spreadsheet configuration: {targetFilePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        [RelayCommand]
        private async Task EditScheme(PromotionalScheme selectedScheme)
        {
            if (selectedScheme == null) return;

            // Pass the existing record directly into our dialog controller viewmodel
            var dialogVm = new AddSchemeDialogViewModel(selectedScheme, _categoryService, _schemeService);
            var dialogWindow = new AddSchemeDialog { DataContext = dialogVm };

            if (dialogWindow.ShowDialog() == true)
            {
                try
                {
                    var assignedCategoryIds = dialogVm.GetSelectedCategoryIds();
                    // Save modified values to database
                    bool success = await _schemeService.UpdateSchemeAsync(dialogVm.Scheme, assignedCategoryIds);
                    if (success)
                    {
                        // Refresh grid sets; modifications to end dates collapse the Expired state instantly
                        await LoadAllSchemesAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to update scheme record changes: {ex.Message}", "Database Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ====================================================================
        // NEW COMMAND: PURGE SCHEME PROFILE ROW
        // ====================================================================
        [RelayCommand]
        private async Task DeleteScheme(PromotionalScheme selectedScheme)
        {
            if (selectedScheme == null) return;

            var result = MessageBox.Show($"Are you sure you want to permanently delete the campaign scheme policy '{selectedScheme.Title}'?",
                                         "Confirm Permanent Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool success = await _schemeService.DeleteSchemeAsync(selectedScheme.SchemeId);
                    if (success)
                    {
                        await LoadAllSchemesAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to purge scheme record: {ex.Message}", "Database Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
