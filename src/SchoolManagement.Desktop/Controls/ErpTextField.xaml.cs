using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SchoolManagement.Desktop.Controls;

public partial class ErpTextField : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ErpTextField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsRequiredProperty =
        DependencyProperty.Register(nameof(IsRequired), typeof(bool), typeof(ErpTextField),
            new PropertyMetadata(false, OnValidationPropertyChanged));

    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(nameof(IconKind), typeof(MaterialDesignThemes.Wpf.PackIconKind), typeof(ErpTextField),
            new PropertyMetadata(MaterialDesignThemes.Wpf.PackIconKind.None));

    public static readonly DependencyProperty FieldWidthProperty =
        DependencyProperty.Register(nameof(FieldWidth), typeof(double), typeof(ErpTextField),
            new PropertyMetadata(260d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(ErpTextField),
            new PropertyMetadata(false, OnValidationPropertyChanged));

    public static readonly DependencyProperty IsEnabledFieldProperty =
        DependencyProperty.Register(nameof(IsEnabledField), typeof(bool), typeof(ErpTextField),
            new PropertyMetadata(true, OnValidationPropertyChanged));

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(ErpTextField),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsValidProperty =
        DependencyProperty.Register(nameof(IsValid), typeof(bool), typeof(ErpTextField), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowValidationProperty =
        DependencyProperty.Register(nameof(ShowValidation), typeof(bool), typeof(ErpTextField),
            new PropertyMetadata(false, OnValidationPropertyChanged));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(ErpTextField),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public static readonly DependencyProperty ValidationModeProperty =
        DependencyProperty.Register(nameof(ValidationMode), typeof(ErpFieldValidationMode), typeof(ErpTextField),
            new PropertyMetadata(ErpFieldValidationMode.None, OnValidationPropertyChanged));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    public MaterialDesignThemes.Wpf.PackIconKind IconKind { get => (MaterialDesignThemes.Wpf.PackIconKind)GetValue(IconKindProperty); set => SetValue(IconKindProperty, value); }
    public double FieldWidth { get => (double)GetValue(FieldWidthProperty); set => SetValue(FieldWidthProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool IsEnabledField { get => (bool)GetValue(IsEnabledFieldProperty); set => SetValue(IsEnabledFieldProperty, value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public bool IsValid { get => (bool)GetValue(IsValidProperty); private set => SetValue(IsValidProperty, value); }
    public bool ShowValidation { get => (bool)GetValue(ShowValidationProperty); set => SetValue(ShowValidationProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public ErpFieldValidationMode ValidationMode { get => (ErpFieldValidationMode)GetValue(ValidationModeProperty); set => SetValue(ValidationModeProperty, value); }

    public ErpTextField()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateLayoutSizing();
            RefreshValidation();
        };
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
        if (d is ErpTextField field)
        {
            field.UpdateLayoutSizing();
        }
    }

    private void UpdateLayoutSizing()
    {
        ErpFieldLayout.ApplyResponsiveWidth(this, FieldWidth);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpTextField field)
        {
            field.RefreshValidation();
        }
    }

    private static void OnValidationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpTextField field)
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

        if (!IsEnabledField)
        {
            ApplyVisualState("Disabled");
            IsValid = true;
            return;
        }

        if (IsReadOnly)
        {
            ApplyVisualState("ReadOnly");
            IsValid = true;
            return;
        }

        var value = Text?.Trim() ?? string.Empty;
        var shouldValidate = ShowValidation || !string.IsNullOrEmpty(value);

        if (!shouldValidate && !IsRequired)
        {
            ApplyVisualState("Normal");
            IsValid = true;
            return;
        }

        if (!ErpFieldValidation.ValidateRequired(value, IsRequired, ShowValidation, Label, out var reqError))
        {
            ApplyVisualState("Error");
            ErrorMessage = reqError ?? string.Empty;
            IsValid = false;
            return;
        }

        if (!ErpFieldValidation.ValidateEmail(value, ValidationMode, out var emailError))
        {
            ApplyVisualState("Error");
            ErrorMessage = emailError ?? string.Empty;
            IsValid = false;
            return;
        }

        if (!ErpFieldValidation.ValidatePhone(value, ValidationMode, out var phoneError))
        {
            ApplyVisualState("Error");
            ErrorMessage = phoneError ?? string.Empty;
            IsValid = false;
            return;
        }

        ApplyVisualState(string.IsNullOrWhiteSpace(value) ? "Normal" : "Valid");
        ErrorMessage = string.Empty;
        IsValid = true;
    }

    private void ApplyVisualState(string state)
    {
        SuccessIcon.Visibility = state == "Valid" ? Visibility.Visible : Visibility.Collapsed;
        ErrorIcon.Visibility = state == "Error" ? Visibility.Visible : Visibility.Collapsed;
        ErrorText.Visibility = state == "Error" && !string.IsNullOrWhiteSpace(ErrorMessage)
            ? Visibility.Visible
            : Visibility.Collapsed;

        InputBorder.BorderBrush = state switch
        {
            "Error" => ErpFieldValidation.GetBrush("ErpInputErrorBrush"),
            "Valid" => ErpFieldValidation.GetBrush("ErpSuccessBrush"),
            _ => ErpFieldValidation.GetBrush("ErpInputBorderBrush")
        };

        InputBorder.Background = state switch
        {
            "ReadOnly" => ErpFieldValidation.GetBrush("ErpInputReadOnlyBrush"),
            "Disabled" => ErpFieldValidation.GetBrush("ErpInputDisabledBrush"),
            _ => Brushes.White
        };

        InputBorder.Opacity = state == "Disabled" ? 0.7 : 1;
        InputTextBox.IsEnabled = IsEnabledField && state != "ReadOnly";
        InputTextBox.IsReadOnly = IsReadOnly;
    }

    private void InputTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        ShowValidation = true;
        RefreshValidation();
    }
}
