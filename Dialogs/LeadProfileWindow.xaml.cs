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
    /// Interaction logic for LeadProfileWindow.xaml
    /// </summary>
    public partial class LeadProfileWindow : Window
    {
        public LeadProfileWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }

        // Optional: Close on Escape key
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) this.Close();
            base.OnKeyDown(e);
        }
    }
}
