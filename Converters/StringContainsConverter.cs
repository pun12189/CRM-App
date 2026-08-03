using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Tijori.Converters
{
    public class StringContainsConverter : IValueConverter
    {
        /// <summary>
        /// Evaluates if a bound text source string contains a targeted substring token parameter.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string sourceText = value.ToString() ?? string.Empty;
            string substringToFind = parameter.ToString() ?? string.Empty;

            // Performs an efficient, case-insensitive substring screening pass
            return sourceText.Contains(substringToFind, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("StringContainsConverter supports One-Way evaluation loops only.");
        }
    }
}
