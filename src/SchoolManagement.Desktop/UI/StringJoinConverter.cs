using System.Globalization;
using System.Windows.Data;

namespace SchoolManagement.Desktop.UI;

public sealed class StringJoinConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        if (value is IEnumerable<string> strings)
            return string.Join(", ", strings);
        return value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
