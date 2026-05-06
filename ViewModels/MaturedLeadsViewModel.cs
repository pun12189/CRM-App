using CallMan.Dialogs;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class MaturedLeadsViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly SettingService _settingService;
        private readonly IUserSession _session;
        private readonly IDialogService _dialogService;
        private readonly ProductService _productService;
        private readonly OrderService _orderService;
        [ObservableProperty] private ObservableCollection<Lead> _maturedLeads = new();
        [ObservableProperty] private decimal _totalOutstanding;

        public MaturedLeadsViewModel(LeadService service, SettingService settingService, IUserSession session, IDialogService dialogService, ProductService productService, OrderService orderService)
        {
            _service = service;
            _settingService = settingService;
            _session = session;
            _dialogService = dialogService;
            _productService = productService;
            _orderService = orderService;
            LoadData();
        }

        [RelayCommand]
        public async Task LoadData()
        {
            var data = await _service.GetMaturedLedgerAsync();
            MaturedLeads = new ObservableCollection<Lead>(data);
            TotalOutstanding = MaturedLeads.Sum(x => x.TotalBalanceDue);
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
            var profileVm = new CustomerProfileViewModel(_service, _session, _settingService, _productService, _orderService, selectedLead);

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
    }
}
