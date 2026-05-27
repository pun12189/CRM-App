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
    public partial class CustomerProfileViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly IUserSession _session;
        private readonly SettingService _settingService;
        private readonly ProductService _productService;
        private readonly OrderService _orderService;
        private readonly OccupiedLocationService _locationService;

        [ObservableProperty] private CustomerAnalytics _data;

        [ObservableProperty] private Lead _selectedLead;
        [ObservableProperty] private int _selectedTabWorkspaceIndex = 2;
        [ObservableProperty] private bool _isInfoTabSelected;
        [ObservableProperty] private CustomerSummaryMetrics _metrics;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<OrderItem> _selectedItems = new();
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private int _customerId;

        public decimal GrandTotal => SelectedItems.Sum(x => x.SubTotal);

        // Add this event
        public event Action<bool>? RequestClose;

        [ObservableProperty] private bool _isMaturedFollowup = true;
        [ObservableProperty] private bool _isMaturedDead;
        [ObservableProperty] private bool _isCreatingProforma;

        // Action Panel Fields
        [ObservableProperty] private bool _isOrderReceived;
        [ObservableProperty] private decimal _orderValue;
        [ObservableProperty] private decimal _paymentReceived;
        [ObservableProperty] private decimal _balancePayment;
        [ObservableProperty] private string _message = string.Empty;
        [ObservableProperty] private SettingItem _selectedMatureStage;
        [ObservableProperty] private SettingItem _selectedDeadStage;
        [ObservableProperty] private DateTime? _selectedTime = DateTime.Now;
        [ObservableProperty] private DateTime _nextFollowupDate = DateTime.Now.AddDays(1);
        [ObservableProperty] private DateTime _minDate = DateTime.Today;

        [ObservableProperty] private ObservableCollection<SettingItem> _matureStages = new();
        [ObservableProperty] private ObservableCollection<SettingItem> _deadStages = new();

        public CustomerProfileViewModel(LeadService service, IUserSession session, SettingService settingService, ProductService productService, OrderService orderService, Lead lead, OccupiedLocationService locationService)
        {
            _service = service;
            _session = session;
            _settingService = settingService;
            _productService = productService;
            _orderService = orderService;
            _customerId = lead.LeadId;
            _selectedLead = lead;
            _locationService = locationService;
            _ = LoadCustomerData(lead.LeadId);
        }

        // --- Logic for Dynamic Balance ---
        partial void OnOrderValueChanged(decimal value) => CalculateBalance();
        partial void OnPaymentReceivedChanged(decimal value) => CalculateBalance();

        partial void OnSelectedTabWorkspaceIndexChanged(int value)
        {
            if (value == 0)
            {
                // Info Tab clicked: Keep the top panel open
                IsInfoTabSelected = true;

                // OPTIONAL UI TRICK: Flip index back to previous working tab if you want 
                // the lower tab body contents to stay visible while info is shown!
            }
            else
            {
                // Any other functional tab item clicked: Collapse the drawer overlay
                IsInfoTabSelected = false;
            }
        }

        private void CalculateBalance()
        {
            BalancePayment = OrderValue - PaymentReceived;
        }

        private async Task LoadCustomerData(int leadId)
        {
            // Simple fetch for the summary boxes
            Data = await _service.GetCustomerSummaryAsync(leadId);

            var products = await _productService.GetAllProductsAsync();
            AvailableProducts = new ObservableCollection<Product>(products);

            // Load the string-based mature stages
            var stages = await _settingService.GetSettingsAsync("MatureStages");
            var reasons = await _settingService.GetSettingsAsync("DeadReasons");
            MatureStages = new ObservableCollection<SettingItem>(stages);
            DeadStages = new ObservableCollection<SettingItem>(reasons);

            Metrics = await _locationService.GetSummaryMetricsAsync(SelectedLead.LeadId);
        }

        [RelayCommand]
        private async Task UpdateCustomer()
        {
            if (IsMaturedDead)
            {
                var history = new LeadHistoryEntry
                {
                    LeadId = SelectedLead.LeadId,

                    // Prefix message with the reason for the timeline
                    Message = Message,
                    Content = $"[MATURE DEAD] {SelectedLead.CustomerName}\r\n Company: {SelectedLead.CompanyName}",
                    ActionType = "Call",
                    UpdatedByContent = $" marked as Dead due to {SelectedDeadStage?.Name}",
                    NextFollowUpDate = null, // CRITICAL: Stop the reminders
                    FollowupStage = SelectedDeadStage?.Name,
                    UpdatedBy = _session.CurrentUser
                };

                SelectedLead.LatestUpdate = history;
                SelectedLead.Status = "Winback Pool";
                SelectedLead.DeadReasonId = SelectedDeadStage?.Id ?? null;
                // Status is updated to 'Dead' in the Leads table
                await _service.UpdateLeadFullAsync(SelectedLead, history);
                RequestClose?.Invoke(true);
            }
            else
            {
                // 1. Create history record
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
                        Content = $"[MATURED FOLLOWUP] {SelectedLead.CustomerName}\r\n Company: {SelectedLead.CompanyName}",
                        UpdatedByContent = $" scheduled a matured follow-up ({SelectedMatureStage?.Name}) on {combinedDateTime:G}",
                        NextFollowUpDate = combinedDateTime,
                        UpdatedBy = _session.CurrentUser,
                        ActionType = "Call",
                        FollowupStage = SelectedMatureStage?.Name
                    };

                    SelectedLead.LatestUpdate = history;
                    SelectedLead.MatureStageId = SelectedMatureStage?.Id ?? null;
                    if (IsOrderReceived)
                    {
                        var newOrder = new Order
                        {
                            LeadId = SelectedLead.LeadId,
                            AmountPaid = PaymentReceived,
                            TotalAmount = OrderValue,
                            Description = $"Repeat Order: {Message}",
                            OrderDate = DateTime.Now,
                            PaymentStatus = BalancePayment == 0 ? "Paid" : "Partially Paid",
                            Status = "Pending",
                            ProcessedBy = _session.CurrentUser,
                        };

                        var payment = new PaymentEntry
                        {
                            LeadId = SelectedLead.LeadId,
                            TotalOrderValue = OrderValue,
                            AmountReceived = PaymentReceived,
                            Remarks = $"Payment Entry for Order. Balance: {BalancePayment}"
                        };

                        history = new LeadHistoryEntry
                        {
                            LeadId = SelectedLead.LeadId,
                            Message = Message,
                            Content = $"{_session.CurrentUser} created an order \r\n {OrderValue:C}\r\n with an initial payment of {PaymentReceived:C}.\r\n Balance: {BalancePayment:C}",
                            UpdatedByContent = $" scheduled a matured follow-up ({SelectedMatureStage}) on {combinedDateTime:G}",
                            NextFollowUpDate = combinedDateTime,
                            UpdatedBy = _session.CurrentUser,
                            ActionType = "Call",
                            FollowupStage = SelectedMatureStage?.Name
                        };

                        await _service.MatureWithOrderAndPaymentAsync(SelectedLead, newOrder, payment, history);
                    }
                    else
                    {
                        // 2. Save using the fresh, simple logic
                        await _service.UpdateLeadFullAsync(SelectedLead, history);
                    }
                }

                RequestClose?.Invoke(true);
            }
        }

        [RelayCommand]
        private void AddItem()
        {
            if (SelectedProduct == null || Quantity <= 0) return;

            // Check if item already exists in the list
            var existing = SelectedItems.FirstOrDefault(x => x.ProductId == SelectedProduct.ProductId);
            if (existing != null)
            {
                existing.Quantity += Quantity;
                // Notify UI of subtotal change
                OnPropertyChanged(nameof(SelectedItems));
            }
            else
            {
                var newItem = new OrderItem
                {
                    ProductId = SelectedProduct.ProductId,
                    ProductName = SelectedProduct.Name,
                    Quantity = Quantity,
                    UnitPrice = SelectedProduct.SellingPrice,
                    GstPercent = SelectedProduct.GstPercent
                };

                // Add directly to the ObservableCollection
                SelectedItems.Add(newItem);
            }

            OnPropertyChanged(nameof(GrandTotal));
            Quantity = 1; // Reset quantity
        }

        [RelayCommand]
        private void RemoveItem(OrderItem item)
        {
            SelectedItems.Remove(item);
            OnPropertyChanged(nameof(GrandTotal));
        }

        [RelayCommand]
        private async Task SaveProforma()
        {
            if (!SelectedItems.Any()) return;

            var order = new Order
            {
                LeadId = _customerId,
                Items = SelectedItems,
                ProformaNumber = $"PF-{DateTime.Now:yyyyMMdd}-{_customerId}"
            };

            var history = new LeadHistoryEntry
            {
                LeadId = SelectedLead.LeadId,
                Message = $"Proforma created {order.ProformaNumber}",
                NextFollowUpDate = DateTime.Now,
                UpdatedBy = _session.CurrentUser,
                ActionType = "Call",
                FollowupStage = "Proforma Send"
            };

            SelectedLead.LatestUpdate = history;

            if (await _orderService.SaveProformaAsync(order, history))
            {
                // Logic to trigger PDF generation would go here
                SelectedItems.Clear();
                OnPropertyChanged(nameof(GrandTotal));
            }
        }
    }
}
