using CallMan.ViewModels;
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
    /// Interaction logic for AddStaffWindow.xaml
    /// </summary>
    public partial class AddStaffWindow : Window
    {
        public AddStaffWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        // Connect the ViewModel event to the Window Close
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            if (DataContext is AddStaffDialogViewModel vm)
            {
                vm.RequestClose += (result) => {
                    this.DialogResult = result;
                    this.Close();
                };
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => this.Close();

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Release the context event listener safely on dialog termination
            if (DataContext is AddStaffDialogViewModel vm)
            {
                vm.Cleanup();
            }
        }
    }
}
