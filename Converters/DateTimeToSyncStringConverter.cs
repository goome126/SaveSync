using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SaveSync.Converters;

public sealed class DateTimeToSyncStringConverter : IValueConverter
{
    public static readonly DateTimeToSyncStringConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt && dt != default)
            return dt.ToString("g", culture);

        return "Never";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
