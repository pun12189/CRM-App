using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Core;
using Tijori.Dialogs;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Services;
using Tijori.Views;

namespace Tijori.ViewModels
{
    public partial class PurchaseDetailsViewModel : ObservableObject
    {
        private readonly PurchaseService _poService;
        private readonly CategoryService _categoryService;
        private readonly IUserSession _userSession;
        private readonly IActionSecurityGuard _securityGuard;
        private readonly IServiceProvider _serviceProvider;
        private readonly ReturnService _returnService;

        public event Action? OnNavigateBackRequested;

        [ObservableProperty]
        private ObservableCollection<PurchaseCharge> _purchaseCharges = new();

        [ObservableProperty]
        private decimal _totalChargesAmount;

        [ObservableProperty]
        private ObservableCollection<PurchaseReturn> _debitNotes = new();

        [ObservableProperty]
        private decimal _totalDebitNotesAmount;

        [ObservableProperty] private PurchaseOrder _currentPurchaseOrder = new();
        [ObservableProperty] private Vendor _currentVendor = new();
        [ObservableProperty] private ObservableCollection<PurchaseOrderDetail> _orderItems = new();

        [ObservableProperty] private int _selectedTabIndex = 0;
        [ObservableProperty] private int _totalQuantityOrdered = 0;

        [ObservableProperty]
        private ObservableCollection<UploadedDocumentRow> _unifiedDocumentsCollection = new();

        [ObservableProperty]
        private ObservableCollection<BusinessCategory> _availableDocumentCategories = new();

        [ObservableProperty]
        private string _documentCountSummaryText = "0 Files Total";

        // --- Filter Properties ---
        [ObservableProperty] private BusinessCategory? _filterCategory;
        [ObservableProperty] private DateTime? _filterDateLogged;
        [ObservableProperty] private string? _filterLoggedUser;
        [ObservableProperty] private bool _isFilterActive;

        // --- Import Properties ---
        [ObservableProperty] private BusinessCategory? _selectedUploadCategory;
        [ObservableProperty] private List<string> _selectedFilePaths = new();
        [ObservableProperty] private string _selectedFilesSummaryText = "No files selected";

        // --- Export Enablement Status ---
        public bool IsFilteredEnabled => IsFilterActive;
        public bool IsSelectedEnabled => UnifiedDocumentsCollection.Any(x => x.IsSelected);

        public IEnumerable<UploadedDocumentRow> DisplayedDocuments
        {
            get
            {
                var list = UnifiedDocumentsCollection.AsEnumerable();

                if (IsFilterActive)
                {
                    if (FilterCategory != null)
                        list = list.Where(x => x.CategoryId == FilterCategory.CategoryId);

                    if (FilterDateLogged.HasValue)
                        list = list.Where(x => x.UploadedAt.Date == FilterDateLogged.Value.Date);

                    if (!string.IsNullOrWhiteSpace(FilterLoggedUser))
                        list = list.Where(x => x.UploadedBy.Contains(FilterLoggedUser, StringComparison.OrdinalIgnoreCase));
                }

                return list;
            }
        }

