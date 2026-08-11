using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Tijori.Converters
{
    public class MultiBooleanToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0)
                return Visibility.Collapsed;

            foreach (var value in values)
            {
                // Handle standard booleans
                if (value is bool boolVal)
                {
                    if (!boolVal) return Visibility.Collapsed;
                }
                // Handle Visibility values passed from nested converters (e.g. FieldVisibilityConverter)
                else if (value is Visibility visVal)
                {
                    if (visVal != Visibility.Visible) return Visibility.Collapsed;
                }
                // Handle UnsetValue during UI initialization
                else if (value == DependencyProperty.UnsetValue || value == null)
                {
                    return Visibility.Collapsed;
                }
            }

            return Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
