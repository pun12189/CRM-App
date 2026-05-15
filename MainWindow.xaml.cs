using CallMan.ViewModels;
using System.Windows;
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

        public MainWindow(MainViewModel vm)
        {
            InitializeComponent();
            this.DataContext = vm;

            _clickResetTimer = new DispatcherTimer();
            _clickResetTimer.Interval = TimeSpan.FromSeconds(2); // Must finish clicks within 2 seconds
            _clickResetTimer.Tick += ClickResetTimer_Tick;
        }

        protected override async void OnClosed(EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Trigger the logout command logic to save the timestamp
                await vm.LogoutCommand.ExecuteAsync(null);
            }
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
    }
}