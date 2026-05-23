using CallMan.Dialogs;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace CallMan.ViewModels
{
    public partial class OccupiedLocationViewModel : ObservableObject
    {
        private readonly OccupiedLocationService _service;

        [ObservableProperty] private string _searchText = string.Empty;
        private List<OccupiedLocation> _allLoadedLocations = new();

        [ObservableProperty] private ObservableCollection<OccupiedLocation> _locations;
        [ObservableProperty] private ObservableCollection<StateStat> _stateStats = new();
        [ObservableProperty] private StateStat _selectedState;

        public OccupiedLocationViewModel(OccupiedLocationService service)
        {
            _service = service;
            LoadDataCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadData()
        {
            var stats = await _service.GetStateStatsAsync();
            StateStats = new ObservableCollection<StateStat>(
            stats.Where(s => s.MaturedCount > 0) // Filters out any state with 0 matured leads
         .Select(s => new StateStat
         {
             State = s.State,
             MaturedCount = (int)s.MaturedCount,
             TotalLeads = (int)s.TotalLeads
         })
);
        }

        // The logic that runs automatically on every keystroke
        partial void OnSearchTextChanged(string value)
        {
            ApplyLiveFilter();
        }

        private void ApplyLiveFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                Locations = new ObservableCollection<OccupiedLocation>(_allLoadedLocations);
                return;
            }

            var filtered = _allLoadedLocations.Where(l =>
                l.CustomerName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                l.FirmName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                l.Pincode?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                l.WorkingArea?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                                l.Phone?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                                l.LeadHolder?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                                l.Senior?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                                l.AssignedDivisions.Any(d => d.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true)
            ).ToList();

            Locations = new ObservableCollection<OccupiedLocation>(filtered);
        }

        [RelayCommand]
        private async Task ShowSummary(OccupiedLocation location)
        {
            var vm = App.ServiceProvider.GetRequiredService<CustomerSummaryViewModel>();

            await vm.InitializeAsync(location);

            var window = new CustomerSummaryView
            {
                DataContext = vm,
                Owner = App.Current.MainWindow
            };            

            // 5. Open as Modal
            if (window.ShowDialog() == true)
            {
                // If data was updated (e.g., status changed to Matured or Dead), refresh the grid
                LoadDataCommand.Execute(null);
            }
        }

        // This partial method runs automatically when SelectedState changes
        partial void OnSelectedStateChanged(StateStat? value)
        {
            _ = RefreshLocations();
        }

        [RelayCommand]
        public async Task RefreshLocations()
        {
            // Fetch only 'Matured' leads for the selected state
            var data = (await _service.GetOccupiedLocationsAsync(SelectedState?.State)).ToList();
            _allLoadedLocations = data;
            ApplyLiveFilter();
        }
    }
}
