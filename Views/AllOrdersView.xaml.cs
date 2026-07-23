using CallMan.Services;
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

namespace CallMan.Views
{
    /// <summary>
    /// Interaction logic for AllOrdersView.xaml
    /// </summary>
    public partial class AllOrdersView : UserControl
    {
        public AllOrdersView()
        {
            InitializeComponent();
            this.Loaded += AllOrdersView_Loaded;
        }

        private void AllOrdersView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingService.Hide();
        }
    }
}
