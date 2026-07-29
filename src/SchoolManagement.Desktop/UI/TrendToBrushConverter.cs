using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SchoolManagement.Desktop.UI;

public sealed class TrendToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var positive = value is true;
        return new SolidColorBrush(positive ? Color.FromRgb(22, 163, 74) : Color.FromRgb(220, 38, 38));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
