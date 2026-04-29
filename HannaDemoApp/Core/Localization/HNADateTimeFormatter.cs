using System.Globalization;

namespace HannaDemoApp.Core.Localization;

public static class HNADateTimeFormatter
{
    public static string FormatDateTime(DateTime value)
    {
        var localValue = value.Kind == DateTimeKind.Unspecified
            ? value
            : value.ToLocalTime();

        return localValue.ToString("dd/MM/yyyy h:mm:ss tt", CultureInfo.CurrentCulture);
    }

    public static string FormatTime(DateTime value)
    {
        var localValue = value.Kind == DateTimeKind.Unspecified
            ? value
            : value.ToLocalTime();

        return localValue.ToString("h:mm:ss tt", CultureInfo.CurrentCulture);
    }

    public static string FormatStoredTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var currentCultureValue))
        {
            return FormatDateTime(currentCultureValue);
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var invariantDateTime))
        {
            return FormatDateTime(invariantDateTime);
        }

        if (TimeOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var currentCultureTime))
        {
            return currentCultureTime.ToString("h:mm:ss tt", CultureInfo.CurrentCulture);
        }

        if (TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariantTime))
        {
            return invariantTime.ToString("h:mm:ss tt", CultureInfo.CurrentCulture);
        }

        return value;
    }
}
