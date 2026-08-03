using Tijori.Models;
using Tijori.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Interaction logic for WorkflowView.xaml
    /// </summary>
    public partial class WorkflowView : UserControl
    {
        public WorkflowView()
        {
            InitializeComponent();
        }

        private void TemplateBox_KeyUp(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Get the position of the cursor
            int caretIndex = textBox.CaretIndex;

            // Check if the character just typed was '@'
            if (caretIndex > 0)
            {
                string textUpToCaret = textBox.Text.Substring(0, caretIndex);
                if (textUpToCaret.EndsWith("@"))
                {
                    if (DataContext is WorkflowViewModel vm)
                    {
                        vm.IsTagPopupOpen = true;
                        // The Popup in XAML is already bound to placement target "TemplateBox"
                    }
                }
                else
                {
                    // Close popup if user continues typing something else or deletes '@'
                    if (DataContext is WorkflowViewModel vm && vm.IsTagPopupOpen)
                        vm.IsTagPopupOpen = false;
                }
            }
        }

        private void TagList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is WorkflowTag selectedTag)
            {
                var textBox = TemplateBox; // Named in your XAML
                int caretIndex = textBox.CaretIndex;

                // Find the last '@' typed before the current cursor position
                string text = textBox.Text;
                int atIndex = text.LastIndexOf('@', Math.Max(0, caretIndex - 1));

                if (atIndex != -1)
                {
                    // Replace the '@' (and any partial tag text) with the proper tag
                    string tagToInsert = "{{" + selectedTag.TagValue + "}}";

                    // Reconstruct the string
                    textBox.Text = text.Remove(atIndex, caretIndex - atIndex).Insert(atIndex, tagToInsert);

                    // Move cursor to the end of the newly inserted tag
                    textBox.CaretIndex = atIndex + tagToInsert.Length;
                }

                // Close the popup and refocus the textbox
                if (DataContext is WorkflowViewModel vm)
                {
                    vm.IsTagPopupOpen = false;
                }

                textBox.Focus();

                // Clear selection so the user can select the same tag again later
                ((ListBox)sender).SelectedIndex = -1;
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // Regex that matches only numeric input
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
