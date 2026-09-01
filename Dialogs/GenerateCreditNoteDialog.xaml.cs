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
using Tijori.ViewModels;

namespace Tijori.Dialogs
{
    /// <summary>
    /// Interaction logic for GenerateCreditNoteDialog.xaml
    /// </summary>
    public partial class GenerateCreditNoteDialog : Window
    {
        public GenerateCreditNoteDialog(GenerateCreditNoteViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.RequestClose = (success) =>
            {
                DialogResult = success;
                Close();
            };
        }
    }
}
