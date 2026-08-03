using Tijori.Core;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Models.Enums;
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

namespace Tijori.ViewModels
{
    public partial class StaffDetailsViewModel : ObservableObject
    {
        private readonly StaffService _staffService;
        private readonly CategoryService _categoryService;
        private readonly IActionSecurityGuard _securityGuard;
        private readonly IUserSession _userSession;

        public event Action? OnNavigateBackRequested;

        [ObservableProperty] private User _staffUser = new();
        [ObservableProperty] private int _selectedTabIndex = 0;

        // Sales & Target Metrics
        [ObservableProperty] private decimal _monthlySalesAchieved = 0m;
        [ObservableProperty] private double _targetAchievementPercentage = 0.0;
        [ObservableProperty] private decimal _targetShortfallSurplus = 0m;
        [ObservableProperty] private bool _isEligibleForScheme = false;
        [ObservableProperty] private string _activeSchemeName = "Standard Monthly Incentive (100% Target Match)";

        // Leads & Customers Metrics
        [ObservableProperty] private int _totalLeadsHeld = 0;
        [ObservableProperty] private int _convertedLeadsCount = 0;
        [ObservableProperty] private double _leadConversionRate = 0.0;
        [ObservableProperty] private int _managedCustomersCount = 0;

        [ObservableProperty] private ObservableCollection<UploadedDocumentRow> _unifiedDocumentsCollection = new();

        // Dropdown lookup source for the upload dialog header section
        [ObservableProperty] private ObservableCollection<BusinessCategory> _availableDocumentCategories = new();
        [ObservableProperty] private BusinessCategory? _selectedUploadCategory;

        [ObservableProperty] private string _documentCountSummaryText = "0 Files Total";

        [ObservableProperty] private ObservableCollection<PromotionalScheme> _activeStaffSchemes = new();
        [ObservableProperty] private PromotionalScheme? _currentScheme;
        [ObservableProperty] private string _schemeRewardText = string.Empty;

        [ObservableProperty] private int _otherSchemesCount = 0;
        [ObservableProperty] private bool _hasMultipleSchemes = false;

        [ObservableProperty] private bool _isSchemesPopupOpen = false;

        public StaffDetailsViewModel(StaffService staffService, CategoryService categoryService, IActionSecurityGuard securityGuard, IUserSession userSession)
        {
            _staffService = staffService;
            _categoryService = categoryService;
            _securityGuard = securityGuard;
            _userSession = userSession;
        }

        public async Task InitializeAsync(User user)
        {
            StaffUser = user;
            await LoadStaffPerformanceAndDataAsync();
            await LoadUnifiedDocumentsWorkspaceAsync(StaffUser.UserId, "Staff");
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
        public async Task LoadStaffPerformanceAndDataAsync()
        {
            if (StaffUser == null || StaffUser.UserId == 0) return;

            DateTime now = DateTime.Now;

            // 1. Fetch Sales Performance for Current Month
            MonthlySalesAchieved = await _staffService.GetMonthlySalesAchievedAsync(StaffUser.Email, now.Year, 5);

            if (MonthlySalesAchieved == 0m)
            {
                // Fallback check against FullName
                MonthlySalesAchieved = await _staffService.GetMonthlySalesAchievedAsync(StaffUser.FullName, now.Year, 5);
            }

            // 2. Compute Target Achievement %
            decimal target = (decimal)StaffUser.MonthlyTarget;
            if (target > 0)
            {
                TargetAchievementPercentage = Math.Min(100.0, Math.Round((double)(MonthlySalesAchieved / target) * 100.0, 1));
                TargetShortfallSurplus = MonthlySalesAchieved - target;
                IsEligibleForScheme = TargetAchievementPercentage >= 90.0; // Eligible if >= 90%
            }
            else
            {
                TargetAchievementPercentage = 0.0;
                TargetShortfallSurplus = MonthlySalesAchieved;
                IsEligibleForScheme = true;
            }

            // 3. Fetch Leads Stats
            var (totalLeads, convertedLeads) = await _staffService.GetLeadStatsByStaffAsync(StaffUser.FullName);
            TotalLeadsHeld = totalLeads;
            ConvertedLeadsCount = convertedLeads;
            LeadConversionRate = TotalLeadsHeld > 0 ? Math.Round(((double)ConvertedLeadsCount / TotalLeadsHeld) * 100.0, 1) : 0.0;

            // 4. Fetch Customers Count
            ManagedCustomersCount = await _staffService.GetManagedCustomersCountAsync(StaffUser.FullName);

            var activeSchemes = await _staffService.GetActiveStaffSchemesAsync(StaffUser.UserId);
            ActiveStaffSchemes = new ObservableCollection<PromotionalScheme>(activeSchemes);

            if (activeSchemes.Any())
            {
                // Option A: Pick the first active scheme
                // Option B: Sort by highest threshold or highest reward value
                CurrentScheme = activeSchemes.FirstOrDefault();

                ActiveSchemeName = CurrentScheme.Title;
                OtherSchemesCount = activeSchemes.Count() - 1;
                HasMultipleSchemes = OtherSchemesCount > 0;

                // Evaluate eligibility for primary scheme
                decimal threshold = CurrentScheme.MinimumOrderThreshold > 0
                    ? CurrentScheme.MinimumOrderThreshold
                    : (decimal)StaffUser.MonthlyTarget;

                IsEligibleForScheme = MonthlySalesAchieved >= threshold && threshold > 0;

                // Format Reward Text
                SchemeRewardText = FormatRewardText(CurrentScheme);
            }
            else
            {
                ActiveSchemeName = "No Active Staff Scheme";
                SchemeRewardText = "N/A";
                IsEligibleForScheme = false;
                HasMultipleSchemes = false;
            }
        }

        private string FormatRewardText(PromotionalScheme scheme)
        {
            return scheme.RewardType switch
            {
                RewardType.GiftItem => !string.IsNullOrWhiteSpace(scheme.GiftItemName)
                    ? $"Reward: {scheme.GiftItemName}"
                    : "Reward: Material Gift Item",
                RewardType.Percentage => $"Reward: {scheme.RewardValue}% Commission Payout",
                _ => $"Reward: ₹{scheme.RewardValue:N0} Flat Cash Payout"
            };
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
            if (StaffUser == null || SelectedUploadCategory == null)
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
                var success = await _categoryService.UploadDocumentAsync(fileDialog.FileNames, "Staff", SelectedUploadCategory, StaffUser.UserId, _userSession.CurrentUser);

                if (success)
                {
                    MessageBox.Show("File(s) uploaded successfully!", "Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("File upload failed. Please check the logs for details.", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // Refresh grid matrix instantly
                await LoadUnifiedDocumentsWorkspaceAsync(StaffUser.UserId, "Staff");
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
                await LoadUnifiedDocumentsWorkspaceAsync(StaffUser.UserId, "Staff");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while purging file instance: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ReplaceDocumentFile(UploadedDocumentRow selectedRow)
        {
            if (selectedRow == null || StaffUser == null) return;

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
                    await LoadUnifiedDocumentsWorkspaceAsync(StaffUser.UserId, "Staff");
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

        [RelayCommand]
        private void OpenSchemesPopup()
        {
            if (ActiveStaffSchemes != null && ActiveStaffSchemes.Any())
            {
                IsSchemesPopupOpen = true;
            }
        }

        // Command to close the popup
        [RelayCommand]
        private void CloseSchemesPopup()
        {
            IsSchemesPopupOpen = false;
        }
    }
}
