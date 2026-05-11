using CallMan.Interfaces;
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
    public partial class WorkflowViewModel : ObservableObject
    {
        private readonly IWorkflowDataService _dataService;

        [ObservableProperty] private Workflow _newWorkflow = new();
        [ObservableProperty] private ObservableCollection<Workflow> _workflows;
        [ObservableProperty] private ObservableCollection<string> _eventList;
        [ObservableProperty] private ObservableCollection<WorkflowTag> _availableTags;
        [ObservableProperty] private bool _isTagPopupOpen;

        // The service is injected here via DI
        public WorkflowViewModel(IWorkflowDataService dataService)
        {
            _dataService = dataService;
            EventList = new ObservableCollection<string> { "OnLeadCreated", "OnOrderPlaced", "OnCustomerInactivity" };
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var list = await _dataService.GetAllWorkflowsAsync();
            Workflows = new ObservableCollection<Workflow>(list);

            var tags = await _dataService.GetTagsByEventAsync("OnLeadCreated");
            AvailableTags = new ObservableCollection<WorkflowTag>(tags);
        }

        [RelayCommand]
        private async Task SaveWorkflow()
        {
            // Simple validation
            if (string.IsNullOrEmpty(NewWorkflow.EventName)) return;

            bool success = await _dataService.SaveWorkflowAsync(NewWorkflow);
            if (success)
            {
                NewWorkflow = new Workflow(); // Reset form
                await InitializeAsync(); // Refresh list
            }
        }
    }
}
