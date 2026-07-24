using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

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

        [ObservableProperty] private decimal _minimumAllowedPrice;
        [ObservableProperty] private string _priceStatusText;
        [ObservableProperty] private bool _isPriceProfitable = true; // Highlighting color trigger flag

        [ObservableProperty] private int _maxAvailableStock;
        [ObservableProperty] private string _quantityStatusText;
        [ObservableProperty] private bool _isQuantityValid = true;

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
        [ObservableProperty] private ObservableCollection<OrderProductLookupItem> _productsLookupCollection = new();
        [ObservableProperty] private decimal _orderValue;        

        [ObservableProperty] private string _billTo;
        [ObservableProperty] private string _deliverTo;
        [ObservableProperty] private string _termsAndConditions = "30% ADVANCE WILL BE REQUIRED";
        [ObservableProperty] private string _preferedTransport;
        [ObservableProperty] private bool _sendEmail = true;
        [ObservableProperty] private string _remarks;
        [ObservableProperty] private string _orderStatus;
        [ObservableProperty] private DateTime _selectedTime = DateTime.Now;
        [ObservableProperty] private DateTime _nextFollowupDate = DateTime.Now.AddDays(1);
        [ObservableProperty] private ObservableCollection<ExtraCharge> _otherCharges = new();

        [ObservableProperty] private string _currentUser = "Admin";
        [ObservableProperty] private int _currentUserId;// Placeholder, replace with actual user context

        [ObservableProperty] private List<Product> _allMasterProducts = new();

        // --- FORM SELECTION FIELDS ---
        [ObservableProperty] private OrderProductLookupItem? _selectedLookupRow;

        public DateTime CombinedDateTime => new DateTime(
                        NextFollowupDate.Year,
                        NextFollowupDate.Month,
                        NextFollowupDate.Day,
                        SelectedTime.Hour,
                        SelectedTime.Minute,
                        0
                    );

        public decimal CalculatedGrandValue
        {
            get
            {
                decimal chargesTotal = OtherCharges.Sum(x => x.TotalCharge);
                return Math.Round(OrderValue + chargesTotal, 2);
            }
        }

        public GlobalNewOrderViewModel(LeadService service, OrderService orderService, ProductService productService, IUserSession userSession)
        {
            _service = service;
            _orderService = orderService;
            _productService = productService;
            _currentUser = userSession.CurrentUser;
            _currentUserId = userSession.UserId;
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

            await _orderService.SaveCompleteOrderAsync(this);
            RequestClose?.Invoke(true);
        }

        /// <summary>
        /// Fires automatically whenever the user types or modifies the custom Rate input box.
        /// </summary>
        partial void OnRateChanged(decimal value)
        {
            EvaluatePriceProfitability(value);
        }

        /// <summary>
        /// Fires automatically whenever the user types or alters the value in the Quantity input box.
        /// </summary>
        partial void OnQuantityChanged(int value)
        {
            EvaluateQuantitySafety(value);
        }

        /// <summary>
        /// TRICK 1: Automatically triggered whenever a user highlights a choice inside the dropdown menu.
        /// Updates the form's entry fields (Rate, GST, and maximum stock safety labels) instantly.
        /// </summary>
        partial void OnSelectedLookupRowChanged(OrderProductLookupItem? value)
        {
            if (value == null)
            {
                MaxAvailableStock = 0;
                QuantityStatusText = "";
                MinimumAllowedPrice = 0;
                PriceStatusText = "";
                return;
            }

            var parentProduct = AllMasterProducts.FirstOrDefault(p => p.ProductId == value.ProductId);
            if (parentProduct == null) return;

            GstPercent = parentProduct.GstPercent;
            Rate = parentProduct.SellingPrice; // Default to Standard Selling Price
            MaxAvailableStock = value.AvailableStock;           // 
            // DETERMINATION RULE:
            // If they picked a specific batch lot, use that batch's specific purchase cost footprint.
            // If they picked the parent product wide option, fall back to the global Weighted Average Cost (WAC).
            if (value.IsBatchRow && value.BatchId.HasValue)
            {
                var specificBatch = parentProduct.InnerBatchesCollection.FirstOrDefault(b => b.BatchId == value.BatchId.Value);
                MinimumAllowedPrice = specificBatch?.MinimumSellingPrice ?? parentProduct.CostPrice;
                CurrentStock = specificBatch?.CurrentStock ?? parentProduct.RemainingStock; // Override stock to batch-specific level for safety display
            }
            else
            {
                MinimumAllowedPrice = parentProduct.CostPrice; // True WAC cost price reference line
                CurrentStock = parentProduct.RemainingStock;
            }

            EvaluateQuantitySafety(Quantity);
            EvaluatePriceProfitability(Rate);
        }

        private void EvaluateQuantitySafety(int currentInputQty)
        {
            if (SelectedLookupRow == null) return;

            if (currentInputQty <= 0)
            {
                IsQuantityValid = false;
                QuantityStatusText = "❌ Quantity must be greater than 0";
            }
            else if (currentInputQty > MaxAvailableStock)
            {
                IsQuantityValid = false;
                int shortBy = currentInputQty - MaxAvailableStock;
                QuantityStatusText = $"❌ Stock Deficit: Exceeds limit by {shortBy} unit(s) (Max Available: {MaxAvailableStock})";
            }
            else
            {
                IsQuantityValid = true;
                int remainingOnShelf = MaxAvailableStock - currentInputQty;
                QuantityStatusText = $"✅ In Stock: Ready to allocate ({remainingOnShelf} units left on shelf)";
            }
        }

        private void EvaluatePriceProfitability(decimal currentInputRate)
        {
            if (SelectedLookupRow == null) return;

            if (currentInputRate < MinimumAllowedPrice)
            {
                IsPriceProfitable = false;
                decimal lossPerUnit = MinimumAllowedPrice - currentInputRate;
                PriceStatusText = $"⚠️ Net Loss: Below base cost price of ₹{MinimumAllowedPrice:N2} (-₹{lossPerUnit:N2}/unit)";
            }
            else if (currentInputRate == MinimumAllowedPrice)
            {
                IsPriceProfitable = true;
                PriceStatusText = $"ℹ️ No Margin: Selling exactly at base cost price (₹{MinimumAllowedPrice:N2})";
            }
            else
            {
                IsPriceProfitable = true;
                decimal profitPerUnit = currentInputRate - MinimumAllowedPrice;
                PriceStatusText = $"✅ Profitable: Above base cost price (+₹{profitPerUnit:N2}/unit)";
            }
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
            if (SelectedLookupRow == null) return;
            if (Quantity <= 0)
            {
                MessageBox.Show("Please enter a valid quantity greater than zero.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsPriceProfitable)
            {
                var result = MessageBox.Show("This item rate results in a loss! Are you sure you have manager clearance to bypass pricing restrictions?",
                                             "Margin Loss Validation Alert", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    return; // Terminate execution block path safely
                }
            }

            if (!IsQuantityValid)
            {
                MessageBox.Show("Cannot add item to cart. The requested quantity exceeds available physical batch stock limits.",
                                "Inventory Allocation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return; // Terminates execution immediately, protecting stock data integrity
            }

            // Fetch the fresh actual stock data arrays from memory/db to verify current real-time state bounds
            var productDetails = AllMasterProducts.FirstOrDefault(p => p.ProductId == SelectedLookupRow.ProductId);
            if (productDetails == null) return;

            // ----------------------------------------------------
            // MODE A: MANUAL BATCH-WISE SELECTION TRACKING LOCK
            // ----------------------------------------------------
            if (SelectedLookupRow.IsBatchRow)
            {
                var targetBatch = productDetails.InnerBatchesCollection.FirstOrDefault(b => b.BatchId == SelectedLookupRow.BatchId);

                if (targetBatch == null || targetBatch.CurrentStock < Quantity)
                {
                    MessageBox.Show($"Insufficient Stock! Batch '{SelectedLookupRow.DisplayText.Split('(')[0].Trim()}' only has {targetBatch?.CurrentStock ?? 0} units available.",
                                    "Stock Deficit Alert", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Add line item mapped directly to this single batch lot segment container footprint
                AddOrUpdateCartItemsList(productDetails, targetBatch, Quantity);
            }

            // ----------------------------------------------------
            // MODE B: PRODUCT-WISE SELECTION (AUTOMATIC FEFO)
            // ----------------------------------------------------
            else
            {
                if (productDetails.RemainingStock < Quantity)
                {
                    MessageBox.Show($"Insufficient Overall Stock! Total available across all batches is {productDetails.RemainingStock} units.",
                                    "Stock Deficit Alert", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Execute FEFO Core: Sort unexpired active inventory batches by closest Expiry Date first
                var fefoSortedBatches = productDetails.InnerBatchesCollection
                    .Where(b => b.CurrentStock > 0 && b.ExpiryDate > DateTime.Today)
                    .OrderBy(b => b.ExpiryDate)
                    .ToList();

                int remainingToAllocate = Quantity;

                foreach (var batch in fefoSortedBatches)
                {
                    if (remainingToAllocate <= 0) break;

                    // Determine how much this batch can fulfill
                    int takeQuantity = Math.Min(batch.CurrentStock, remainingToAllocate);

                    // Add or split into distinct cart lines based on batch breakdown paths
                    AddOrUpdateCartItemsList(productDetails, batch, takeQuantity);

                    remainingToAllocate -= takeQuantity;
                }
            }

            // Reset Input Box UI Values variables state safely
            Quantity = 1;
        }

        private void AddOrUpdateCartItemsList(Product product, ProductBatch batch, int quantity)
        {
            // Look for existing item matching BOTH ProductId and BatchId in your active shopping cart container
            var existingCartLine = CartItems.FirstOrDefault(x => x.ProductId == product.ProductId && x.BatchId == batch.BatchId);

            if (existingCartLine != null)
            {
                // Enforce combined stock checking boundary safeguards
                if (existingCartLine.Quantity + quantity > batch.CurrentStock)
                {
                    MessageBox.Show($"Cannot add more items. Combined cart total exceeds active lot capacity restrictions.", "Limit Exceeded", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                existingCartLine.Quantity += quantity;
            }
            else
            {
                CartItems.Add(new OrderItem
                {
                    ProductId = product.ProductId,
                    BatchId = batch.BatchId,
                    ProductName = product.Name,
                    BatchNumber = batch.BatchNumber,
                    Quantity = quantity,
                    UnitPrice = Rate, // Default standard selling baseline counter
                    GstPercent = product.GstPercent
                });
            }

            // Refresh layout calculations parameters summary panels
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

            ProductsLookupCollection.Clear();

            // 1. Fetch raw master arrays directly out of your database service layers
            var productsFromDb = await _productService.GetProductsWithBatchesAsync(1);
            AllMasterProducts = productsFromDb.ToList();

            // 2. Transform and flatten data trees into the ComboBox representation structure
            foreach (var prod in AllMasterProducts)
            {
                // Create Parent Product Summary Anchor
                ProductsLookupCollection.Add(new OrderProductLookupItem
                {
                    ProductId = prod.ProductId,
                    BatchId = null,
                    IsBatchRow = false,
                    DisplayText = $"{prod.Name} ({prod.InnerBatchesCollection.Count} Batches) (Stock: {prod.RemainingStock}) (Avg Price: ₹{prod.CostPrice:N2})",
                    AvailableStock = prod.RemainingStock,
                    Price = prod.SellingPrice
                });

                // Append Child Lots sequentially right under their parent node anchor location
                var activeBatches = prod.InnerBatchesCollection.Where(b => b.CurrentStock > 0).OrderBy(b => b.ExpiryDate);
                foreach (var batch in activeBatches)
                {
                    ProductsLookupCollection.Add(new OrderProductLookupItem
                    {
                        ProductId = prod.ProductId,
                        BatchId = batch.BatchId,
                        IsBatchRow = true,
                        DisplayText = $"-> {batch.BatchNumber} (Exp: {batch.ExpiryDate:dd-MM-yyyy}) (Stock: {batch.CurrentStock}) (Price: ₹{batch.MinimumSellingPrice:N2})",
                        AvailableStock = batch.CurrentStock,
                        Price = prod.SellingPrice
                    });
                }
            }
        }
    }
}
