using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SchoolManagement.Desktop.UI;

public sealed class StringMatchVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var current = value as string;
        var expected = parameter as string;
        return string.Equals(current, expected, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
