using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

public partial class ErpStepper : UserControl
{
    public static readonly DependencyProperty StepsProperty =
        DependencyProperty.Register(nameof(Steps), typeof(IEnumerable), typeof(ErpStepper),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(nameof(Columns), typeof(int), typeof(ErpStepper),
            new PropertyMetadata(3));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(ErpStepper),
            new PropertyMetadata(false));

    public IEnumerable? Steps
    {
        get => (IEnumerable?)GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public ErpStepper()
    {
        InitializeComponent();
    }
}
