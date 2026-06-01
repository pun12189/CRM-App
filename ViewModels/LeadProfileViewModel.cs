using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Models.Enums;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CallMan.ViewModels
{
    public partial class LeadProfileViewModel : ObservableObject
    {
        private readonly LeadService _leadService;
        private readonly SettingService _settingService;
        private readonly IUserSession _session;
        private readonly OccupiedLocationService _locationService;
        private readonly NotificationRoutingService _notificationRoutingService;
        private readonly ProductService _productService;
        private readonly OrderService _orderService;

        [ObservableProperty] private CustomerAnalytics _data;

        [ObservableProperty] private Lead _selectedLead;
        [ObservableProperty] private int _selectedTabWorkspaceIndex = 2;
        [ObservableProperty] private bool _isInfoTabSelected;
        [ObservableProperty] private bool _isAdminNotification;
        [ObservableProperty] private CustomerSummaryMetrics _metrics;

        [ObservableProperty]
        private DateTime? _selectedTime = DateTime.Now;

        [ObservableProperty]
        private DateTime? _paymentSelectedTime = DateTime.Now;

        [ObservableProperty] private ObservableCollection<ProformaSummaryItem> _associatedProformas = new();
        [ObservableProperty] private ProformaSummaryItem? _selectedHistoricalProforma;

        // Add this event
        public event Action<bool>? RequestClose;

        // Status Radio Buttons
        [ObservableProperty] private bool _isFollowup = true;
        [ObservableProperty] private bool _isMatured;
        [ObservableProperty] private bool _isDead;
        [ObservableProperty] private bool _isCreateProforma;
        [ObservableProperty] private bool _isCreateOrder;

        [ObservableProperty] private string _followupDateLabel;
        [ObservableProperty] private string _followupTimeLabel;

        // --- Financial Properties (Matured Section) ---
        [ObservableProperty] private decimal _orderValue;
        [ObservableProperty] private decimal _paymentReceived;
        [ObservableProperty] private decimal _balancePayment;
        [ObservableProperty] private bool _isReorder;
        [ObservableProperty] private bool _isPaymentReminder;
        [ObservableProperty] private bool _isPaymentReminderVisible;

        // Followup Details
        [ObservableProperty] private string _message = "";
        [ObservableProperty] private DateTime _nextFollowupDate = DateTime.Now.AddDays(1);
        [ObservableProperty] private DateTime _paymentReminderDate = DateTime.Now.AddDays(1);
        [ObservableProperty] private DateTime _minDate = DateTime.Today;
        [ObservableProperty] private string _selectedAction = "Call"; // Default
        [ObservableProperty] private bool _isPriority;

        [ObservableProperty] private ObservableCollection<SettingItem> _followupStages = new();
        [ObservableProperty] private ObservableCollection<SettingItem> _deadReasons = new();

        [ObservableProperty] private SettingItem _selectedStatus;
        [ObservableProperty] private SettingItem _selectedDeadReason;

        [ObservableProperty]
        private ObservableCollection<LeadHistoryEntry> _historyItems = new();

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<OrderItem> _selectedItems = new();
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private int _customerId;

        public decimal GrandTotal => SelectedItems.Sum(x => x.SubTotal);

        [ObservableProperty] private string _customProductNameText = string.Empty;
        [ObservableProperty] private string _inputBatchNo = string.Empty;
        [ObservableProperty] private int _inputQuantity = 1;
        [ObservableProperty] private decimal _inputRate;
        [ObservableProperty] private decimal _inputGstPercent;

        [ObservableProperty] private string _inputChargeDescription = string.Empty;
        [ObservableProperty] private decimal _inputChargeAmount;
        [ObservableProperty] private string _inputChargeAction = "Add (+)";
        [ObservableProperty] private string _inputChargeGst = "0%";

        [ObservableProperty] private byte[]? _selectedImageBytes;
        [ObservableProperty] private BitmapImage? _selectedImagePreview;

        // Core transactional context object mapping
        [ObservableProperty] private ProformaHeader _activeProforma = new();

        public LeadProfileViewModel(LeadService service, SettingService settingService, IUserSession session, Lead lead, OccupiedLocationService locationService, NotificationRoutingService notificationRoutingService, ProductService productService, OrderService orderService)
        {
            _leadService = service;
            _settingService = settingService;
            _session = session;
            _locationService = locationService; 
            _notificationRoutingService = notificationRoutingService;
            _productService = productService;
            _orderService = orderService;
            _selectedLead = lead;
            _customerId = lead.LeadId;
            _ = LoadCollections();
        }

        // --- Logic for Dynamic Balance ---
        partial void OnOrderValueChanged(decimal value) => CalculateBalance();
        partial void OnPaymentReceivedChanged(decimal value) => CalculateBalance();

        partial void OnIsMaturedChanged(bool value)
        {
            FollowupDateLabel = value ? "Next Order Date" : "Next Follow-up Date";
            FollowupTimeLabel = value ? "Next Order Time" : "Next Follow-up Time";  
        }

        partial void OnSelectedTabWorkspaceIndexChanged(int value)
        {
            if (value == 0)
            {
                // Info Tab clicked: Keep the top panel open
                IsInfoTabSelected = true;

                // OPTIONAL UI TRICK: Flip index back to previous working tab if you want 
                // the lower tab body contents to stay visible while info is shown!
            }
            else if (value == 3)
            {
                // Timeline Tab clicked: Load the timeline data
                IsInfoTabSelected = false; // Ensure info panel is closed
                _ = LoadTimelineDataAsync();
            }
            else
            {
                // Any other functional tab item clicked: Collapse the drawer overlay
                IsInfoTabSelected = false;
                ActiveProforma = new ProformaHeader();
                RecalculateProformaFinancials();
            }
        }

        private async Task LoadTimelineDataAsync()
        {
            if (SelectedLead == null) return;
            var timeline = await _leadService.GetHistoryByLeadIdAsync(SelectedLead.LeadId);
            HistoryItems = new ObservableCollection<LeadHistoryEntry>(timeline);
        }

        private void CalculateBalance()
        {
            BalancePayment = OrderValue - PaymentReceived;
            IsPaymentReminderVisible = BalancePayment > 0;
            IsPaymentReminder = false; // Reset the reminder checkbox if balance is cleared
        }

        private async Task LoadCollections()
        {
            // Simple fetch for the summary boxes
            Data = await _leadService.GetCustomerSummaryAsync(_customerId);

            var products = await _productService.GetProductsWithBatchesAsync(1);
            AvailableProducts = new ObservableCollection<Product>(products);

            var stages = await _settingService.GetSettingsAsync("LeadStatuses");
            var reasons = await _settingService.GetSettingsAsync("DeadReasons");
            Metrics = await _locationService.GetSummaryMetricsAsync(SelectedLead.LeadId);
            FollowupStages = new ObservableCollection<SettingItem>(stages);
            DeadReasons = new ObservableCollection<SettingItem>(reasons);

            var result = await _leadService.LoadHistoricalProformasAsync(SelectedLead.LeadId);
            AssociatedProformas = new ObservableCollection<ProformaSummaryItem>(result);
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
                        Message = Message,
                        // Prefix message with the reason for the timeline
                        Content = $"[DEAD] {SelectedLead.CustomerName}\r\n Company: {SelectedLead.CompanyName}",
                        ActionType = SelectedAction,
                        UpdatedByContent = $" marked as Dead due to {SelectedDeadReason?.Name}",
                        NextFollowUpDate = null, // CRITICAL: Stop the reminders
                        FollowupStage = SelectedDeadReason?.Name,
                        UpdatedBy = _session.CurrentUser
                    };

                    SelectedLead.LatestUpdate = history;
                    SelectedLead.Status = "Dead";
                    SelectedLead.DeadReasonId = SelectedDeadReason?.Id ?? null;
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
                            Content = $"[FOLLOWUP] {SelectedLead?.CustomerName}\r\n Company: {SelectedLead?.CompanyName}",
                            UpdatedByContent = $"scheduled a follow-up ({SelectedStatus?.Name}) on {combinedDateTime:G}",
                            NextFollowUpDate = combinedDateTime,
                            UpdatedBy = _session.CurrentUser,
                            ActionType = SelectedAction,
                            FollowupStage = SelectedStatus?.Name,
                            IsPriority = IsPriority
                        };

                        var targetNotification = new NewToastRequest
                        {
                            EventId = 1983,
                            LeadId = SelectedLead?.LeadId ?? 0,
                            ReminderType = ReminderType.FollowUp.ToString() + "Reminder: " + SelectedLead?.CustomerName,
                            MessageContent = Message,
                            ScheduleTime = combinedDateTime, // Pops up instantly on target's workstation
                            TargetUser = _session.CurrentUser,      // <-- Direct routing targeting self profile layout
                            TargetMachine = Environment.MachineName,        // Set this if you want to explicitly target a specific physical terminal name
                            SenderUser = _session.CurrentUser        // Authored by User
                        };

                        SelectedLead.LatestUpdate = history;
                        SelectedLead.Status = IsMatured ? "Matured" : (IsDead ? "Dead" : "Followup");
                        SelectedLead.StatusId = SelectedStatus?.Id ?? null;
                        if (IsMatured)
                        {
                            var newOrder = new Order
                            {
                                LeadId = SelectedLead.LeadId,
                                TotalAmount = OrderValue,
                                AmountPaid = PaymentReceived,
                                Description = $"First Order: {Message}",
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
                                Content = $"{SelectedLead.CustomerName} placed an order worth {OrderValue:C}\r\n with an initial payment of {PaymentReceived:C}.\r\n Balance: {BalancePayment:C}",
                                UpdatedByContent = $"matured this lead on {combinedDateTime:G}",
                                NextFollowUpDate = combinedDateTime,
                                UpdatedBy = _session.CurrentUser,
                                ActionType = SelectedAction,
                                FollowupStage = "First Order Recieved",
                                IsPriority = IsPriority
                            };

                            SelectedLead.MatureStageId = null; // Reset any previous stage

                            targetNotification = new NewToastRequest
                            {
                                EventId = 1983,
                                ReminderType = Message,
                                MessageContent = _session.CurrentUser + " " + history.UpdatedByContent,
                                ScheduleTime = combinedDateTime, // Pops up instantly on target's workstation
                                TargetUser = _session.CurrentUser,      // <-- Direct routing targeting self profile layout
                                TargetMachine = Environment.MachineName,        // Set this if you want to explicitly target a specific physical terminal name
                                SenderUser = _session.CurrentUser        // Authored by User
                            };

                            if (IsPaymentReminder)
                            {
                                DateTime combinedDateTime1 = new DateTime(
                                    PaymentReminderDate.Year,
                                    PaymentReminderDate.Month,
                                    PaymentReminderDate.Day,
                                    PaymentSelectedTime.Value.Hour,
                                    PaymentSelectedTime.Value.Minute,
                                    0
                                );

                                // If there's a balance, we also want to schedule a payment reminder
                                var paymentReminderNotification = new NewToastRequest
                                {
                                    EventId = 1984,
                                    LeadId = SelectedLead.LeadId,
                                    ReminderType = ReminderType.Payment.ToString() + "Reminder: " + SelectedLead.CustomerName,
                                    MessageContent = $"Payment of {BalancePayment:C} is pending for {SelectedLead.CustomerName}",
                                    ScheduleTime = combinedDateTime1, // Schedule the payment reminder for the specified date
                                    TargetUser = _session.CurrentUser,      // <-- Direct routing targeting self profile layout
                                    TargetMachine = Environment.MachineName,        // Set this if you want to explicitly target a specific physical terminal name
                                    SenderUser = _session.CurrentUser        // Authored by User
                                };
                                await _notificationRoutingService.DispatchTargetedToastAsync(paymentReminderNotification);
                            }

                            if (IsAdminNotification)
                            {
                                var adminReminderNotification = new NewToastRequest
                                {
                                    EventId = 1984,
                                    LeadId = SelectedLead.LeadId,
                                    ReminderType = ReminderType.Payment.ToString() + "Reminder: " + SelectedLead.CustomerName,
                                    MessageContent = _session.CurrentUser + " has scheduled a reminder for Admin.\r\n" + Message,
                                    ScheduleTime = combinedDateTime, // Schedule the payment reminder for the specified date
                                    TargetUser = "Admin",      // <-- Direct routing targeting admin profile layout
                                    TargetMachine = Environment.MachineName,        // Set this if you want to explicitly target a specific physical terminal name
                                    SenderUser = _session.CurrentUser        // Authored by User
                                };
                                await _notificationRoutingService.DispatchTargetedToastAsync(adminReminderNotification);
                            }

                            // Use the service method that handles the transaction
                            await _leadService.MatureLeadWithDoubleHistoryAsync(SelectedLead, newOrder, payment, history);
                            await _notificationRoutingService.DispatchTargetedToastAsync(targetNotification);
                        }
                        else
                        {
                            if (IsAdminNotification)
                            {
                                var adminReminderNotification = new NewToastRequest
                                {
                                    EventId = 1984,
                                    LeadId = SelectedLead.LeadId,
                                    ReminderType = ReminderType.Payment.ToString() + "Reminder: " + SelectedLead.CustomerName,
                                    MessageContent = _session.CurrentUser + " has scheduled a followup reminder for Admin.\r\n" + Message,
                                    ScheduleTime = combinedDateTime, // Schedule the payment reminder for the specified date
                                    TargetUser = "Admin",      // <-- Direct routing targeting admin profile layout
                                    TargetMachine = Environment.MachineName,        // Set this if you want to explicitly target a specific physical terminal name
                                    SenderUser = _session.CurrentUser        // Authored by User
                                };
                                await _notificationRoutingService.DispatchTargetedToastAsync(adminReminderNotification);
                            }

                            // Standard Follow-up/Dead update
                            await _leadService.UpdateLeadFullAsync(SelectedLead, history);
                            await _notificationRoutingService.DispatchTargetedToastAsync(targetNotification);
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

        [RelayCommand]
        private void AddLineItem()
        {
            string finalName = !string.IsNullOrWhiteSpace(CustomProductNameText) ? CustomProductNameText.Trim() : (SelectedProduct?.Name ?? string.Empty);
            if (string.IsNullOrWhiteSpace(finalName) || InputQuantity <= 0) return;

            var catalogMatch = AvailableProducts.FirstOrDefault(x => x.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase));

            // Simply pass the raw input values; the model handles the GST calculation internally!
            var newItem = new ProformaLineItem
            {
                ProductId = catalogMatch?.ProductId,
                ProductName = finalName,
                BatchNo = InputBatchNo,
                Quantity = InputQuantity,
                UnitPrice = InputRate, // Base Rate Exclusive of GST (e.g., ₹95,000.00)
                GstPercent = InputGstPercent, // (e.g., 18)
                IsCustom = catalogMatch == null ? 1 : 0,
                ProductImageBlob = SelectedImageBytes
            };

            ActiveProforma.Items.Add(newItem);
            RecalculateProformaFinancials();

            // Reset control fields
            SelectedProduct = null;
            CustomProductNameText = string.Empty;
            InputBatchNo = string.Empty;
            InputRate = 0;
            InputGstPercent = 0;
            InputQuantity = 1;
            SelectedImageBytes = null;
            SelectedImagePreview = null;
        }

        [RelayCommand]
        private void RemoveCartRow(ProformaLineItem item)
        {
            if (item == null) return;
            ActiveProforma.Items.Remove(item);
            RecalculateProformaFinancials();
        }

        [RelayCommand]
        private void AddExtraCharge()
        {
            if (string.IsNullOrWhiteSpace(InputChargeDescription) || InputChargeAmount == 0) return;

            // Clean out percentage string maps to isolate pure numbers
            decimal gstValue = decimal.TryParse(InputChargeGst.Replace("%", ""), out decimal parsedGst) ? parsedGst : 0;
            decimal absoluteCalculatedImpactAmount = 0;

            if (InputChargeAction.Contains("Percentage"))
            {
                // Compute base percentage values relative to the active product item subtotal bounds
                decimal baseline = ActiveProforma.ItemSubTotal;
                absoluteCalculatedImpactAmount = baseline * (InputChargeAmount / 100);
            }
            else
            {
                absoluteCalculatedImpactAmount = InputChargeAmount;
            }

            // Apply standard systemic tax calculations onto the base value
            if (gstValue > 0)
            {
                absoluteCalculatedImpactAmount += absoluteCalculatedImpactAmount * (gstValue / 100);
            }

            // Flip sign structures if set to subtraction modes
            if (InputChargeAction.Contains("Subtract") || InputChargeAction.Contains("Minus") || InputChargeAction.Contains("(-)"))
            {
                absoluteCalculatedImpactAmount = -Math.Abs(absoluteCalculatedImpactAmount);
            }

            ActiveProforma.ExtraCharges.Add(new ProformaExtraChargeItem
            {
                ChargeDescription = InputChargeDescription.Trim(),
                ChargeAction = InputChargeAction,
                BaseValue = InputChargeAmount,
                GstPercent = gstValue,
                ChargeAmount = absoluteCalculatedImpactAmount
            });

            RecalculateProformaFinancials();

            // Clear control values
            InputChargeDescription = string.Empty;
            InputChargeAmount = 0;
            InputChargeAction = "Add (+)";
            InputChargeGst = "0%";
        }

        [RelayCommand]
        private void RemoveExtraCharge(ProformaExtraChargeItem item)
        {
            if (item == null) return;
            ActiveProforma.ExtraCharges.Remove(item);
            RecalculateProformaFinancials();
        }

        private void RecalculateProformaFinancials()
        {
            // 2. AGGREGATE COMPOUNDED ROW VALUATIONS
            // ItemSubTotal now accurately represents the true commercial sum (Items + GST)
            ActiveProforma.ItemSubTotal = ActiveProforma.Items.Sum(x => (x.Quantity * x.UnitPrice) * (1 + (x.GstPercent / 100)));

            ActiveProforma.ExtraChargesTotal = ActiveProforma.ExtraCharges.Sum(x => x.ChargeAmount);
            ActiveProforma.GrandTotal = ActiveProforma.ItemSubTotal + ActiveProforma.ExtraChargesTotal;
            ActiveProforma.BalanceDue = ActiveProforma.GrandTotal - ActiveProforma.TotalPaid;
        }

        [RelayCommand]
        private async Task SaveProforma()
        {
            if (!ActiveProforma.Items.Any()) return;

            ActiveProforma.LeadId = _customerId;
            ActiveProforma.ProformaNumber = $"PF-{DateTime.Now:yyyyMMdd}-{_customerId}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
            ActiveProforma.CreatedBy = _session.CurrentUser ?? "Admin";

            DateTime combinedDateTime = new DateTime(
                            NextFollowupDate.Year,
                            NextFollowupDate.Month,
                            NextFollowupDate.Day,
                            SelectedTime.Value.Hour,
                            SelectedTime.Value.Minute,
                            0
                        );

            var history = new LeadHistoryEntry
            {
                LeadId = SelectedLead.LeadId,
                Message = "Proforma Created",
                Content = $"Proforma {ActiveProforma.ProformaNumber} created with total {ActiveProforma.GrandTotal:C} of {SelectedLead.CustomerName}",
                UpdatedByContent = $" created Proforma on {DateTime.Now.ToString("dd-MM-yyyy, hh:mm tt")}",
                NextFollowUpDate = combinedDateTime, // Example follow-up
                UpdatedBy = _session.CurrentUser ?? "Admin",
                ActionType = "Call"
            };

            bool success = await _orderService.SaveCompleteProformaWorkflowAsync(ActiveProforma, history);
            if (success)
            {
                ActiveProforma = new ProformaHeader();
                RecalculateProformaFinancials();
            }
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                CustomProductNameText = value.Name;
                InputBatchNo = value.InnerBatchesCollection?[0].BatchNumber ?? string.Empty;
                InputRate = value.SellingPrice;
                InputGstPercent = value.GstPercent;
                InputQuantity = 1;
                SelectedImageBytes = value.ProductImageBytes;

                if (SelectedImageBytes != null && SelectedImageBytes.Length > 0)
                {
                    using var ms = new MemoryStream(SelectedImageBytes);
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = ms;
                    img.EndInit();
                    SelectedImagePreview = img;
                }
            }
        }

        [RelayCommand]
        private void UploadItemImage()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
                Title = "Select Product Specification Media Record"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    byte[] rawBytes = File.ReadAllBytes(openFileDialog.FileName);
                    if (rawBytes.Length > 2.5 * 1024 * 1024) return; // Silent guard size boundary threshold limits

                    SelectedImageBytes = rawBytes;

                    var previewImage = new BitmapImage();
                    using (var ms = new MemoryStream(rawBytes))
                    {
                        previewImage.BeginInit();
                        previewImage.CacheOption = BitmapCacheOption.OnLoad;
                        previewImage.StreamSource = ms;
                        previewImage.EndInit();
                    }
                    SelectedImagePreview = previewImage;
                }
                catch { /* Image decode error fallback safety sink */ }
            }
        }

        [RelayCommand]
        private void LaunchProformaCreationWizard()
        {
            // Set the view state flag high to reveal the creation overlay workspace natively
            IsCreateProforma = true;

            // Auto-navigate user viewport directly onto your entry workspace panel fields
            SelectedTabWorkspaceIndex = 2; // Targets your 'Update Here' layout index tracker
        }

        [RelayCommand]
        private async Task DownloadInvoicePdf(ProformaSummaryItem? item)
        {
            if (item == null) return;
            // Trigger your native QuestPDF file generation streams here...
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task DeleteProformaRecord(ProformaSummaryItem? item)
        {
            if (item == null) return;

            var success = await _leadService.DeleteProformaRecordAsync(item.ProformaId);
            if (success)
            {
                AssociatedProformas.Remove(item);
            }
        }

        [RelayCommand]
        private void Whatsapp(ProformaSummaryItem item)
        {
            if (SelectedLead != null)
            {
                if (!string.IsNullOrEmpty(SelectedLead.Phone))
                {
                    // Phone number se extra characters (+, spaces, dashes) hatane ke liye
                    string cleanNumber = new string(SelectedLead.Phone.Where(char.IsDigit).ToArray());

                    // Agar number 10 digit ka hai, toh country code (e.g., 91) add karna zaroori hai
                    if (cleanNumber.Length == 10)
                    {
                        cleanNumber = "91" + cleanNumber;   
                    }

                    string message = $"Hello {SelectedLead.CustomerName} , \n\n" +
                         $"Thanks for showing trust in us.\n" +
                         $"Your proforma has been created with Id : {item.ProformaNumber} \n" +
                         $"_automated msg, sent from SofricERP_";

                    string encodedMessage = Uri.EscapeDataString(message);

                    // WhatsApp Web URL
                    string url = $"https://web.whatsapp.com/send?phone={cleanNumber}&text={encodedMessage}";

                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        // Error handling agar browser open na ho sake
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
        }
    }
}
