using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Tijori.Core;

namespace Tijori.Converters
{
    public class FieldVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string propertyName && !string.IsNullOrWhiteSpace(propertyName))
            {
                if (value is ModuleFieldConfigMap configMap)
                {
                    bool isVisible = configMap.GetIsVisible(propertyName.Trim());
                    return isVisible ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return Visibility.Visible; // Default fallback: visible
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
