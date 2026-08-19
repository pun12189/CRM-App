using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;

namespace Tijori.ViewModels
{
    public partial class ServiceOrderViewModel : ObservableObject
    {
        private readonly ServiceOrderService _orderService;
        private readonly MasterFormulationService _formulationService;
        private readonly ProductService _productService;
        private readonly LeadService _customerService;

        // Navigation state
        [ObservableProperty] private bool _isFormOpen; // False = Directory, True = Order Wizard
        [ObservableProperty] private int _currentStep = 1; // 1 = Product & Recipe Builder, 2 = Final Summary
        [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private bool _isOrderLocked;

        // Directory & Search
        [ObservableProperty] private ObservableCollection<ServiceOrder> _ordersList = new();
        public ICollectionView FilteredOrders { get; private set; } = null!;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _totalOrdersCount;

        // Active Order Cart State
        [ObservableProperty] private ServiceOrder _currentOrder = new();
        [ObservableProperty] private Lead? _selectedCustomer;
        [ObservableProperty] private ObservableCollection<CustomerBrand> _customerBrandsList = new();
        [ObservableProperty] private CustomerBrand? _selectedBrand;
        [ObservableProperty] private string _customBrandText = string.Empty;

        // Step 1 Active Product & BOM Buffer
        [ObservableProperty] private ServiceOrderItem _activeItem = new();
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private MasterFormulation? _selectedFormulation;
        [ObservableProperty] private ObservableCollection<ServiceOrderItemBom> _activeBOMList = new();

        // New Ingredient Inputs for active formula grid
        [ObservableProperty] private Product? _selectedRawMaterialToAdd;
        [ObservableProperty] private decimal _newIngredientPercentage;
        [ObservableProperty] private string _newIngredientPhase = "Phase A";

        // Dropdowns & Masters
        [ObservableProperty] private ObservableCollection<Lead> _customersList = new();
        [ObservableProperty] private ObservableCollection<Product> _finishedProductsList = new();
        [ObservableProperty] private ObservableCollection<Product> _rawMaterialsList = new();
        [ObservableProperty] private ObservableCollection<MasterFormulation> _masterFormulationsList = new();

        public ObservableCollection<string> PackagingTypes { get; } = new()
        {
            "Bottle", "Tube", "Strip (Alu-Alu)", "Blister Pack", "Jar", "Sachet", "Ampoule", "Vial", "Carton Box"
        };

        public ObservableCollection<string> CommonPackSizes { get; } = new()
        {
            "15 ml", "30 ml", "50 ml", "100 ml", "200 ml", "500 ml", "1 Ltr",
            "10 gm", "20 gm", "50 gm", "100 gm", "10x10 Tablets", "10x1x10 Strips"
        };

        public ObservableCollection<string> CommonFragrances { get; } = new()
        {
            "Fragrance-Free / Unscented", "Lavender", "Aloe Vera", "Green Apple", "Rose", "Sandalwood", "Lemon Mint", "Oud"
        };

        public bool CanChangeCustomer => !IsOrderLocked && (CurrentOrder?.Items.Count ?? 0) == 0;
        public int ConfiguredProductsCount => CurrentOrder?.Items.Count ?? 0;
        public decimal ActiveBOMTotalPercentage => ActiveBOMList.Sum(x => x.PercentageValue);
        public decimal ActiveBOMWaterPercentage => 100m - ActiveBOMTotalPercentage;

        public ServiceOrderViewModel(
            ServiceOrderService orderService,
            MasterFormulationService formulationService,
            ProductService productService,
            LeadService customerService)
        {
            _orderService = orderService;
            _formulationService = formulationService;
            _productService = productService;
            _customerService = customerService;

            _ = InitializeDataAsync();
        }

        public async Task InitializeDataAsync()
        {
            await LoadMastersAsync();
            await LoadOrdersListAsync();
        }

        private async Task LoadMastersAsync()
        {
            var customers = await _customerService.GetAllActiveLeadsAsync();
            var products = await _productService.GetAllProductsAsync();
            var formulations = await _formulationService.GetAllFormulationsAsync();

            App.Current.Dispatcher.Invoke(() =>
            {
                CustomersList = new ObservableCollection<Lead>(customers);
                FinishedProductsList = new ObservableCollection<Product>(products.Where(p => p.CategoryType == ItemClassification.FinishedGood));
                RawMaterialsList = new ObservableCollection<Product>(products.Where(p => p.CategoryType == ItemClassification.RawMaterial));
                MasterFormulationsList = new ObservableCollection<MasterFormulation>(formulations);
            });
        }

        public async Task LoadOrdersListAsync()
        {
            var list = (await _orderService.GetAllOrdersAsync()).ToList();
            App.Current.Dispatcher.Invoke(() =>
            {
                OrdersList = new ObservableCollection<ServiceOrder>(list);
                TotalOrdersCount = OrdersList.Count;
                FilteredOrders = CollectionViewSource.GetDefaultView(OrdersList);
                FilteredOrders.Filter = (obj) =>
                {
                    if (obj is not ServiceOrder item) return false;
                    if (string.IsNullOrWhiteSpace(SearchText)) return true;
                    var term = SearchText.Trim();
                    return (item.OrderNumber != null && item.OrderNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
                        || (item.CustomerName != null && item.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase))
                        || (item.OrderStatus != null && item.OrderStatus.Contains(term, StringComparison.OrdinalIgnoreCase));
                };
                OnPropertyChanged(nameof(FilteredOrders));
            });
        }

        partial void OnSearchTextChanged(string value) => FilteredOrders?.Refresh();

        // 🌟 Customer Change Handler
        async partial void OnSelectedCustomerChanged(Lead? value)
        {
            if (value != null)
            {
                CurrentOrder.CustomerId = value.LeadId;
                CurrentOrder.CustomerName = value.CustomerName;
                var brands = await _orderService.GetBrandsByCustomerIdAsync(value.LeadId);
                App.Current.Dispatcher.Invoke(() =>
                {
                    CustomerBrandsList = new ObservableCollection<CustomerBrand>(brands);
                });
            }
            else
            {
                CustomerBrandsList.Clear();
            }
        }

        private bool FilterOrder(object obj)
        {
            if (obj is not ServiceOrder item) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var term = SearchText.Trim();
            return (item.OrderNumber != null && item.OrderNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (item.CustomerName != null && item.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (item.OrderStatus != null && item.OrderStatus.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        partial void OnSelectedBrandChanged(CustomerBrand? value)
        {
            if (value != null)
            {
                ActiveItem.BrandId = value.BrandId;
                ActiveItem.BrandName = value.BrandName;
            }
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                ActiveItem.ProductId = value.ProductId;
                ActiveItem.ProductName = value.Name;
                ActiveItem.UnitPrice = value.SellingPrice;
            }
        }

        // 🌟 Master Formulation Selection: Pre-fills Recipe
        async partial void OnSelectedFormulationChanged(MasterFormulation? value)
        {
            if (value != null && value.FormulationId > 0)
            {
                ActiveItem.MasterFormulationId = value.FormulationId;
                ActiveItem.FormulationName = value.FormulationName;

                var recipe = await _formulationService.GetFormulationByIdAsync(value.FormulationId);
                if (recipe != null)
                {
                    ActiveBOMList.Clear();
                    foreach (var line in recipe.Items)
                    {
                        var bomItem = new ServiceOrderItemBom
                        {
                            RawMaterialProductId = line.RawMaterialProductId,
                            RawMaterialName = line.RawMaterialName,
                            RawMaterialCode = line.RawMaterialCode,
                            Phase = line.Phase,
                            PercentageValue = line.PercentageValue,
                            Unit = line.Unit ?? "Kg",
                            Remarks = line.Remarks
                        };
                        bomItem.PropertyChanged += (s, e) => RecalculateActiveBOMTotals();
                        ActiveBOMList.Add(bomItem);
                    }
                    RecalculateActiveBOMTotals();
                }
            }
        }

        // ==========================================
        // 🌟 FORMULA / BOM TABLE ACTIONS (STEP 1)
        // ==========================================
        [RelayCommand]
        private void AddIngredientToActiveFormula()
        {
            if (SelectedRawMaterialToAdd == null)
            {
                MessageBox.Show("Please select a raw material / chemical.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ActiveBOMList.Any(x => x.RawMaterialProductId == SelectedRawMaterialToAdd.ProductId))
            {
                MessageBox.Show($"'{SelectedRawMaterialToAdd.Name}' is already in this recipe.", "Duplicate Item", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newItem = new ServiceOrderItemBom
            {
                RawMaterialProductId = SelectedRawMaterialToAdd.ProductId,
                RawMaterialName = SelectedRawMaterialToAdd.Name,
                RawMaterialCode = SelectedRawMaterialToAdd.ShortName ?? string.Empty,
                Phase = string.IsNullOrWhiteSpace(NewIngredientPhase) ? "Phase A" : NewIngredientPhase.Trim(),
                PercentageValue = NewIngredientPercentage,
                Unit = SelectedRawMaterialToAdd.Unit ?? "Kg"
            };

            newItem.PropertyChanged += (s, e) => RecalculateActiveBOMTotals();
            ActiveBOMList.Add(newItem);
            RecalculateActiveBOMTotals();

            SelectedRawMaterialToAdd = null;
            NewIngredientPercentage = 0m;
        }

        [RelayCommand]
        private void RemoveIngredientFromActiveFormula(ServiceOrderItemBom? item)
        {
            if (item != null && ActiveBOMList.Contains(item))
            {
                ActiveBOMList.Remove(item);
                RecalculateActiveBOMTotals();
            }
        }

        private void RecalculateActiveBOMTotals()
        {
            decimal totalBatchSize = ActiveItem.TargetQuantity > 0 ? ActiveItem.TargetQuantity : 100m;
            foreach (var item in ActiveBOMList)
            {
                item.CalculatedQuantity = Math.Round((item.PercentageValue / 100m) * totalBatchSize, 3);
            }
            OnPropertyChanged(nameof(ActiveBOMTotalPercentage));
            OnPropertyChanged(nameof(ActiveBOMWaterPercentage));
        }

        // ==========================================
        // 🌟 NAVIGATION COMMANDS
        // ==========================================
        [RelayCommand]
        private void OpenCreateOrder()
        {
            CurrentOrder = new ServiceOrder
            {
                OrderNumber = $"SO-{DateTime.Now:yyyyMMdd}-{DateTime.Now.Ticks % 1000:D3}",
                OrderDate = DateTime.Today,
                DeliveryDueDate = DateTime.Today.AddDays(15),
                OrderStatus = "Draft"
            };
            SelectedCustomer = null;
            ResetItemForm();
            CurrentStep = 1;
            IsOrderLocked = false;
            IsFormOpen = true;
        }

        [RelayCommand]
        private async Task EditOrder(ServiceOrder? item)
        {
            if (item == null) return;
            var fullOrder = await _orderService.GetOrderByIdAsync(item.OrderId);
            if (fullOrder == null) return;

            CurrentOrder = fullOrder;
            CurrentOrder.RecalculateSummary();
            SelectedCustomer = CustomersList.FirstOrDefault(c => c.LeadId == fullOrder.CustomerId);
            IsOrderLocked = fullOrder.BatchOrdersCount > 0 || fullOrder.OrderStatus == "Completed" || fullOrder.OrderStatus == "InProduction";

            CurrentStep = 2; // Open directly in Review/Overview mode for existing orders
            IsFormOpen = true;
        }

        [RelayCommand]
        private async Task DeleteOrderAsync(ServiceOrder? item)
        {
            if (item == null) return;
            var confirm = MessageBox.Show($"Are you sure you want to delete order '{item.OrderNumber}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    await _orderService.DeleteOrderAsync(item.OrderId);
                    await LoadOrdersListAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Delete Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        [RelayCommand]
        private void CloseWizard()
        {
            IsFormOpen = false;
            CurrentOrder = new ServiceOrder();
            CurrentStep = 1;
        }        

        // ==========================================
        // 🌟 ADD ITEM & WIZARD NAVIGATION
        // ==========================================
        [RelayCommand]
        private async Task AddItemToCart()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Please select a Customer first.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string finalBrand = SelectedBrand?.BrandName ?? CustomBrandText;
            if (string.IsNullOrWhiteSpace(finalBrand))
            {
                MessageBox.Show("Please enter or select a Client Brand Name.", "Brand Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ActiveBOMList.Any())
            {
                MessageBox.Show("Please add at least one ingredient to the formula / BOM table.", "BOM Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ActiveBOMTotalPercentage > 100m)
            {
                MessageBox.Show($"Total active ingredients ({ActiveBOMTotalPercentage:N2}%) cannot exceed 100%.", "Formula Exceeded", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Auto-save brand to customer registry if it's a new name
            if (SelectedBrand == null && !string.IsNullOrWhiteSpace(finalBrand))
            {
                var newBrand = new CustomerBrand { CustomerId = SelectedCustomer.LeadId, BrandName = finalBrand.Trim() };
                newBrand.BrandId = await _orderService.SaveCustomerBrandAsync(newBrand);
                CustomerBrandsList.Add(newBrand);
                SelectedBrand = newBrand;
            }

            string resolvedProductName = SelectedProduct?.Name
                ?? SelectedFormulation?.FormulationName
                ?? $"{finalBrand.Trim()} {ActiveItem.PackagingType} ({ActiveItem.PackSize.Trim()})";

            var lineItem = new ServiceOrderItem
            {
                BrandId = SelectedBrand?.BrandId,
                BrandName = finalBrand.Trim(),
                ProductId = SelectedProduct?.ProductId,
                ProductName = resolvedProductName,
                MasterFormulationId = SelectedFormulation?.FormulationId,
                FormulationName = SelectedFormulation?.FormulationName ?? "Custom Recipe",
                PackagingType = ActiveItem.PackagingType,
                PackSize = ActiveItem.PackSize.Trim(),
                ColorShade = ActiveItem.ColorShade?.Trim(),
                FragranceFlavor = ActiveItem.FragranceFlavor?.Trim(),
                TargetQuantity = ActiveItem.TargetQuantity,
                Unit = ActiveItem.Unit,
                UnitPrice = ActiveItem.UnitPrice,
                GstPercent = ActiveItem.GstPercent,
                BomItems = new ObservableCollection<ServiceOrderItemBom>(ActiveBOMList)
            };

            lineItem.RecalculateLineTotal();
            lineItem.RecalculateBOMQuantities(lineItem.TargetQuantity);

            CurrentOrder.Items.Add(lineItem);
            CurrentOrder.RecalculateSummary();

            OnPropertyChanged(nameof(CanChangeCustomer));
            OnPropertyChanged(nameof(ConfiguredProductsCount));

            ResetItemForm();
            MessageBox.Show($"Product '{lineItem.BrandName}' added to order. Total configured: {CurrentOrder.Items.Count}", "Item Added", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void GoToStep2()
        {
            if (!CurrentOrder.Items.Any())
            {
                MessageBox.Show("Please configure and add at least 1 product before proceeding to review.", "No Products Added", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            CurrentStep = 2;
        }

        [RelayCommand]
        private void GoToStep1() => CurrentStep = 1;

        [RelayCommand]
        private void RemoveLineItem(ServiceOrderItem? item)
        {
            if (item != null && CurrentOrder.Items.Contains(item))
            {
                CurrentOrder.Items.Remove(item);
                CurrentOrder.RecalculateSummary();
                OnPropertyChanged(nameof(CanChangeCustomer));
                OnPropertyChanged(nameof(ConfiguredProductsCount));
            }
        }

        private void ResetItemForm()
        {
            SelectedProduct = null;
            SelectedFormulation = null;
            SelectedBrand = null;
            CustomBrandText = string.Empty;
            ActiveBOMList.Clear();
            ActiveItem = new ServiceOrderItem
            {
                PackagingType = "Bottle",
                PackSize = "100 ml",
                TargetQuantity = 1000m,
                Unit = "Pcs",
                UnitPrice = 0m,
                GstPercent = 18m
            };
            RecalculateActiveBOMTotals();
        }

        // ==========================================
        // 🌟 SAVE / DELETE ACTIONS
        // ==========================================
        [RelayCommand]
        private async Task SubmitOrderAsync()
        {
            try
            {
                await _orderService.SaveServiceOrderAsync(CurrentOrder);
                MessageBox.Show("Service Line Order submitted successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadOrdersListAsync();
                IsFormOpen = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Submission error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