        public PurchaseDetailsViewModel(PurchaseService poService, CategoryService categoryService, IUserSession userSession, IActionSecurityGuard securityGuard, IServiceProvider serviceProvider, ReturnService returnService)
        {
            _poService = poService;
            _categoryService = categoryService;
            _userSession = userSession;
            _securityGuard = securityGuard;
            _returnService = returnService;
            _serviceProvider = serviceProvider;

            UnifiedDocumentsCollection.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (UploadedDocumentRow item in e.NewItems)
                        item.PropertyChanged += Item_PropertyChanged;
                }
            };
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UploadedDocumentRow.IsSelected))
            {
                OnPropertyChanged(nameof(IsSelectedEnabled));
            }
        }

        public async Task LoadDebitNotesAsync()
        {
            if (CurrentPurchaseOrder == null || CurrentPurchaseOrder.PurchaseOrderId <= 0) return;

            try
            {
                var records = await _returnService.GetPurchaseReturnsByPoIdAsync(CurrentPurchaseOrder.PurchaseOrderId);
                DebitNotes = new ObservableCollection<PurchaseReturn>(records);
                TotalDebitNotesAmount = DebitNotes.Sum(d => d.TotalAmount);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBIT NOTES ERROR] {ex.Message}");
            }
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

            var charges = await _poService.GetChargesByPurchaseOrderIdAsync(purchaseOrderId);
            PurchaseCharges = new ObservableCollection<PurchaseCharge>(charges);
            TotalChargesAmount = PurchaseCharges.Sum(c => c.TotalAmount);

            await LoadUnifiedDocumentsWorkspaceAsync(purchaseOrderId, "Purchase");

            await LoadDebitNotesAsync();
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

                OnPropertyChanged(nameof(DisplayedDocuments));
            });
        }

        [RelayCommand]
        private void NavigateBack()
        {
            OnNavigateBackRequested?.Invoke();
        }

        // =========================================================================
        // PURCHASE RETURN (DEBIT NOTE) DIALOG INTEGRATION COMMAND
        // =========================================================================
        [RelayCommand]
        private async Task OpenDebitNoteDialog()
        {
            if (CurrentPurchaseOrder == null || CurrentPurchaseOrder.PurchaseOrderId <= 0)
            {
                MessageBox.Show("Invalid purchase order context.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!OrderItems.Any())
            {
                MessageBox.Show("No line items found to return for this purchase order.", "Cannot Generate Return", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var debitNoteVm = _serviceProvider.GetRequiredService<GenerateDebitNoteViewModel>();

            // Populate dialog with current PO and lines
            debitNoteVm.LoadFromPurchase(CurrentPurchaseOrder, OrderItems, $"BAT-PO{CurrentPurchaseOrder.PurchaseOrderId}");

            var dialog = new GenerateDebitNoteDialog(debitNoteVm)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                MessageBox.Show("Debit Note successfully created and stock deducted!", "Debit Note Issued", MessageBoxButton.OK, MessageBoxImage.Information);

                // Refresh PO details, lines, and stock
                await InitializeAsync(CurrentPurchaseOrder.PurchaseOrderId);
            }
        }

        #region TAB 4: DOCUMENTS COMMANDS

        [RelayCommand]
        private void ToggleSelectAll(bool? isChecked)
        {
            if (isChecked == null || DisplayedDocuments == null) return;

            // Cast the elements of the view to your specific Lead model
            foreach (var item in DisplayedDocuments.Cast<UploadedDocumentRow>())
            {
                item.IsSelected = isChecked.Value;
            }

            OnPropertyChanged(nameof(IsSelectedEnabled));
        }

        // --- 1. FILTER DIALOG COMMANDS ---
        [RelayCommand]
        private async Task OpenFilterDialog()
        {
            await DialogHost.Show(new FilterDialogView { DataContext = this }, "DocumentTabDialogHost");
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            IsFilterActive = FilterCategory != null || FilterDateLogged.HasValue || !string.IsNullOrWhiteSpace(FilterLoggedUser);
            OnPropertyChanged(nameof(DisplayedDocuments));
            OnPropertyChanged(nameof(IsFilteredEnabled));
            DialogHost.Close("DocumentTabDialogHost");
        }

        [RelayCommand]
        private void ClearFilters()
        {
            FilterCategory = null;
            FilterDateLogged = null;
            FilterLoggedUser = string.Empty;
            IsFilterActive = false;

            // 2. Refresh UI Grid and Export status
            OnPropertyChanged(nameof(DisplayedDocuments));
            OnPropertyChanged(nameof(IsFilterActive));
            OnPropertyChanged(nameof(IsFilteredEnabled));

            // 3. Safely close dialog if it was called from inside the FilterDialog
            if (DialogHost.IsDialogOpen("DocumentTabDialogHost"))
            {
                DialogHost.Close("DocumentTabDialogHost");
            }
        }

        // --- 2. IMPORT DIALOG COMMANDS ---
        [RelayCommand]
        private async Task OpenImportDialog()
        {
            SelectedFilePaths.Clear();
            SelectedFilesSummaryText = "No files selected";
            await DialogHost.Show(new ImportDialogView { DataContext = this }, "DocumentTabDialogHost");
        }

        [RelayCommand]
        private void BrowseFiles()
        {
            var dlg = new OpenFileDialog
            {
                Multiselect = true, // Supports single & multiple selection
                Title = "Select Documents to Upload"
            };

            if (dlg.ShowDialog() == true)
            {
                SelectedFilePaths = dlg.FileNames.ToList();
                SelectedFilesSummaryText = $"{SelectedFilePaths.Count} file(s) selected";
            }
        }

        [RelayCommand]
        private async Task UploadSelectedFiles()
        {
            if (SelectedUploadCategory == null || !SelectedFilePaths.Any())
            {
                MessageBox.Show("Please select a category and at least one file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Upload files loop...
            if (SelectedFilePaths.Count > 0)
            {
                string moduleContext = "Purchase";
                var success = await _categoryService.UploadDocumentAsync(SelectedFilePaths.ToArray(), moduleContext, SelectedUploadCategory, CurrentPurchaseOrder.PurchaseOrderId, _userSession.CurrentUser);

                if (success)
                {
                    MessageBox.Show("File(s) uploaded successfully!", "Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("File upload failed. Please check the logs for details.", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // Refresh grid matrix instantly
                await LoadUnifiedDocumentsWorkspaceAsync(CurrentPurchaseOrder.PurchaseOrderId, moduleContext);
            }

            DialogHost.Close("DocumentTabDialogHost");
        }

        // --- 3. EXPORT ZIP COMMAND ---
        [RelayCommand]
        private async Task ExportZip(string mode)
        {
            List<UploadedDocumentRow> targetRows = mode switch
            {
                "Selected" => UnifiedDocumentsCollection.Where(x => x.IsSelected).ToList(),
                "Filtered" => DisplayedDocuments.ToList(),
                _ => UnifiedDocumentsCollection.ToList() // "All"
            };

            if (!targetRows.Any())
            {
                MessageBox.Show("No documents available for export in this mode.", "Export Zip", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Zip Archive (*.zip)|*.zip",
                FileName = $"Tijori_Docs_{mode}_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };

            if (dlg.ShowDialog() == true)
            {
                await Task.Run(() =>
                {
                    if (File.Exists(dlg.FileName)) File.Delete(dlg.FileName);
                    using var archive = ZipFile.Open(dlg.FileName, ZipArchiveMode.Create);
                    foreach (var doc in targetRows)
                    {
                        if (File.Exists(doc.StoragePath))
                        {
                            archive.CreateEntryFromFile(doc.StoragePath, $"{doc.CategoryName}_{doc.FileName}");
                        }
                    }
                });

                MessageBox.Show($"Successfully exported {targetRows.Count} file(s) as Zip!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
                string moduleContext = "Purchase";
                await LoadUnifiedDocumentsWorkspaceAsync(CurrentPurchaseOrder.PurchaseOrderId, moduleContext);
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
                    string moduleContext = "Purchase";
                    await LoadUnifiedDocumentsWorkspaceAsync(CurrentPurchaseOrder.PurchaseOrderId, moduleContext);
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
