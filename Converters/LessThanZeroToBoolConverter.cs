using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CallMan.Converters
{
    [ValueConversion(typeof(object), typeof(bool))]
    public class LessThanZeroToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;

            // Handle decimal values (Primary for currency)
            if (value is decimal decimalValue)
                return decimalValue < 0;

            // Handle double/float values
            if (value is double doubleValue)
                return doubleValue < 0;

            if (value is float floatValue)
                return floatValue < 0;

            // Handle integer values
            if (value is int intValue)
                return intValue < 0;

            if (value is long longValue)
                return longValue < 0;

            // Fallback parsing for string representations
            if (decimal.TryParse(value.ToString(), NumberStyles.Any, culture, out decimal parsedValue))
                return parsedValue < 0;

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for LessThanZeroToBoolConverter.");
        }
    }
}
