using CallMan.Dialogs;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Models.Enums;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;

namespace CallMan.ViewModels
{
    public partial class MaturedLeadsViewModel : ObservableObject, IDashboardFilterable
    {
        private readonly LeadService _service;
        private readonly SettingService _settingService;
        private readonly IUserSession _session;
        private readonly IDialogService _dialogService;
        private readonly ProductService _productService;
        private readonly OrderService _orderService;
        private readonly OccupiedLocationService _locationService;
        [ObservableProperty] private decimal _totalOutstanding;
        [ObservableProperty] private CustomerStats _customerStats = new();

        private ICollectionView _leadsCollection;

        [ObservableProperty]
        private string _searchText = string.Empty;

        // This is what the DataGrid actually binds to now
        public ICollectionView LeadsCollection => _leadsCollection;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BulkDeleteCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenChangeLeadHolderDialogCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenAssignLabelsDialogCommand))]
        [NotifyCanExecuteChangedFor(nameof(MoveToDeadCommand))]
        private int _selectedLeadsCount;

        // Tracks properties to bind dynamically to our modal popup overlays
        [ObservableProperty] private bool _isChangeLeadHolderOpen;
        [ObservableProperty] private bool _isAssignLabelsOpen;

        // Dropdown lookup source lists
        [ObservableProperty] private ObservableCollection<User> _systemUsersList = new();
        [ObservableProperty] private ObservableCollection<SettingItem> _availableLabelsList = new();

        [ObservableProperty] private User? _targetSelectedUser;
        [ObservableProperty] private bool _transferAsNew;
        [ObservableProperty] private bool _sendNotificationToUser;
        [ObservableProperty] private DateTime _transferSelectedDate = DateTime.Today;
        [ObservableProperty] private SettingItem? _targetSelectedLabel;
        [ObservableProperty] private ObservableCollection<SettingItem> _selectedLabelsList = new();

        private bool _isInitialized;

        public MaturedLeadsViewModel(LeadService service, SettingService settingService, IUserSession session, IDialogService dialogService, ProductService productService, OrderService orderService, OccupiedLocationService locationService)
        {
            _service = service;
            _settingService = settingService;
            _session = session;
            _dialogService = dialogService;
            _productService = productService;
            _orderService = orderService;
            _locationService = locationService;
            _ = LoadInitialDataAsync();
        }

        private async Task LoadInitialDataAsync()
        {
            var users = await _service.GetAllUsersAsync();
            SystemUsersList = new ObservableCollection<User>(users);

            var labels = await _settingService.GetSettingsAsync("LeadLabels");

            AvailableLabelsList = new ObservableCollection<SettingItem>(labels);

            CustomerStats = await _service.GetCustomerFinancialSummaryAsync(1);

            if (_isInitialized) return;
            var data = await _service.GetMaturedLedgerAsync();

            if (_isInitialized) return;
            var list = new ObservableCollection<Lead>(data);

            TotalOutstanding = list.Sum(x => x.TotalBalanceDue);

            _leadsCollection = CollectionViewSource.GetDefaultView(list);

            // 4. Re-apply your search filter logic
            _leadsCollection.Filter = FilterLeads;

            // 5. Notify the UI to refresh the table
            OnPropertyChanged(nameof(LeadsCollection));

            OnPropertyChanged(nameof(TotalOutstanding));
        }

        [RelayCommand]
        public async Task LoadData()
        {
            _isInitialized = false; // Reset flag to allow complete reload
            await LoadInitialDataAsync();
        }

        // This logic runs every time SearchText changes
        partial void OnSearchTextChanged(string value)
        {
            _leadsCollection?.Refresh();
        }

        private bool FilterLeads(object obj)
        {
            if (obj is not Lead lead) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            // Search across multiple fields: Name, Phone, City, and Company
            return lead.CustomerName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   (lead.Phone?.Contains(SearchText) ?? false) ||
                   (lead.AltPhone?.Contains(SearchText) ?? false) ||
                   (lead.City?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.CompanyName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.LeadHolder?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.Pincode?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.District?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.Email?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.Status?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.State?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.AssignedDivisions?.Any(d => d.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                   (lead.LeadLabels?.Any(label => label.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                   (lead.CustomFields?.Any(cf => cf.Value.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                   (lead.CustomFields?.Any(cf => cf.Key.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                   (lead.LeadSource?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.LeadTag?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        [RelayCommand]
        private async Task OpenImportLeadsDialog()
        {
            var vm = App.ServiceProvider.GetRequiredService<ImportViewModel>();
            await vm.InitializeAsync(ImportType.Lead);
            var dialogWindow = new ImportView { DataContext = vm };
            // No need for a close event here since the ImportViewModel can directly call LoadLeads() after a successful import
            vm.RequestClose += (result) =>
            {
                dialogWindow.DialogResult = result;
                dialogWindow.Close();
            };

            if (dialogWindow.ShowDialog() == true)
            {
                // Re-run the query to show the new lead in the DataGrid
                await LoadData();
            }
        }

        [RelayCommand]
        private void ToggleSelectAll(bool? isChecked)
        {
            if (isChecked == null || LeadsCollection == null) return;

            // Cast the elements of the view to your specific Lead model
            foreach (var item in LeadsCollection.Cast<Lead>())
            {
                item.IsSelectedForAction = isChecked.Value;
            }
        }

        /// <summary>
        /// Call this helper method whenever an operator checks/unchecks a row item checkbox in the grid
        /// </summary>
        public void RecalculateSelectionStates()
        {
            SelectedLeadsCount = LeadsCollection.Cast<Lead>().Count(x => x.IsSelectedForAction);
        }

        private bool HasSelection() => SelectedLeadsCount > 0;

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private async Task BulkDelete()
        {
            // 1. Grab all rows where the checkbox is checked
            var selectedLeads = LeadsCollection.Cast<Lead>().Where(l => l.IsSelectedForAction).ToList();

            if (!selectedLeads.Any())
            {
                MessageBox.Show("Please select at least one lead to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. Extract the IDs for your database operation
            List<int> leadIdsToProcess = selectedLeads.Select(l => l.LeadId).ToList();

            var confirm = MessageBox.Show($"Are you sure you want to delete {leadIdsToProcess.Count} selected leads?",
                                         "Confirm Bulk Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            // 3. Pass the IDs list to your service layer
            await _service.BulkDeleteLeadsAsync(leadIdsToProcess);

            // 4. Refresh your grid data
            await LoadData();
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private async Task MoveToDead()
        {
            // 1. Grab all rows where the checkbox is checked
            var selectedLeads = LeadsCollection.Cast<Lead>().Where(l => l.IsSelectedForAction).ToList();

            if (!selectedLeads.Any())
            {
                MessageBox.Show("Please select at least one lead to move.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2. Extract the IDs for your database operation
            List<int> leadIdsToProcess = selectedLeads.Select(l => l.LeadId).ToList();

            var confirm = MessageBox.Show($"Are you sure you want to move {leadIdsToProcess.Count} leads to DEAD?",
                                         "Confirm Batch Dead", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            // 3. Pass the IDs list to your service layer
            await _service.BulkMatureDeadLeadsAsync(leadIdsToProcess);

            // 4. Refresh your grid data
            await LoadData();
            RecalculateSelectionStates();
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void OpenChangeLeadHolderDialog()
        {
            IsChangeLeadHolderOpen = true;
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void OpenAssignLabelsDialog()
        {
            TargetSelectedLabel = null;
            IsAssignLabelsOpen = true;
        }

        [RelayCommand]
        private async Task SubmitChangeLeadHolder()
        {
            if (string.IsNullOrEmpty(TargetSelectedUser?.FullName)) return;

            var targetIds = LeadsCollection.Cast<Lead>().Where(x => x.IsSelectedForAction).Select(x => x.LeadId).ToList();

            var success = await _service.BulkChangeLeadHolderAsync(targetIds, TargetSelectedUser.FullName, TransferAsNew, TransferSelectedDate);

            if (success)
            {
                IsChangeLeadHolderOpen = false;
                await LoadData();
                RecalculateSelectionStates();
            }
        }

        [RelayCommand]
        private async Task SubmitAssignLabels()
        {
            if (SelectedLabelsList == null || SelectedLabelsList.Count == 0) return;

            var selectedLeads = LeadsCollection.Cast<Lead>().Where(x => x.IsSelectedForAction).ToList();

            foreach (var lead in selectedLeads)
            {
                foreach (var lable in SelectedLabelsList)
                {
                    if (!lead.LeadLabels.Contains(lable.Name))
                    {
                        lead.LeadLabels.Add(lable.Name);
                    }
                }

                string updatedJson = System.Text.Json.JsonSerializer.Serialize(lead.LeadLabels);
                await _service.BulkChangeLeadLablesAsync(lead.LeadId, updatedJson);
            }

            IsAssignLabelsOpen = false;
            await LoadData();
            RecalculateSelectionStates();
        }

        partial void OnTargetSelectedLabelChanged(SettingItem? value)
        {
            if (value != null)
            {
                SelectedLabelsList.Add(value);
                // Clear selection so the user can pick the same one again if they delete it
                TargetSelectedLabel = null;
            }
        }

        [RelayCommand]
        public void RemoveLabel(SettingItem? value)
        {
            if (value != null)
            {
                SelectedLabelsList.Remove(value);
            }
        }

        /// <summary>
        /// Explicitly resets all dialog visibility states to force Close actions safely
        /// </summary>
        [RelayCommand]
        private void CloseAllDialogs()
        {
            IsChangeLeadHolderOpen = false;
            IsAssignLabelsOpen = false;

            // Optional: Flush temporary form input models here if needed
            TargetSelectedUser = null;
            TargetSelectedLabel = null;
            SelectedLabelsList = new();
        }

        [RelayCommand]
        private void OpenOrder(Lead selectedLead)
        {
            if (selectedLead == null) return;

            // Access the MainWindowViewModel to switch the view
            if (App.Current.MainWindow.DataContext is MainViewModel mainVM)
            {
                // Call the method we created in MainWindowViewModel to switch screens
                _dialogService.ShowOrderWindow(selectedLead);
            }
        }

        [RelayCommand]
        private async Task OpenAddLeadDialog()
        {
            var vm = App.ServiceProvider.GetRequiredService<AddLeadDialogViewModel>();
            vm.Initialize(null, true); // Pass null for new lead
            var dialogWindow = new AddLeadWindow { DataContext = vm };

            // Subscribe to the close request
            vm.RequestClose += (result) =>
            {
                dialogWindow.DialogResult = result;
                dialogWindow.Close();
            };

            if (dialogWindow.ShowDialog() == true)
            {
                // Re-run the query to show the new lead in the DataGrid
                await LoadData();
            }
        }

        [RelayCommand]
        private void EditLead(Lead leadToEdit)
        {
            if (leadToEdit == null) return;

            // Open the Dialog and pass the lead data
            var vm = App.ServiceProvider.GetRequiredService<AddLeadDialogViewModel>();
            vm.Initialize(leadToEdit);
            var dialogWindow = new AddLeadWindow { DataContext = vm, Title = "Update Lead Info" };

            vm.RequestClose += (result) => {
                dialogWindow.DialogResult = result;
                dialogWindow.Close();
            };

            if (dialogWindow.ShowDialog() == true)
            {
                LoadData(); // Refresh list after update
            }
        }

        [RelayCommand]
        private async Task DeleteLead(Lead leadToDelete)
        {
            if (leadToDelete == null) return;

            var confirm = MessageBox.Show($"Are you sure you want to delete {leadToDelete.CustomerName}?",
                                         "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                await _service.DeleteLeadAsync(leadToDelete.LeadId);
                LoadData(); // Refresh list
            }
        }

        [RelayCommand]
        private void OpenLeadProfile(Lead selectedLead)
        {
            if (selectedLead == null) return;

            // 1. Create the ViewModel for the Dialog
            // We pass the LeadService and the Selected Lead instance
            var profileVm = new CustomerProfileViewModel(_service, _session, _settingService, _productService, _orderService, selectedLead, _locationService);

            // 2. Initialize the Window
            var profileWindow = new CustomerProfileWindow();
            profileWindow.DataContext = profileVm;

            // 3. Set Ownership (Important so the dialog stays centered over your app)
            profileWindow.Owner = System.Windows.Application.Current.MainWindow;

            // 4. Handle Closure (If you want to refresh the grid after an update)
            // You can add a 'RequestClose' event in LeadProfileViewModel like we did for AddLead
            profileVm.RequestClose += (bool isUpdated) =>
            {
                profileWindow.DialogResult = isUpdated;
                profileWindow.Close();
            };

            // 5. Open as Modal
            if (profileWindow.ShowDialog() == true)
            {
                // If data was updated (e.g., status changed to Matured or Dead), refresh the grid
                LoadData();
            }
        }

        [RelayCommand]
        private void ShowHistoryDialog(Lead lead)
        {
            if (lead == null) return;

            // Use DI or a Factory to create the Window
            var historyWindow = new LeadTimelineWindow();

            // Create the ViewModel, inject the service and the selected Lead ID
            var historyVm = new LeadTimelineViewModel(_service, lead.LeadId);
            historyVm.RequestClose += () => historyWindow.Close();

            historyWindow.DataContext = historyVm;
            historyWindow.Owner = App.Current.MainWindow; // Set parent window
            historyWindow.ShowDialog();
        }

        [RelayCommand]
        private void Whatsapp(Lead selectedLead)
        {
            if (selectedLead != null)
            {
                if (!string.IsNullOrEmpty(selectedLead.Phone))
                {
                    // Phone number se extra characters (+, spaces, dashes) hatane ke liye
                    string cleanNumber = new string(selectedLead.Phone.Where(char.IsDigit).ToArray());

                    // Agar number 10 digit ka hai, toh country code (e.g., 91) add karna zaroori hai
                    if (cleanNumber.Length == 10)
                    {
                        cleanNumber = "91" + cleanNumber;
                    }

                    string message = $"Hello {selectedLead.CustomerName} , \n\n" +
                         $"Thanks for showing trust in us.\n" +
                         $"Please feel free to contact us on this whatsapp \n" +
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

        public async void ApplyDashboardFilter(DashboardFilter? filter, DashboardTargetView target)
        {
            try
            {
                _isInitialized = true;

                // 1. Call the service layer to run the fast database check
                var retrievedLeads = await _service.GetCustomerByDashboardContextAsync(target, filter);

                // 2. Clear and append to the existing bound collection to keep references intact
                var list = new ObservableCollection<Lead>(retrievedLeads);

                // 3. Re-aggregate monetary totals
                TotalOutstanding = list.Sum(x => x.TotalBalanceDue);

                // 4. Force the permanent collection view to refresh its text layouts and redrawing loops
                _leadsCollection = CollectionViewSource.GetDefaultView(list);
                _leadsCollection.Filter = FilterLeads;

                // Tell the WPF DataGrid explicitly to drop its visual index cache and look at the new collection
                OnPropertyChanged(nameof(LeadsCollection));
                OnPropertyChanged(nameof(TotalOutstanding));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
