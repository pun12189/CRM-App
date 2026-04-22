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

        public MainViewModel()
        {
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
                    // Add other cases as you build them
            }
        }
    }
}
