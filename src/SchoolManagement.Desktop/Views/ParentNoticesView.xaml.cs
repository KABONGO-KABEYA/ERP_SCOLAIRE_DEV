using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class ParentNoticesView : UserControl
{
    private double _inlineActionsDesiredWidth;

    public ParentNoticesView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is ParentNoticesViewModel vm)
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(vm.Fields);
                if (view.GroupDescriptions.Count == 0)
                    view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(ParentNoticeField.Group)));
            }

            UpdateActionPresentation();
        };
    }

    private void EditorHeader_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded)
            UpdateActionPresentation();
    }

    private void UpdateActionPresentation()
    {
        if (_inlineActionsDesiredWidth <= 0)
        {
            InlineActionButtons.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _inlineActionsDesiredWidth = InlineActionButtons.DesiredSize.Width;
        }

        LeftPanelToggle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        EditorSectionTitle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        RightPanelToggle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        const double breathingRoom = 36;
        var requiredWidth = LeftPanelToggle.DesiredSize.Width
                            + EditorSectionTitle.DesiredSize.Width
                            + _inlineActionsDesiredWidth
                            + RightPanelToggle.DesiredSize.Width
                            + breathingRoom;
        var showInlineActions = EditorHeader.ActualWidth >= requiredWidth;

        InlineActionButtons.Visibility = showInlineActions ? Visibility.Visible : Visibility.Collapsed;
        CompactActionsMenu.Visibility = showInlineActions ? Visibility.Collapsed : Visibility.Visible;
    }

    private void InsertField_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is not ParentNoticeField field) return;
        Editor.Focus();
        Editor.Selection.Text = field.Token;
        Editor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(219, 234, 254)));
        Editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.SemiBold);
    }

    private void AlignLeft_Click(object sender, RoutedEventArgs e) => ApplyAlignment(TextAlignment.Left);
    private void AlignCenter_Click(object sender, RoutedEventArgs e) => ApplyAlignment(TextAlignment.Center);
    private void AlignRight_Click(object sender, RoutedEventArgs e) => ApplyAlignment(TextAlignment.Right);

    private void ApplyAlignment(TextAlignment alignment)
    {
        Editor.Selection.ApplyPropertyValue(Paragraph.TextAlignmentProperty, alignment);
        Editor.Focus();
    }

    private void FontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || sender is not ComboBox { SelectedItem: ComboBoxItem item }
            || !double.TryParse(item.Content?.ToString(), out var size)) return;
        Editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
        Editor.Focus();
    }

    private void FontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || sender is not ComboBox { SelectedItem: ComboBoxItem item }) return;
        var familyName = item.Content?.ToString();
        if (string.IsNullOrWhiteSpace(familyName)) return;
        Editor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(familyName));
        Editor.Focus();
    }

    private void FontColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || sender is not ComboBox { SelectedItem: ComboBoxItem item }) return;
        var colorValue = item.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(colorValue)) return;
        if (new BrushConverter().ConvertFromString(colorValue) is not Brush brush) return;
        Editor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
        Editor.Focus();
    }
}
