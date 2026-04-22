using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CallMan.Converters
{
    public class StringToBoolConverter : IValueConverter
    {
        // Converts String to Boolean (ViewModel -> UI)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;

            string checkValue = value.ToString();
            string targetValue = parameter.ToString();

            return checkValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase);
        }

        // Converts Boolean back to String (UI -> ViewModel)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return null;

            // If the RadioButton was checked (true), return the parameter string
            return (bool)value ? parameter.ToString() : Binding.DoNothing;
        }
    }
}
