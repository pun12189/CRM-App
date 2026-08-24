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
    /// Returns FontWeights.SemiBold for Sub-Categories and FontWeights.Normal for Root items.
    /// </summary>
    public class SubCategoryWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSubCategory && isSubCategory)
            {
                return FontWeights.SemiBold;
            }

            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
