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
        DependencyProperty.Register(nameof(FieldWidth), typeof(double), typeof(ErpComboField), new PropertyMetadata(260d));
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

    public ErpComboField()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshValidation();
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
}
