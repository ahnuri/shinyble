using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace HannaDemoApp.Core.Converters
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && b ? 1.0 : 0.3;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}