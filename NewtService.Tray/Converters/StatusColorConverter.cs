using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NewtService.Tray.Converters;

public class StatusColorConverter : IValueConverter
{
    public static readonly StatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isRunning)
        {
            return isRunning 
                ? new SolidColorBrush(Color.Parse("#4CAF50")) 
                : new SolidColorBrush(Color.Parse("#F44336"));
        }
        return new SolidColorBrush(Color.Parse("#8b8b9a"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

