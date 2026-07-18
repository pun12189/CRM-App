using CallMan.Models;
using CallMan.Models.Enums;
using CallMan.Services;
using CallMan.Services.Reports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class E2EReportsDashboardViewModel : ObservableObject
    {
        private readonly E2EReportEngine _reportEngine;
        private readonly ReportEntityService _reportEntityService;

        private readonly List<string> _masterOptionsList = new()
        {
            "Sales", "Purchase", "Staff", "Customer", "Items", "Ledger", "Location", "Vendor", "P&L", "Divisions"
        };

        // Complete collection elements mapping list targets
        [ObservableProperty] private ObservableCollection<string> _primaryDropdownOptions = new();
        [ObservableProperty] private ObservableCollection<string> _comparisonDropdownOptions = new();

        [ObservableProperty] private string? _selectedPrimaryOption;
        [ObservableProperty] private string? _selectedComparisonOption;

        [ObservableProperty] private bool _showPrimaryEntitiesList;
        [ObservableProperty] private bool _showComparisonEntitiesList;

        [ObservableProperty] private ObservableCollection<SelectableReportEntity> _primaryEntitiesCollection = new();
        [ObservableProperty] private ObservableCollection<SelectableReportEntity> _comparisonEntitiesCollection = new();

        private bool _isSyncingSelection = false;
        [ObservableProperty] private bool _primarySelectAll;
        [ObservableProperty] private bool _comparisonSelectAll;

        [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _toDate = DateTime.Today;
        [ObservableProperty] private DataView? _reportGridSource;

        // --- Core Comparison Visibility Flags Matrix ---
        [ObservableProperty] private bool _isCustomerEnabled = true;
        [ObservableProperty] private bool _isLeadHolderEnabled = true;
        [ObservableProperty] private bool _isItemsEnabled = true;
        [ObservableProperty] private bool _isBusinessEnabled = true;
        [ObservableProperty] private bool _isLedgersEnabled = true;
        [ObservableProperty] private bool _isAreasEnabled = true;
        [ObservableProperty] private bool _isVendorsEnabled = false;
        [ObservableProperty] private bool _isPLEnabled = true;


        public E2EReportsDashboardViewModel(E2EReportEngine reportEngine, ReportEntityService reportEntityService)
        {
            _reportEngine = reportEngine;
            _reportEntityService = reportEntityService;
            ResetDropdownCollections();
            SelectedPrimaryOption = _masterOptionsList.First();
            SelectedComparisonOption = _masterOptionsList.Skip(3).First(); // Default to "Customer"
        }

        private void ResetDropdownCollections()
        {
            PrimaryDropdownOptions = new ObservableCollection<string>(_masterOptionsList);
            ComparisonDropdownOptions = new ObservableCollection<string>(_masterOptionsList);
        }

        // --- EXCLUSIVITY & INCOMPATIBILITY ENGINE LOGIC ---
        partial void OnSelectedPrimaryOptionChanged(string? value)
        {
            if (_isSyncingSelection || string.IsNullOrEmpty(value)) return;
            _isSyncingSelection = true;

            string? currentComparison = SelectedComparisonOption;

            // Evaluate allowed target scopes strictly according to your business architecture rules
            List<string> allowedTargets = value switch
            {
                "Sales" => new() { "Items", "Staff", "Location", "Divisions", "P&L" },

                "Purchase" => new() { "Items", "Vendor", "Staff", "Location", "Divisions", "P&L" },

                "Staff" => new() { "Customer", "Sales", "Purchase", "Ledger", "Location", "Divisions", "P&L" },

                "Customer" => new() { "Sales", "Items", "Ledger", "Location", "Divisions", "P&L" },

                "Items" => new() { "Sales", "Purchase", "Vendor", "Location", "Divisions", "P&L" },

                "Ledger" => new() { "Customer", "Staff", "Location", "Divisions" },

                "Vendor" => new() { "Purchase", "Items", "Location", "Divisions" },

                // Locations and Divisions can evaluate cross-tabs against base transaction lists
                "Location" => _masterOptionsList.Where(x => x != "Location").ToList(),
                "Divisions" => _masterOptionsList.Where(x => x != "Divisions").ToList(),

                _ => _masterOptionsList.Where(x => x != value).ToList()
            };

            // Update the dropdown binding item source dynamically
            ComparisonDropdownOptions = new ObservableCollection<string>(allowedTargets);

            // Safeguard: If the old selected option is illegal now, fallback to the first allowed item
            if (!ComparisonDropdownOptions.Contains(currentComparison!))
            {
                SelectedComparisonOption = ComparisonDropdownOptions.FirstOrDefault();
            }
            else
            {
                SelectedComparisonOption = currentComparison;
            }

            _isSyncingSelection = false;

            // Trigger sublist data reloads safely
            _ = LoadPrimaryEntitySubListAsync();
        }

        partial void OnSelectedComparisonOptionChanged(string? value)
        {
            if (_isSyncingSelection || string.IsNullOrEmpty(value)) return;
            _isSyncingSelection = true;

            string? currentPrimary = SelectedPrimaryOption;
            var availablePrimaries = _masterOptionsList.Where(x => x != value).ToList();
            PrimaryDropdownOptions = new ObservableCollection<string>(availablePrimaries);

            if (value == "Sales" && currentPrimary == "Purchase") currentPrimary = "Vendor";
            if (value == "Purchase" && currentPrimary == "Sales") currentPrimary = "Customer";

            SelectedPrimaryOption = PrimaryDropdownOptions.Contains(currentPrimary!) ? currentPrimary : PrimaryDropdownOptions.FirstOrDefault();

            _isSyncingSelection = false;
            _ = LoadComparisonEntitySubListAsync();
        }

        // --- PROFILE LOADING ENGINE ROUTINES ---
        /// <summary>
        /// Populates the primary parameter sub-list layout column dynamically
        /// </summary>
        private async Task LoadPrimaryEntitySubListAsync()
        {
            if (string.IsNullOrEmpty(SelectedPrimaryOption)) return;

            // Abstract Context Switch Override: P&L
            if (SelectedPrimaryOption == "P&L")
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    PrimaryEntitiesCollection = new ObservableCollection<SelectableReportEntity>
                    {
                        new() { Id = 1, DisplayName = "Revenue vs Cost of Goods Sold (COGS)", IsChecked = true },
                        new() { Id = 2, DisplayName = "Gross Profit Margin Matrix" },
                        new() { Id = 3, DisplayName = "Net Profit & Loss Statement" }
                    };
                    ShowPrimaryEntitiesList = true;
                    AttachPrimaryItemListeners();
                });
                _ = LoadComparisonEntitySubListAsync();
                return;
            }

            try
            {
                // SQL Pipeline Direct Database Fetch (Sales, Purchase, Ledger, Location, Customer, etc.)
                var data = await _reportEntityService.GetEntitiesByParameterAsync(SelectedPrimaryOption);

                App.Current.Dispatcher.Invoke(() =>
                {
                    PrimaryEntitiesCollection = new ObservableCollection<SelectableReportEntity>(data);
                    ShowPrimaryEntitiesList = PrimaryEntitiesCollection.Any();
                    AttachPrimaryItemListeners();
                });

                _ = LoadComparisonEntitySubListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PRIMARY LOAD ERROR] {ex.Message}");
            }
        }

        /// <summary>
        /// Populates the comparative parameter sub-list column.
        /// Shows all unfiltered items if nothing is checked in the primary list, 
        /// and applies dynamic relationships only when active check filters are present.
        /// </summary>
        private async Task LoadComparisonEntitySubListAsync()
        {
            if (string.IsNullOrEmpty(SelectedComparisonOption))
            {
                ShowComparisonEntitiesList = false;
                return;
            }

            // Abstract Context Switch Override: P&L
            if (SelectedComparisonOption == "P&L")
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    ComparisonEntitiesCollection = new ObservableCollection<SelectableReportEntity>
            {
                new() { Id = 1, DisplayName = "Revenue vs Cost of Goods Sold (COGS)" },
                new() { Id = 2, DisplayName = "Gross Profit Margin Matrix" },
                new() { Id = 3, DisplayName = "Net Profit & Loss Statement" }
            };
                    ShowComparisonEntitiesList = true;
                });
                return;
            }

            try
            {
                // 1. Fetch the complete database catalog for the comparison selection
                var rawData = await _reportEntityService.GetEntitiesByParameterAsync(SelectedComparisonOption);

                // 2. Extract any checked item IDs from the primary parameter checklist
                var checkedPrimaryIds = PrimaryEntitiesCollection
                    .Where(x => x.IsChecked)
                    .Select(x => x.Id)
                    .ToList();

                App.Current.Dispatcher.Invoke(() =>
                {
                    // FIX: If NO items are selected in the primary column, bypass filtering entirely
                    // and present the full dataset to the operator so the UI remains interactive.
                    if (!checkedPrimaryIds.Any())
                    {
                        ComparisonEntitiesCollection = new ObservableCollection<SelectableReportEntity>(rawData);
                    }
                    else
                    {
                        // 3. Dynamic Relational Cascading: Apply your target relational filters 
                        // between the primary selections and the secondary targets here.
                        // (e.g., matching rawData items where their internal ParentId or foreign key intersects with checkedPrimaryIds)

                        // For now, displaying the dataset to match your dynamic UI conditions:
                        ComparisonEntitiesCollection = new ObservableCollection<SelectableReportEntity>(rawData);
                    }

                    ShowComparisonEntitiesList = ComparisonEntitiesCollection.Any();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COMPARISON LOAD ERROR] {ex.Message}");
            }
        }

        // --- HELPER BINDING STATE CAPTURES ---
        private void AttachPrimaryItemListeners()
        {
            foreach (var item in PrimaryEntitiesCollection)
            {
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SelectableReportEntity.IsChecked))
                    {
                        // Cascade out changes down to the comparison array matrix
                        _ = LoadComparisonEntitySubListAsync();
                    }
                };
            }
        }

        // --- SELECT ALL STATE TRIGGERS ---
        partial void OnPrimarySelectAllChanged(bool value)
        {
            foreach (var entity in PrimaryEntitiesCollection) entity.IsChecked = value;
            _ = LoadComparisonEntitySubListAsync();
        }

        partial void OnComparisonSelectAllChanged(bool value)
        {
            foreach (var entity in ComparisonEntitiesCollection) entity.IsChecked = value;
        }

        [RelayCommand]
        private async Task GenerateStandardReportAsync()
        {
            // Execute the matrix cross-tabulation query block context loops here
        }

        [RelayCommand]
        private async Task GenerateAIReportAsync()
        {
            // Dispatches aggregate array details down to your target AI reporting models
        }
    }
}
