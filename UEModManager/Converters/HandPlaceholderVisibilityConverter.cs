using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UEModManager.Converters
{
    public class HandPlaceholderVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? name = value as string;
            if (name == "全部" || name == "已启用" || name == "已禁用")
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 