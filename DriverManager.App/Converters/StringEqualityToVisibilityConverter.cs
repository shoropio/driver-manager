using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DriverManager.App.Converters;

public sealed class StringEqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && parameter is string param)
        {
            return string.Equals(str, param, StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}