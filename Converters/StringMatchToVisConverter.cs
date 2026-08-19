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
    /// <summary>
    /// Compares a string value to a parameter string.
    /// Returns Visible if they match (case-insensitive); otherwise Collapsed.
    /// </summary>
    public class StringMatchToVisConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string strValue = value.ToString()?.Trim() ?? string.Empty;
            string strParam = parameter.ToString()?.Trim() ?? string.Empty;

            return string.Equals(strValue, strParam, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
            {
                return parameter.ToString()!;
            }

            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Inverted variant: Returns Collapsed if they match, Visible if they differ.
    /// </summary>
    public class InvertedStringMatchToVisConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Visible;

            string strValue = value.ToString()?.Trim() ?? string.Empty;
            string strParam = parameter.ToString()?.Trim() ?? string.Empty;

            return string.Equals(strValue, strParam, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
