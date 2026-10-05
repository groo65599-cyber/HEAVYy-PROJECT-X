using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HPXPirate.Converters;

/// <summary>Maps a 0-100 percentage to a green/amber/red brush.</summary>
public sealed class PercentToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Ok = new(Color.FromRgb(0x35, 0xE0, 0x8A));
    private static readonly SolidColorBrush Warn = new(Color.FromRgb(0xFF, 0xB0, 0x20));
    private static readonly SolidColorBrush Bad = new(Color.FromRgb(0xFF, 0x4D, 0x6D));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double p = value switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => 0
        };
        if (p >= 90) return Bad;
        if (p >= 70) return Warn;
        return Ok;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not Visibility.Visible;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Converts a hex colour string (e.g. "#FF2BD6") into a frozen SolidColorBrush.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                return brush;
            }
        }
        catch { }
        return new SolidColorBrush(Colors.Magenta);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Maps a status keyword (Stable / Bottleneck / Pressure ...) to a brush.</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = (value?.ToString() ?? "").ToLowerInvariant();
        if (s.Contains("critical") || s.Contains("bottleneck") || s.Contains("pressure") || s.Contains("instab") || s.Contains("bad"))
            return new SolidColorBrush(Color.FromRgb(0xFF, 0x4D, 0x6D));
        if (s.Contains("warn") || s.Contains("moderate") || s.Contains("elevated"))
            return new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x20));
        return new SolidColorBrush(Color.FromRgb(0x35, 0xE0, 0x8A));
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
