using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.UI;

public sealed class WizardStepStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not WizardStepVisualState state)
        {
            return System.Windows.Application.Current.FindResource("ErpBorderBrush");
        }

        return state switch
        {
            WizardStepVisualState.Active => System.Windows.Application.Current.FindResource("ErpPrimaryBrush"),
            WizardStepVisualState.Completed => System.Windows.Application.Current.FindResource("ErpSuccessBrush"),
            _ => new SolidColorBrush(Color.FromRgb(229, 231, 235))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class WizardStepStateToForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is WizardStepVisualState state
            && (state == WizardStepVisualState.Active || state == WizardStepVisualState.Completed))
        {
            return Brushes.White;
        }

        return System.Windows.Application.Current.FindResource("ErpTextSecondaryBrush");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class WizardStepStateToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter?.ToString() == "check" && value is WizardStepVisualState.Completed)
        {
            return Visibility.Visible;
        }

        if (parameter?.ToString() == "number" && value is WizardStepVisualState state && state != WizardStepVisualState.Completed)
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is bool b && b;
        return visible ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var empty = value is not string text || string.IsNullOrWhiteSpace(text);
        var invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
        if (invert)
        {
            return empty ? Visibility.Visible : Visibility.Collapsed;
        }

        return empty ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PathToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var absolutePath = ResolveAbsolutePath(path);
        if (absolutePath is null)
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(absolutePath);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveAbsolutePath(string path)
    {
        if (Path.IsPathRooted(path) && File.Exists(path))
        {
            return path;
        }

        var resolver = App.Services?.GetService(typeof(Services.IStudentDossierPathResolver)) as Services.IStudentDossierPathResolver;
        return resolver?.ResolveAbsolutePath(path);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class AlertBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isWarning = value is bool b && b;
        return System.Windows.Application.Current.FindResource(isWarning ? "ErpWarningBrush" : "ErpSuccessBrush");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
