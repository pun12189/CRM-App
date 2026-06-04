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
    public class EnumToBooleanConverter : IValueConverter
    {
        // ViewModel state ---> UI RadioButton selection status
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;

            string checkValue = value.ToString();
            string targetValue = parameter.ToString();

            return string.Equals(checkValue, targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        // UI RadioButton Clicked ---> ViewModel state updates
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return DependencyProperty.UnsetValue;

            if ((bool)value)
            {
                string targetValue = parameter.ToString();
                return Enum.Parse(targetType, targetValue, true);
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
