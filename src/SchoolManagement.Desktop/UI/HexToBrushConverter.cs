using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SchoolManagement.Desktop.UI;

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex))
            return new SolidColorBrush(Color.FromRgb(37, 99, 235));

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex.StartsWith('#') ? hex : "#" + hex)!;
            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(Color.FromRgb(37, 99, 235));
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
