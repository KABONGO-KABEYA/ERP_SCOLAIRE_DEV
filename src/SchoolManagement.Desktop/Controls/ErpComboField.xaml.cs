using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

public partial class ErpComboField : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ErpComboField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty IsRequiredProperty =
        DependencyProperty.Register(nameof(IsRequired), typeof(bool), typeof(ErpComboField), new PropertyMetadata(false, OnChanged));
    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(nameof(IconKind), typeof(MaterialDesignThemes.Wpf.PackIconKind), typeof(ErpComboField),
            new PropertyMetadata(MaterialDesignThemes.Wpf.PackIconKind.None));
    public static readonly DependencyProperty FieldWidthProperty =
        DependencyProperty.Register(nameof(FieldWidth), typeof(double), typeof(ErpComboField),
            new PropertyMetadata(260d, OnLayoutPropertyChanged));
    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(ErpComboField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ShowValidationProperty =
        DependencyProperty.Register(nameof(ShowValidation), typeof(bool), typeof(ErpComboField), new PropertyMetadata(false, OnChanged));
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ErpComboField), new PropertyMetadata(null));
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(ErpComboField),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnChanged));
    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(ErpComboField),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnChanged));
    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(ErpComboField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SelectedValuePathProperty =
        DependencyProperty.Register(nameof(SelectedValuePath), typeof(string), typeof(ErpComboField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RefreshOnDropDownOpenProperty =
        DependencyProperty.Register(nameof(RefreshOnDropDownOpen), typeof(bool), typeof(ErpComboField), new PropertyMetadata(false));

    public static readonly DependencyProperty MaxDropDownHeightProperty =
        DependencyProperty.Register(nameof(MaxDropDownHeight), typeof(double), typeof(ErpComboField),
            new PropertyMetadata(double.NaN, OnMaxDropDownHeightChanged));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(ErpComboField),
            new PropertyMetadata(false, OnCompactChanged));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    public MaterialDesignThemes.Wpf.PackIconKind IconKind { get => (MaterialDesignThemes.Wpf.PackIconKind)GetValue(IconKindProperty); set => SetValue(IconKindProperty, value); }
    public double FieldWidth { get => (double)GetValue(FieldWidthProperty); set => SetValue(FieldWidthProperty, value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public bool ShowValidation { get => (bool)GetValue(ShowValidationProperty); set => SetValue(ShowValidationProperty, value); }
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public object? SelectedValue { get => GetValue(SelectedValueProperty); set => SetValue(SelectedValueProperty, value); }
    public string DisplayMemberPath { get => (string)GetValue(DisplayMemberPathProperty); set => SetValue(DisplayMemberPathProperty, value); }
    public string SelectedValuePath { get => (string)GetValue(SelectedValuePathProperty); set => SetValue(SelectedValuePathProperty, value); }
    public bool RefreshOnDropDownOpen { get => (bool)GetValue(RefreshOnDropDownOpenProperty); set => SetValue(RefreshOnDropDownOpenProperty, value); }
    public double MaxDropDownHeight { get => (double)GetValue(MaxDropDownHeightProperty); set => SetValue(MaxDropDownHeightProperty, value); }
    public bool IsCompact { get => (bool)GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    public event EventHandler? DropDownOpened;
    public event Func<EventArgs, Task>? PreparingDropDownAsync;

    public ErpComboField()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateLayoutSizing();
            ApplyMaxDropDownHeight();
            ApplyCompactLayout();
            RefreshValidation();
        };
    }

    private static void OnCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpComboField field)
        {
            field.ApplyCompactLayout();
        }
    }

    private void ApplyCompactLayout()
    {
        if (!IsLoaded)
        {
            return;
        }

        ErpFieldCompactLayout.Apply(LabelPanel, LabelText, InputBorder, InputComboBox, IsCompact);
    }

    private static void OnMaxDropDownHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpComboField field)
        {
            field.ApplyMaxDropDownHeight();
        }
    }

    private void ApplyMaxDropDownHeight()
    {
        if (!IsLoaded)
        {
            return;
        }

        if (double.IsNaN(MaxDropDownHeight) || MaxDropDownHeight <= 0)
        {
            InputComboBox.ClearValue(ComboBox.MaxDropDownHeightProperty);
            return;
        }

        InputComboBox.MaxDropDownHeight = MaxDropDownHeight;
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == HorizontalAlignmentProperty)
        {
            UpdateLayoutSizing();
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpComboField field)
        {
            field.UpdateLayoutSizing();
        }
    }

    private void UpdateLayoutSizing()
    {
        ErpFieldLayout.ApplyResponsiveWidth(this, FieldWidth);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpComboField field)
        {
            field.RefreshValidation();
        }
    }

    private void RefreshValidation()
    {
        if (!IsLoaded)
        {
            return;
        }

        var hasValue = SelectedItem != null || SelectedValue != null;
        if (IsRequired && !hasValue && ShowValidation)
        {
            ApplyVisualState("Error");
            ErrorMessage = $"Sélectionnez une valeur pour « {Label} ».";
            return;
        }

        ApplyVisualState(hasValue ? "Valid" : "Normal");
        ErrorMessage = string.Empty;
    }

    private void ApplyVisualState(string state)
    {
        SuccessIcon.Visibility = state == "Valid" ? Visibility.Visible : Visibility.Collapsed;
        ErrorIcon.Visibility = state == "Error" ? Visibility.Visible : Visibility.Collapsed;
        ErrorText.Visibility = state == "Error" ? Visibility.Visible : Visibility.Collapsed;
        InputBorder.BorderBrush = state == "Error"
            ? ErpFieldValidation.GetBrush("ErpInputErrorBrush")
            : state == "Valid"
                ? ErpFieldValidation.GetBrush("ErpSuccessBrush")
                : ErpFieldValidation.GetBrush("ErpInputBorderBrush");
    }

    private void InputComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowValidation = true;
        RefreshValidation();
    }

    private void InputComboBox_OnDropDownOpened(object sender, EventArgs e)
    {
        DropDownOpened?.Invoke(this, EventArgs.Empty);
    }

    private async void InputComboBox_OnPreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (PreparingDropDownAsync is null)
        {
            return;
        }

        e.Handled = true;
        try
        {
            await PreparingDropDownAsync(EventArgs.Empty);
            InputComboBox.IsDropDownOpen = true;
        }
        catch
        {
            InputComboBox.IsDropDownOpen = false;
        }
    }
}
