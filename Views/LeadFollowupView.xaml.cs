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
    /// Interaction logic for LeadFollowupView.xaml
    /// </summary>
    public partial class LeadFollowupView : UserControl
    {
        public LeadFollowupView()
        {
            InitializeComponent();
            this.Loaded += LeadFollowupView_Loaded;
        }

        private void LeadFollowupView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingService.Hide();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is LeadFollowupViewModel vm)
            {
                vm.RecalculateSelectionStates();
            }
        }
    }
}
