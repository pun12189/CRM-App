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
    public partial class VendorViewModel : ObservableObject
    {
        private readonly VendorService _vendorService;
        private readonly PurchaseService _purchaseService;
        private readonly CategoryService _categoryService;
        private readonly IUserSession _userSession;
        private readonly IActionSecurityGuard _securityGuard;
        private readonly ProductService _productService;

        [ObservableProperty]
        private ObservableCollection<Vendor> _vendorsList = new();

        [ObservableProperty]
        private Vendor? _selectedVendor;

        [ObservableProperty]
        private object _tabsDataContext;

        [ObservableProperty] private bool _workspaceViewIsActive;

        public VendorViewModel(VendorService vendorService, PurchaseService purchaseService, CategoryService categoryService, IUserSession userSession, IActionSecurityGuard securityGuard, ProductService productService)
        {
            _vendorService = vendorService;
            _purchaseService = purchaseService;
            _categoryService = categoryService;
            _userSession = userSession;
            _securityGuard = securityGuard;
            _productService = productService;
            _ = LoadVendorsAsync();
        }

        [RelayCommand]
        public async Task LoadVendorsAsync()
        {
            var data = await _vendorService.GetAllVendorsAsync();

            Application.Current.Dispatcher.Invoke(() =>
            {
                VendorsList.Clear();
                foreach (var v in data)
                {
                    VendorsList.Add(v);
                }
            });
        }

        [RelayCommand]
        private async Task OpenAddVendorWindowAsync()
        {
            var dialogVm = new AddVendorWindowViewModel(_vendorService);

            var addWindow = new AddVendorWindow
            {
                DataContext = dialogVm,
                Owner = Application.Current.MainWindow
            };

            bool? isSaved = addWindow.ShowDialog();

            if (isSaved == true)
            {
                await LoadVendorsAsync();
            }
        }

        [RelayCommand]
        private async Task EditVendorAsync(Vendor? vendor)
        {
            if (vendor == null) return;

            // Pass existing vendor toViewModel constructor to trigger Edit Mode
            var dialogVm = new AddVendorWindowViewModel(_vendorService, vendor);

            var editWindow = new AddVendorWindow
            {
                DataContext = dialogVm,
                Owner = Application.Current.MainWindow
            };

            bool? isSaved = editWindow.ShowDialog();

            if (isSaved == true)
            {
                // The dialog ViewModel handles the database update, so we just reload the UI list
                await LoadVendorsAsync();
            }
        }

        [RelayCommand]
        private async Task DeleteVendorAsync(Vendor? vendor)
        {
            if (vendor == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete vendor '{vendor.CompanyName}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                bool deleted = await _vendorService.DeleteVendorAsync(vendor.VendorId);
                if (deleted)
                {
                    await LoadVendorsAsync();
                }
                else
                {
                    MessageBox.Show("Failed to delete vendor. It may be linked to existing transactions/orders.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public async Task ShowVendorDetailsAsync(Vendor selectedVendor)
        {
            if (selectedVendor == null) return;

            try
            {
                LoadingService.Show("Loading vendor details... Please wait.");

                // Instantiate the ViewModel with DI services
                var profileVm = new VendorDetailsViewModel(_vendorService, _purchaseService, _categoryService, _userSession, _securityGuard, _productService);

                // Wire up back-navigation callback
                profileVm.OnNavigateBackRequested += () => HideVendorWorkspace();

                // Load metrics, POs, and products asynchronously
                await profileVm.InitializeAsync(selectedVendor);

                // Assign DataContext and trigger template swap
                TabsDataContext = profileVm;
                WorkspaceViewIsActive = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VENDOR WORKSPACE ERROR] Failed to load details: {ex.Message}");
                MessageBox.Show("Failed to load vendor workspace. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingService.Hide();
            }
        }

        [RelayCommand]
        public void HideVendorWorkspace()
        {
            TabsDataContext = null;
            WorkspaceViewIsActive = false;
        }
    }
}
