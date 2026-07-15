using System.Windows;

namespace SchoolManagement.Desktop.Controls;

internal static class ErpFieldLayout
{
    public const double MinFieldWidth = 72;

    public static void ApplyResponsiveWidth(FrameworkElement control, double fieldWidth)
    {
        if (control.HorizontalAlignment == HorizontalAlignment.Stretch)
        {
            control.MinWidth = MinFieldWidth;
            control.ClearValue(FrameworkElement.WidthProperty);
            return;
        }

        var width = fieldWidth > 0 ? fieldWidth : 260;
        control.MinWidth = width;
        control.Width = width;
    }
}
