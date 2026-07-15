using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public class ViewLocator : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        var viewModelType = value.GetType();
        Type? viewType;

        if (viewModelType == typeof(EnrollmentWizardViewModel))
        {
            viewType = typeof(InscriptionEleveV2View);
        }
        else
        {
            var viewName = viewModelType.FullName!
                .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
                .Replace("ViewModel", "View", StringComparison.Ordinal);
            viewType = viewModelType.Assembly.GetType(viewName);
        }

        if (viewType is null)
        {
            return new TextBlock
            {
                Text = $"Vue non implémentée : {viewModelType.Name}",
                Margin = new Thickness(24),
                FontSize = 16
            };
        }

        var view = (FrameworkElement)Activator.CreateInstance(viewType)!;
        view.DataContext = value;
        return view;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
