using CallMan.Core;
using CallMan.Dialogs;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CallMan.ViewModels
{
    public partial class PurchaseViewModel : ObservableObject
    {
        private readonly PurchaseService _purchaseService;
        private readonly VendorService _vendorService;
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly IUserSession _userSession;
        private readonly IActionSecurityGuard _securityGuard;

        [ObservableProperty] private ObservableCollection<PurchaseOrder> _purchaseOrdersList = new();
        [ObservableProperty] private PurchaseOrder? _selectedOrder;
        [ObservableProperty] private bool _workspaceViewIsActive;
        [ObservableProperty]
        private object _tabsDataContext;

        public PurchaseViewModel(PurchaseService purchaseService, VendorService vendorService, ProductService productService, CategoryService categoryService, IUserSession userSession, IActionSecurityGuard securityGuard)
        {
            _purchaseService = purchaseService;
            _vendorService = vendorService;
            _productService = productService;
            _categoryService = categoryService;
            _userSession = userSession;
            _securityGuard = securityGuard;
            _ = LoadPurchaseOrdersAsync();
        }

        [RelayCommand]
        public async Task LoadPurchaseOrdersAsync()
        {
            var data = await _purchaseService.GetAllOrdersAsync();
            App.Current.Dispatcher.Invoke(() =>
            {
                PurchaseOrdersList.Clear();
                foreach (var po in data) PurchaseOrdersList.Add(po);
            });
        }

        [RelayCommand]
        public async Task ReceiveStockAsync(PurchaseOrder order)
        {
            if (order == null || order.OrderStatus == "Received") return;

            await _purchaseService.ProcessStockReceiptAsync(order.PurchaseOrderId);
            await LoadPurchaseOrdersAsync(); // Instant UI sync
        }

        [RelayCommand]
        private async Task OpenCreatePoWindowAsync()
        {
            // Resolve all service constructor initialization constraints safely
            var dialogVm = new CreatePoWindowViewModel(_purchaseService, _vendorService, _productService);

            var poWindow = new CreatePoWindow
            {
                DataContext = dialogVm,
                Owner = Application.Current.MainWindow
            };

            bool? isSaved = poWindow.ShowDialog();

            if (isSaved == true)
            {
                await LoadPurchaseOrdersAsync(); // Sync primary listing view instantly
            }
        }

        [RelayCommand]
        public async Task ShowPODetails(PurchaseOrder selectedOrder)
        {
            if (selectedOrder == null) return;

            var profileVm = new PurchaseDetailsViewModel(_purchaseService, _categoryService, _userSession, _securityGuard);
            profileVm.OnNavigateBackRequested += () =>
            {
                // Hide the workspace view & return to the main Purchase Orders grid
                WorkspaceViewIsActive = false;
                TabsDataContext = null;
            };

            await profileVm.InitializeAsync(selectedOrder.PurchaseOrderId);
            this.TabsDataContext = profileVm;
            WorkspaceViewIsActive = true; // Swaps grid out for profile workspace view layout instantly
        }
    }
}
