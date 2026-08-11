using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Tijori.Core;

namespace Tijori.Converters
{
    public class FieldLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string paramStr && !string.IsNullOrWhiteSpace(paramStr))
            {
                var parts = paramStr.Split('|');
                string propertyName = parts[0].Trim();
                string defaultPrompt = parts.Length > 1 ? parts[1].Trim() : propertyName;

                if (value is ModuleFieldConfigMap configMap)
                {
                    return configMap.GetLabel(propertyName, defaultPrompt);
                }

                return defaultPrompt;
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
