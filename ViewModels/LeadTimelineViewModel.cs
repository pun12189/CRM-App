using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.ViewModels
{
    public partial class LeadTimelineViewModel : ObservableObject
    {
        private readonly LeadService _leadService;
        private readonly int _leadId;

        [ObservableProperty]
        private ObservableCollection<LeadHistoryEntry> _historyItems = new();

        public event Action RequestClose;

        public LeadTimelineViewModel(LeadService service, int leadId)
        {
            _leadService = service;
            _leadId = leadId;
            LoadFullHistory();
        }

        private async void LoadFullHistory()
        {
            // Use the existing service method we built previously
            var timeline = await _leadService.GetHistoryByLeadIdAsync(_leadId);
            HistoryItems = new ObservableCollection<LeadHistoryEntry>(timeline);
        }
    }
}
