using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Pdf.Filters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Tijori.Dialogs;
using Tijori.Helper;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class LeadFollowupViewModel : ObservableObject
    {
        private readonly LeadService _leadService;
        private readonly SettingService _settingService;
        private readonly IUserSession _session;
        private readonly IDialogService _dialogService;
        private readonly ProductService _productService;
        private readonly OrderService _orderService;
        private readonly OccupiedLocationService _locationService;
        private readonly NotificationRoutingService _routingService;
        private readonly StaffService _staffService;
        private readonly CategoryService _categoryService;
        private readonly IActionSecurityGuard _securityGuard;
        private ICollectionView _leadsCollection;
        private List<Lead> _rawLeadsList = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        // This is what the DataGrid actually binds to now
        public ICollectionView LeadsCollection => _leadsCollection;

        [ObservableProperty] private ObservableCollection<SettingItem> _leadTags = new();

        [ObservableProperty] private LeadViewMode _currentMode = LeadViewMode.AllLeads;

        [ObservableProperty]
        private ObservableCollection<Lead> _leads = new();

        [ObservableProperty]
        private Lead? _selectedLead;

        [ObservableProperty]
        private SettingItem? _selectedLeadTag;

        [ObservableProperty]
        private bool _isFuture;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BulkDeleteCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenChangeLeadHolderDialogCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenAssignLabelsDialogCommand))]
        [NotifyCanExecuteChangedFor(nameof(MoveToDeadCommand))]
        private int _selectedLeadsCount;

        // Tracks properties to bind dynamically to our modal popup overlays
        [ObservableProperty] private bool _isChangeLeadHolderOpen;
        [ObservableProperty] private bool _isAssignLabelsOpen;

        [ObservableProperty] private int _selectedTabIndex = 0;
        [ObservableProperty] private int _allCount;
        [ObservableProperty] private int _leadsCount;
        [ObservableProperty] private int _customersCount;
        [ObservableProperty] private int _remindersCount;

        // Dropdown lookup source lists
        [ObservableProperty] private ObservableCollection<User> _systemUsersList = new();
        [ObservableProperty] private ObservableCollection<SettingItem> _availableLabelsList = new();

        [ObservableProperty] private User? _targetSelectedUser;
        [ObservableProperty] private bool _transferAsNew;
        [ObservableProperty] private bool _sendNotificationToUser;
        [ObservableProperty] private DateTime _transferSelectedDate = DateTime.Today;
        [ObservableProperty] private SettingItem? _targetSelectedLabel;
        [ObservableProperty] private ObservableCollection<SettingItem> _selectedLabelsList = new();

        [ObservableProperty] private bool _workspaceViewIsActive;
        [ObservableProperty] private Lead? _activeProfileLead;

        [ObservableProperty]
        private object _tabsDataContext;

        private ICollectionView _cardsCollection;
        public ICollectionView CardsCollection => _cardsCollection;
        [ObservableProperty]
        private WorkspaceViewMode _currentViewMode = WorkspaceViewMode.Table;

        public LeadFollowupViewModel(LeadService leadService, SettingService settingService, IUserSession session, IDialogService dialogService, ProductService productService, OrderService orderService, OccupiedLocationService locationService, NotificationRoutingService routingService, StaffService staffService, CategoryService categoryService, IActionSecurityGuard securityGuard)
        {
            _leadService = leadService;
            _settingService = settingService;
            _session = session;
            _dialogService = dialogService;
            _staffService = staffService;
            _productService = productService;
            _orderService = orderService;
            _locationService = locationService;
            _routingService = routingService;
            _categoryService = categoryService;
            _securityGuard = securityGuard;
            LoadLeads();
        }

        public async Task InitializeAsync(LeadViewMode mode)
        {
            CurrentMode = mode;
            if (mode == LeadViewMode.FutureFollowUp)
            {
                IsFuture = true;
            }
            else
            {
                IsFuture = false;
            }

            await LoadLeads();
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

            if (CardsCollection != null)
            {
                foreach (var card in CardsCollection.Cast<ITileCardItem>())
                {
                    card.IsSelectedForAction = isChecked.Value;
                }
            }
        }

        partial void OnSelectedTabIndexChanged(int value)
        {
            _leadsCollection?.Refresh();
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
            await _leadService.BulkDeleteLeadsAsync(leadIdsToProcess);

            // 4. Refresh your grid data
            await LoadLeads();
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
            await _leadService.BulkDeadLeadsAsync(leadIdsToProcess);

            // 4. Refresh your grid data
            await LoadLeads();
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

            var success = await _leadService.BulkChangeLeadHolderAsync(targetIds, TargetSelectedUser.FullName, TransferAsNew, TransferSelectedDate);

            if (success)
            {
                IsChangeLeadHolderOpen = false;
                await LoadLeads();
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
                await _leadService.BulkChangeLeadLablesAsync(lead.LeadId, updatedJson);
            }

            IsAssignLabelsOpen = false;
            await LoadLeads();
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

        private void UpdateTabCounts()
        {
            if (_rawLeadsList == null) return;

            AllCount = _rawLeadsList.Count;

            // Leads: Non-matured / Not tagged as Customer
            LeadsCount = _rawLeadsList.Count(l => !IsCustomer(l));

            // Customers: Matured status or tagged as Customer
            CustomersCount = _rawLeadsList.Count(l => IsCustomer(l));

            // Reminders: Follow-up date is today or overdue
            RemindersCount = _rawLeadsList.Count(l => IsReminderDue(l));
        }

        private async Task LoadLeads()
        {
            var users = await _staffService.GetAllStaffAsync();
            SystemUsersList = new ObservableCollection<User>(users);

            var labels = await _settingService.GetSettingsAsync("LeadLabels");

            AvailableLabelsList = new ObservableCollection<SettingItem>(labels);

            // 1. Call the new service method that joins Leads with their latest History
            var data = await _leadService.GetAllFollowupLeadsAsync(CurrentMode);

            // 2. Wrap the result in an ObservableCollection
            var list = new ObservableCollection<Lead>(data);

            var userId = _session.CurrentUser;
            if (userId.ToLower() != "admin")
            {
                list = new ObservableCollection<Lead>(list.Where(l => l.LeadHolder == userId));
            }

            _rawLeadsList = list.ToList();
            UpdateTabCounts();

            // 3. Update the CollectionView (the actual source for your DataGrid)
            _leadsCollection = CollectionViewSource.GetDefaultView(list);

            // 4. Re-apply your search filter logic
            _leadsCollection.Filter = CombinedFilter;

            // 5. Notify the UI to refresh the table
            OnPropertyChanged(nameof(LeadsCollection));

            var cardList = new ObservableCollection<ITileCardItem>(list.Select(l => l.ToTileCard()));
            _cardsCollection = CollectionViewSource.GetDefaultView(cardList);
            _cardsCollection.Filter = FilterCards;
            OnPropertyChanged(nameof(CardsCollection));

            LeadTags = new ObservableCollection<SettingItem>(await _settingService.GetSettingsAsync("LeadTags"));
        }

        private bool FilterCards(object obj)
        {
            if (obj is not ITileCardItem card) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            if (card.RawModel is Lead lead)
            {
                return FilterLeads(lead);
            }

            return card.PrimaryTitle.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   card.HeaderTag.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   card.OwnerOrMetaLabel.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        // This logic runs every time SearchText changes
        partial void OnSearchTextChanged(string value)
        {
            _leadsCollection?.Refresh();
            _cardsCollection?.Refresh();
        }

        private bool IsReminderDue(Lead l)
        {
            if (l.LatestUpdate?.NextFollowUpDate == null) return false;
            return l.LatestUpdate.NextFollowUpDate.Value.Date <= DateTime.Today;
        }

        private bool CombinedFilter(object obj)
        {
            if (obj is not Lead lead) return false;

            // 1. Apply Tab Filtering
            switch (SelectedTabIndex)
            {
                case 1: // Leads Tab
                    if (IsCustomer(lead)) return false;
                    break;

                case 2: // Customer Tab
                    if (!IsCustomer(lead)) return false;
                    break;

                case 3: // Reminders Tab
                    if (!IsReminderDue(lead)) return false;
                    break;

                case 0: // All Tab
                default:
                    break;
            }

            // 2. Apply Text Search Filtering
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

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

        // This logic runs every time SearchText changes
        partial void OnSearchTextChanged(string value)
        {
            _leadsCollection?.Refresh();
        }        

        [RelayCommand]
        private void OpenAddLeadDialog()
        {
            var vm = App.ServiceProvider.GetRequiredService<AddLeadDialogViewModel>();
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
                LoadLeads();
            }
        }

        // ==========================================
        // 6. GENERIC TILE / CARD ACTION DISPATCHERS
        // ==========================================
        [RelayCommand]
        private async Task EditItem(object? rawModel)
        {
            if (rawModel is Lead lead)
            {
                await EditLead(lead);
            }
        }

        [RelayCommand]
        private async Task EditLead(Lead leadToEdit)
        {
            if (leadToEdit == null) return;

            // Open the Dialog and pass the lead data
            var vm = App.ServiceProvider.GetRequiredService<AddLeadDialogViewModel>();
            await vm.Initialize(leadToEdit);
            var dialogWindow = new AddLeadWindow { DataContext = vm, Title = "Update Lead Info" };

            vm.RequestClose += (result) => {
                dialogWindow.DialogResult = result;
                dialogWindow.Close();
            };

            if (dialogWindow.ShowDialog() == true)
            {
               await LoadLeads(); // Refresh list after update
            }
        }

        [RelayCommand]
        private async Task DeleteItem(object? rawModel)
        {
            if (rawModel is Lead lead)
            {
                await DeleteLead(lead);
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
                await _leadService.DeleteLeadAsync(leadToDelete.LeadId);
                LoadLeads(); // Refresh list
            }
        }

        [RelayCommand]
        private void ShowHistoryDialog(Lead lead)
        {
            if (lead == null) return;

            // Use DI or a Factory to create the Window
            var historyWindow = new LeadTimelineWindow();

            // Create the ViewModel, inject the service and the selected Lead ID
            var historyVm = new LeadTimelineViewModel(_leadService, lead.LeadId);
            historyVm.RequestClose += () => historyWindow.Close();

            historyWindow.DataContext = historyVm;
            historyWindow.Owner = App.Current.MainWindow; // Set parent window
            historyWindow.ShowDialog();
        }

        [RelayCommand]
        private void MoreOptions(object? rawModel)
        {
            if (rawModel is Lead lead)
            {
                OpenLeadProfile(lead);
            }
        }

        [RelayCommand]
        private void OpenLeadProfile(Lead selectedLead)
        {
            if (selectedLead == null) return;

            // 1. Create the ViewModel for the Dialog
            // We pass the LeadService and the Selected Lead instance
            dynamic profileVm = new LeadProfileViewModel(_leadService, _settingService, _session, selectedLead, _locationService, _routingService, _productService, _orderService, _categoryService, _securityGuard);

            if (selectedLead.Status?.ToLower() == "matured")
            {
                profileVm = new CustomerProfileViewModel(_leadService, _session, _settingService, _productService, _orderService, selectedLead, _locationService, _categoryService, _securityGuard);
            }

            // 2. Initialize the Window
            Window profileWindow = new LeadProfileWindow();
            if (selectedLead.Status?.ToLower() == "matured")
            {
                profileWindow = new CustomerProfileWindow();
            }

            profileWindow.DataContext = profileVm;

            // 3. Set Ownership (Important so the dialog stays centered over your app)
            profileWindow.Owner = System.Windows.Application.Current.MainWindow;

            // 4. Handle Closure (If you want to refresh the grid after an update)
            // You can add a 'RequestClose' event in LeadProfileViewModel like we did for AddLead
            profileVm.RequestClose += (Action<bool>)(isUpdated =>
            {
                profileWindow.DialogResult = isUpdated;
                profileWindow.Close();
            });

            // 5. Open as Modal
            if (profileWindow.ShowDialog() == true)
            {
                // If data was updated (e.g., status changed to Matured or Dead), refresh the grid
                LoadLeads();
            }
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

        [RelayCommand]
        private async Task RefreshLeads(LeadViewMode viewMode)
        {
            // 1. Call the new service method that joins Leads with their latest History
            var data = await _leadService.GetAllFollowupTodayPendingAsync(viewMode);

            // 2. Wrap the result in an ObservableCollection
            var list = new ObservableCollection<Lead>(data);

            var userId = _session.CurrentUser;
            if (userId.ToLower() != "admin")
            {
                list = new ObservableCollection<Lead>(list.Where(l => l.LeadHolder == userId));
            }

            _rawLeadsList = list.ToList();
            UpdateTabCounts();

            // 3. Update the CollectionView (the actual source for your DataGrid)
            _leadsCollection = CollectionViewSource.GetDefaultView(list);

            // 4. Re-apply your search filter logic
            _leadsCollection.Filter = CombinedFilter;

            // 5. Notify the UI to refresh the table
            OnPropertyChanged(nameof(LeadsCollection));

            this.SelectedLeadTag = null;
        }

        partial void OnSelectedLeadTagChanged(SettingItem? value)
        {
            if (value == null)
            {
                _ = LoadLeads(); // If no tag is selected, show all leads
            }
            else
            {
                _ = LoadLeadTags(value.Id); // Load leads filtered by the selected tag
            }
        }

        private async Task LoadLeadTags(int id)
        {
            // 1. Call the new service method that joins Leads with their latest History
            var data = await _leadService.GetAllLeadsWithLeadTagsAsync(id);

            // 2. Wrap the result in an ObservableCollection
            var list = new ObservableCollection<Lead>(data);

            var userId = _session.CurrentUser;
            if (userId.ToLower() != "admin")
            {
                list = new ObservableCollection<Lead>(list.Where(l => l.LeadHolder == userId));
            }

            _rawLeadsList = list.ToList();
            UpdateTabCounts();

            // 3. Update the CollectionView (the actual source for your DataGrid)
            _leadsCollection = CollectionViewSource.GetDefaultView(list);

            // 4. Re-apply your search filter logic
            _leadsCollection.Filter = CombinedFilter;

            // 5. Notify the UI to refresh the table
            OnPropertyChanged(nameof(LeadsCollection));

            var cardList = new ObservableCollection<ITileCardItem>(list.Select(l => l.ToTileCard()));
            _cardsCollection = CollectionViewSource.GetDefaultView(cardList);
            _cardsCollection.Filter = FilterCards;
            OnPropertyChanged(nameof(CardsCollection));
        }

        [RelayCommand]
        private void UpdateStatus(object? rawModel)
        {
            if (rawModel is Lead lead)
            {
                ShowLeadWorkspace(lead);
            }
        }

        [RelayCommand]
        public void ShowLeadWorkspace(Lead selectedLead)
        {
            if (selectedLead == null) return;
            ActiveProfileLead = selectedLead;

            dynamic profileVm = new LeadProfileViewModel(_leadService, _settingService, _session, selectedLead, _locationService, _routingService, _productService, _orderService, _categoryService, _securityGuard, true);

            if (selectedLead.Status?.ToLower() == "matured")
            {
                profileVm = new CustomerProfileViewModel(_leadService, _session, _settingService, _productService, _orderService, selectedLead, _locationService, _categoryService, _securityGuard, true);
            }

            this.TabsDataContext = profileVm;
            WorkspaceViewIsActive = true; // Swaps grid out for profile workspace view layout instantly
        }

        [RelayCommand]
        public void HideLeadWorkspace()
        {
            WorkspaceViewIsActive = false;
            this.TabsDataContext = null;
            this.ActiveProfileLead = null;
        }

        [RelayCommand]
        private void ItemSelectionChanged(object? item)
        {
            RecalculateSelectionStates();
        }
    }
}
