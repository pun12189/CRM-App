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
        [ObservableProperty] private string _dataUpdatedStatus = "All Time Data";

        // Stats Counters
        [ObservableProperty] private DashboardStats _stats;

        // This property controls the button's visibility
        [ObservableProperty] private bool _isFilterActive;

        // Financial Summaries for Expanders
        [ObservableProperty] private ObservableCollection<PaymentReminder> _reminders = new();

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

                // 2. Fetch Payment Reminders for the Right Sidebar
                var remindersData = await _service.GetPaymentRemindersAsync();
                Reminders = new ObservableCollection<PaymentReminder>(remindersData);

                // 3. Logic for Followup Stages (Mock data or DB call)
                // You can add logic here to count leads in different categories
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
            DataUpdatedStatus = "All Time Data";
            IsFilterActive = false;
        }

        private async Task RefreshDashboardWithFilter(DashboardFilter filter)
        {
            IsLoading = true;
            try
            {
                Stats = await _service.GetDashboardStatsFilteredAsync(filter);                

                // Optional: Update 'Last Updated' timestamp
                LastUpdatedStatus = $"Filtered by {filter.LeadHolder ?? "All"} ({filter.PresetRange})";
                DataUpdatedStatus = $"All Time Data: Filtered by {filter.LeadHolder ?? "All "} ({filter.PresetRange})";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
