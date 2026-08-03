using Tijori.Services;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Tijori.Views
{
    /// <summary>
    /// Interaction logic for MaturedLeadsView.xaml
    /// </summary>
    public partial class MaturedLeadsView : UserControl
    {
        public MaturedLeadsView()
        {
            InitializeComponent();
            this.Loaded += MaturedLeadsView_Loaded;
        }

        private void MaturedLeadsView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingService.Hide();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MaturedLeadsViewModel vm)
            {
                vm.RecalculateSelectionStates();
            }
        }
    }
}
