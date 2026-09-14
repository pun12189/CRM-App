using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tijori.Interfaces;
using Tijori.Models;

namespace Tijori.ViewModels
{
    public partial class ActionChipItem : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private bool _isSelected;
    }

    public partial class CreateWorkflowDialogViewModel : ObservableObject
    {
        private readonly IWorkflowDataService _dataService;

        [ObservableProperty] private Workflow _currentWorkflow = new();
        [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private bool _isDaysInputVisible;

        [ObservableProperty]
        private ObservableCollection<string> _eventOptions = new()
        {
            "Lead Create", "Lead Fetched", "Lead Matured",
            "New Proforma", "New Order", "Repeat Orders",
            "Followup stages", "Mature stages", "Brand Missed",
            "Update Proforma", "Update Order",
            "No updation since", "No order since",
            "Balance Payment", "Product/Service Renewal",
            "Birthday", "Anniversary"
        };

        [ObservableProperty]
        private ObservableCollection<ActionChipItem> _availableActions = new()
        {
            new ActionChipItem { Name = "Send Whatsapp" },
            new ActionChipItem { Name = "Send Email" },
            new ActionChipItem { Name = "Send Notification" }
        };

        [ObservableProperty] private ObservableCollection<WorkflowTag> _availableTags = new();
        [ObservableProperty] private bool _isTagPopupOpen;

        public CreateWorkflowDialogViewModel(IWorkflowDataService dataService, Workflow? existing = null)
        {
            _dataService = dataService;

            if (existing != null)
            {
                IsEditMode = true;
                CurrentWorkflow = existing;
                EvaluateDaysVisibility(existing.EventName);
                SyncActionChipsFromModel();
            }
            else
            {
                CurrentWorkflow = new Workflow { EventName = "Lead Create" };
                EvaluateDaysVisibility(CurrentWorkflow.EventName);
            }

            _ = LoadTagsAsync();
        }

        partial void OnCurrentWorkflowChanged(Workflow value)
        {
            if (value != null)
            {
                value.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(Workflow.EventName))
                    {
                        EvaluateDaysVisibility(CurrentWorkflow.EventName);
                        _ = LoadTagsAsync();
                    }
                };
            }
        }

        private void EvaluateDaysVisibility(string? eventName)
        {
            // Dynamic Days Field applies to time-based schedules
            IsDaysInputVisible = eventName is "No updation since"
                                          or "No order since"
                                          or "Product/Service Renewal";
        }

        private void SyncActionChipsFromModel()
        {
            foreach (var act in AvailableActions)
            {
                if (act.Name == "Send Whatsapp") act.IsSelected = CurrentWorkflow.SendWhatsApp;
                if (act.Name == "Send Email") act.IsSelected = CurrentWorkflow.SendEmail;
                if (act.Name == "Send Notification") act.IsSelected = CurrentWorkflow.SendNotification;
            }
        }

        [RelayCommand]
        private void ToggleAction(ActionChipItem item)
        {
            item.IsSelected = !item.IsSelected;
            if (item.Name == "Send Whatsapp") CurrentWorkflow.SendWhatsApp = item.IsSelected;
            if (item.Name == "Send Email") CurrentWorkflow.SendEmail = item.IsSelected;
            if (item.Name == "Send Notification") CurrentWorkflow.SendNotification = item.IsSelected;
        }

        [RelayCommand]
        private void RemoveAction(string actionName)
        {
            var match = AvailableActions.FirstOrDefault(a => a.Name == actionName);
            if (match != null)
            {
                match.IsSelected = false;
                if (actionName == "Send Whatsapp") CurrentWorkflow.SendWhatsApp = false;
                if (actionName == "Send Email") CurrentWorkflow.SendEmail = false;
                if (actionName == "Send Notification") CurrentWorkflow.SendNotification = false;
            }
        }

        private async Task LoadTagsAsync()
        {
            var tags = await _dataService.GetTagsForEventAsync(CurrentWorkflow.EventName);
            AvailableTags = new ObservableCollection<WorkflowTag>(tags);
        }

        [RelayCommand]
        private async Task SaveAsync(Window window)
        {
            if (string.IsNullOrWhiteSpace(CurrentWorkflow.WorkflowName))
            {
                MessageBox.Show("Please enter a Title / Subject for the rule.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!CurrentWorkflow.SendWhatsApp && !CurrentWorkflow.SendEmail && !CurrentWorkflow.SendNotification)
            {
                MessageBox.Show("Please select at least one action (WhatsApp, Email, or Notification).", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool saved = await _dataService.SaveWorkflowAsync(CurrentWorkflow);
            if (saved)
            {
                window.DialogResult = true;
                window.Close();
            }
            else
            {
                MessageBox.Show("Failed to save workflow.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
