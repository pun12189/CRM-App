using CallMan.Models.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace CallMan.Converters
{
    public class EnumToIdConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UserRole role)
            {
                // Maps the enum values directly to the serial numbers from image_ca3de0.png
                return role switch
                {
                    UserRole.Admin => 1,
                    UserRole.Executive => 2,
                    UserRole.SubAdmin => 3,
                    UserRole.TeamLeader => 4,
                    _ => 0
                };
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("Reverse routing is not required for read-only index columns.");
        }
    }
}
