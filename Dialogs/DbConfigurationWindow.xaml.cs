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
    /// Interaction logic for DbConfigurationWindow.xaml
    /// </summary>
    public partial class DbConfigurationWindow : Window
    {
        public DbConfigurationWindow()
        {
            InitializeComponent();

            var viewModel = new DbConfigurationViewModel();
            this.DataContext = viewModel;

            // Secure password property transfer hook
            viewModel.RequestClose += (success) =>
            {
                if (success) this.DialogResult = true;
                this.Close();
            };

            this.BtnTestConnection.Click += (s, e) =>
            {
                viewModel.Config.Password = TxtPassword.Password;
            };
        }
    }
}
