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
    public partial class AddLeadDialogViewModel : ObservableObject
    {
        private readonly LeadService _leadService;
        private readonly IUserSession _session;
        private readonly WorkflowEngine _workflowEngine;
        private readonly SettingService _settingService;    

        [ObservableProperty]
        private Lead _newLead = new();

        [ObservableProperty]
        private string _tempFieldName = "";

        [ObservableProperty]
        private string _tempFieldValue = "";

        private bool _isEditMode;

        // Event to close the window from ViewModel
        public event Action<bool>? RequestClose;

        // Small helper class
        public record CustomFieldEntry(string Key, string Value);

        // In ViewModel
        [ObservableProperty]
        private ObservableCollection<CustomFieldEntry> _visibleCustomFields = new();

        [ObservableProperty]
        private ObservableCollection<SettingItem> _tagsList = new();

        [ObservableProperty]
        private ObservableCollection<SettingItem> _labelsList = new();

        [ObservableProperty]
        private ObservableCollection<SettingItem> _sourceList = new();

        [ObservableProperty]
        private SettingItem _selectedLabelItem;

        public AddLeadDialogViewModel(LeadService leadService, IUserSession session, WorkflowEngine workflowEngine, SettingService settingService)
        {
            _leadService = leadService;
            _session = session;
            _isEditMode = false;
            _workflowEngine = workflowEngine;
            _settingService = settingService;
            NewLead.Status = "New";
        // Initialize with default status

        NewLead.LeadHolder = _session.CurrentUser;

            _ = LoadSettingsAsync();
        }

        public void Initialize(Lead? existingLead)
        {
            if (existingLead != null)
            {
                NewLead = existingLead;
                _isEditMode = true;
                // Load address fields from existingLead if they aren't auto-bound
            }
        }

        private async Task LoadSettingsAsync()
        {
            // Assuming your DataService has methods to fetch these from Admin tables
            var sources = await _settingService.GetSettingsAsync("LeadSources");
            var tags = await _settingService.GetSettingsAsync("LeadTags");
            var labels = await _settingService.GetSettingsAsync("LeadLabels");

            SourceList = new ObservableCollection<SettingItem>(sources);
            TagsList = new ObservableCollection<SettingItem>(tags);
            LabelsList = new ObservableCollection<SettingItem>(labels);
        }

        [RelayCommand]
        private void AddCustomField()
        {
            if (!string.IsNullOrWhiteSpace(TempFieldName))
            {
                // Add to the dictionary in the Model
                NewLead.CustomFields[TempFieldName] = TempFieldValue;

                // Trigger UI refresh for the Dictionary summary
                OnPropertyChanged(nameof(NewLead));

                // 2. Add to the ObservableCollection for the UI to SEE it
                VisibleCustomFields.Add(new CustomFieldEntry(TempFieldName, TempFieldValue));

                // Clear inputs
                TempFieldName = "";
                TempFieldValue = "";
            }
        }

        [RelayCommand]
        private async Task SaveLead()
        {
            if (string.IsNullOrWhiteSpace(NewLead.CustomerName))
            {
                // You could add a StatusMessage property here for validation errors
                return;
            }

            try
            {
                if (_isEditMode)
                {
                    await _leadService.UpdateLeadAsync(NewLead);
                }
                else
                {
                    // Pass a default history message for the first entry
                    string initialLog = $"Lead generated as '{NewLead.Status}' type.";

                    int newLeadId = await _leadService.SaveLeadAsync(NewLead, initialLog, _session.CurrentUser);
                    await _workflowEngine.EnqueueEventAsync("OnLeadCreated", newLeadId, "Lead");
                }               

                // Close window with 'True' result
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                // Handle DB errors here
            }
        }

        partial void OnSelectedLabelItemChanged(SettingItem value)
        {
            if (value != null && !NewLead.LeadLabels.Contains(value.Name))
            {
                NewLead.LeadLabels.Add(value.Name);
                // Clear selection so the user can pick the same one again if they delete it
                SelectedLabelItem = null;
            }
        }

        [RelayCommand]
        public void RemoveLabel(string labelName)
        {
            NewLead.LeadLabels.Remove(labelName);
        }
    }
}
