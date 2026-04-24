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
    public partial class MaturedLeadsViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly IDialogService _dialogService;
        [ObservableProperty] private ObservableCollection<Lead> _maturedLeads = new();
        [ObservableProperty] private decimal _totalOutstanding;

        public MaturedLeadsViewModel(LeadService service, IDialogService dialogService)
        {
            _service = service;
            _dialogService = dialogService;
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
    }
}
