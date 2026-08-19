using Tijori.Dialogs;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.Services;
using Tijori.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Tijori.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _userName = "Sanchi Developer";

        private DispatcherTimer _idleTimer;
        private const int IdleTimeoutMinutes = 30;

        private readonly IServiceProvider _serviceProvider;
        private readonly LeadService _leadService;
        private LeadViewModel _leadsPageViewModel;
        private MaturedLeadsViewModel _maturedLeadsPageViewModel;
        private InventoryViewModel _inventoryPageViewModel;
        private AllOrdersViewModel _orderPageViewModel;

        [ObservableProperty]
        private IDialogService _dialogService;
        private readonly IUserSession _session;
        private readonly LoginLogService _logService;

        [ObservableProperty]
        private NotificationHistoryService _historyService;

        [ObservableProperty]
        private NotificationRoutingService _routingService;

        [ObservableProperty] private string _globalSearchQueryText = string.Empty;
        [ObservableProperty] private ObservableCollection<GlobalSearchRowItem> _globalSearchResults = new();
        [ObservableProperty] private GlobalSearchRowItem? _selectedGlobalSearchLead;

        [ObservableProperty] private bool _isGlobalLoadingActive;
        [ObservableProperty] private string _globalLoadingMessage = "Loading...";

        private bool _isAdminMenuOpen;
        public bool IsAdminMenuOpen
        {
            get => _isAdminMenuOpen;
            set
            {
                _isAdminMenuOpen = value;
                OnPropertyChanged();
            }
        }

        // These are injected via DI when the app starts
        public MainViewModel(IServiceProvider serviceProvider, LeadService leadService, IDialogService dialogService, IUserSession session, LoginLogService logService, NotificationHistoryService historyService, NotificationRoutingService routingService)
        {
            _leadService = leadService;
            _dialogService = dialogService; 
            _session = session;
            _logService = logService;
            _historyService = historyService;
            _routingService = routingService;
            _serviceProvider = serviceProvider;
            _userName = session.CurrentUser;
            // Load Dashboard by default
            _ = Navigate("Dashboard");

            SetupIdleTimer();

            LoadingService.OnLoadingStateChanged += HandleGlobalLoadingEvent;
        }

        private void HandleGlobalLoadingEvent(bool isActive, string message)
        {
            // Safely route execution parameters back to the main UI thread pool
            App.Current.Dispatcher.Invoke(() =>
            {
                IsGlobalLoadingActive = isActive;
                if (isActive && !string.IsNullOrEmpty(message))
                {
                    GlobalLoadingMessage = message;
                }
            });
        }

        [RelayCommand] 
        private void QuickAddLead()
        {
            _leadsPageViewModel = _serviceProvider.GetRequiredService<LeadViewModel>();
            if (_leadsPageViewModel != null)
            {
                _leadsPageViewModel.OpenAddLeadDialogCommand.Execute(null);
            }
        }

        [RelayCommand] private void QuickAddCustomer() 
        {
            _maturedLeadsPageViewModel = _serviceProvider.GetRequiredService<MaturedLeadsViewModel>();
            if (_maturedLeadsPageViewModel != null)
            {
                _maturedLeadsPageViewModel.OpenAddLeadDialogCommand.Execute(null);
            }
        }

        [RelayCommand] private void QuickAddProduct() 
        {
            _inventoryPageViewModel = _serviceProvider.GetRequiredService<InventoryViewModel>();
            if (_inventoryPageViewModel != null)
            {
                _inventoryPageViewModel.OpenAddProductCommand.Execute(null);
            }
        }

        [RelayCommand] private void QuickAddOrder() 
        {
            _orderPageViewModel = _serviceProvider.GetRequiredService<AllOrdersViewModel>();
            if (_orderPageViewModel != null)
            {
                _orderPageViewModel.AddNewOrderCommand.Execute(null);
            }
        }

        [RelayCommand] private void QuickAddComplaint() { /* ... */ }

        [RelayCommand]
        private async Task Navigate(string destination)
        {
            try
            {
                LoadingService.Show("Loading view... Please wait.");

                switch (destination)
                {
                    case "Leads":
                        // We pull the ViewModel from the DI container we set up in App.xaml.cs
                        var vm = _serviceProvider.GetRequiredService<LeadViewModel>();
                        await vm.InitializeAsync(Models.Enums.LeadViewMode.AllLeads);
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm;
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Dashboard":
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = _serviceProvider.GetRequiredService<DashboardViewModel>();
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Customers":
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = _serviceProvider.GetRequiredService<MaturedLeadsViewModel>();
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Orders":
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = _serviceProvider.GetRequiredService<AllOrdersViewModel>();
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Admin":
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = _serviceProvider.GetRequiredService<AdminSettingsViewModel>();
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Inventory":
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = _serviceProvider.GetRequiredService<InventoryViewModel>();
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Location":
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = _serviceProvider.GetRequiredService<OccupiedLocationViewModel>();
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Reports":
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = _serviceProvider.GetRequiredService<E2EReportsDashboardViewModel>();
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Dead":
                        var vm2 = _serviceProvider.GetRequiredService<LeadViewModel>();
                        await vm2.InitializeAsync(Models.Enums.LeadViewMode.Dead);
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm2;
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "WinbackPool":
                        var vm5 = _serviceProvider.GetRequiredService<LeadViewModel>();
                        await vm5.InitializeAsync(Models.Enums.LeadViewMode.WinbackPool);
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm5;
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "MyLeads":
                        var vm1 = _serviceProvider.GetRequiredService<LeadViewModel>();
                        await vm1.InitializeAsync(Models.Enums.LeadViewMode.MyLeads);
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm1;
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Today":
                        var vm3 = _serviceProvider.GetRequiredService<LeadFollowupViewModel>();
                        await vm3.InitializeAsync(Models.Enums.LeadViewMode.TodayFollowUp);
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm3;
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Future":
                        var vm4 = _serviceProvider.GetRequiredService<LeadFollowupViewModel>();
                        await vm4.InitializeAsync(Models.Enums.LeadViewMode.FutureFollowUp);
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm4;
                        }, DispatcherPriority.Background);
                        
                        break;
                    case "Drive":
                        var vm6 = _serviceProvider.GetRequiredService<DriveViewModel>();
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm6;
                        }, DispatcherPriority.Background);

                        break;
                    case "Purchase":
                        var vm7 = _serviceProvider.GetRequiredService<PurchaseViewModel>();
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm7;
                        }, DispatcherPriority.Background);

                        break;
                    case "Vendor":
                        var vm8 = _serviceProvider.GetRequiredService<VendorViewModel>();
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm8;
                        }, DispatcherPriority.Background);

                        break;
                    case "Ledger":
                        var vm9 = _serviceProvider.GetRequiredService<LedgerViewModel>();
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm9;
                        }, DispatcherPriority.Background);

                        break;
                    // Add other cases as you build them
                    case "Register":
                        var vm10 = _serviceProvider.GetRequiredService<StockRegisterViewModel>();
                        await vm10.InitializeAsync();
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm10;
                        }, DispatcherPriority.Background);

                        break;
                    case "ServiceOrder":
                        var vm11 = _serviceProvider.GetRequiredService<ServiceOrderViewModel>();
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm11;
                        }, DispatcherPriority.Background);

                        break;
                    case "Batch":
                        var vm12 = _serviceProvider.GetRequiredService<BatchTrackerViewModel>();
                        await App.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CurrentView = vm12;
                        }, DispatcherPriority.Background);

                        break;
                        // Add other cases as you build them
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to {destination}: {ex.Message}");
            }
            finally
            {
                // Ensure admin menu is closed after navigation
                LoadingService.Hide();
            }
        }

        private void SetupIdleTimer()
        {
            _idleTimer = new DispatcherTimer();
            _idleTimer.Interval = TimeSpan.FromMinutes(IdleTimeoutMinutes);
            _idleTimer.Tick += (s, e) => Logout();
            _idleTimer.Start();

            // Listen for activity in the window
            EventManager.RegisterClassHandler(typeof(Window), Window.MouseMoveEvent, new MouseEventHandler(ResetTimer));
            EventManager.RegisterClassHandler(typeof(Window), Window.KeyDownEvent, new KeyEventHandler(ResetTimer));
        }

        private void ResetTimer(object sender, EventArgs e)
        {
            if (_idleTimer.IsEnabled)
            {
                _idleTimer.Stop();
                _idleTimer.Start();
            }
        }

        [RelayCommand]
        private async Task Logout()
        {
            _idleTimer.Stop();

            if (_session.LogId > 0)
            {
                await _logService.RecordLogoutAsync(_session.LogId);
            }

            // 2. Clear session and return to Login Screen
            Application.Current.Dispatcher.Invoke(() =>
            {
                var loginView = _serviceProvider.GetRequiredService<LoginView>();
                loginView.Show();

                Application.Current.MainWindow = loginView;

                // 4. Close the old dashboard window
                // We find the specific window that belongs to this ViewModel
                var dashboardWindow = Application.Current.Windows.OfType<Window>()
            .FirstOrDefault(w => w != loginView && w.IsVisible);

                dashboardWindow?.Close();
            });
        }

        partial void OnGlobalSearchQueryTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
            {
                GlobalSearchResults.Clear();
                return;
            }

            // Fire an off-thread background database scan task to keep the UI super responsive
            _ = ExecuteGlobalQueryAsync(value.Trim());
        }

        private async Task ExecuteGlobalQueryAsync(string textPattern)
        {
            try
            {
                var rows = await _leadService.SearchGlobalQueryAsync(textPattern);

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Overwriting the collection reference can cause input layout lag.
                    // Clear and add items to preserve the internal visual tree focus.
                    GlobalSearchResults.Clear();
                    foreach (var row in rows)
                    {
                        GlobalSearchResults.Add(row);
                    }
                }, DispatcherPriority.Background);
            }
            catch { Debug.WriteLine("Error executing global query."); }
        }

        partial void OnSelectedGlobalSearchLeadChanged(GlobalSearchRowItem? value)
        {
            if (value == null) return;

            LoadingService.Show("Loading lead details... Please wait.");

            // 1. Navigate your application's primary content presenter view straight to your Leads page
            _leadsPageViewModel = _serviceProvider.GetRequiredService<LeadViewModel>();

            // 2. Clear out the full table collection and load strictly the single matched record (ref. Image 3)
            _ = LoadSingleIsolatedLeadIntoGridAsync(value.Id);

            // Clear search query value inputs to look clean for subsequent actions
            GlobalSearchQueryText = string.Empty;
            GlobalSearchResults.Clear();
        }

        private async Task LoadSingleIsolatedLeadIntoGridAsync(int targetLeadId)
        {
            if (targetLeadId != 0)
            {
                // FIXED: Using await with InvokeAsync allows the background thread 
                // to wait until the UI initialization completes sequentially.
                await App.Current.Dispatcher.InvokeAsync(async () =>
                {
                    // 1. Wait for database records to load fully into memory
                    await _leadsPageViewModel.InitializeAsync(Models.Enums.LeadViewMode.AllLeads);

                    // 2. Now apply the predicate filter securely
                    _leadsPageViewModel.LeadsCollection.Filter = item =>
                    {
                        if (item is Lead lead)
                        {
                            return lead.LeadId == targetLeadId; // Only show this row
                        }
                        return false;
                    };

                    // 3. Force the UI DataGrid layout to update its rows immediately
                    _leadsPageViewModel.LeadsCollection.Refresh();

                    // 4. Reset your toolbar counter metrics
                    _leadsPageViewModel.SelectedLeadsCount = 0;
                }, DispatcherPriority.Background);

                // Switch workspace tabs safely after the collection is filtered
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentView = _leadsPageViewModel;
                }, DispatcherPriority.Background);
                
                await Task.Delay(100); // Small delay to allow UI to update before hiding the loading indicator
                LoadingService.Hide();
            }
        }

        [RelayCommand]
        private async Task OpenActivationModal()
        {
            var activationViewModel = _serviceProvider.GetRequiredService<ActivationViewModel>();
            var activationWindow = new ActivationWindow
            {
                DataContext = activationViewModel
            };

            activationWindow.ShowDialog();
        }
    }
}
