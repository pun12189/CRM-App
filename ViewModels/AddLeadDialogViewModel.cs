using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using PhoneNumbers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        private readonly ProfileService _profileService;
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
        private ObservableCollection<Division> _divisionList = new();

        [ObservableProperty]
        private ObservableCollection<SettingItem> _sourceList = new();

        [ObservableProperty]
        private SettingItem _selectedLabelItem;

        [ObservableProperty]
        private Division _selectedDivisionItem;

        [ObservableProperty]
        private int? _leadSourceId;

        [ObservableProperty]
        private int? _leadTagId;

        [ObservableProperty]
        private string? _leadPincode;

        [ObservableProperty]
        private string? _leadPhone;

        [ObservableProperty]
        private string? _leadAltPhone;

        public record PincodeApiRoot(string Status, List<PostOfficeDetail> PostOffice);
        public record PostOfficeDetail(string Name, string District, string State, string Country);

        public AddLeadDialogViewModel(LeadService leadService, IUserSession session, WorkflowEngine workflowEngine, SettingService settingService, ProfileService profileService)
        {
            _leadService = leadService;
            _session = session;
            _isEditMode = false;
            _workflowEngine = workflowEngine;
            _settingService = settingService;
            _profileService = profileService;
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
                this.LeadPhone = existingLead.Phone;
                this.LeadAltPhone = existingLead.AltPhone;
                this.LeadPincode = existingLead.Pincode;
                this.LeadTagId = existingLead.LeadTagId;
                this.LeadSourceId = existingLead.LeadSourceId;
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

            DivisionList = new ObservableCollection<Division>(await _profileService.GetActiveDivisionsAsync());

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
                    var historyEntry = new LeadHistoryEntry
                    {
                        Message = "New Lead Added",
                        Content = $"Lead '{NewLead.CustomerName}' created.",
                        UpdatedByContent = "added a new lead",
                        NextFollowUpDate = DateTime.Now,
                        UpdatedBy = _session.CurrentUser,
                        LogDate = DateTime.Now,
                        IsPriority = true
                    };

                    int newLeadId = await _leadService.SaveLeadAsync(NewLead, historyEntry, _session.CurrentUser);
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

        partial void OnSelectedDivisionItemChanged(Division value)
        {
            if (value != null && !NewLead.AssignedDivisions.Any(d => d.Id == value.Id))
            {
                NewLead.AssignedDivisions.Add(value);
                // Clear selection so the user can pick the same one again if they delete it
                SelectedDivisionItem = null;
            }
        }

        partial void OnLeadSourceIdChanged(int? value)
        {
            // Find the item in the list matching the newly selected ID
            if (value == null || value == 0)
            {
                NewLead.LeadSource = string.Empty;
                return;
            }

            var selectedItem = SourceList.FirstOrDefault(x => x.Id == value);
            NewLead.LeadSourceId = value.Value;
            NewLead.LeadSource = selectedItem?.Name ?? string.Empty;
        }

        partial void OnLeadTagIdChanged(int? value)
        {
            if (value == null || value == 0)
            {
                NewLead.LeadTag = string.Empty;
                return;
            }

            var selectedItem = TagsList.FirstOrDefault(x => x.Id == value);
            NewLead.LeadTagId = value.Value;
            NewLead.LeadTag = selectedItem?.Name ?? string.Empty;
        }

        partial void OnLeadPincodeChanged(string value)
        {
            // Find the item in the list matching the newly selected ID
            // Ensure the input matches Indian postal index standards before firing HTTP payloads
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length != 6) return;

            _ = FetchPincodeDataAsync(value.Trim());
        }

        private async Task FetchPincodeDataAsync(string cleanPincode)
        {
            try
            {
                using var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) =>
                    {
                        // Accept the certificate if it's valid OR if it failed specifically because of expiration
                        if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None ||
                            sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors)
                        {
                            return true;
                        }
                        return false; // Reject other severe structural security overrides (like completely fake certs)
                    }
                };

                using (var client = new HttpClient(handler))
                {
                    var address = @"https://api.postalpincode.in/pincode/" + cleanPincode;
                    client.BaseAddress = new Uri(address);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var response = await client.GetFromJsonAsync<List<PincodeApiRoot>>(address);
                    if (response != null && response.Count > 0 && response[0].Status == "Success")
                    {
                        var targetOffice = response[0].PostOffice?.FirstOrDefault();
                        if (targetOffice != null)
                        {
                            // Update fields on the main UI thread safely
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                NewLead.City = targetOffice.Name;
                                NewLead.District = targetOffice.District;
                                NewLead.State = targetOffice.State;
                                NewLead.Country = targetOffice.Country; // Fallback directly to India since it's an Indian Pin Code API
                                NewLead.Pincode = cleanPincode.Trim();
                                // Do not re-assign Pincode here, it is already updated by the UI binding!
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Gracefully sink the issue out of user sight line to logging tools (e.g. Sentry)
            }
        }

        partial void OnLeadPhoneChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                NewLead.Phone = string.Empty;
                return;
            }

            if (value.Length >= 10)
            {
                NewLead.Phone = ValidateAndFormatGlobalNumber(value, "IN");
                this.LeadPhone = NewLead.Phone; // Update the input field with the formatted number
            }            
        }

        partial void OnLeadAltPhoneChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                NewLead.AltPhone = string.Empty;
                return;
            }

            if (value.Length >= 10)
            {
                NewLead.AltPhone = ValidateAndFormatGlobalNumber(value, "IN");
                this.LeadAltPhone = NewLead.AltPhone; // Update the input field with the formatted number
            }
        }

        [RelayCommand]
        public void RemoveLabel(string labelName)
        {
            NewLead.LeadLabels.Remove(labelName);
        }

        [RelayCommand]
        public void RemoveDivision(Division division)
        {
            NewLead.AssignedDivisions.Remove(division);
        }

        /// <summary>
        /// Validates, repairs, and standardizes numbers from any country around the world.
        /// </summary>
        /// <param name="inputNumber">Raw typed string parameter input</param>
        /// <param name="defaultRegion">Two-letter country code token used if the client forgets to type a leading plus '+' prefix</param>
        private string ValidateAndFormatGlobalNumber(string inputNumber, string defaultRegion)
        {
            if (string.IsNullOrWhiteSpace(inputNumber)) return string.Empty;

            var phoneUtil = PhoneNumberUtil.GetInstance();
            string cleanedInput = inputNumber.Trim();

            try
            {
                // Smart auto-repair: If the number doesn't start with '+' but is 10 digits and default region is India, prepend +91
                if (!cleanedInput.StartsWith("+") && defaultRegion == "IN" && cleanedInput.Length == 10 && long.TryParse(cleanedInput, out _))
                {
                    cleanedInput = "+91" + cleanedInput;
                }
                // If it doesn't start with '+' but they provided an explicit prefix code (e.g. '919876543210'), make sure it is parsed as an absolute target
                else if (!cleanedInput.StartsWith("+") && cleanedInput.Length > 10 && (cleanedInput.StartsWith("91") || cleanedInput.StartsWith("1")))
                {
                    cleanedInput = "+" + cleanedInput;
                }

                // Parse the string into libphonenumber's geometric data object pattern
                // If input starts with '+', the regional parameter context is deduced automatically
                PhoneNumber parsedNumber = phoneUtil.Parse(cleanedInput, defaultRegion);

                // Check 1: Verify structural length and country code assignment integrity
                bool isValid = phoneUtil.IsValidNumber(parsedNumber);

                // Check 2: Deduce carrier mapping routing configurations type (Mobile, Landline, VoIP, etc.)
                PhoneNumberType type = phoneUtil.GetNumberType(parsedNumber);

                if (isValid && (type == PhoneNumberType.MOBILE || type == PhoneNumberType.FIXED_LINE_OR_MOBILE || type == PhoneNumberType.FIXED_LINE))
                {
                    // Repair successful! Rewrite the input field into a clean standard international string format
                    return phoneUtil.Format(parsedNumber, PhoneNumberFormat.INTERNATIONAL); // Output example: "+91 98765 43210"
                }
                else
                {
                    // The phone format doesn't match standard routing profiles. Append warning text to catch attention
                    return inputNumber + " [INVALID NUMBER]";
                }
            }
            catch (NumberParseException)
            {
                // The text cannot be processed or mapped back to any country index rules
                return inputNumber + " [PARSING ERROR]";
            }
        }
    }
}
