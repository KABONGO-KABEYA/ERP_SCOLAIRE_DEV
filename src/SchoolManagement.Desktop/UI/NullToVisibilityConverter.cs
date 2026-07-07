using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SchoolManagement.Desktop.UI;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNullOrEmpty = value is null || (value is string text && string.IsNullOrWhiteSpace(text));
        return isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
