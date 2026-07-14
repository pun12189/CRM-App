using CallMan.Dialogs;
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
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class VendorViewModel : ObservableObject
    {
        private readonly VendorService _vendorService;

        [ObservableProperty] private ObservableCollection<Vendor> _vendorsList = new();
        [ObservableProperty] private Vendor? _selectedVendor;

        public VendorViewModel(VendorService vendorService)
        {
            _vendorService = vendorService;
            _ = LoadVendorsAsync();
        }

        [RelayCommand]
        public async Task LoadVendorsAsync()
        {
            var data = await _vendorService.GetAllVendorsAsync();
            App.Current.Dispatcher.Invoke(() =>
            {
                VendorsList.Clear();
                foreach (var v in data) VendorsList.Add(v);
            });
        }

        [RelayCommand]
        private async Task OpenAddVendorWindowAsync()
        {
            var dialogVm = new AddVendorWindowViewModel(_vendorService);

            // Explicitly create the Window element instead of a UserControl view wrapper
            var addWindow = new AddVendorWindow
            {
                DataContext = dialogVm,
                Owner = Application.Current.MainWindow // Anchors layout position safely
            };

            // Halts background orchestration execution loop flow processing until user returns state
            bool? isSaved = addWindow.ShowDialog();

            if (isSaved == true)
            {
                await LoadVendorsAsync(); // Sync layout grids immediately
            }
        }
    }
}
