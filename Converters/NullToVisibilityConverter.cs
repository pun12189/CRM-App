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
    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Returns Visibility.Visible if the value is NULL.
        /// Returns Visibility.Collapsed if the value is NOT NULL.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If the value is null, show the placeholder icon
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
