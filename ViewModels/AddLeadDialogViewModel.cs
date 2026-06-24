using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhoneNumbers;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace CallMan.ViewModels
{
    public partial class AddLeadDialogViewModel : ObservableObject
    {
        private readonly LeadService _leadService;
        private readonly IUserSession _session;
        private readonly WorkflowEngine _workflowEngine;
        private readonly SettingService _settingService;
        private readonly ProfileService _profileService;
        private readonly CustomFieldService _customFieldService;
        [ObservableProperty]
        private Lead _newLead = new();

        [ObservableProperty]
        private string _tempFieldName = "";

        [ObservableProperty]
        private string _tempFieldValue = "";

        private bool _isEditMode;

        private bool _isCustomerMode;

        // Event to close the window from ViewModel
        public event Action<bool>? RequestClose;

        // Small helper class
        public record CustomFieldEntry(string Key, string Value);

        [ObservableProperty] private ObservableCollection<CustomFieldInputValue> _dynamicLeadFields = new();

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

        [ObservableProperty]
        private string? _leadEmail;

        [ObservableProperty] private bool _isPhoneDuplicate;
        [ObservableProperty] private bool _isAltPhoneDuplicate;
        [ObservableProperty] private bool _isEmailDuplicate;
        [ObservableProperty] private bool _isEmailMalformed;

        private string _originalPhone = string.Empty;
        private string _originalAltPhone = string.Empty;
        private string _originalEmail = string.Empty;

        private bool _isProcessingPhoneUpdate;

        [ObservableProperty] private string _validationMessage = string.Empty;

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public record PincodeApiRoot(string Status, List<PostOfficeDetail> PostOffice);
        public record PostOfficeDetail(string Name, string District, string State, string Country);

        public AddLeadDialogViewModel(LeadService leadService, IUserSession session, WorkflowEngine workflowEngine, SettingService settingService, ProfileService profileService, CustomFieldService customFieldService)
        {
            _leadService = leadService;
            _session = session;
            _isEditMode = false;
            _workflowEngine = workflowEngine;
            _customFieldService = customFieldService;
            _settingService = settingService;
            _profileService = profileService;
            NewLead.Status = "New";
            // Initialize with default status

            NewLead.LeadHolder = _session.CurrentUser;

            _ = LoadSettingsAndCustomFieldsAsync();
        }

        public async Task Initialize(Lead? existingLead, bool IsCustomer = false)
        {
            if (existingLead != null)
            {
                NewLead = existingLead;               

                _originalPhone = existingLead.Phone ?? string.Empty;
                _originalAltPhone = existingLead.AltPhone ?? string.Empty;
                _originalEmail = existingLead.Email ?? string.Empty;

                _isEditMode = true;

                this.LeadPhone = existingLead.Phone;
                this.LeadAltPhone = existingLead.AltPhone;
                this.LeadPincode = existingLead.Pincode;
                this.LeadTagId = existingLead.LeadTagId;
                this.LeadSourceId = existingLead.LeadSourceId;
                // Load address fields from existingLead if they aren't auto-bound                
                var savedValues = await _leadService.GetCustomFieldValuesForLeadAsync(existingLead.LeadId, IsCustomer ? "Customer" : "Lead");
                foreach (var inputField in DynamicLeadFields)
                {
                    if (savedValues.TryGetValue(inputField.FieldId, out var val))
                    {
                        inputField.FieldValue = val;

                        // IF THE TYPE IS CALENDAR, POPULATE BOTH TRANSFORMS AUTOMATICALLY
                        if (inputField.FieldType == "CalendarClock" && DateTime.TryParse(val, out var parsedDateTime))
                        {
                            inputField.SelectedDate = parsedDateTime.Date;
                            inputField.SelectedTime = parsedDateTime;
                        }
                    }
                }
            }

            if (IsCustomer)
            {
                NewLead.Status = "Matured";
                _isCustomerMode = true;
            }
        }

        private async Task LoadSettingsAndCustomFieldsAsync()
        {
            // Assuming your DataService has methods to fetch these from Admin tables
            var sources = await _settingService.GetSettingsAsync("LeadSources");
            var tags = await _settingService.GetSettingsAsync("LeadTags");
            var labels = await _settingService.GetSettingsAsync("LeadLabels");

            DivisionList = new ObservableCollection<Division>(await _profileService.GetActiveDivisionsAsync());
            SourceList = new ObservableCollection<SettingItem>(sources);
            TagsList = new ObservableCollection<SettingItem>(tags);
            LabelsList = new ObservableCollection<SettingItem>(labels);
            await GetCustomFields();
        }

        private async Task GetCustomFields()
        {
            // Fetch dynamic database configurations schemas matching this view target space
            var fieldDefinitions = await _customFieldService.GetAllFieldsAsync();
            App.Current.Dispatcher.Invoke(() =>
            {
                DynamicLeadFields.Clear();
                foreach (var f in fieldDefinitions.Where(x => _isCustomerMode ? x.IsVisibleInCustomer : x.IsVisibleInLead))
                {
                    DynamicLeadFields.Add(new CustomFieldInputValue
                    {
                        FieldId = f.FieldId,
                        FieldName = f.FieldName,
                        FieldType = f.FieldType,
                        IsRequiredInLead = f.IsRequiredInLead,
                        SeedValueOptionsList = f.SeedValueOptionsList ?? new ObservableCollection<string>()
                    });
                }
            });
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
            // Block execution if validation rule flags remain high
            if (IsPhoneDuplicate || IsAltPhoneDuplicate || IsEmailDuplicate || IsEmailMalformed)
            {
                ValidationMessage = "Cannot save lead. Please resolve format and duplication flags before continuing.";
                return;
            }

            if (string.IsNullOrWhiteSpace(NewLead.CustomerName))
            {
                // You could add a StatusMessage property here for validation errors
                return;
            }

            bool validationFailed = false;
            foreach (var inputField in DynamicLeadFields)
            {
                if (inputField.IsRequiredInLead && string.IsNullOrWhiteSpace(inputField.FieldValue))
                {
                    inputField.HasValidationError = true;
                    inputField.ValidationErrorMessage = $"{inputField.FieldName} is mandatory.";
                    validationFailed = true;
                }
                else
                {
                    inputField.HasValidationError = false;
                }
            }

            if (validationFailed)
            {
                ValidationMessage = "Please complete all required custom validation properties before submitting.";
                return;
            }

            try
            {
                int targetLeadId = NewLead.LeadId;

                if (_isEditMode)
                {
                    await _leadService.UpdateLeadAsync(NewLead);
                }
                else
                {
                    var historyEntry = new LeadHistoryEntry
                    {
                        Message = _isCustomerMode ? "New Customer Added" : "New Lead Added",
                        Content = _isCustomerMode ? $"Customer '{NewLead.CustomerName}' created." : $"Lead '{NewLead.CustomerName}' created.",
                        UpdatedByContent = _isCustomerMode ? "added a new customer" : "added a new lead",
                        NextFollowUpDate = DateTime.Now,
                        UpdatedBy = _session.CurrentUser,
                        LogDate = DateTime.Now,
                        IsPriority = false
                    };

                    targetLeadId = await _leadService.SaveLeadAsync(NewLead, historyEntry, _session.CurrentUser);
                    await _workflowEngine.EnqueueEventAsync("OnLeadCreated", targetLeadId, "Lead");                    
                }

                var valuesPayload = DynamicLeadFields.Select(f => new KeyValuePair<int, string>(f.FieldId, f.FieldValue ?? string.Empty));
                await _leadService.SaveLeadCustomFieldValuesAsync(targetLeadId, valuesPayload, _isCustomerMode ? "Customer" : "Lead");
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

            if (value != null && value.Value != NewLead.LeadSourceId)
            {
                var selectedItem = SourceList.FirstOrDefault(x => x.Id == value);
                NewLead.LeadSourceId = value.Value;
                NewLead.LeadSource = selectedItem?.Name ?? string.Empty;
            }           
        }

        partial void OnLeadTagIdChanged(int? value)
        {
            if (value == null || value == 0)
            {
                NewLead.LeadTag = string.Empty;
                return;
            }

            if (value != null && value.Value != NewLead.LeadTagId)
            {
                var selectedItem = TagsList.FirstOrDefault(x => x.Id == value);
                NewLead.LeadTagId = value.Value;
                NewLead.LeadTag = selectedItem?.Name ?? string.Empty;
            }
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
            if (_isProcessingPhoneUpdate) return;

            if (string.IsNullOrEmpty(value))
            {
                NewLead.Phone = string.Empty;
                IsPhoneDuplicate = false;
                return;
            }

            if (value.Length >= 10)
            {
                string formattedResult = ValidateAndFormatGlobalNumber(value, "IN");

                // Guard Condition: Only update properties if the new format actually differs
                if (formattedResult != value || formattedResult != NewLead.Phone)
                {
                    try
                    {
                        // Raise the guard flag high to block incoming change events
                        _isProcessingPhoneUpdate = true;

                        NewLead.Phone = formattedResult;

                        // This programmatic assignment updates the UI, but step 2 above will catch it 
                        // and return immediately, completely breaking the infinite loop!
                        this.LeadPhone = formattedResult;
                    }
                    finally
                    {
                        // Always lower the flag in a finally block to keep the UI input responsive
                        _isProcessingPhoneUpdate = false;
                    }
                }

                // Run your async duplicate database check safely without background interference
                if (!formattedResult.Contains("[INVALID") && !formattedResult.Contains("[PARSING"))
                {
                    _ = VerifyDuplicateFieldAsync("Phone", formattedResult, isAlt: true);
                }
            }            
        }

        partial void OnLeadAltPhoneChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                NewLead.AltPhone = string.Empty;
                IsAltPhoneDuplicate = false;
                return;
            }

            if (value.Length >= 10)
            {
                string formattedResult = ValidateAndFormatGlobalNumber(value, "IN");

                // Guard Condition: Only update properties if the new format actually differs
                if (formattedResult != value || formattedResult != NewLead.AltPhone)
                {
                    try
                    {
                        // Raise the guard flag high to block incoming change events
                        _isProcessingPhoneUpdate = true;

                        NewLead.AltPhone = formattedResult;

                        // This programmatic assignment updates the UI, but step 2 above will catch it 
                        // and return immediately, completely breaking the infinite loop!
                        this.LeadAltPhone = formattedResult;
                    }
                    finally
                    {
                        // Always lower the flag in a finally block to keep the UI input responsive
                        _isProcessingPhoneUpdate = false;
                    }
                }

                // Run your async duplicate database check safely without background interference
                if (!formattedResult.Contains("[INVALID") && !formattedResult.Contains("[PARSING"))
                {
                    _ = VerifyDuplicateFieldAsync("AltPhone", formattedResult, isAlt: true);
                }
            }
        }

        partial void OnLeadEmailChanged(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                NewLead.Email = string.Empty;
                IsEmailDuplicate = false;
                IsEmailMalformed = false;
                return;
            }

            if (!EmailRegex.IsMatch(value))
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    IsEmailMalformed = true;
                    IsEmailDuplicate = false; // Reset duplicate flag if pattern itself is broken
                    EvaluateMasterValidationMessage();
                });
            }
            else
            {
                // Layer 2: Format is correct, proceed to scan MySQL database for duplication over LAN
                NewLead.Email = value.Trim();
                App.Current.Dispatcher.Invoke(() => IsEmailMalformed = false);
                _ = VerifyDuplicateFieldAsync("Email", NewLead.Email, isAlt: false);
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

        private void EvaluateMasterValidationMessage()
        {
            if (IsPhoneDuplicate || IsAltPhoneDuplicate || IsEmailDuplicate)
            {
                ValidationMessage = "⚠️ Warning: Input details match an existing customer profile in your database.";
            }
            else if (IsEmailMalformed)
            {
                ValidationMessage = "⚠️ Warning: The provided email address format is invalid (e.g., user@domain.com).";
            }
            else
            {
                ValidationMessage = string.Empty;
            }
        }

        private async Task VerifyDuplicateFieldAsync(string columnName, string columnValue, bool isAlt)
        {
            if (string.IsNullOrWhiteSpace(columnValue) || columnValue.Contains("[INVALID]")) return;

            if (_isEditMode)
            {
                if (columnName == "Phone" && columnValue == _originalPhone)
                {
                    App.Current.Dispatcher.Invoke(() => { IsPhoneDuplicate = false; EvaluateMasterValidationMessage(); });
                    return;
                }
                if (columnName == "AltPhone" && columnValue == _originalAltPhone)
                {
                    App.Current.Dispatcher.Invoke(() => { IsAltPhoneDuplicate = false; EvaluateMasterValidationMessage(); });
                    return;
                }
                if (columnName == "Email" && columnValue == _originalEmail)
                {
                    App.Current.Dispatcher.Invoke(() => { IsEmailDuplicate = false; EvaluateMasterValidationMessage(); });
                    return;
                }
            }

            // Check against database index rules
            bool exists = await _leadService.CheckDuplicateFieldAsync(columnName, columnValue, NewLead.LeadId);

            App.Current.Dispatcher.Invoke(() =>
            {
                if (columnName == "Phone") IsPhoneDuplicate = exists;
                else if (columnName == "AltPhone" || isAlt) IsAltPhoneDuplicate = exists;
                else if (columnName == "Email") IsEmailDuplicate = exists;

                EvaluateMasterValidationMessage();
            });
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

            // Strip out standard typing noise like dashes, spaces, or brackets, but preserve the leading '+'
            string cleanedInput = Regex.Replace(inputNumber.Trim(), @"[^\d+]", "");

            try
            {
                // 1. SMART AUTO-REPAIR FOR DOMESTIC DEFAULT REGION (e.g., India 10 Digits)
                if (!cleanedInput.StartsWith("+") && defaultRegion == "IN" && cleanedInput.Length == 10 && long.TryParse(cleanedInput, out _))
                {
                    cleanedInput = "+91" + cleanedInput;
                }

                // 2. GLOBAL SMART AUTO-REPAIR (e.g., typed '447123456789' or '14155552671' without '+')
                else if (!cleanedInput.StartsWith("+"))
                {
                    try
                    {
                        // Test parse by prepending a '+' to see if it resolves to a valid international number
                        string testInput = "+" + cleanedInput;
                        PhoneNumber testParsed = phoneUtil.Parse(testInput, "ZZ"); // "ZZ" means Unknown/Global Region Identification Mode

                        if (phoneUtil.IsValidNumber(testParsed))
                        {
                            // The number is a perfectly valid global number; accept the repaired prefix!
                            cleanedInput = testInput;
                        }
                    }
                    catch (NumberParseException)
                    {
                        // Fallback: If prepending '+' fails parsing entirely, leave it un-prefixed 
                        // so the standard domestic parser configuration below can evaluate it.
                    }
                }

                // 3. FINAL VALIDATION LAYER PASS
                // If cleanedInput now starts with '+', defaultRegion is safely ignored by the engine.
                PhoneNumber parsedNumber = phoneUtil.Parse(cleanedInput, defaultRegion);

                bool isValid = phoneUtil.IsValidNumber(parsedNumber);
                PhoneNumberType type = phoneUtil.GetNumberType(parsedNumber);

                // Broadened routing profiles to accommodate varying global carrier rules (e.g., VOIP/Pager/Personal numbers)
                if (isValid && (type == PhoneNumberType.MOBILE ||
                                type == PhoneNumberType.FIXED_LINE_OR_MOBILE ||
                                type == PhoneNumberType.FIXED_LINE ||
                                type == PhoneNumberType.UAN ||
                                type == PhoneNumberType.VOIP))
                {
                    // Success! Rewrite into clean standard international format (e.g., "+44 7123 456789")
                    return phoneUtil.Format(parsedNumber, PhoneNumberFormat.INTERNATIONAL);
                }
                else
                {
                    return inputNumber + " [INVALID NUMBER]";
                }
            }
            catch (NumberParseException)
            {
                return inputNumber + " [PARSING ERROR]";
            }
        }
    }
}
