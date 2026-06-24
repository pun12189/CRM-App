using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CallMan.Dialogs
{
    /// <summary>
    /// Interaction logic for BackupConfirmationWindow.xaml
    /// </summary>
    public partial class BackupConfirmationWindow : Window
    {
        private readonly bool _emailSettingsExist;
        private readonly bool _isInternetAvailable;

        public bool UserSelectedBackup { get; private set; } = false;
        public bool UserSelectedCancel { get; private set; } = false;
        public bool SendEmailChecked => ChkSendEmail.IsChecked ?? false;
        public string TargetEmailAddress => TxtTargetEmail.Text.Trim();

        public BackupConfirmationWindow(DateTime? lastBackupDate, string lastUser, bool emailSettingsExist, bool isInternetAvailable)
        {
            InitializeComponent();

            _emailSettingsExist = emailSettingsExist;
            _isInternetAvailable = isInternetAvailable;

            // Apply historical metadata values
            if (lastBackupDate.HasValue)
            {
                RunLastBackupDate.Text = lastBackupDate.Value.ToString("dd-MM-yyyy hh:mm tt");
                RunLastBackupUser.Text = lastUser;
            }
            else
            {
                RunLastBackupDate.Text = "No previous logs found";
                RunLastBackupUser.Text = "N/A";
            }

            // Evaluation Guard: Default checkbox selection based on live configuration settings availability
            if (_emailSettingsExist && _isInternetAvailable)
            {
                ChkSendEmail.IsChecked = true;
                // Seed a default email template value if desired (optional)
                TxtTargetEmail.Text = "backup@yourdomain.com";
            }
            else
            {
                ChkSendEmail.IsChecked = false;
            }
        }

        private void ChkSendEmail_Click(object sender, RoutedEventArgs e)
        {
            // Condition 1: Check License Manager/Hardware Connectivity State context maps
            if (!_isInternetAvailable)
            {
                MessageBox.Show(
                    "Cloud Sync services are currently unavailable because the application is running in OFFLINE mode or disconnected from the network infrastructure.",
                    "Network Connection Required", MessageBoxButton.OK, MessageBoxImage.Warning);

                ChkSendEmail.IsChecked = false;
                return;
            }

            // Condition 2: Check Database Table Row Configurations existence parameters
            if (!_emailSettingsExist)
            {
                MessageBox.Show(
                    "No configured communication parameters found.\n\nPlease save your outgoing email server parameters in the system Admin Settings workspace panel first.",
                    "Email Settings Missing", MessageBoxButton.OK, MessageBoxImage.Information);

                ChkSendEmail.IsChecked = false;
                return;
            }
        }

        private void BackupAndExit_Click(object sender, RoutedEventArgs e)
        {
            if (SendEmailChecked && string.IsNullOrWhiteSpace(TargetEmailAddress))
            {
                MessageBox.Show("Please enter a valid destination email address to receive the compressed database backup archive.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UserSelectedBackup = true;
            UserSelectedCancel = false;
            this.DialogResult = true;
            this.Close();
        }

        private void JustExit_Click(object sender, RoutedEventArgs e)
        {
            UserSelectedBackup = false;
            UserSelectedCancel = false;
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            UserSelectedBackup = false;
            UserSelectedCancel = true;
            this.DialogResult = false;
            this.Close();
        }
    }
}
