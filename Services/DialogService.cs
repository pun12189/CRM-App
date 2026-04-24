using CallMan.Dialogs;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class DialogService : IDialogService
    {
        private readonly LeadService _leadService;

        // Inject LeadService here so it can be passed to ViewModels
        public DialogService(LeadService leadService)
        {
            _leadService = leadService;
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
            var vm = new AddPaymentViewModel(order, _leadService);
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
            var vm = new GlobalNewOrderViewModel(_leadService);
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
    }
}
