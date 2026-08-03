using Tijori.Dialogs;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.ViewModels
{
    public partial class LedgerViewModel : ObservableObject
    {
        private readonly LedgerService _ledgerService;
        private readonly LeadService _leadService;
        private readonly IUserSession _userSession;
        private readonly OrderService _orderService;

        [ObservableProperty] private ObservableCollection<PaymentEntry> _ledgerEntries = new();
        [ObservableProperty] private bool _isSelectAll;

        #region CAN EXECUTE CHECK

        /// <summary>
        /// Controls enablement of 'Delete Ledger' button.
        /// Returns TRUE if 1 or more items are selected.
        /// </summary>
        public bool CanDeleteSelected => LedgerEntries != null && LedgerEntries.Any(x => x.IsSelected);

        #endregion

        public LedgerViewModel(LedgerService ledgerService, LeadService leadService, IUserSession userSession, OrderService orderService)
        {
            _ledgerService = ledgerService;
            _leadService = leadService;
            _userSession = userSession;
            _orderService = orderService;
            _ = LoadLedgerDataAsync();
        }

        public async Task LoadLedgerDataAsync()
        {
            var data = await _ledgerService.GetAllLedgerEntriesAsync();

            foreach (var item in data)
            {
                item.OnSelectionChanged = () => DeleteSelectedCommand.NotifyCanExecuteChanged();
            }

            LedgerEntries = new ObservableCollection<PaymentEntry>(data);
            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsSelectAllChanged(bool value)
        {
            foreach (var item in LedgerEntries)
            {
                item.IsSelected = value;
            }

            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private async Task AddNewAsync()
        {
            var dialogVM = new CreateLedgerViewModel(_ledgerService, _leadService, _orderService, _userSession);
            var dialog = new CreateLedgerDialog()
            {
                Owner = Application.Current.MainWindow,
                DataContext = dialogVM
            };

            dialogVM.RequestClose += (result) =>
            {
                dialog.DialogResult = result;
                dialog.Close();
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadLedgerDataAsync(); // Refresh ledger table upon submission
            }
        }

        [RelayCommand]
        private async Task DeleteSingleAsync(PaymentEntry? entry)
        {
            if (entry == null) return;

            var res = MessageBox.Show(
                $"Are you sure you want to delete ledger entry ID #{entry.PaymentId}?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                bool deleted = await _ledgerService.DeleteLedgerEntryAsync(entry.PaymentId);
                if (deleted)
                {
                    LedgerEntries.Remove(entry);
                    DeleteSelectedCommand.NotifyCanExecuteChanged();
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
        private async Task DeleteSelectedAsync()
        {
            var selectedItems = LedgerEntries.Where(x => x.IsSelected).ToList();
            if (!selectedItems.Any()) return;

            var res = MessageBox.Show(
                $"Are you sure you want to delete {selectedItems.Count} selected ledger entries?",
                "Confirm Bulk Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                foreach (var item in selectedItems)
                {
                    await _ledgerService.DeleteLedgerEntryAsync(item.PaymentId);
                    LedgerEntries.Remove(item);
                }

                // Reset Select All state & update button availability
                IsSelectAll = false;
                DeleteSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }
}
