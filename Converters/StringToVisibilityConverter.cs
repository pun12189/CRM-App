using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace CallMan.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a string value to a WPF Visibility state.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                // If the string has actual content, show the element. Otherwise, collapse it.
                return !string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        /// <summary>
        /// Optional: Backwards conversion if needed (typically not used for visibility).
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
