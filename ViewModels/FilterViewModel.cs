using CallMan.Models;
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
            SelectedPreset = preset;
            switch (preset)
            {
                case "Today": StartDate = EndDate = DateTime.Today; break;
                case "Yesterday": StartDate = EndDate = DateTime.Today.AddDays(-1); break;
                case "This Week": StartDate = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek); EndDate = DateTime.Today; break;
                case "This Month": StartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); EndDate = DateTime.Today; break;
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
