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
    /// Interaction logic for CreateLedgerDialog.xaml
    /// </summary>
    public partial class CreateLedgerDialog : Window
    {
        public CreateLedgerDialog()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }
    }
}
