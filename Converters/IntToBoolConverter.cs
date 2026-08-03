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
    /// Converts an integer count value into a boolean flag. 
    /// Returns true if value > 0; otherwise, false.
    /// </summary>
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int integerCount)
            {
                return integerCount > 0;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("Reverse conversion from boolean back to integer count paths is not supported.");
        }
    }
}
