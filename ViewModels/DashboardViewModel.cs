using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

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

        public DashboardViewModel(LeadService service, IDialogService dialog, IServiceProvider serviceProvider, MainViewModel mainViewModel)
        {
            _service = service;
            _dialog = dialog;
            _serviceProvider = serviceProvider;
            _mainViewModel = mainViewModel;
            RefreshCommand.Execute(null);
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

                // Optional: Update 'Last Updated' timestamp
                LastUpdatedStatus = $"Filtered by {filter.LeadHolder ?? "All"} ({filter.PresetRange})";
                DataUpdatedStatus = $"Filtered Target Group: {filter.LeadHolder ?? "All Staff Operations"} ({filter.PresetRange})";
            }
            finally
            {
                IsLoading = false;
            }
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

