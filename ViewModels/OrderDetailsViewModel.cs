using Tijori.Core;
using Tijori.Dialogs;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Services;
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

namespace Tijori.ViewModels
{
    public partial class OrderDetailsViewModel : ObservableObject
    {
        private readonly AllOrdersViewModel _parentViewModel;
        private readonly LeadService _leadService;
        private readonly OrderService _orderService;
        private readonly CategoryService _categoryService;
        private readonly IUserSession _userSession;
        private readonly IOrderHistoryService _orderHistoryService;
        private readonly IActionSecurityGuard _securityGuard;

        #region OBSERVABLE PROPERTIES
        // Reference to parent ViewModel so the Back button can trigger HideLeadWorkspaceCommand
        [ObservableProperty]
        private Order _selectedOrder;

        [ObservableProperty]
        private Lead _selectedLead;

        [ObservableProperty]
        private ObservableCollection<PaymentEntry> _payments = new();

        [ObservableProperty]
        private ObservableCollection<OrderHistoryEntry> _historyLogs = new();
        [ObservableProperty] private int _selectedTabWorkspaceIndex;

        [ObservableProperty] private ObservableCollection<UploadedDocumentRow> _unifiedDocumentsCollection = new();

        // Dropdown lookup source for the upload dialog header section
        [ObservableProperty] private ObservableCollection<BusinessCategory> _availableDocumentCategories = new();
        [ObservableProperty] private BusinessCategory? _selectedUploadCategory;

        [ObservableProperty] private string _documentCountSummaryText = "0 Files Total";

        #endregion

        #region BUTTON CAN-EXECUTE STATE CHECKS

