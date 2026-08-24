using Tijori.Data;
using Tijori.Dialogs;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Services;
using Tijori.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Tijori
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private int _secretClickCount = 0;
        private DispatcherTimer _clickResetTimer;

        private ToastPollingWorker? _bgToasterEngine;

        private bool _isSafeToClose = false;

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

        /// <summary>
        /// Automatically forces the search ComboBox dropdown to slide open 
        /// the exact moment records populate from the database.
        /// </summary>
        private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Safeguard cast access to ensure we are modifying the correct control element
            if (sender is ComboBox comboBox)
            {
                // Only open the dropdown box if the operator typed an actual query string 
                // and the backend has successfully bound valid matching row items to display.
                if (comboBox.IsKeyboardFocusWithin &&
                    !string.IsNullOrWhiteSpace(comboBox.Text) &&
                    comboBox.Items.Count > 0)
                {
                    // Forces the pop-up panel to slide down instantly on the screen
                    comboBox.IsDropDownOpen = true;
                }
                else if (string.IsNullOrWhiteSpace(comboBox.Text) || comboBox.Items.Count == 0)
                {
                    // Cleanly close the dropdown box if the search query is completely erased
                    comboBox.IsDropDownOpen = false;
                }

                if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is TextBox textBox)
                {
                    // Deselect text and keep caret at the end
                    textBox.SelectionLength = 0;
                    textBox.CaretIndex = textBox.Text.Length;
                }
            }
        }

        /// <summary>
        /// FIXED: Clears logical keyboard focus from clicked menu headers.
        /// This ensures subsequent hover actions continue to slide open dropdown panels instantly!
        /// </summary>
        private void TopLevelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                // If it's a top-level action button (no sub-items), release focus immediately after click
                if (menuItem.Items.Count == 0)
                {
                    // Pass focus back to the parent control window grid context
                    Keyboard.ClearFocus();
                }
            }
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            if (_isSafeToClose)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;

            var backupService = App.ServiceProvider?.GetRequiredService<BackupService>();
            var session = App.ServiceProvider?.GetRequiredService<IUserSession>();

            DateTime? lastBackupDate = null;
            string lastBackupUser = "None";
            bool emailSettingsExist = false;

            // Evaluate operational connectivity modes via Network interfaces & License states rules
            bool isInternetAvailable = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()
                                       && Tijori.Core.LicenseManager.Current?.AreOnlineServicesAllowed == true;

            if (backupService != null)
            {
                // Gather database log summaries and row verification checks simultaneously
                var (date, user) = await backupService.GetLastBackupDetailsAsync();
                lastBackupDate = date;
                lastBackupUser = user;
                emailSettingsExist = await backupService.CheckEmailSettingsExistAsync();
            }

            // Initialize custom modal window dialog pre-seeded with validation contexts
            var dialog = new BackupConfirmationWindow(lastBackupDate, lastBackupUser, emailSettingsExist, isInternetAvailable) { Owner = this };
            dialog.ShowDialog();

            if (dialog.UserSelectedCancel)
            {
                return;
            }

            if (!dialog.UserSelectedBackup)
            {
                _isSafeToClose = true;
                Application.Current.Shutdown();
                return;
            }

            // Thread Affinity Extraction Fix: Extract standard primitives safely on the UI Thread first
            bool shouldEmail = dialog.SendEmailChecked;
            string destinationEmail = dialog.TargetEmailAddress;

            this.Hide();

            string currentUserName = session?.CurrentUser ?? "System Terminal";

            if (backupService != null)
            {
                // Pass UI primitives into the background file-system streaming engine worker thread task
                await Task.Run(async () =>
                    await backupService.ProcessManualExitBackupAsync(currentUserName, shouldEmail, destinationEmail));
            }

            _isSafeToClose = true;
            Application.Current.Shutdown();
        }
    }
}