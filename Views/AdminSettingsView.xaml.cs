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
    /// Interaction logic for AdminSettingsView.xaml
    /// </summary>
    public partial class AdminSettingsView : UserControl
    {
        public AdminSettingsView()
        {
            InitializeComponent();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            var expandedExpander = sender as Expander;
            if (expandedExpander != null && expandedExpander.Tag != null)
            {
                // Update the ViewModel with the Tag of the expander that was just opened
                int index = int.Parse(expandedExpander.Tag.ToString());
                var vm = (AdminSettingsViewModel)this.DataContext;
                vm.OpenExpanderIndex = index;
            }
        }
    }
}
