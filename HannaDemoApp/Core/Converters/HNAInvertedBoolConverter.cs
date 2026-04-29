using System.Globalization;

namespace HannaDemoApp.Core.Converters;

public class HNAInvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        return value is bool boolValue ? !boolValue : false;
    }

    public object? ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        return value is bool boolValue ? !boolValue : false;
    }
}
