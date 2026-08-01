using CallMan.Core;
using CallMan.Interfaces;
using CallMan.Models;
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
    public partial class PurchaseDetailsViewModel : ObservableObject
    {
        private readonly PurchaseService _poService;
        private readonly CategoryService _categoryService;
        private readonly IUserSession _userSession;
        private readonly IActionSecurityGuard _securityGuard;

        public event Action? OnNavigateBackRequested;

        [ObservableProperty] private PurchaseOrder _currentPurchaseOrder = new();
        [ObservableProperty] private Vendor _currentVendor = new();
        [ObservableProperty] private ObservableCollection<PurchaseOrderDetail> _orderItems = new();

        [ObservableProperty] private int _selectedTabIndex = 0;
        [ObservableProperty] private int _totalQuantityOrdered = 0;

        [ObservableProperty] private ObservableCollection<UploadedDocumentRow> _unifiedDocumentsCollection = new();

        // Dropdown lookup source for the upload dialog header section
        [ObservableProperty] private ObservableCollection<BusinessCategory> _availableDocumentCategories = new();
        [ObservableProperty] private BusinessCategory? _selectedUploadCategory;

        [ObservableProperty] private string _documentCountSummaryText = "0 Files Total";

        public PurchaseDetailsViewModel(PurchaseService poService, CategoryService categoryService, IUserSession userSession, IActionSecurityGuard securityGuard)
        {
            _poService = poService;
            _categoryService = categoryService;
            _userSession = userSession;
            _securityGuard = securityGuard;
        }

        public async Task InitializeAsync(int purchaseOrderId)
        {
            // 1. Load Header and Vendor Information
            var (order, vendor) = await _poService.GetPurchaseOrderWithVendorAsync(purchaseOrderId);
            if (order != null)
            {
                CurrentPurchaseOrder = order;
            }
            if (vendor != null)
            {
                CurrentVendor = vendor;
            }

            // 2. Load Purchased Line Items
            var items = await _poService.GetPurchaseOrderDetailsAsync(purchaseOrderId);
            OrderItems = new ObservableCollection<PurchaseOrderDetail>(items);

            // 3. Calculate Total Units
            TotalQuantityOrdered = OrderItems.Sum(item => item.Quantity);

            await LoadUnifiedDocumentsWorkspaceAsync(purchaseOrderId, "Purchase");
        }

        /// <summary>
        /// Invoke this inside ShowLeadWorkspace to cleanly build the single document grid.
        /// Pass "lead" or "customer" as the activeModule string parameter context.
        /// </summary>
        public async Task LoadUnifiedDocumentsWorkspaceAsync(int entityId, string activeModule)
        {
            var categoriesList = await _categoryService.GetCategoriesByModulesAsync(activeModule);

            // 2. Fetch all files currently uploaded for this specific profile ID            

            var filesList = await _categoryService.GetFilesByProfileIdAsync(activeModule, entityId);
            App.Current.Dispatcher.Invoke(() =>
            {
                AvailableDocumentCategories = new ObservableCollection<BusinessCategory>(categoriesList);
                SelectedUploadCategory = AvailableDocumentCategories.FirstOrDefault();

                UnifiedDocumentsCollection.Clear();
                foreach (var file in filesList)
                {
                    UnifiedDocumentsCollection.Add(file);
                }

                DocumentCountSummaryText = $"{filesList.Count()} Total Document Attachments Registered";
            });
        }

        [RelayCommand]
        private void NavigateBack()
        {
            OnNavigateBackRequested?.Invoke();
        }

        #region TAB 4: DOCUMENTS COMMANDS

        [RelayCommand]
        private async Task UploadDocumentAsync()
        {
            if (CurrentPurchaseOrder == null || SelectedUploadCategory == null)
            {
                MessageBox.Show("Please choose a target Document Category from the dropdown selector first.", "Context Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true, // Bulk multi-uploads to a single category made simple!
                Filter = "Compliance Formats|*.pdf;*.jpg;*.jpeg;*.png;*.xlsx;*.docx"
            };

            if (fileDialog.ShowDialog() == true)
            {
                var success = await _categoryService.UploadDocumentAsync(fileDialog.FileNames, "Purchase", SelectedUploadCategory, CurrentPurchaseOrder.PurchaseOrderId, _userSession.CurrentUser);

                if (success)
                {
                    MessageBox.Show("File(s) uploaded successfully!", "Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("File upload failed. Please check the logs for details.", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // Refresh grid matrix instantly
                await LoadUnifiedDocumentsWorkspaceAsync(CurrentPurchaseOrder.PurchaseOrderId, "Purchase");
            }
        }

        [RelayCommand]
        private async Task DeleteDocumentFile(UploadedDocumentRow selectedRow)
        {
            if (selectedRow == null) return;

            var result = MessageBox.Show($"Are you sure you want to permanently delete '{selectedRow.FileName}'?", "Confirm Purge", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                // 1. Clean up physical host disk block
                if (System.IO.File.Exists(selectedRow.StoragePath))
                {
                    System.IO.File.Delete(selectedRow.StoragePath);
                }

                // 2. Clear out database pointer record
                await _categoryService.DeleteDocumentRecordAsync(selectedRow.DocumentId);

                // 3. Refresh display layout
                await LoadUnifiedDocumentsWorkspaceAsync(CurrentPurchaseOrder.PurchaseOrderId, "Purchase");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while purging file instance: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ReplaceDocumentFile(UploadedDocumentRow selectedRow)
        {
            if (selectedRow == null || CurrentPurchaseOrder == null) return;

            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Supported Files|*.pdf;*.jpg;*.jpeg;*.png;*.xlsx;*.docx",
                Title = $"Replace Document: {selectedRow.FileName}"
            };

            if (fileDialog.ShowDialog() == true)
            {
                try
                {
                    // 1. Delete the old physical file to prevent disk bloat
                    if (System.IO.File.Exists(selectedRow.StoragePath))
                    {
                        System.IO.File.Delete(selectedRow.StoragePath);
                    }

                    // 2. Write the new file instance exactly to the same vault directory layout path
                    string newLocalPath = fileDialog.FileName;
                    string extension = System.IO.Path.GetExtension(newLocalPath);
                    string cleanName = System.IO.Path.GetFileName(newLocalPath);

                    string targetDir = System.IO.Path.GetDirectoryName(selectedRow.StoragePath)!;
                    string dynamicStoragePath = System.IO.Path.Combine(targetDir, $"{Guid.NewGuid()}_{cleanName}");

                    System.IO.File.Copy(newLocalPath, dynamicStoragePath, true);

                    // 3. Update the tracking row properties inside MySQL server records mapping
                    await _categoryService.ReplaceUploadDocumentAsync(cleanName, dynamicStoragePath, _userSession.CurrentUser, selectedRow.DocumentId);

                    // 4. Instantly refresh the UI matrix grid list
                    await LoadUnifiedDocumentsWorkspaceAsync(CurrentPurchaseOrder.PurchaseOrderId, "Purchase");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to replace document attachment: {ex.Message}", "IO Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task DownloadDocumentFile(UploadedDocumentRow selectedRow)
        {
            bool accessGranted = await _securityGuard.IsActionAuthorizedAsync();
            if (!accessGranted) return; // Halt execution path immediately

            if (selectedRow == null || string.IsNullOrEmpty(selectedRow.StoragePath)) return;

            if (!System.IO.File.Exists(selectedRow.StoragePath))
            {
                MessageBox.Show("The source document file could not be discovered on server storage paths.", "File Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Initialize native save dialog frame
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = selectedRow.FileName, // Prefills original filename automatically
                Filter = $"File Extension (*{System.IO.Path.GetExtension(selectedRow.StoragePath)})|*{System.IO.Path.GetExtension(selectedRow.StoragePath)}",
                Title = "Download Document Reference Copy As"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.Copy(selectedRow.StoragePath, saveDialog.FileName, true);
                    MessageBox.Show("File copied and saved locally successfully!", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not export document file copy: {ex.Message}", "Download Fault", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}
