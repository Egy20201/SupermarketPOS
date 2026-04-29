using System;
using System.Globalization;
using System.Windows.Data;

namespace SupermarketPOS.UI.Converters
{
    public class BooleanToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool && (bool)value) ? "✅ نشط" : "❌ غير نشط";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == "✅ نشط";
        }
    }
}