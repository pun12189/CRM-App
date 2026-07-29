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

namespace CallMan.Dialogs
{
    /// <summary>
    /// Interaction logic for CreatePoWindow.xaml
    /// </summary>
    public partial class CreatePoWindow : Window
    {
        public CreatePoWindow()
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
        }
    }
}
