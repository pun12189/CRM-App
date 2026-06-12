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

namespace CallMan.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly IDialogService _dialog;

        [ObservableProperty] private object? _selectedTabContent;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _lastUpdatedStatus = "Last Updated: Just Now";
        [ObservableProperty] private string _dataUpdatedStatus;

        // Stats Counters
        [ObservableProperty] private DashboardStats _stats;

        // This property controls the button's visibility
        [ObservableProperty] private bool _isFilterActive;

        // ====================================================================
        // NEW ADDITIONS: SIDEBAR STAGE SUMMARY COLLECTIONS
        // ====================================================================
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _reminderCounters = new();
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _followupStagesCounters = new();
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _matureStagesCounters = new();
        [ObservableProperty] private ObservableCollection<KeyValuePair<string, int>> _leadLabelsCounters = new();

        public DashboardViewModel(LeadService service, IDialogService dialog)
        {
            _service = service;
            _dialog = dialog;
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
                DataUpdatedStatus = $"Filtered by {filter.LeadHolder ?? "All "} ({filter.PresetRange})";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
