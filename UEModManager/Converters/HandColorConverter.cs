using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UEModManager.Converters
{
    public class HandColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                // 绿色背景时，手柄为白色，否则为红色
                if (brush.Color == (Color)ColorConverter.ConvertFromString("#10B981"))
                    return Brushes.White;
            }
            return Brushes.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 