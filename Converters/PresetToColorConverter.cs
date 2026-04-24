using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace CallMan.Converters
{
    public class PresetToColorConverter : IValueConverter
    {
        // Colors from your image
        private static readonly SolidColorBrush ActiveBlue = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4285F4"));
        private static readonly SolidColorBrush InactiveGrey = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return InactiveGrey;

            // If the SelectedPreset matches the button's parameter, make it Blue
            return value.ToString() == parameter.ToString() ? ActiveBlue : InactiveGrey;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
