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
    public class LeadStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value?.ToString()?.ToLower() ?? "";
            string param = parameter?.ToString()?.ToLower();
            if (param == "dark")
            {
                return status switch
                {
                    "new" => new SolidColorBrush(Colors.SkyBlue),      // Light Blue
                    "followup" => new SolidColorBrush(Colors.OrangeRed), // Light Yellow
                    "matured" => new SolidColorBrush(Colors.ForestGreen),
                    "dead" => new SolidColorBrush(Colors.Red),
                    "winback pool" => new SolidColorBrush(Colors.DarkRed),// Light Green
                    _ => Brushes.White
                };
            }

            return status switch
            {
                "new" => new SolidColorBrush(Color.FromArgb(150, 215, 237, 254)),      // Light Blue
                "followup" => new SolidColorBrush(Color.FromArgb(150, 209, 209, 209)), // Light Yellow
                "matured" => new SolidColorBrush(Color.FromArgb(150, 133, 225, 137)),
                "dead" => new SolidColorBrush(Color.FromRgb(214, 191, 191)),
                "winback pool" => new SolidColorBrush(Color.FromRgb(255, 191, 191)),// Light Green
                _ => Brushes.White
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
