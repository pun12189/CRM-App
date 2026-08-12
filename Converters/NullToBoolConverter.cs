using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Tijori.Converters
{
    /// <summary>
    /// Returns true if value is NOT null (or true if inverted).
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public bool IsInverted { get; set; } = false;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool hasValue = value != null;

            // Handle string-specific empty checks if value is a string
            if (value is string str)
            {
                hasValue = !string.IsNullOrWhiteSpace(str);
            }

            return IsInverted ? !hasValue : hasValue;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
