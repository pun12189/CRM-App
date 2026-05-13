using CallMan.Dialogs;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Models.Enums;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
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

namespace CallMan.ViewModels
{
    public partial class LeadViewModel : ObservableObject
    {
        private readonly LeadService _leadService;
        private readonly SettingService _settingService;
        private readonly IUserSession _session;
        private readonly IDialogService _dialogService;
        private readonly ProductService _productService;
        private readonly OrderService _orderService;
        private ICollectionView _leadsCollection;

        [ObservableProperty]
        private string _searchText = string.Empty;

        // This is what the DataGrid actually binds to now
        public ICollectionView LeadsCollection => _leadsCollection;

        [ObservableProperty] private LeadViewMode _currentMode = LeadViewMode.AllLeads;

        [ObservableProperty]
        private ObservableCollection<Lead> _leads = new();

        [ObservableProperty]
        private Lead? _selectedLead;

        public LeadViewModel(LeadService leadService, SettingService settingService, IUserSession session, IDialogService dialogService, ProductService productService, OrderService orderService)
        {
            _leadService = leadService;
            _settingService = settingService;
            _session = session;
            _dialogService = dialogService;
            _productService = productService;
            _orderService = orderService;
            _ = LoadLeads();
        }

        public async Task InitializeAsync(LeadViewMode mode)
        {
            CurrentMode = mode;
            await LoadLeads();
        }

        private async Task LoadLeads()
        {
            // 1. Call the new service method that joins Leads with their latest History
            var data = await _leadService.GetAllLeadsWithLatestUpdateAsync();

            // 2. Wrap the result in an ObservableCollection
            var list = new ObservableCollection<Lead>(data);

            if (CurrentMode == LeadViewMode.MyLeads)
            {
                var userId = _session.CurrentUser;
                list = new ObservableCollection<Lead>(list.Where(l => l.LeadHolder == userId));
            }

            if (CurrentMode == LeadViewMode.Dead)
            {
                list = new ObservableCollection<Lead>(list.Where(l => l.Status == "Dead"));
            }

            // 3. Update the CollectionView (the actual source for your DataGrid)
            _leadsCollection = CollectionViewSource.GetDefaultView(list);

            // 4. Re-apply your search filter logic
            _leadsCollection.Filter = FilterLeads;

            // 5. Notify the UI to refresh the table
            OnPropertyChanged(nameof(LeadsCollection));
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
                   (lead.City?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.CompanyName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.LeadHolder?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.District?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.Email?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.Status?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.State?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (lead.AssignedDivisions?.Any(d => d.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ?? false);
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
                await LoadLeads();
            }
        }

        [RelayCommand]
        private async Task OpenAddLeadDialog()
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
                await LoadLeads();
            }
        }

        [RelayCommand]
        private async Task EditLead(Lead leadToEdit)
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
                await LoadLeads(); // Refresh list after update
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
                await LoadLeads(); // Refresh list
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
        private void OpenLeadProfile(Lead selectedLead)
        {
            if (selectedLead == null) return;

            // 1. Create the ViewModel for the Dialog
            // We pass the LeadService and the Selected Lead instance
            dynamic profileVm = new LeadProfileViewModel(_leadService, _settingService, _session, selectedLead);

            if (selectedLead.Status?.ToLower() == "matured")
            {
                profileVm = new CustomerProfileViewModel(_leadService, _session, _settingService, _productService, _orderService, selectedLead);
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
                _ = LoadLeads();
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
    }
}
