using CallMan.Interfaces;
using CallMan.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object? _currentView;

        [ObservableProperty]
        private string _userName = "Sanchi Developer";

        private readonly LeadService _leadService;
        private readonly IDialogService _dialogService;

        // These are injected via DI when the app starts
        public MainViewModel(LeadService leadService, IDialogService dialogService)
        {
            _leadService = leadService;
            _dialogService = dialogService; 
            // Load Dashboard by default
            Navigate("Dashboard");
        }

        [RelayCommand]
        private void Navigate(string destination)
        {
            switch (destination)
            {
                case "Leads":
                    // We pull the ViewModel from the DI container we set up in App.xaml.cs
                    CurrentView = App.ServiceProvider.GetRequiredService<LeadViewModel>();
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
                    // Add other cases as you build them
            }
        }
    }
}
