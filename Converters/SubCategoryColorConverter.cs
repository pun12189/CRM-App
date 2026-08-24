using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace Tijori.Converters
{
    /// <summary>
    /// Returns a distinct blue brush when an item is a Sub-Category (has a parent), 
    /// and a muted gray brush for root/main items.
    /// </summary>
    public class SubCategoryColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush ActiveParentBrush = new((Color)ColorConverter.ConvertFromString("#0284C7")); // Vibrant Teal/Blue
        private static readonly SolidColorBrush DefaultDashBrush = new((Color)ColorConverter.ConvertFromString("#94A3B8"));   // Muted Slate

        static SubCategoryColorConverter()
        {
            // Freeze brushes for optimal WPF rendering performance & thread safety
            if (ActiveParentBrush.CanFreeze) ActiveParentBrush.Freeze();
            if (DefaultDashBrush.CanFreeze) DefaultDashBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSubCategory && isSubCategory)
            {
                return ActiveParentBrush;
            }

            return DefaultDashBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
