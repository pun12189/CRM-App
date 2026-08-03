using CallMan.Core;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MySqlX.XDevAPI.Common;
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
    public partial class CustomerProfileViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly IUserSession _session;
        private readonly SettingService _settingService;
        private readonly ProductService _productService;
        private readonly OrderService _orderService;
        private readonly OccupiedLocationService _locationService;
        private readonly CategoryService _categoryService;
        private readonly IActionSecurityGuard _securityGuard;

        [ObservableProperty] private CustomerAnalytics _data;

        [ObservableProperty] private string _followupDateLabel;
        [ObservableProperty] private string _followupTimeLabel;

        [ObservableProperty] private Lead _selectedLead;
        [ObservableProperty] private int _selectedTabWorkspaceIndex = 3;
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

        [ObservableProperty]
        private ObservableCollection<LeadHistoryEntry> _historyItems = new();

        [ObservableProperty] private ObservableCollection<ProformaSummaryItem> _associatedProformas = new();
        [ObservableProperty] private ProformaSummaryItem? _selectedHistoricalProforma;

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
        [ObservableProperty] private bool _isInEditMode;

        [ObservableProperty] private ObservableCollection<UploadedDocumentRow> _unifiedDocumentsCollection = new();

        // Dropdown lookup source for the upload dialog header section
        [ObservableProperty] private ObservableCollection<BusinessCategory> _availableDocumentCategories = new();
        [ObservableProperty] private BusinessCategory? _selectedUploadCategory;

        [ObservableProperty] private string _documentCountSummaryText = "0 Files Total";

        public CustomerProfileViewModel(LeadService service, IUserSession session, SettingService settingService, ProductService productService, OrderService orderService, Lead lead, OccupiedLocationService locationService, CategoryService categoryService, IActionSecurityGuard securityGuard, bool isInEditMode = false)
        {
            _service = service;
            _session = session;
            _isInEditMode = isInEditMode;
            _settingService = settingService;
            _categoryService = categoryService;
            _productService = productService;
            _orderService = orderService;
            _customerId = lead.LeadId;
            _selectedLead = lead;
            _locationService = locationService;
            _securityGuard = securityGuard;
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
            else if (value == 3)
            {
                // Timeline Tab clicked: Load the timeline data
                IsInfoTabSelected = false; // Ensure info panel is closed
                _ = LoadTimelineDataAsync();
            }
            else if (value == 7)
            {
                // Timeline Tab clicked: Load the timeline data
                IsInfoTabSelected = false; // Ensure info panel is closed
                _ = LoadUnifiedDocumentsWorkspaceAsync(SelectedLead.LeadId, "Customer");
            }
            else
            {
                // Any other functional tab item clicked: Collapse the drawer overlay
                IsInfoTabSelected = false;
                ActiveProforma = new ProformaHeader();
                RecalculateProformaFinancials();
            }
        }

        partial void OnIsOrderReceivedChanged(bool value)
        {
            FollowupDateLabel = value ? "Next Order Date" : "Next Follow-up Date";
            FollowupTimeLabel = value ? "Next Order Time" : "Next Follow-up Time";
        }

        private void CalculateBalance()
        {
            BalancePayment = OrderValue - PaymentReceived;
        }

        /// <summary>
        /// Invoke this inside ShowLeadWorkspace to cleanly build the single document grid.
        /// Pass "lead" or "customer" as the activeModule string parameter context.
        /// </summary>
        public async Task LoadUnifiedDocumentsWorkspaceAsync(int entityId, string activeModule)
        {
            var categoriesList = await _categoryService.GetCategoriesByModulesAsync(activeModule);

            // 2. Fetch all files currently uploaded for this specific profile ID            

            var filesList = await _categoryService.GetFilesByProfileIdAsync(activeModule, entityId);
            App.Current.Dispatcher.Invoke(() =>
            {
                AvailableDocumentCategories = new ObservableCollection<BusinessCategory>(categoriesList);
                SelectedUploadCategory = AvailableDocumentCategories.FirstOrDefault();

                UnifiedDocumentsCollection.Clear();
                foreach (var file in filesList)
                {
                    UnifiedDocumentsCollection.Add(file);
                }

                DocumentCountSummaryText = $"{filesList.Count()} Total Document Attachments Registered";
            });
        }

        private async Task LoadCustomerData(int leadId)
        {
            // Simple fetch for the summary boxes
            Data = await _service.GetCustomerSummaryAsync(leadId);

            var products = await _productService.GetProductsWithBatchesAsync(1);
            AvailableProducts = new ObservableCollection<Product>(products);

            // Load the string-based mature stages
            var stages = await _settingService.GetSettingsAsync("MatureStages");
            var reasons = await _settingService.GetSettingsAsync("DeadReasons");
            MatureStages = new ObservableCollection<SettingItem>(stages);
            DeadStages = new ObservableCollection<SettingItem>(reasons);

            Metrics = await _locationService.GetSummaryMetricsAsync(SelectedLead.LeadId);

            var result = await _service.LoadHistoricalProformasAsync(SelectedLead.LeadId);
            AssociatedProformas = new ObservableCollection<ProformaSummaryItem>(result);
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
                            BalanceAmount = OrderValue - PaymentReceived,
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

        private async Task LoadTimelineDataAsync()
        {
            if (SelectedLead == null) return;
            var timeline = await _service.GetHistoryByLeadIdAsync(SelectedLead.LeadId);
            HistoryItems = new ObservableCollection<LeadHistoryEntry>(timeline);
        }

        [RelayCommand]
        private void LaunchProformaCreationWizard()
        {
            // Set the view state flag high to reveal the creation overlay workspace natively
            IsCreatingProforma = true;

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

            var success =await _service.DeleteProformaRecordAsync(item.ProformaId);
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

        [RelayCommand]
        private async Task ExecuteNewFileUpload()
        {
            if (SelectedLead == null || SelectedUploadCategory == null)
            {
                MessageBox.Show("Please choose a target Document Category from the dropdown selector first.", "Context Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true, // Bulk multi-uploads to a single category made simple!
                Filter = "Compliance Formats|*.pdf;*.jpg;*.jpeg;*.png;*.xlsx;*.docx"
            };

            if (fileDialog.ShowDialog() == true)
            {
                string moduleContext = SelectedLead.Status?.ToLower() == "matured" ? "Customer" : "Lead";
                var success = await _categoryService.UploadDocumentAsync(fileDialog.FileNames, moduleContext, SelectedUploadCategory, SelectedLead.LeadId, _session.CurrentUser);

                if (success)
                {
                    MessageBox.Show("File(s) uploaded successfully!", "Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("File upload failed. Please check the logs for details.", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // Refresh grid matrix instantly
                await LoadUnifiedDocumentsWorkspaceAsync(SelectedLead.LeadId, moduleContext);
            }
        }

        [RelayCommand]
        private async Task DeleteDocumentFile(UploadedDocumentRow selectedRow)
        {
            if (selectedRow == null) return;

            var result = MessageBox.Show($"Are you sure you want to permanently delete '{selectedRow.FileName}'?", "Confirm Purge", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                // 1. Clean up physical host disk block
                if (System.IO.File.Exists(selectedRow.StoragePath))
                {
                    System.IO.File.Delete(selectedRow.StoragePath);
                }

                // 2. Clear out database pointer record
                await _categoryService.DeleteDocumentRecordAsync(selectedRow.DocumentId);

                // 3. Refresh display layout
                string moduleContext = SelectedLead.Status?.ToLower() == "matured" ? "Customer" : "Lead";
                await LoadUnifiedDocumentsWorkspaceAsync(SelectedLead.LeadId, moduleContext);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while purging file instance: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ReplaceDocumentFile(UploadedDocumentRow selectedRow)
        {
            if (selectedRow == null || SelectedLead == null) return;

            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Supported Files|*.pdf;*.jpg;*.jpeg;*.png;*.xlsx;*.docx",
                Title = $"Replace Document: {selectedRow.FileName}"
            };

            if (fileDialog.ShowDialog() == true)
            {
                try
                {
                    // 1. Delete the old physical file to prevent disk bloat
                    if (System.IO.File.Exists(selectedRow.StoragePath))
                    {
                        System.IO.File.Delete(selectedRow.StoragePath);
                    }

                    // 2. Write the new file instance exactly to the same vault directory layout path
                    string newLocalPath = fileDialog.FileName;
                    string extension = System.IO.Path.GetExtension(newLocalPath);
                    string cleanName = System.IO.Path.GetFileName(newLocalPath);

                    string targetDir = System.IO.Path.GetDirectoryName(selectedRow.StoragePath)!;
                    string dynamicStoragePath = System.IO.Path.Combine(targetDir, $"{Guid.NewGuid()}_{cleanName}");

                    System.IO.File.Copy(newLocalPath, dynamicStoragePath, true);

                    // 3. Update the tracking row properties inside MySQL server records mapping
                    await _categoryService.ReplaceUploadDocumentAsync(cleanName, dynamicStoragePath, _session.CurrentUser, selectedRow.DocumentId);

                    // 4. Instantly refresh the UI matrix grid list
                    string moduleContext = SelectedLead.Status?.ToLower() == "matured" ? "Customer" : "Lead";
                    await LoadUnifiedDocumentsWorkspaceAsync(SelectedLead.LeadId, moduleContext);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to replace document attachment: {ex.Message}", "IO Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task DownloadDocumentFile(UploadedDocumentRow selectedRow)
        {
            bool accessGranted = await _securityGuard.IsActionAuthorizedAsync();
            if (!accessGranted) return; // Halt execution path immediately

            if (selectedRow == null || string.IsNullOrEmpty(selectedRow.StoragePath)) return;

            if (!System.IO.File.Exists(selectedRow.StoragePath))
            {
                MessageBox.Show("The source document file could not be discovered on server storage paths.", "File Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Initialize native save dialog frame
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = selectedRow.FileName, // Prefills original filename automatically
                Filter = $"File Extension (*{System.IO.Path.GetExtension(selectedRow.StoragePath)})|*{System.IO.Path.GetExtension(selectedRow.StoragePath)}",
                Title = "Download Document Reference Copy As"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.Copy(selectedRow.StoragePath, saveDialog.FileName, true);
                    MessageBox.Show("File copied and saved locally successfully!", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not export document file copy: {ex.Message}", "Download Fault", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
