using CallMan.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CallMan.ViewModels
{
    public partial class FilterViewModel : ObservableObject
    {
        public event Action<DashboardFilter?>? RequestClose;

        [ObservableProperty] private ObservableCollection<string> _leadHolders = new();
        [ObservableProperty] private string? _selectedLeadHolder;
        [ObservableProperty] private string _selectedPreset = "Today";
        [ObservableProperty] private DateTime _startDate = DateTime.Today;
        [ObservableProperty] private DateTime _endDate = DateTime.Today;

        public FilterViewModel(IEnumerable<string> holders)
        {
            LeadHolders = new ObservableCollection<string>(holders);
        }

        [RelayCommand]
        private void SetPreset(string preset)
        {
            SelectedPreset = preset + ": ";
            switch (preset)
            {
                case "Today": StartDate = EndDate = DateTime.Today; SelectedPreset += StartDate.ToShortDateString(); break;
                case "Yesterday": StartDate = EndDate = DateTime.Today.AddDays(-1); SelectedPreset += StartDate.ToShortDateString();  break;
                case "This Week": StartDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek); EndDate = DateTime.Today; SelectedPreset += $"{StartDate.ToShortDateString()} - {EndDate.ToShortDateString()}"; break;
                case "Last Week": StartDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek - 7); EndDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek - 1); SelectedPreset += $"{StartDate.ToShortDateString()} - {EndDate.ToShortDateString()}"; break;
                case "This Month": StartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); EndDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month)); SelectedPreset += $"{StartDate.ToShortDateString()} - {EndDate.ToShortDateString()}"; break;
                case "Last Month": StartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month - 1, 1); EndDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month - 1, DateTime.DaysInMonth(DateTime.Today.Year, DateTime.Today.Month - 1)); SelectedPreset += $"{StartDate.ToShortDateString()} - {EndDate.ToShortDateString()}"; break;
            }
        }

        [RelayCommand]
        private void ApplyFilter()
        {
            var result = new DashboardFilter
            {
                LeadHolder = SelectedLeadHolder,
                FromDate = StartDate,
                ToDate = EndDate,
                PresetRange = SelectedPreset
            };
            RequestClose?.Invoke(result);
        }
    }
}
