using CallMan.Dialogs;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.Services
{
    public class DialogService : IDialogService
    {
        private readonly LeadService _leadService;
        private readonly IUserSession _session;
        private readonly IOrderHistoryService _orderHistoryService;

        // Inject LeadService here so it can be passed to ViewModels
        public DialogService(LeadService leadService, IUserSession session, IOrderHistoryService orderHistoryService)
        {
            _leadService = leadService;
            _session = session;
            _orderHistoryService = orderHistoryService;
        }

        public async Task<bool?> ShowNewOrderDialog(int leadId)
        {
            var vm = new NewOrderViewModel(leadId, _leadService);
            var win = new NewOrderWindow { DataContext = vm };
            vm.RequestClose += (res) => { win.DialogResult = res; win.Close(); };
            return win.ShowDialog();
        }

        public async Task<bool?> ShowAddPaymentDialog(Order order)
        {
            var vm = new AddPaymentViewModel(order, _leadService, _session, _orderHistoryService);
            var win = new AddPaymentWindow { DataContext = vm };
            vm.RequestClose += (res) => { win.DialogResult = res; win.Close(); };
            return win.ShowDialog();
        }

        public void ShowOrderWindow(Lead selectedLead)
        {
            // Build the VM manually with the 3 parameters we discussed
            var viewModel = new OrderViewModel(selectedLead, _leadService, this);

            var window = new OrderWindow
            {
                DataContext = viewModel,
                Owner = App.Current.MainWindow,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
            };

            window.ShowDialog(); // Use ShowDialog so user finishes viewing before returning to the list
        }

        public async Task<bool?> ShowGlobalNewOrderDialog()
        {
            var vm = App.ServiceProvider.GetRequiredService<GlobalNewOrderViewModel>();
            var win = new GlobalNewOrderWindow { DataContext = vm };

            vm.RequestClose += (res) => {
                win.DialogResult = res;
                win.Close();
            };

            return win.ShowDialog();
        }

        public async Task<DashboardFilter?> ShowFilterDialog()
        {
            // 1. Fetch the collection from the database
            var holders = await _leadService.GetUniqueLeadHoldersAsync();

            // 2. Pass that collection into the ViewModel constructor
            var viewModel = new FilterViewModel(holders);

            var window = new FilterWindow
            {
                DataContext = viewModel,
                Owner = App.Current.MainWindow
            };

            DashboardFilter? result = null;

            // Handle the close event
            viewModel.RequestClose += (filterData) =>
            {
                result = filterData;
                window.DialogResult = filterData != null;
                window.Close();
            };

            window.ShowDialog();
            return result;
        }

        public async Task<bool?> ShowAddStaffWindow(User? userToEdit)
        {
            var vm = App.ServiceProvider.GetRequiredService<AddStaffDialogViewModel>();
            await vm.InitializeAsync(userToEdit);

            var window = new AddStaffWindow
            {
                DataContext = vm,
                Owner = App.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            return window.ShowDialog();
        }

        public async Task<string> ShowSingleInputDialog(string item, string? existingValue = null)
        {
            var viewModel = new AddSettingDialogViewModel();
            viewModel.Initialize(item, existingValue);

            var window = new AddSettingWindow
            {
                DataContext = viewModel,
                Owner = App.Current.MainWindow
            };

            string? inputValue = "";
            // Handle the close event
            viewModel.RequestClose += (filterData) =>
            {
                inputValue = filterData;
                window.DialogResult = string.IsNullOrEmpty(filterData);
                window.Close();
            };

            window.ShowDialog();
            return inputValue;
        }

        public async Task ShowHistoryDialog(int leadId)
        {
            if (leadId == 0) return;

            // Use DI or a Factory to create the Window
            var historyWindow = new LeadTimelineWindow();

            // Create the ViewModel, inject the service and the selected Lead ID
            var historyVm = new LeadTimelineViewModel(_leadService, leadId);
            historyVm.RequestClose += () => historyWindow.Close();

            historyWindow.DataContext = historyVm;
            historyWindow.Owner = App.Current.MainWindow; // Set parent window
            historyWindow.ShowDialog();
        }
    }
}
