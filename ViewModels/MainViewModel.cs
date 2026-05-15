using CallMan.Interfaces;
using CallMan.Services;
using CallMan.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
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
        private readonly IDialogService _dialogService;
        private readonly IUserSession _session;
        private readonly LoginLogService _logService;

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
        public MainViewModel(LeadService leadService, IDialogService dialogService, IUserSession session, LoginLogService logService)
        {
            _leadService = leadService;
            _dialogService = dialogService; 
            _session = session;
            _logService = logService;
            _userName = session.CurrentUser;
            // Load Dashboard by default
            Navigate("Dashboard");

            SetupIdleTimer();
        }

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
                case "Dead":
                    var vm2 = App.ServiceProvider.GetRequiredService<LeadViewModel>();
                    await vm2.InitializeAsync(Models.Enums.LeadViewMode.Dead);
                    CurrentView = vm2;
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
    }
}
