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
        [ObservableProperty] private string _selectedStage;
        [ObservableProperty] private DateTime _nextFollowupDate = DateTime.Now.AddDays(1);
        [ObservableProperty] private string _selectedAction = "Call"; // Default
        [ObservableProperty] private bool _isPriority;

        [ObservableProperty] private string? _selectedDeadReason;

        // Common reasons for your cycle business
        public ObservableCollection<string> DeadReasons { get; } = new()
    {
        "Price too high",
        "Bought from competitor",
        "No longer interested",
        "Stock unavailable",
        "Wrong number/Contact issue"
    };

        public ObservableCollection<string> Stages { get; } = new() { "Initial Contact", "Price Shared", "Negotiation", "Technical Discussion" };

        public LeadProfileViewModel(LeadService service, Lead lead)
        {
            _leadService = service;
            _selectedLead = lead;
        }

        // --- Logic for Dynamic Balance ---
        partial void OnOrderValueChanged(decimal value) => CalculateBalance();
        partial void OnPaymentReceivedChanged(decimal value) => CalculateBalance();

        private void CalculateBalance()
        {
            BalancePayment = OrderValue - PaymentReceived;
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
                        Message = $"[DEAD - {SelectedDeadReason}] {Message}",
                        ActionType = SelectedAction,
                        NextFollowUpDate = null, // CRITICAL: Stop the reminders
                        FollowupStage = "Dead",
                        UpdatedBy = "Admin"
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
                            NextFollowUpDate = IsFollowup ? combinedDateTime : null,
                            UpdatedBy = "Admin",
                            ActionType = SelectedAction,
                            FollowupStage = SelectedStage
                        };

                        SelectedLead.LatestUpdate = history;
                        SelectedLead.Status = IsMatured ? "Matured" : (IsDead ? "Dead" : "Followup");
                        if (IsMatured)
                        {
                            var newOrder = new Order
                            {
                                LeadId = SelectedLead.LeadId,
                                TotalAmount = OrderValue,
                                Description = $"Initial Order: {Message}",
                                OrderDate = DateTime.Now
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
