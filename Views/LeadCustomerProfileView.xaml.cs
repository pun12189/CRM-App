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
    /// Interaction logic for LeadCustomerProfileView.xaml
    /// </summary>
    public partial class LeadCustomerProfileView : UserControl
    {
        // Using a DependencyProperty as the backing store for TabsDataContext.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TabsDataContextProperty =
            DependencyProperty.Register("TabsDataContext", typeof(object), typeof(LeadCustomerProfileView), new PropertyMetadata(null));

        // Using a DependencyProperty as the backing store for IsInEditModel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsInEditModelProperty =
            DependencyProperty.Register("IsInEditModel", typeof(bool), typeof(LeadCustomerProfileView), new PropertyMetadata(false));

        public LeadCustomerProfileView()
        {
            InitializeComponent();
        }

        public bool IsInEditModel
        {
            get { return (bool)GetValue(IsInEditModelProperty); }
            set { SetValue(IsInEditModelProperty, value); }
        }

        public object TabsDataContext
        {
            get { return (object)GetValue(TabsDataContextProperty); }
            set { SetValue(TabsDataContextProperty, value); }
        }
    }
}
