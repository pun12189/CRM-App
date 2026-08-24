using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Tijori.Models;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class StockLedgerViewModel : ObservableObject
    {
        private readonly StockLedgerService _stockService;

        [ObservableProperty] private ObservableCollection<StockLedger> _ledgerEntries = new();
        public ICollectionView FilteredEntries { get; private set; } = null!;

        [ObservableProperty] private string _selectedMovementFilter = "All";
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _totalEntriesCount;

        public StockLedgerViewModel(StockLedgerService stockService)
        {
            _stockService = stockService;
            _ = LoadLedgerAsync();
        }

        public async Task LoadLedgerAsync()
        {
            var list = (await _stockService.GetAllStockMovementsAsync(SelectedMovementFilter)).ToList();

            App.Current.Dispatcher.Invoke(() =>
            {
                LedgerEntries = new ObservableCollection<StockLedger>(list);
                TotalEntriesCount = LedgerEntries.Count;

                FilteredEntries = CollectionViewSource.GetDefaultView(LedgerEntries);
                FilteredEntries.Filter = (obj) =>
                {
                    if (obj is not StockLedger item) return false;
                    if (string.IsNullOrWhiteSpace(SearchText)) return true;

                    var term = SearchText.Trim();
                    return (item.ProductName != null && item.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase))
                        || (item.ReferenceDocument != null && item.ReferenceDocument.Contains(term, StringComparison.OrdinalIgnoreCase))
                        || (item.BatchNumber != null && item.BatchNumber.Contains(term, StringComparison.OrdinalIgnoreCase));
                };
                OnPropertyChanged(nameof(FilteredEntries));
            });
        }

        partial void OnSearchTextChanged(string value) => FilteredEntries?.Refresh();
        async partial void OnSelectedMovementFilterChanged(string value) => await LoadLedgerAsync();

        [RelayCommand]
        private async Task RefreshLedgerAsync() => await LoadLedgerAsync();
    }
}
