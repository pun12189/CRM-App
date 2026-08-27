using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly IDialogService _dialog;
        private readonly IServiceProvider _serviceProvider;
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private object? _selectedTabContent;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _lastUpdatedStatus = "Last Updated: Just Now";
        [ObservableProperty] private string _dataUpdatedStatus;

        // Stats Counters
        [ObservableProperty] private DashboardStats _stats;

        // This property controls the button's visibility
        [ObservableProperty] private bool _isFilterActive;
        private DashboardFilter? _currentActiveFilter;

        // ====================================================================
        // NEW ADDITIONS: SIDEBAR STAGE SUMMARY COLLECTIONS
        // ====================================================================
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _reminderCounters = new();
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _followupStagesCounters = new();
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _matureStagesCounters = new();
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _leadLabelsCounters = new();

        // ==========================================
        // 🌟 MULTI-VIEW TOGGLES & CHART SERIES
        // ==========================================
        [ObservableProperty] private DashboardViewMode _productViewMode;
        [ObservableProperty] private DashboardViewMode _allTimeDataViewMode;
        [ObservableProperty] private DashboardViewMode _orderViewMode;

        // Product Charts
        [ObservableProperty] private ISeries[] _productBarSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _productXAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _productPieSeries = Array.Empty<ISeries>();

        // Lead / All-Time Data Charts
        [ObservableProperty] private ISeries[] _leadBarSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _leadXAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _leadPieSeries = Array.Empty<ISeries>();

        // Customer / All-Time Data Charts
        [ObservableProperty] private ISeries[] _customerBarSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _customerXAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _customerPieSeries = Array.Empty<ISeries>();

        // Order Charts
        [ObservableProperty] private ISeries[] _orderBarSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _orderXAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _orderPieSeries = Array.Empty<ISeries>();

        private bool _isInitializing = true;

        public DashboardViewModel(LeadService service, IDialogService dialog, IServiceProvider serviceProvider, MainViewModel mainViewModel)
        {
            _service = service;
            _dialog = dialog;
            _serviceProvider = serviceProvider;
            _mainViewModel = mainViewModel;
            LoadUserPreferences();
            _isInitializing = false;
            RefreshCommand.Execute(null);
        }

        private void LoadUserPreferences()
        {
            var saved = UserPreferencesService.LoadDashboardPreferences();
            _productViewMode = saved.ProductViewMode;
            _allTimeDataViewMode = saved.AllTimeDataViewMode;
            _orderViewMode = saved.OrderViewMode;
        }

        private void SaveCurrentPreferences()
        {
            if (_isInitializing) return;

            UserPreferencesService.SaveDashboardPreferences(new UserDashboardSettings
            {
                ProductViewMode = this.ProductViewMode,
                AllTimeDataViewMode = this.AllTimeDataViewMode,
                OrderViewMode = this.OrderViewMode
            });
        }

        [RelayCommand]
        private async Task Refresh()
        {
            IsLoading = true;
            try
            {
                // 1. Fetch Stats for Tiles
                Stats = await _service.GetDashboardStatsAsync();                

                var stagesData = await _service.GetDashboardStageSummariesAsync();

                ReminderCounters = new ObservableCollection<KeyValuePair<string, int>>(stagesData.Reminders);
                FollowupStagesCounters = new ObservableCollection<KeyValuePair<string, int>>(stagesData.FollowupStages);
                MatureStagesCounters = new ObservableCollection<KeyValuePair<string, int>>(stagesData.MatureStages);
                LeadLabelsCounters = new ObservableCollection<KeyValuePair<string, int>>(stagesData.LeadLabels);

                PopulateAllChartSeries(Stats);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenFilter()
        {
            // 1. Open the popup and wait for result
            var filterResult = await _dialog.ShowFilterDialog();

            if (filterResult != null)
            {
                // 2. Call the filtered service method
                await RefreshDashboardWithFilter(filterResult);
                IsFilterActive = true;
            }
        }

        [RelayCommand]
        private async Task ClearFilter()
        {
            await Refresh();
            DataUpdatedStatus = string.Empty;
            IsFilterActive = false;
        }

        private async Task RefreshDashboardWithFilter(DashboardFilter filter)
        {
            IsLoading = true;
            try
            {
                Stats = await _service.GetDashboardStatsFilteredAsync(filter);

                var filteredStages = await _service.GetDashboardStageSummariesFilteredAsync(filter);

                ReminderCounters = new ObservableCollection<KeyValuePair<string, int>>(filteredStages.Reminders);
                FollowupStagesCounters = new ObservableCollection<KeyValuePair<string, int>>(filteredStages.FollowupStages);
                MatureStagesCounters = new ObservableCollection<KeyValuePair<string, int>>(filteredStages.MatureStages);
                LeadLabelsCounters = new ObservableCollection<KeyValuePair<string, int>>(filteredStages.LeadLabels);

                PopulateAllChartSeries(Stats);

                // Optional: Update 'Last Updated' timestamp
                LastUpdatedStatus = $"Filtered by {filter.LeadHolder ?? "All"} ({filter.PresetRange})";
                DataUpdatedStatus = $"Filtered Target Group: {filter.LeadHolder ?? "All Staff Operations"} ({filter.PresetRange})";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void PopulateAllChartSeries(DashboardStats s)
        {
            if (s == null) return;

            // 1. PRODUCT METRICS
            ProductBarSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "Stock Metric Count",
                    Values = new int[] { s.TotalCategoriesUsed, s.TotalProducts, s.TotalNewProducts, s.FastMovingProducts, s.SlowMovingProducts, s.NearSkuCount, s.NearExpiryCount, s.SkippedProductsCount },
                    Fill = new SolidColorPaint(new SKColor(41, 128, 185)),
                    DataLabelsPaint = new SolidColorPaint(new SKColor(44, 62, 80)),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
                }
            };
            ProductXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = new string[] { "Categories", "Total", "New", "Fast Moving", "Slow Moving", "Near SKU", "Near Expiry", "Skipped" },
                    LabelsRotation = 0, // 🌟 Keep horizontal
                    TextSize = 11,
                    LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)), // Muted slate color (#64748B)
                    SeparatorsPaint = new SolidColorPaint(new SKColor(241, 245, 249)) // Subtle divider lines
                }
            };
            ProductPieSeries = new ISeries[]
            {
                new PieSeries<int> { Name = "New Products", Values = new int[] { s.TotalNewProducts }, Fill = new SolidColorPaint(new SKColor(22, 160, 133)) },
                new PieSeries<int> { Name = "Fast Moving", Values = new int[] { s.FastMovingProducts }, Fill = new SolidColorPaint(new SKColor(211, 84, 0)) },
                new PieSeries<int> { Name = "Slow Moving", Values = new int[] { s.SlowMovingProducts }, Fill = new SolidColorPaint(new SKColor(192, 57, 43)) },
                new PieSeries<int> { Name = "Near SKU", Values = new int[] { s.NearSkuCount }, Fill = new SolidColorPaint(new SKColor(153, 27, 27)) },
                new PieSeries<int> { Name = "Near Expiry", Values = new int[] { s.NearExpiryCount }, Fill = new SolidColorPaint(new SKColor(194, 65, 12)) },
                new PieSeries<int> { Name = "Skipped", Values = new int[] { s.SkippedProductsCount }, Fill = new SolidColorPaint(new SKColor(109, 40, 217)) }
            };

            // 2. LEADS ENTITY PIPELINE
            LeadBarSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "Leads Volume",
                    Values = new int[] { s.AllLeads, s.NewLeads, s.FollowupLeads, s.NoFollowupLeads, s.Dead },
                    Fill = new SolidColorPaint(new SKColor(23, 148, 161)),
                    DataLabelsPaint = new SolidColorPaint(new SKColor(44, 62, 80)),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
                }
            };
            LeadXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = new string[] { "All", "Open", "Follow-ups", "Overdue\n(>30d)", "Dead" },
                    LabelsRotation = 0, // 🌟 Keep horizontal
                    TextSize = 11,
                    LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(241, 245, 249))
                }
            };
            LeadPieSeries = new ISeries[]
            {
                new PieSeries<int> { Name = "Open / New Leads", Values = new int[] { s.NewLeads }, Fill = new SolidColorPaint(new SKColor(41, 128, 185)) },
                new PieSeries<int> { Name = "In Follow-up", Values = new int[] { s.FollowupLeads }, Fill = new SolidColorPaint(new SKColor(22, 160, 133)) },
                new PieSeries<int> { Name = "No Follow-up (>30d)", Values = new int[] { s.NoFollowupLeads }, Fill = new SolidColorPaint(new SKColor(194, 65, 12)) },
                new PieSeries<int> { Name = "Dead Leads", Values = new int[] { s.Dead }, Fill = new SolidColorPaint(new SKColor(192, 57, 43)) }
            };

            // 3. CUSTOMERS ENTITY & RETENTION
            CustomerBarSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "Customer Metrics",
                    Values = new int[] { s.Customers, s.NoUpdation7Days, s.NoRepeatOrder, s.NoOrder, s.BelowTarget },
                    Fill = new SolidColorPaint(new SKColor(39, 174, 96)),
                    DataLabelsPaint = new SolidColorPaint(new SKColor(44, 62, 80)),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
                }
            };
            CustomerXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = new string[] { "Total", "No Update\n(>7d)", "Single\nOrder", "Dormant\n(>30d)", "Below\nTarget" },
                    LabelsRotation = 0, // 🌟 Keep horizontal
                    TextSize = 11,
                    LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(241, 245, 249))
                }
            };
            CustomerPieSeries = new ISeries[]
            {
                new PieSeries<int> { Name = "Active Base", Values = new int[] { Math.Max(0, s.Customers - s.NoOrder) }, Fill = new SolidColorPaint(new SKColor(39, 174, 96)) },
                new PieSeries<int> { Name = "No Update (7d)", Values = new int[] { s.NoUpdation7Days }, Fill = new SolidColorPaint(new SKColor(211, 84, 0)) },
                new PieSeries<int> { Name = "Single Order Only", Values = new int[] { s.NoRepeatOrder }, Fill = new SolidColorPaint(new SKColor(142, 68, 173)) },
                new PieSeries<int> { Name = "Dormant (30d)", Values = new int[] { s.NoOrder }, Fill = new SolidColorPaint(new SKColor(153, 27, 27)) },
                new PieSeries<int> { Name = "Below Target", Values = new int[] { s.BelowTarget }, Fill = new SolidColorPaint(new SKColor(109, 40, 217)) }
            };

            // 4. ORDER METRICS
            OrderBarSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Name = "Orders Count",
                    Values = new int[] { s.TotalOrders, s.TotalNewOrders, s.TotalRepeatedOrders, s.TotalUnpaidOrders, s.TotalPartialPaidOrders },
                    Fill = new SolidColorPaint(new SKColor(41, 128, 185)),
                    DataLabelsPaint = new SolidColorPaint(new SKColor(44, 62, 80)),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
                }
            };
            OrderXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = new string[] { "Total Orders", "First Orders", "Repeated", "Unpaid", "Partially Paid" },
                    LabelsRotation = 0, // 🌟 Keep horizontal
                    TextSize = 11,
                    LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)),
                    SeparatorsPaint = new SolidColorPaint(new SKColor(241, 245, 249))
                }
            };
            OrderPieSeries = new ISeries[]
            {
                new PieSeries<int> { Name = "First Orders", Values = new int[] { s.TotalNewOrders }, Fill = new SolidColorPaint(new SKColor(41, 128, 185)) },
                new PieSeries<int> { Name = "Repeated Orders", Values = new int[] { s.TotalRepeatedOrders }, Fill = new SolidColorPaint(new SKColor(142, 68, 173)) },
                new PieSeries<int> { Name = "Unpaid Orders", Values = new int[] { s.TotalUnpaidOrders }, Fill = new SolidColorPaint(new SKColor(192, 57, 43)) },
                new PieSeries<int> { Name = "Partially Paid", Values = new int[] { s.TotalPartialPaidOrders }, Fill = new SolidColorPaint(new SKColor(243, 156, 18)) }
            };
        }

        partial void OnProductViewModeChanged(DashboardViewMode value) => SaveCurrentPreferences();
        partial void OnAllTimeDataViewModeChanged(DashboardViewMode value) => SaveCurrentPreferences();
        partial void OnOrderViewModeChanged(DashboardViewMode value) => SaveCurrentPreferences();

        [RelayCommand]
        private void SetProductViewMode(DashboardViewMode mode)
        {
            ProductViewMode = mode;
        }

        [RelayCommand]
        private void SetAllTimeDataViewMode(DashboardViewMode mode)
        {
            AllTimeDataViewMode = mode;
        }

        [RelayCommand]
        private void SetOrderViewMode(DashboardViewMode mode)
        {
            OrderViewMode = mode;
        }

        /// <summary>
        /// Global Click Routing Engine for Dashboard Tiles
        /// </summary>
        [RelayCommand]
        private async Task NavigateFromCounter(DashboardTargetView target)
        {
            try
            {
                LoadingService.Show("Loading view... Please wait.");

                object targetViewModel = null;

                // 1. Map your target enum context to the exact DI View Model requested
                switch (target)
                {
                    case DashboardTargetView.AllLeads:
                    case DashboardTargetView.OpenLeads:
                    case DashboardTargetView.FollowupLeads:
                    case DashboardTargetView.NoFollowupLeads:
                    case DashboardTargetView.DeadLeads:
                        targetViewModel = _serviceProvider.GetRequiredService<LeadViewModel>();
                        break;

                    case DashboardTargetView.Customers:
                    case DashboardTargetView.NoUpdation7Days:
                    case DashboardTargetView.NoRepeatOrders:
                    case DashboardTargetView.NoOrders30Days:
                    case DashboardTargetView.BelowTargetCustomers:
                        targetViewModel = _serviceProvider.GetRequiredService<MaturedLeadsViewModel>();
                        break;

                    case DashboardTargetView.ProductsList:
                    case DashboardTargetView.CategoriesList:
                    case DashboardTargetView.NewProducts:
                    case DashboardTargetView.FastMovingProducts:
                    case DashboardTargetView.SlowMovingProducts:
                    case DashboardTargetView.NearSkuProducts:
                    case DashboardTargetView.NearExpiryBatches:
                    case DashboardTargetView.SkippedProducts:
                        targetViewModel = _serviceProvider.GetRequiredService<InventoryViewModel>();
                        break;

                    case DashboardTargetView.AllOrders:
                    case DashboardTargetView.NewOrders:
                    case DashboardTargetView.RepeatedOrders:
                    case DashboardTargetView.UnpaidOrders:
                    case DashboardTargetView.PartiallyPaidOrders:
                        targetViewModel = _serviceProvider.GetRequiredService<AllOrdersViewModel>();
                        break;
                }

                if (targetViewModel == null) return;

                // 2. Inject the current filter context state into the resolved view model
                if (targetViewModel is IDashboardFilterable filterableVm)
                {
                    // Passes your filter state (or null if unfiltered) along with the specific tile context clicked
                    filterableVm.ApplyDashboardFilter(IsFilterActive ? _currentActiveFilter : null, target);
                }

                // 3. Switch the workspace layout smoothly via the Main Window dispatcher frame
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    _mainViewModel.CurrentView = targetViewModel;
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An error occurred while navigating: {ex.Message}");
            }
            finally
            {
                LoadingService.Hide();
            }            
        }

        // Keep your existing Refresh, OpenFilter, and ClearFilter methods completely unchanged
    }
}

