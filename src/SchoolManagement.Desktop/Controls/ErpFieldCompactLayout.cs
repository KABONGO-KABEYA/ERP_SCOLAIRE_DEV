using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

internal static class ErpFieldCompactLayout
{
    public const double CompactInputHeight = 34;
    public const double CompactFontSize = 12;
    public static readonly Thickness CompactLabelSpacing = new(0, 0, 0, 3);

    public static void Apply(
        FrameworkElement labelPanel,
        TextBlock? label,
        Border inputBorder,
        Control? input,
        bool isCompact)
    {
        if (isCompact)
        {
            labelPanel.Margin = CompactLabelSpacing;
            if (label is not null)
            {
                label.FontSize = CompactFontSize;
            }

            inputBorder.Height = CompactInputHeight;
            if (input is not null)
            {
                input.Height = CompactInputHeight;
                input.FontSize = CompactFontSize;
            }
        }
        else
        {
            labelPanel.ClearValue(FrameworkElement.MarginProperty);
            if (label is not null)
            {
                label.ClearValue(TextBlock.FontSizeProperty);
            }

            inputBorder.ClearValue(FrameworkElement.HeightProperty);
            if (input is not null)
            {
                input.ClearValue(FrameworkElement.HeightProperty);
                input.ClearValue(Control.FontSizeProperty);
            }
        }
    }
}
