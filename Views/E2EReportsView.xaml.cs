using Tijori.Services;
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
    /// Interaction logic for E2EReportsView.xaml
    /// </summary>
    public partial class E2EReportsView : UserControl
    {
        public E2EReportsView()
        {
            InitializeComponent();
            this.Loaded += E2EReportsView_Loaded;
        }

        private void E2EReportsView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingService.Hide();
        }
    }
}
