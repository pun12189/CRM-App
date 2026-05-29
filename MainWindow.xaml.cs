using CallMan.Data;
using CallMan.Models;
using CallMan.Services;
using CallMan.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CallMan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int _secretClickCount = 0;
        private DispatcherTimer _clickResetTimer;

        private ToastPollingWorker? _bgToasterEngine;

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            this.DataContext = vm;

            _clickResetTimer = new DispatcherTimer();
            _clickResetTimer.Interval = TimeSpan.FromSeconds(2); // Must finish clicks within 2 seconds
            _clickResetTimer.Tick += ClickResetTimer_Tick;

            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 3. Fire up the Live Background Listener Engine 
            _bgToasterEngine = App.ServiceProvider!.GetRequiredService<ToastPollingWorker>();
        }

        protected override async void OnClosed(EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Trigger the logout command logic to save the timestamp
                await vm.LogoutCommand.ExecuteAsync(null);
            }

            _bgToasterEngine?.Stop();
            base.OnClosed(e);
        }

        private void SecretButton_Click(object sender, RoutedEventArgs e)
        {
            // Restart the timeout timer on every click
            _clickResetTimer.Stop();
            _clickResetTimer.Start();

            _secretClickCount++;

            // Trigger after exactly 4 clicks
            if (_secretClickCount >= 4)
            {
                _secretClickCount = 0; // Reset counter
                _clickResetTimer.Stop();

                // Open the right drawer via ViewModel or direct view property
                var vm = this.DataContext as MainViewModel;
                if (vm != null)
                {
                    vm.IsAdminMenuOpen = true;
                }
            }
        }

        private void ClickResetTimer_Tick(object sender, EventArgs e)
        {
            _secretClickCount = 0; // User took too long, reset the count
            _clickResetTimer.Stop();
        }

        private async void NotificationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null || listBox.SelectedItem == null) return;

            if (listBox.SelectedItem is ToastQueueItem clickedItem)
            {
                // 1. Perform database state synchronization update over your LAN
                if (DataContext is MainViewModel vm)
                {
                    // Trigger the logout command logic to save the timestamp
                    await vm.RoutingService.HandleNotificationClick(clickedItem.ToastId);
                    await vm.DialogService.ShowHistoryDialog(clickedItem.LeadId);
                }                
            }

            listBox.SelectedIndex = -1; // Reset selection index tracking array
        }
    }
}