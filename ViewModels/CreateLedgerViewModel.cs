using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class CreateLedgerViewModel : ObservableObject
    {
        private readonly LedgerService _ledgerService;
        private readonly LeadService _customerService; // Service to get customer list
        private readonly OrderService _orderService;
        private readonly IUserSession _userSession;// Service to get user session

        public event Action<bool>? RequestClose;

        [ObservableProperty] private ObservableCollection<Lead> _customers = new();
        [ObservableProperty] private ObservableCollection<Order> _orders = new();

        [ObservableProperty] private Lead? _selectedCustomer;
        [ObservableProperty] private Order? _selectedOrder;
        [ObservableProperty] private decimal _amount;
        [ObservableProperty] private DateTime _transactionDate = DateTime.Now;
        [ObservableProperty] private string _message = string.Empty;

        [ObservableProperty] private bool _isLoadingCustomers = true;
        [ObservableProperty] private string _loadingStatusText = "Fetching Customers Please wait...";

        [ObservableProperty] private decimal _pendingBalance;
        [ObservableProperty] private decimal _remainingBalance;

        // Dynamic Fun Mood Properties
        [ObservableProperty] private string _moodEmoji = "😢";
        [ObservableProperty] private string _moodMessage = "No payment entered yet... Full pending balance!";
        [ObservableProperty] private string _moodBadgeColor = "#EF4444"; // Red

        public CreateLedgerViewModel(LedgerService ledgerService, LeadService customerService, OrderService orderService, IUserSession userSession)
        {
            _ledgerService = ledgerService;
            _customerService = customerService;
            _orderService = orderService;
            _userSession = userSession;
            _ = InitializeDataAsync();
        }

        private async Task InitializeDataAsync()
        {
            IsLoadingCustomers = true;
            try
            {
                var customerList = await _customerService.GetMaturedLedgerAsync();
                Customers = new ObservableCollection<Lead>(customerList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading customers: {ex.Message}");
            }
            finally
            {
                IsLoadingCustomers = false;
            }
        }

        // When customer changes, load associated orders
        partial void OnSelectedCustomerChanged(Lead? value)
        {
            SelectedOrder = null;
            Orders.Clear();
            PendingBalance = 0;
            Amount = 0;

            if (value != null)
            {
                _ = LoadOrdersForCustomerAsync(value.LeadId);
            }
        }

        private async Task LoadOrdersForCustomerAsync(int leadId)
        {
            var customerOrders = await _customerService.GetOrdersByLeadIdAsync(leadId);
            Orders = new ObservableCollection<Order>(customerOrders);
        }

        partial void OnSelectedOrderChanged(Order? value)
        {
            if (value != null)
            {
                // Accessing your base Order class's calculative property
                PendingBalance = value.OrderBalance;
                Amount = 0;
            }
            else
            {
                PendingBalance = 0;
                Amount = 0;
            }

            UpdateBalancesAndMood();
        }

        partial void OnAmountChanged(decimal value)
        {
            // CAP: Amount cannot exceed Order.OrderBalance
            if (value > PendingBalance)
            {
                _amount = PendingBalance;
                OnPropertyChanged(nameof(Amount));
            }

            UpdateBalancesAndMood();
        }

        private void UpdateBalancesAndMood()
        {
            RemainingBalance = Math.Max(0, PendingBalance - Amount);

            if (PendingBalance <= 0)
            {
                MoodEmoji = "😊";
                MoodMessage = "Order has zero pending balance!";
                MoodBadgeColor = "#10B981"; // Emerald Green
                return;
            }

            decimal paidRatio = Amount / PendingBalance;

            if (Amount <= 0)
            {
                MoodEmoji = "😢";
                MoodMessage = "Full balance pending... Time to collect!";
                MoodBadgeColor = "#EF4444"; // Red
            }
            else if (paidRatio < 0.5m)
            {
                MoodEmoji = "😐";
                MoodMessage = $"Received ₹ {Amount:N0}! Keep 'em coming!";
                MoodBadgeColor = "#F59E0B"; // Amber
            }
            else if (paidRatio < 1.0m)
            {
                MoodEmoji = "🙂";
                MoodMessage = $"More than half paid! Remaining: ₹ {RemainingBalance:N0}";
                MoodBadgeColor = "#0284C7"; // Blue
            }
            else // 100% Paid
            {
                MoodEmoji = "🎉";
                MoodMessage = "Woohoo! Order fully settled!";
                MoodBadgeColor = "#10B981"; // Emerald Green
            }
        }

        [RelayCommand]
        private async Task SubmitAsync()
        {
            // Validation
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Please select a customer.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedOrder == null)
            {
                MessageBox.Show("Please select an order.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Amount <= 0)
            {
                MessageBox.Show("Please enter a valid amount.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Message))
            {
                MessageBox.Show("Please enter a message.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var orderValue = SelectedOrder.GrandTotal > 0 ? SelectedOrder.GrandTotal : SelectedOrder.TotalAmount;
            var p = new PaymentEntry
            {
                OrderId = SelectedOrder.OrderId,
                LeadId = SelectedCustomer.LeadId,
                DivisionId = SelectedOrder.DivisionId,
                TotalOrderValue = orderValue,
                AmountReceived = Amount,
                BalanceAmount = orderValue - Amount,
                PaymentMethod = "Cash",
                UserId = _userSession.UserId, // Assuming current user ID is 1 for this example
                Remarks = string.IsNullOrWhiteSpace(Message) ? "Payment Received" : Message,
                PaymentDate = TransactionDate
            };

            var historyEntry = new LeadHistoryEntry
            {
                LeadId = SelectedOrder.LeadId,
                Message = "Payment Received",
                Content = $"Received ₹ {Amount:N2} via {p.PaymentMethod} for Order #{SelectedOrder.FormattedOrderId}. Remarks: {p.Remarks}",
                UpdatedByContent = "record a payment",
                NextFollowUpDate = DateTime.Now,
                UpdatedBy = _userSession.CurrentUser ?? "Admin",
                LogDate = DateTime.Now,
                IsPriority = true
            };

            bool success = await _customerService.RecordPaymentAsync(p, historyEntry);
            if (success)
            {
                RequestClose?.Invoke(true);
            }
            else
            {
                MessageBox.Show("Failed to save ledger entry.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Close()
        {
            RequestClose?.Invoke(false);
        }
    }    
}
