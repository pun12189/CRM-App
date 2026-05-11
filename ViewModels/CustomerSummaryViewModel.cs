using CallMan.Models;
using CallMan.Models.Enums;
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
    public partial class CustomerSummaryViewModel : ObservableObject
    {
        [ObservableProperty] private OccupiedLocation _customer; // The lead/customer passed in
        [ObservableProperty] private CustomerSummaryMetrics _metrics;
        [ObservableProperty] private ObservableCollection<Product> _productDetails;

        private readonly OccupiedLocationService _service;

        public CustomerSummaryViewModel(OccupiedLocationService service)
        {
            _service = service;
        }

        public async Task InitializeAsync(OccupiedLocation location)
        {
            Customer = location;
            await LoadSummaryAsync(location.Id);
        }

        // Command to load data when the window opens
        public async Task LoadSummaryAsync(int customerId)
        {
            Metrics = await _service.GetSummaryMetricsAsync(customerId);
            //await LoadProductsOrdered(); // Default tab
        }

        [RelayCommand]
        private async Task LoadProductsOrdered()
        {
            // Query OrderHistory grouped by Product for this Customer
            var data = await _service.GetOrderedProductsAsync(Customer.Id);
            ProductDetails = new ObservableCollection<Product>(data);
        }
    }
}
