using Tijori.ViewModels;
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

namespace Tijori.Dialogs
{
    /// <summary>
    /// Interaction logic for AdminVerificationDialog.xaml
    /// </summary>
    public partial class AdminVerificationDialog : Window
    {
        public AdminVerificationDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            this.Loaded += AdminVerificationDialog_Loaded;
        }

        private void AdminVerificationDialog_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as AdminVerificationViewModel;
            if (vm != null)
            {
                vm.CloseDialogRequested += (sender, dialogResult) =>
                {
                    this.DialogResult = dialogResult;
                    this.Close();
                };
            }     
        }
    }
}
