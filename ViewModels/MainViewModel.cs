using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Models.Enums;
using CallMan.Services;
using CallMan.Views;
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

namespace CallMan.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _userName = "Sanchi Developer";

        private DispatcherTimer _idleTimer;
        private const int IdleTimeoutMinutes = 30;

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
        public MainViewModel(LeadService leadService, IDialogService dialogService, IUserSession session, LoginLogService logService, NotificationHistoryService historyService, NotificationRoutingService routingService)
        {
            _leadService = leadService;
            _dialogService = dialogService; 
            _session = session;
            _logService = logService;
            _historyService = historyService;
            _routingService = routingService;
            _userName = session.CurrentUser;
            // Load Dashboard by default
            Navigate("Dashboard");

            SetupIdleTimer();
        }

        [RelayCommand] 
        private void QuickAddLead()
        {
            _leadsPageViewModel = App.ServiceProvider.GetRequiredService<LeadViewModel>();
            if (_leadsPageViewModel != null)
            {
                _leadsPageViewModel.OpenAddLeadDialogCommand.Execute(null);
            }
        }

        [RelayCommand] private void QuickAddCustomer() 
        {
            _maturedLeadsPageViewModel = App.ServiceProvider.GetRequiredService<MaturedLeadsViewModel>();
            if (_maturedLeadsPageViewModel != null)
            {
                _maturedLeadsPageViewModel.OpenAddLeadDialogCommand.Execute(null);
            }
        }

        [RelayCommand] private void QuickAddProduct() 
        {
            _inventoryPageViewModel = App.ServiceProvider.GetRequiredService<InventoryViewModel>();
            if (_inventoryPageViewModel != null)
            {
                _inventoryPageViewModel.OpenAddProductCommand.Execute(null);
            }
        }

        [RelayCommand] private void QuickAddOrder() 
        {
            _orderPageViewModel = App.ServiceProvider.GetRequiredService<AllOrdersViewModel>();
            if (_orderPageViewModel != null)
            {
                _orderPageViewModel.AddNewOrderCommand.Execute(null);
            }
        }

        [RelayCommand] private void QuickAddComplaint() { /* ... */ }

        [RelayCommand]
        private async Task Navigate(string destination)
        {
            switch (destination)
            {
                case "Leads":
                    // We pull the ViewModel from the DI container we set up in App.xaml.cs
                    var vm = App.ServiceProvider.GetRequiredService<LeadViewModel>();
                    await vm.InitializeAsync(Models.Enums.LeadViewMode.AllLeads);
                    CurrentView = vm;
                    break;
                case "Dashboard":
                    CurrentView = App.ServiceProvider.GetRequiredService<DashboardViewModel>();
                    break;
                case "Customers":
                    CurrentView = App.ServiceProvider.GetRequiredService<MaturedLeadsViewModel>();
                    break;
                case "Orders":
                    CurrentView = App.ServiceProvider.GetRequiredService<AllOrdersViewModel>();
                    break;
                case "Admin":
                    CurrentView = App.ServiceProvider.GetRequiredService<AdminSettingsViewModel>();
                    break;
                case "Inventory":
                    CurrentView = App.ServiceProvider.GetRequiredService<InventoryViewModel>();
                    break;
                case "Location":
                    CurrentView = App.ServiceProvider.GetRequiredService<OccupiedLocationViewModel>();
                    break;
                case "Reports":
                    CurrentView = App.ServiceProvider.GetRequiredService<E2EReportsDashboardViewModel>();
                    break;
                case "Dead":
                    var vm2 = App.ServiceProvider.GetRequiredService<LeadViewModel>();
                    await vm2.InitializeAsync(Models.Enums.LeadViewMode.Dead);
                    CurrentView = vm2;
                    break;
                case "WinbackPool":
                    var vm5 = App.ServiceProvider.GetRequiredService<LeadViewModel>();
                    await vm5.InitializeAsync(Models.Enums.LeadViewMode.WinbackPool);
                    CurrentView = vm5;
                    break;
                case "MyLeads":
                    var vm1 = App.ServiceProvider.GetRequiredService<LeadViewModel>();
                    await vm1.InitializeAsync(Models.Enums.LeadViewMode.MyLeads);
                    CurrentView = vm1;
                    break;
                case "Today":
                    var vm3 = App.ServiceProvider.GetRequiredService<LeadFollowupViewModel>();
                    await vm3.InitializeAsync(Models.Enums.LeadViewMode.TodayFollowUp);
                    CurrentView = vm3;
                    break;
                case "Future":
                    var vm4 = App.ServiceProvider.GetRequiredService<LeadFollowupViewModel>();
                    await vm4.InitializeAsync(Models.Enums.LeadViewMode.FutureFollowUp);
                    CurrentView = vm4;
                    break;
                    // Add other cases as you build them
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
                var loginView = App.ServiceProvider.GetRequiredService<LoginView>();
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

                App.Current.Dispatcher.Invoke(() =>
                {
                    // Overwriting the collection reference can cause input layout lag.
                    // Clear and add items to preserve the internal visual tree focus.
                    GlobalSearchResults.Clear();
                    foreach (var row in rows)
                    {
                        GlobalSearchResults.Add(row);
                    }
                });
            }
            catch { Debug.WriteLine("Error executing global query."); }
        }

        partial void OnSelectedGlobalSearchLeadChanged(GlobalSearchRowItem? value)
        {
            if (value == null) return;

            // 1. Navigate your application's primary content presenter view straight to your Leads page
            _leadsPageViewModel = App.ServiceProvider.GetRequiredService<LeadViewModel>();

            // 2. Clear out the full table collection and load strictly the single matched record (ref. Image 3)
            LoadSingleIsolatedLeadIntoGridAsync(value.Id);

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
                });

                // Switch workspace tabs safely after the collection is filtered
                CurrentView = _leadsPageViewModel;
            }
        }
    }
}