        /// <summary>
        /// Accept button is enabled ONLY if the order is NOT already Accepted or Dispatched.
        /// </summary>
        public bool CanAcceptOrder => SelectedOrder != null
            && !string.Equals(SelectedOrder.Status, "Accepted", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(SelectedOrder.Status, "Dispatched", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Dispatch button is enabled ONLY if the order is NOT already Dispatched.
        /// </summary>
        public bool CanDispatchOrder => SelectedOrder != null
            && !string.Equals(SelectedOrder.Status, "Dispatched", StringComparison.OrdinalIgnoreCase);

        #endregion

        #region DYNAMIC FINANCIAL SUMMARY PROPERTIES

        public decimal SubTotalAmount => SelectedOrder?.Items?.Sum(i => i.SubTotal) ?? 0;
        public decimal TotalGstAmount => SelectedOrder?.Items?.Sum(i => i.GstAmount) ?? 0;
        public decimal ExtraChargesSum => SelectedOrder?.ExtraCharges?.Sum(c => c.TotalCharge) ?? 0;
        public decimal GrandTotal
        {
            get
            {
                if (SelectedOrder == null) return 0;

                // Mode 1: Itemized Order (Sum of products + extra charges)
                if (SelectedOrder.Items != null && SelectedOrder.Items.Count > 0)
                {
                    return SubTotalAmount + TotalGstAmount + ExtraChargesSum;
                }

                // Mode 2: Flat-Amount Order (Direct TotalAmount + extra charges)
                return SelectedOrder.TotalAmount + ExtraChargesSum;
            }
        }
        public decimal OutstandingBalance => Math.Max(0, GrandTotal - (SelectedOrder?.AmountPaid ?? 0));

        #region TAB 2: PAYMENT DETAILS COMPUTED PROPERTIES

        public decimal TotalPaymentsReceived => Payments?.Sum(p => p.AmountReceived) ?? 0;

        public decimal OutstandingPaymentBalance => Math.Max(0, GrandTotal - TotalPaymentsReceived);

        public string OrderPaymentStatusBadge
        {
            get
            {
                if (TotalPaymentsReceived >= GrandTotal && GrandTotal > 0)
                    return "Fully Paid";
                if (TotalPaymentsReceived > 0)
                    return "Partially Paid";
                return "Unpaid";
            }
        }

        #endregion

        #endregion

        #region CONSTRUCTOR

        public OrderDetailsViewModel(AllOrdersViewModel parentViewModel, Order selectedOrder, LeadService leadService, OrderService orderService, CategoryService categoryService, IUserSession userSession, IOrderHistoryService orderHistoryService, IActionSecurityGuard securityGuard)
        {
            _parentViewModel = parentViewModel;
            _selectedOrder = selectedOrder;
            _leadService = leadService;
            _orderService = orderService;
            _categoryService = categoryService;
            _userSession = userSession;
            _orderHistoryService = orderHistoryService;
            _securityGuard = securityGuard;
            // Load dummy/real collection data for testing if child collections are null
            _ = LoadFullOrderDetailsAsync();            
        }

        partial void OnSelectedTabWorkspaceIndexChanged(int value)
        {
            if (value == 2)
            {
               _ = LoadOrderHistoryAsync();

            }
            else if (value == 1)
            {
                _ = LoadOrderPaymentsAsync();
            }
            else if (value == 3)
            {
                _ = LoadUnifiedDocumentsWorkspaceAsync(SelectedOrder.OrderId, "Orders");
            }            
        }

        public async Task LoadOrderHistoryAsync()
        {
            if (SelectedOrder == null || SelectedOrder.OrderId <= 0) return;

            try
            {
                var logs = await _orderHistoryService.GetHistoryByOrderIdAsync(SelectedOrder.OrderId);
                HistoryLogs = new ObservableCollection<OrderHistoryEntry>(logs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching order history: {ex.Message}");
            }
        }

        /// <summary>
        /// Fetches payment details from database for the active order
        /// </summary>
        public async Task LoadOrderPaymentsAsync()
        {
            if (SelectedOrder == null || SelectedOrder.OrderId <= 0) return;

            try
            {
                var paymentList = await _orderService.GetPaymentsByOrderIdAsync(SelectedOrder.OrderId);

                // Populate UI Collection on the UI thread
                Payments = new ObservableCollection<PaymentEntry>(paymentList);

                // Notify UI to recalculate live totals bar
                OnPropertyChanged(nameof(TotalPaymentsReceived));
                OnPropertyChanged(nameof(OutstandingPaymentBalance));
                OnPropertyChanged(nameof(OrderPaymentStatusBadge));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading order payments: {ex.Message}");
            }
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

        private async Task LoadLeadDetailsAsync()
        {
            if (SelectedOrder == null || SelectedOrder.LeadId <= 0) return;

            try
            {
                // Fetch lead details using your LeadService
                SelectedLead = await _leadService.GetLeadByIdAsync(SelectedOrder.LeadId);
            }
            catch (Exception ex)
            {
                // Log or handle service exception
                System.Diagnostics.Debug.WriteLine($"Failed to load lead details: {ex.Message}");
            }
        }

        public async Task LoadFullOrderDetailsAsync()
        {
            if (SelectedOrder == null || SelectedOrder.OrderId <= 0) return;

            try
            {
                // Fetch complete Order with loaded Items and ExtraCharges from database
                var fullOrder = await _orderService.GetOrderDetailsByIdAsync(SelectedOrder.OrderId);

                if (fullOrder != null)
                {
                    SelectedOrder = fullOrder;

                    // Re-hook collection change listeners for dynamic total calculations
                    await InitializeAndHookCollections();

                    // Refresh UI bindings and financial properties
                    RefreshCalculatedTotals();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching full order details: {ex.Message}");
            }
        }

        private async Task InitializeAndHookCollections()
        {
            if (SelectedOrder == null) return;

            await LoadLeadDetailsAsync();

            // Ensure sub-collections exist
            SelectedOrder.Items ??= new ObservableCollection<OrderItem>();
            SelectedOrder.ExtraCharges ??= new ObservableCollection<ExtraCharge>();

            // Listen to item/charge collection changes to keep UI totals reactive
            SelectedOrder.Items.CollectionChanged += (s, e) => RefreshCalculatedTotals();
            SelectedOrder.ExtraCharges.CollectionChanged += (s, e) => RefreshCalculatedTotals();
        }

        private void RefreshCalculatedTotals()
        {
            OnPropertyChanged(nameof(SubTotalAmount));
            OnPropertyChanged(nameof(TotalGstAmount));
            OnPropertyChanged(nameof(ExtraChargesSum));
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(OutstandingBalance));
        }        

        #endregion

        #region NAVIGATION COMMANDS

        [RelayCommand]
        private void BackToOrders()
        {
            // Executes the parent ViewModel's command to flip WorkspaceViewIsActive back to false
            _parentViewModel?.HideLeadWorkspace();
        }

        #endregion

        #region TAB 1: ORDER DETAILS COMMANDS

        [RelayCommand]
        private async Task SendEmailInvoiceAsync()
        {
            if (SelectedOrder == null) return;
            // TODO: Execute Email Dispatch Logic
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task SendWhatsappInvoiceAsync()
        {
            if (SelectedOrder == null) return;
            // TODO: Execute WhatsApp Dispatch Logic
            await Task.CompletedTask;
        }

        [RelayCommand(CanExecute = nameof(CanAcceptOrder))]
        private async Task AcceptOrderAsync()
        {
            if (SelectedOrder == null || SelectedOrder.OrderId <= 0) return;

            string oldStatus = SelectedOrder.Status;
            string newStatus = "Accepted";

            // 1. Update database status
            bool updated = await _orderService.UpdateOrderStatusAsync(SelectedOrder.OrderId, newStatus);

            if (updated)
            {
                SelectedOrder.Status = newStatus;

                // 2. Log in OrderHistory table
                var historyLog = new OrderHistoryEntry
                {
                    OrderId = SelectedOrder.OrderId,
                    LeadId = SelectedOrder.LeadId,
                    ActionTitle = "Order Accepted",
                    Description = $"Order #{SelectedOrder.FormattedOrderId} was accepted successfully.",
                    ActionType = "StatusChange",
                    PreviousState = oldStatus,
                    NewState = newStatus,
                    PerformedBy = _userSession?.CurrentUser ?? "Admin",
                    LogDate = DateTime.Now,
                    IsImportant = true
                };

                await _orderHistoryService.LogActivityAsync(historyLog);
                await LoadOrderHistoryAsync();

                // 3. Notify Commands to recalculate button enablement
                RefreshButtonStates();

                // 4. Confirmation Message Box
                MessageBox.Show(
                    $"Order #{SelectedOrder.FormattedOrderId} has been accepted successfully!",
                    "Order Accepted",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        [RelayCommand(CanExecute = nameof(CanDispatchOrder))]
        private async Task DispatchOrderAsync()
        {
            if (SelectedOrder == null || SelectedOrder.OrderId <= 0) return;

            // 1. Determine Payment Status for tracking message
            string paymentInfoMessage;
            MessageBoxImage paymentIcon = MessageBoxImage.Information;

            if (OutstandingPaymentBalance == 0 && TotalPaymentsReceived > 0)
            {
                paymentInfoMessage = "Payment Status: FULLY PAID";
            }
            else if (TotalPaymentsReceived > 0)
            {
                paymentInfoMessage = $"Payment Status: PARTIALLY PAID\nRemaining Outstanding: ₹ {OutstandingPaymentBalance:N2}";
                paymentIcon = MessageBoxImage.Warning;
            }
            else
            {
                paymentInfoMessage = $"Payment Status: UNPAID\nTotal Pending Amount: ₹ {GrandTotal:N2}";
                paymentIcon = MessageBoxImage.Warning;
            }

            // Optional: Ask for confirmation before dispatching
            var result = MessageBox.Show(
                $"Are you sure you want to dispatch Order #{SelectedOrder.FormattedOrderId}?\n\n{paymentInfoMessage}",
                "Confirm Dispatch",
                MessageBoxButton.YesNo,
                paymentIcon);

            if (result != MessageBoxResult.Yes) return;

            string oldStatus = SelectedOrder.Status;
            string newStatus = "Dispatched";

            // 2. Update database status
            bool updated = await _orderService.UpdateOrderStatusAsync(SelectedOrder.OrderId, newStatus);

            if (updated)
            {
                SelectedOrder.Status = newStatus;

                // 3. Log in OrderHistory table
                var historyLog = new OrderHistoryEntry
                {
                    OrderId = SelectedOrder.OrderId,
                    LeadId = SelectedOrder.LeadId,
                    ActionTitle = "Order Dispatched",
                    Description = $"Order #{SelectedOrder.FormattedOrderId} dispatched. {paymentInfoMessage.Replace("\n", " - ")}",
                    ActionType = "Dispatched",
                    PreviousState = oldStatus,
                    NewState = newStatus,
                    PerformedBy = _userSession?.CurrentUser ?? "Admin",
                    LogDate = DateTime.Now,
                    IsImportant = true
                };

                await _orderHistoryService.LogActivityAsync(historyLog);
                await LoadOrderHistoryAsync();

                // 4. Notify Commands to disable both Accept and Dispatch buttons
                RefreshButtonStates();

                // 5. Success Message Box
                MessageBox.Show(
                    $"Order #{SelectedOrder.FormattedOrderId} has been dispatched successfully!\n\n{paymentInfoMessage}",
                    "Order Dispatched",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Forces WPF command bindings to re-evaluate CanExecute checks.
        /// </summary>
        private void RefreshButtonStates()
        {
            AcceptOrderCommand.NotifyCanExecuteChanged();
            DispatchOrderCommand.NotifyCanExecuteChanged();
        }

        #endregion

        #region TAB 2: PAYMENT DETAILS COMMANDS

        [RelayCommand]
        private async Task OpenAddPaymentDialogAsync()
        {
            if (SelectedOrder == null) return;

            // 1. Instantiate the AddPaymentViewModel using current Order, LeadService, and IUserSession instances
            var paymentVm = new AddPaymentViewModel(SelectedOrder, _leadService, _userSession, _orderHistoryService);

            // 2. Prepare the Dialog View UserControl and assign its DataContext
            var paymentDialogView = new AddPaymentWindow
            {
                DataContext = paymentVm
            };

            bool isPaymentSaved = false;

            // 3. Subscribe to the RequestClose event to automatically dismiss the Material Design DialogHost
            paymentVm.RequestClose += (success) =>
            {
                isPaymentSaved = success;

                // Closes the open DialogHost session
                paymentDialogView.Close();
            };

            // 4. Display the modal dialog asynchronously
            paymentDialogView.ShowDialog();

            // 5. If the payment was successfully recorded, reload the updated payments list from the database
            if (isPaymentSaved)
            {
                // Refresh payments list & dynamic financial summary badges
                await LoadOrderPaymentsAsync();
            }
        }        

        #endregion

        #region TAB 4: DOCUMENTS COMMANDS

        [RelayCommand]
        private async Task UploadDocumentAsync()
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
                var success = await _categoryService.UploadDocumentAsync(fileDialog.FileNames, "Orders", SelectedUploadCategory, SelectedOrder.OrderId, _userSession.CurrentUser);

                if (success)
                {
                    MessageBox.Show("File(s) uploaded successfully!", "Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("File upload failed. Please check the logs for details.", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                // Refresh grid matrix instantly
                await LoadUnifiedDocumentsWorkspaceAsync(SelectedOrder.OrderId, "Orders");
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
                    await _categoryService.ReplaceUploadDocumentAsync(cleanName, dynamicStoragePath, _userSession.CurrentUser, selectedRow.DocumentId);

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

        #endregion
    }
}
