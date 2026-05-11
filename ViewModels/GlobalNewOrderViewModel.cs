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
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CallMan.ViewModels
{
    public partial class GlobalNewOrderViewModel : ObservableObject
    {
        private readonly LeadService _service;
        private readonly OrderService _orderService;
        private readonly ProductService _productService;

        public event Action<bool>? RequestClose;

        #region Header & Customer Data
        [ObservableProperty] private ObservableCollection<Lead> _maturedCustomers = new();
        [ObservableProperty] private Lead? _selectedCustomer;
        [ObservableProperty] private string _paymentMode = "Cash";
        [ObservableProperty] private decimal _amountReceived;
        #endregion

        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private string _description = string.Empty;

        #region Step Management
        [ObservableProperty] private int _currentStep = 1;

        [RelayCommand]
        private void NextStep() => CurrentStep = 2;

        [RelayCommand]
        private void Back() => CurrentStep = 1;
        #endregion

        #region Step 1: Product Selection & Cart
        [ObservableProperty] private ObservableCollection<Product> _allProducts = new();
        [ObservableProperty] private Product _selectedProduct;
        [ObservableProperty] private int _currentStock;
        [ObservableProperty] private int _quantity = 1;
        [ObservableProperty] private decimal _rate;
        [ObservableProperty] private decimal _gstPercent;
        [ObservableProperty] private ObservableCollection<decimal> _gstRates = new() { 0, 5, 12, 18, 28 };
        [ObservableProperty] private ObservableCollection<OrderItem> _cartItems = new();
        [ObservableProperty] private decimal _orderValue;        

        [ObservableProperty] private string _billTo;
        [ObservableProperty] private string _deliverTo;
        [ObservableProperty] private string _termsAndConditions = "30% ADVANCE WILL BE REQUIRED";
        [ObservableProperty] private string _preferedTransport;
        [ObservableProperty] private bool _sendEmail = true;
        [ObservableProperty] private string _remarks;
        [ObservableProperty] private string _orderStatus;
        [ObservableProperty] private ObservableCollection<ExtraCharge> _otherCharges = new();

        [ObservableProperty] private string _currentUser = "Admin"; // Placeholder, replace with actual user context

        public decimal CalculatedGrandValue
        {
            get
            {
                decimal chargesTotal = OtherCharges.Sum(x => x.TotalCharge);
                return OrderValue + chargesTotal;
            }
        }

        public GlobalNewOrderViewModel(LeadService service, OrderService orderService, ProductService productService, IUserSession userSession)
        {
            _service = service;
            _orderService = orderService;
            _productService = productService;
            _currentUser = userSession.CurrentUser;
            // Initialize Collections
            CartItems.CollectionChanged += (s, e) => {
                OrderValue = CartItems.Sum(x => x.Total);
                OnPropertyChanged(nameof(CalculatedGrandValue)); // GrandTotal depends on OrderValue
            };

            OtherCharges.CollectionChanged += (s, e) => OnPropertyChanged(nameof(CalculatedGrandValue));

            // Load Initial Data (Method implementations assumed in Service)
            LoadInitialData();
        }

        [RelayCommand]
        private async Task SaveOrder()
        {
            if (SelectedCustomer == null || TotalAmount <= 0)
            {
                // You could add a Snackbar or Messagebox here
                return;
            }

            var order = new Order
            {
                LeadId = SelectedCustomer.LeadId,
                TotalAmount = TotalAmount,
                Description = Description,
                OrderDate = DateTime.Now
            };

            await _service.CreateOrderAsync(order);
            RequestClose?.Invoke(true);
        }

        partial void OnSelectedProductChanged(Product value)
        {
            if (value != null)
            {
                CurrentStock = value.InitialStock;
                Rate = value.SellingPrice;
                GstPercent = value.GstPercent;
            }
        }

        [RelayCommand]
        private void AddToCart()
        {
            if (SelectedProduct == null || Quantity <= 0) return;

            var newItem = new OrderItem
            {
                ProductId = SelectedProduct.ProductId,
                ProductName = SelectedProduct.Name,
                Quantity = Quantity,
                UnitPrice = Rate,
                GstPercent = GstPercent
            };

            CartItems.Add(newItem);

            // Reset entry fields
            Quantity = 1;
            OnPropertyChanged(nameof(OrderValue));
            OnPropertyChanged(nameof(CalculatedGrandValue));
        }

        [RelayCommand]
        private void RemoveFromCart(OrderItem item)
        {
            if (item != null)
            {
                CartItems.Remove(item);
                OnPropertyChanged(nameof(OrderValue));
                OnPropertyChanged(nameof(CalculatedGrandValue));
            }
        }

        [RelayCommand]
        private void AddExtraCharge()
        {
            var charge = new ExtraCharge { Name = "carriage", Action = "Add (+)", GstPercent = 18 };
            // Subscribe to property changes to update Grand Total live
            charge.PropertyChanged += (s, e) => OnPropertyChanged(nameof(CalculatedGrandValue));
            OtherCharges.Add(charge);
        }

        [RelayCommand]
        private void RemoveExtraCharge(ExtraCharge charge)
        {
            if (charge != null)
            {
                OtherCharges.Remove(charge);
                OnPropertyChanged(nameof(CalculatedGrandValue));
            }
        }
        #endregion

        #region Submission
        [RelayCommand]
        private async Task SubmitOrder()
        {
            if (SelectedCustomer == null) return;

            var success = await _orderService.SaveCompleteOrderAsync(this);
            if (success)
            {
                RequestClose?.Invoke(true);
            }
        }
        #endregion

        private async void LoadInitialData()
        {
            // Placeholder for service calls to populate MaturedCustomers and AllProducts
            var customers = await _service.GetAllActiveLeadsAsync();
            MaturedCustomers = new ObservableCollection<Lead>(customers);

            var products = await _productService.GetAllProductsAsync();
            AllProducts = new ObservableCollection<Product>(products);
        }
    }
}
