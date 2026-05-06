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

        public AddLeadDialogViewModel(LeadService leadService, IUserSession session)
        {
            _leadService = leadService;
            _session = session;
            _isEditMode = false;
            NewLead.Status = "New";
        // Initialize with default status

        NewLead.LeadHolder = _session.CurrentUser;
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

                    await _leadService.SaveLeadAsync(NewLead, initialLog, _session.CurrentUser);
                }               

                // Close window with 'True' result
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                // Handle DB errors here
            }
        }
    }
}
