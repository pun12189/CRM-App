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
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class LeadProfileViewModel : ObservableObject
    {
        private readonly LeadService _leadService;
        private readonly SettingService _settingService;
        private readonly IUserSession _session;

        [ObservableProperty] private Lead _selectedLead;

        [ObservableProperty]
        private DateTime? _selectedTime = DateTime.Now;

        // Add this event
        public event Action<bool>? RequestClose;

        // Status Radio Buttons
        [ObservableProperty] private bool _isFollowup = true;
        [ObservableProperty] private bool _isMatured;
        [ObservableProperty] private bool _isDead;

        // --- Financial Properties (Matured Section) ---
        [ObservableProperty] private decimal _orderValue;
        [ObservableProperty] private decimal _paymentReceived;
        [ObservableProperty] private decimal _balancePayment;
        [ObservableProperty] private bool _isReorder;
        [ObservableProperty] private bool _isPaymentReminder;

        // Followup Details
        [ObservableProperty] private string _message = "";
        [ObservableProperty] private DateTime _nextFollowupDate = DateTime.Now.AddDays(1);
        [ObservableProperty] private DateTime _minDate = DateTime.Today;
        [ObservableProperty] private string _selectedAction = "Call"; // Default
        [ObservableProperty] private bool _isPriority;

        [ObservableProperty] private ObservableCollection<SettingItem> _followupStages = new();
        [ObservableProperty] private ObservableCollection<SettingItem> _deadReasons = new();

        [ObservableProperty] private SettingItem _selectedStatus;
        [ObservableProperty] private SettingItem _selectedDeadReason;   

        public LeadProfileViewModel(LeadService service, SettingService settingService, IUserSession session, Lead lead)
        {
            _leadService = service;
            _settingService = settingService;
            _session = session;
            _selectedLead = lead;
            _ = LoadCollections();
        }

        // --- Logic for Dynamic Balance ---
        partial void OnOrderValueChanged(decimal value) => CalculateBalance();
        partial void OnPaymentReceivedChanged(decimal value) => CalculateBalance();

        private void CalculateBalance()
        {
            BalancePayment = OrderValue - PaymentReceived;
        }

        private async Task LoadCollections()
        {
            var stages = await _settingService.GetSettingsAsync("LeadStatuses");
            var reasons = await _settingService.GetSettingsAsync("DeadReasons");

            FollowupStages = new ObservableCollection<SettingItem>(stages);
            DeadReasons = new ObservableCollection<SettingItem>(reasons);
        }

        [RelayCommand]
        private async Task UpdateLeadStatus()
        {
            try
            {
                // Logic for 'Matured' (Maybe open a 'Create Invoice' screen later?)
                /*if (IsMatured)
                {
                    SelectedLead.Status = "Matured";
                    Message = "[MATURED] " + Message;
                }*/
                if (IsDead)
                {
                    var history = new LeadHistoryEntry
                    {
                        LeadId = SelectedLead.LeadId,
                        // Prefix message with the reason for the timeline
                        Message = $"[DEAD] {Message}",
                        ActionType = SelectedAction,
                        NextFollowUpDate = null, // CRITICAL: Stop the reminders
                        FollowupStage = SelectedDeadReason?.Name,
                        UpdatedBy = _session.CurrentUser
                    };

                    SelectedLead.LatestUpdate = history;
                    SelectedLead.Status = "Dead";
                    // Status is updated to 'Dead' in the Leads table
                    await _leadService.UpdateLeadFullAsync(SelectedLead, history);
                    RequestClose?.Invoke(true);
                }
                else
                {
                    if (NextFollowupDate != null && SelectedTime != null)
                    {
                        DateTime combinedDateTime = new DateTime(
                            NextFollowupDate.Year,
                            NextFollowupDate.Month,
                            NextFollowupDate.Day,
                            SelectedTime.Value.Hour,
                            SelectedTime.Value.Minute,
                            0
                        );

                        /// Call the service to save history and update status
                        var history = new LeadHistoryEntry
                        {
                            LeadId = SelectedLead.LeadId,
                            Message = Message,
                            NextFollowUpDate = combinedDateTime,
                            UpdatedBy = _session.CurrentUser,
                            ActionType = SelectedAction,
                            FollowupStage = SelectedStatus?.Name
                        };

                        SelectedLead.LatestUpdate = history;
                        SelectedLead.Status = IsMatured ? "Matured" : (IsDead ? "Dead" : "Followup");
                        if (IsMatured)
                        {
                            history.FollowupStage = "First Order Recieved";
                            var newOrder = new Order
                            {
                                LeadId = SelectedLead.LeadId,
                                TotalAmount = OrderValue,
                                Description = $"First Order: {Message}",
                                OrderDate = DateTime.Now,
                                Status = BalancePayment == 0 ? "Paid" : "Partially Paid",
                                ProcessedBy = _session.CurrentUser,
                            };
                            
                            var payment = new PaymentEntry
                            {
                                LeadId = SelectedLead.LeadId,
                                TotalOrderValue = OrderValue,
                                AmountReceived = PaymentReceived,
                                Remarks = $"Payment Entry for Order. Balance: {BalancePayment}"
                            };

                            // Use the service method that handles the transaction
                            await _leadService.MatureLeadWithDoubleHistoryAsync(SelectedLead, newOrder, payment, history);
                        }
                        else
                        {
                            // Standard Follow-up/Dead update
                            await _leadService.UpdateLeadFullAsync(SelectedLead, history);
                        }
                    }

                    // Success! Close the dialog and tell the main grid to refresh
                    RequestClose?.Invoke(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating lead: " + ex.Message);
            }
        }
    }
}
