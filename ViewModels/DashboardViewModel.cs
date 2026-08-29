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
        private readonly DashboardService _dashboardService;
        private readonly IDialogService _dialog;
        private readonly IServiceProvider _serviceProvider;
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _lastUpdatedStatus = "Last Updated: Just Now";
        [ObservableProperty] private string _dataUpdatedStatus = string.Empty;
        [ObservableProperty] private bool _isFilterActive;
        private DashboardFilter? _currentActiveFilter;

        // Master Consolidated DTO
        [ObservableProperty] private ExecutiveDashboardData _data = new();

        // Single Master View Controller
        [ObservableProperty] private GlobalDashboardViewMode _globalViewMode = GlobalDashboardViewMode.Cards;

        // Sidebar Stage Summary Collections
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _reminderCounters = new();
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _followupStagesCounters = new();
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _matureStagesCounters = new();
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _leadLabelsCounters = new();

        // 🌟 LIVECHARTS SERIES BINDINGS
        // 1. Inventory & Products
        [ObservableProperty] private ISeries[] _inventoryTrendLineSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _inventoryTrendXAxes = Array.Empty<Axis>();
        [ObservableProperty] private Axis[] _inventoryTrendYAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _inventoryDonutSeries = Array.Empty<ISeries>();

        // 2. Sales, Leads & Territory
        [ObservableProperty] private ISeries[] _salesFunnelSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _salesFunnelXAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _territoryPieSeries = Array.Empty<ISeries>();

        // 3. 3P Manufacturing & Batches
        [ObservableProperty] private ISeries[] _manufacturingThroughputSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _manufacturingXAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _manufacturingQualityPieSeries = Array.Empty<ISeries>();

        // 4. Sidebar Dynamic Series
        [ObservableProperty] private ISeries[] _remindersPieSeries = Array.Empty<ISeries>();
        [ObservableProperty] private ISeries[] _followupStagesBarSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _followupStagesYAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _matureStagesBarSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _matureStagesXAxes = Array.Empty<Axis>();
        [ObservableProperty] private ISeries[] _leadLabelsBarSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _leadLabelsYAxes = Array.Empty<Axis>();

        private bool _isInitializing = true;

        public DashboardViewModel(DashboardService dashboardService, IDialogService dialog, IServiceProvider serviceProvider, MainViewModel mainViewModel)
        {
            _dashboardService = dashboardService;
            _dialog = dialog;
            _serviceProvider = serviceProvider;
            _mainViewModel = mainViewModel;

            LoadUserPreferences();
            _isInitializing = false;
            _ = Refresh();
        }

        private void LoadUserPreferences()
        {
            var saved = UserPreferencesService.LoadDashboardPreferences();
            _globalViewMode = (GlobalDashboardViewMode)saved.AllTimeDataViewMode;
        }

        partial void OnGlobalViewModeChanged(GlobalDashboardViewMode value)
        {
            if (_isInitializing) return;
            UserPreferencesService.SaveDashboardPreferences(new UserDashboardSettings
            {
                AllTimeDataViewMode = (GlobalDashboardViewMode)value
            });
        }

        [RelayCommand]
        private void SetGlobalViewMode(GlobalDashboardViewMode mode)
        {
            GlobalViewMode = mode;
        }

        [RelayCommand]
        private async Task Refresh()
        {
            IsLoading = true;
            try
            {
                Data = await _dashboardService.GetExecutiveDashboardDataAsync(_currentActiveFilter);

                ReminderCounters = new ObservableCollection<KeyValuePair<string, int>>(Data.Sidebar.Reminders);
                FollowupStagesCounters = new ObservableCollection<KeyValuePair<string, int>>(Data.Sidebar.FollowupStages);
                MatureStagesCounters = new ObservableCollection<KeyValuePair<string, int>>(Data.Sidebar.MatureStages);
                LeadLabelsCounters = new ObservableCollection<KeyValuePair<string, int>>(Data.Sidebar.LeadLabels);

                PopulateExecutiveCharts(Data);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenFilter()
        {
            var filterResult = await _dialog.ShowFilterDialog();
            if (filterResult != null)
            {
                _currentActiveFilter = filterResult;
                IsFilterActive = true;
                LastUpdatedStatus = $"Filtered ({filterResult.PresetRange})";
                DataUpdatedStatus = $"Target: {filterResult.LeadHolder ?? "All Operations"}";
                await Refresh();
            }
        }

        [RelayCommand]
        private async Task ClearFilter()
        {
            _currentActiveFilter = null;
            IsFilterActive = false;
            DataUpdatedStatus = string.Empty;
            await Refresh();
        }

        private void PopulateProductAndInventoryCharts(ExecutiveDashboardData d)
        {
            var slateText = new SolidColorPaint(SKColor.Parse("#475569"))
            {
                SKTypeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
            };
            var dividerGrid = new SolidColorPaint(SKColor.Parse("#F1F5F9"));

            // Color Palette matching reference screenshot
            var bluePrimary = SKColor.Parse("#0052CC"); // Active/Healthy
            var slateAccent = SKColor.Parse("#8993A4"); // Slow Moving
            var coralAlert = SKColor.Parse("#FF5630"); // Near Low SKU
            var amberWarning = SKColor.Parse("#F59E0B"); // Near Expiry

            // ---------------------------------------------------------------------
            // A. PROPER MUTUALLY EXCLUSIVE DOUGHNUT RING (Sum = 100%)
            // ---------------------------------------------------------------------
            int totalCatalog = Math.Max(1, d.Inventory.TotalProducts);

            // Non-overlapping healthy base calculation
            int nearSku = d.Inventory.NearSkuAlertCount;
            int fastMoving = d.Inventory.FastMovingProducts;
            int slowMoving = Math.Max(0, totalCatalog - nearSku - fastMoving);

            InventoryDonutSeries = new ISeries[]
            {
        new PieSeries<double>
        {
            Name = "Slow Moving",
            Values = new double[] { Math.Round(((double)slowMoving / totalCatalog) * 100, 1) },
            Fill = new SolidColorPaint(slateAccent),
            InnerRadius = 95, // Defined hollow center ring
            DataLabelsPaint = slateText,
            DataLabelsSize = 11,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
        },
        new PieSeries<double>
        {
            Name = "Near Low SKU",
            Values = new double[] { Math.Round(((double)nearSku / totalCatalog) * 100, 1) },
            Fill = new SolidColorPaint(coralAlert),
            InnerRadius = 95,
            DataLabelsPaint = slateText,
            DataLabelsSize = 11,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
        },
        new PieSeries<double>
        {
            Name = "Fast Moving",
            Values = new double[] { Math.Round(((double)fastMoving / totalCatalog) * 100, 1) },
            Fill = new SolidColorPaint(bluePrimary),
            InnerRadius = 95,
            DataLabelsPaint = slateText,
            DataLabelsSize = 11,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
        }
            };

            // ---------------------------------------------------------------------
            // B. DUAL-AXIS LINE SPLINE GRAPH
            // ---------------------------------------------------------------------
            InventoryTrendLineSeries = new ISeries[]
            {
        new LineSeries<int>
        {
            Name = "Stock Volume Movement",
            Values = new int[] { d.Inventory.TotalCategoriesUsed * 5, d.Inventory.FastMovingProducts, d.Inventory.SlowMovingProducts, d.Inventory.NearSkuAlertCount, d.Inventory.TotalProducts },
            Stroke = new SolidColorPaint(bluePrimary) { StrokeThickness = 3 },
            Fill = null,
            GeometrySize = 0,
            LineSmoothness = 0.65,
            ScalesYAt = 0
        },
        new LineSeries<int>
        {
            Name = "Procurement Pipeline",
            Values = new int[] { d.Inventory.ActiveVendorsCount, d.Inventory.OpenVendorPurchaseOrders, 2, d.Inventory.OpenVendorPurchaseOrders * 2, d.Inventory.ActiveVendorsCount * 3 },
            Stroke = new SolidColorPaint(SKColor.Parse("#00B4D8")) { StrokeThickness = 3 },
            Fill = null,
            GeometrySize = 0,
            LineSmoothness = 0.65,
            ScalesYAt = 1
        }
            };

            InventoryTrendXAxes = new Axis[]
            {
        new Axis
        {
            Labels = new string[] { "Categories", "Fast Movers", "Slow Movers", "Near SKU", "Total Catalog" },
            LabelsRotation = 0,
            TextSize = 11,
            LabelsPaint = slateText,
            SeparatorsPaint = dividerGrid
        }
            };

            InventoryTrendYAxes = new Axis[]
            {
        new Axis
        {
            Name = "Stock Count",
            Position = LiveChartsCore.Measure.AxisPosition.Start,
            Labeler = val => val >= 1000 ? $"{(val / 1000):N0}K" : val.ToString("N0"),
            TextSize = 10,
            LabelsPaint = slateText,
            SeparatorsPaint = dividerGrid
        },
        new Axis
        {
            Name = "POs / Vendors",
            Position = LiveChartsCore.Measure.AxisPosition.End,
            ShowSeparatorLines = false,
            TextSize = 10,
            LabelsPaint = slateText
        }
            };
        }

        private void PopulateExecutiveCharts(ExecutiveDashboardData d)
        {
            var slateText = new SolidColorPaint(SKColor.Parse("#64748B"));
            var dividerGrid = new SolidColorPaint(SKColor.Parse("#F1F5F9"));

            // Color Palette Definition (Coupler.io UI styling)
            var bluePrimary = SKColor.Parse("#0052CC");
            var cyanAccent = SKColor.Parse("#00A3BF");
            var purpleAccent = SKColor.Parse("#6554C0");
            var slateAccent = SKColor.Parse("#8993A4");
            var coralAlert = SKColor.Parse("#FF5630");
            var greenSuccess = SKColor.Parse("#36B37E");
            var amberWarning = SKColor.Parse("#F59E0B");

            this.PopulateProductAndInventoryCharts(d);

            // =========================================================================
            // 2. SALES PIPELINE STAGE CALLOUT DOUGHNUT
            // =========================================================================
            double totalLeads = Math.Max(1, d.SalesPipeline.AllLeads);

            SalesFunnelSeries = new ISeries[]
            {
        new ColumnSeries<int>
        {
            Name = "Pipeline Volume",
            Values = new int[] { d.SalesPipeline.AllLeads, d.SalesPipeline.FollowupLeads, d.SalesPipeline.ActiveCustomers, Math.Max(0, d.SalesPipeline.ActiveCustomers - d.SalesPipeline.NoRepeatOrders) },
            Fill = new SolidColorPaint(bluePrimary),
            DataLabelsPaint = new SolidColorPaint(SKColor.Parse("#1E293B")),
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
        }
            };
            SalesFunnelXAxes = new Axis[]
            {
        new Axis
        {
            Labels = new string[] { "Total Leads", "In Follow-up", "Converted", "Repeat Buyers" },
            LabelsRotation = 0,
            TextSize = 11,
            LabelsPaint = slateText,
            SeparatorsPaint = dividerGrid
        }
            };

            double totalPincodes = Math.Max(1, d.Territory.CoveredPincodes + d.Territory.VacantPincodes);
            TerritoryPieSeries = new ISeries[]
            {
        new PieSeries<double>
        {
            Name = "Lead In (Open)",
            Values = new double[] { Math.Round((d.SalesPipeline.NewLeads / totalLeads) * 100, 2) },
            Fill = new SolidColorPaint(bluePrimary),
            InnerRadius = 60,
            DataLabelsPaint = slateText,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
        },
        new PieSeries<double>
        {
            Name = "In Follow-up",
            Values = new double[] { Math.Round((d.SalesPipeline.FollowupLeads / totalLeads) * 100, 2) },
            Fill = new SolidColorPaint(cyanAccent),
            InnerRadius = 60,
            DataLabelsPaint = slateText,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
        },
        new PieSeries<double>
        {
            Name = "Matured Accounts",
            Values = new double[] { Math.Round((d.SalesPipeline.ActiveCustomers / totalLeads) * 100, 2) },
            Fill = new SolidColorPaint(greenSuccess),
            InnerRadius = 60,
            DataLabelsPaint = slateText,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
        },
        new PieSeries<double>
        {
            Name = "Closed Lost (Dead)",
            Values = new double[] { Math.Round((d.SalesPipeline.DeadLeads / totalLeads) * 100, 2) },
            Fill = new SolidColorPaint(purpleAccent),
            InnerRadius = 60,
            DataLabelsPaint = slateText,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
        }
            };

            // =========================================================================
            // 3. 3P MANUFACTURING THROUGHPUT & QUALITY DOUGHNUT
            // =========================================================================
            ManufacturingThroughputSeries = new ISeries[]
            {
        new ColumnSeries<int>
        {
            Name = "Batches in Stage",
            Values = new int[] { d.Manufacturing.RunningBatches, d.Manufacturing.BatchesInFormulation, d.Manufacturing.BatchesInQaHold, d.Manufacturing.BatchesInPackaging, d.Manufacturing.ReadyForDispatch },
            Fill = new SolidColorPaint(purpleAccent),
            DataLabelsPaint = new SolidColorPaint(SKColor.Parse("#1E293B")),
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
        }
            };
            ManufacturingXAxes = new Axis[]
            {
        new Axis
        {
            Labels = new string[] { "Running", "Formulation", "QA Hold", "Packaging", "Ready" },
            LabelsRotation = 0,
            TextSize = 11,
            LabelsPaint = slateText,
            SeparatorsPaint = dividerGrid
        }
            };

            double totalBatches = Math.Max(1, d.Manufacturing.RunningBatches);
            int onTrackBatches = Math.Max(0, d.Manufacturing.RunningBatches - d.Manufacturing.DelayedBatchesAlert - d.Manufacturing.BatchesInQaHold);

            ManufacturingQualityPieSeries = new ISeries[]
            {
        new PieSeries<double>
        {
            Name = "On Schedule",
            Values = new double[] { Math.Round((onTrackBatches / totalBatches) * 100, 2) },
            Fill = new SolidColorPaint(greenSuccess),
            InnerRadius = 60,
            DataLabelsPaint = slateText,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
        },
        new PieSeries<double>
        {
            Name = "QA / Lab Hold",
            Values = new double[] { Math.Round((d.Manufacturing.BatchesInQaHold / totalBatches) * 100, 2) },
            Fill = new SolidColorPaint(amberWarning),
            InnerRadius = 60,
            DataLabelsPaint = slateText,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
        },
        new PieSeries<double>
        {
            Name = "Delayed (SLA Alert)",
            Values = new double[] { Math.Round((d.Manufacturing.DelayedBatchesAlert / totalBatches) * 100, 2) },
            Fill = new SolidColorPaint(coralAlert),
            InnerRadius = 60,
            DataLabelsPaint = slateText,
            DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
            DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
        }
            };

            // =========================================================================
            // 4. SIDEBAR STAGE DOUGHNUTS & RANK BARS
            // =========================================================================
            // Reminders Callout Doughnut
            var remList = d.Sidebar.Reminders.Where(k => !k.Key.StartsWith("All", StringComparison.OrdinalIgnoreCase)).ToList();
            double totalReminders = Math.Max(1, remList.Sum(x => x.Value));

            RemindersPieSeries = remList.Select(x => new PieSeries<double>
            {
                Name = x.Key,
                Values = new double[] { Math.Round((x.Value / totalReminders) * 100, 2) },
                Fill = x.Key.Equals("New", StringComparison.OrdinalIgnoreCase) ? new SolidColorPaint(bluePrimary) : new SolidColorPaint(cyanAccent),
                InnerRadius = 50,
                DataLabelsPaint = slateText,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Outer,
                DataLabelsFormatter = point => $"{point.Context.Series.Name} {point.Model:N1}%"
            }).ToArray();

            // Followup Stages Horizontal Bar
            var folList = d.Sidebar.FollowupStages.Where(k => !k.Key.StartsWith("All", StringComparison.OrdinalIgnoreCase)).ToList();
            FollowupStagesBarSeries = new ISeries[]
            {
        new RowSeries<int>
        {
            Values = folList.Select(x => x.Value).ToArray(),
            Fill = new SolidColorPaint(amberWarning),
            DataLabelsPaint = slateText,
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End
        }
            };
            FollowupStagesYAxes = new Axis[] { new Axis { Labels = folList.Select(x => x.Key).ToArray(), TextSize = 10, LabelsPaint = slateText } };

            // Mature Stages Column Bar
            var matList = d.Sidebar.MatureStages.Where(k => !k.Key.StartsWith("All", StringComparison.OrdinalIgnoreCase)).ToList();
            MatureStagesBarSeries = new ISeries[]
            {
        new ColumnSeries<int>
        {
            Values = matList.Select(x => x.Value).ToArray(),
            Fill = new SolidColorPaint(greenSuccess),
            DataLabelsPaint = slateText,
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top
        }
            };
            MatureStagesXAxes = new Axis[] { new Axis { Labels = matList.Select(x => x.Key).ToArray(), LabelsRotation = 0, TextSize = 10, LabelsPaint = slateText } };

            // Lead Labels Horizontal Bar
            var lblList = d.Sidebar.LeadLabels.Where(k => !k.Key.StartsWith("All", StringComparison.OrdinalIgnoreCase)).ToList();
            LeadLabelsBarSeries = new ISeries[]
            {
        new RowSeries<int>
        {
            Values = lblList.Select(x => x.Value).ToArray(),
            Fill = new SolidColorPaint(cyanAccent),
            DataLabelsPaint = slateText,
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End
        }
            };
            LeadLabelsYAxes = new Axis[] { new Axis { Labels = lblList.Select(x => x.Key).ToArray(), TextSize = 10, LabelsPaint = slateText } };
        }

        [RelayCommand]
        private async Task NavigateFromCounter(DashboardTargetView target)
        {
            try
            {
                LoadingService.Show("Loading view... Please wait.");
                object? targetViewModel = target switch
                {
                    DashboardTargetView.AllLeads or DashboardTargetView.OpenLeads or DashboardTargetView.FollowupLeads or DashboardTargetView.NoFollowupLeads or DashboardTargetView.DeadLeads => _serviceProvider.GetRequiredService<LeadViewModel>(),
                    DashboardTargetView.Customers or DashboardTargetView.NoUpdation7Days or DashboardTargetView.NoRepeatOrders or DashboardTargetView.NoOrders30Days or DashboardTargetView.BelowTargetCustomers => _serviceProvider.GetRequiredService<MaturedLeadsViewModel>(),
                    DashboardTargetView.ProductsList or DashboardTargetView.CategoriesList or DashboardTargetView.NewProducts or DashboardTargetView.FastMovingProducts or DashboardTargetView.SlowMovingProducts or DashboardTargetView.NearSkuProducts or DashboardTargetView.NearExpiryBatches or DashboardTargetView.SkippedProducts => _serviceProvider.GetRequiredService<InventoryViewModel>(),
                    DashboardTargetView.AllOrders or DashboardTargetView.NewOrders or DashboardTargetView.RepeatedOrders or DashboardTargetView.UnpaidOrders or DashboardTargetView.PartiallyPaidOrders => _serviceProvider.GetRequiredService<AllOrdersViewModel>(),
                    _ => null
                };

                if (targetViewModel == null) return;

                if (targetViewModel is IDashboardFilterable filterableVm)
                {
                    filterableVm.ApplyDashboardFilter(IsFilterActive ? _currentActiveFilter : null, target);
                }

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    _mainViewModel.CurrentView = targetViewModel;
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex.Message}");
            }
            finally
            {
                LoadingService.Hide();
            }
        }
    }
}

