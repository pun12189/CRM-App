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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CallMan.Views
{
    /// <summary>
    /// Interaction logic for LeadFollowupView.xaml
    /// </summary>
    public partial class LeadFollowupView : UserControl
    {
        public LeadFollowupView()
        {
            InitializeComponent();
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
