using System.Windows;
using System.Windows.Controls;

namespace SchoolManagement.Desktop.Controls;

public partial class ErpTextAreaField : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ErpTextAreaField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty IsRequiredProperty =
        DependencyProperty.Register(nameof(IsRequired), typeof(bool), typeof(ErpTextAreaField), new PropertyMetadata(false));
    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(nameof(IconKind), typeof(MaterialDesignThemes.Wpf.PackIconKind), typeof(ErpTextAreaField),
            new PropertyMetadata(MaterialDesignThemes.Wpf.PackIconKind.None));
    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(ErpTextAreaField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ShowValidationProperty =
        DependencyProperty.Register(nameof(ShowValidation), typeof(bool), typeof(ErpTextAreaField), new PropertyMetadata(false));
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(ErpTextAreaField),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty MinLinesProperty =
        DependencyProperty.Register(nameof(MinLines), typeof(int), typeof(ErpTextAreaField), new PropertyMetadata(3));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    public MaterialDesignThemes.Wpf.PackIconKind IconKind { get => (MaterialDesignThemes.Wpf.PackIconKind)GetValue(IconKindProperty); set => SetValue(IconKindProperty, value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public bool ShowValidation { get => (bool)GetValue(ShowValidationProperty); set => SetValue(ShowValidationProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public int MinLines { get => (int)GetValue(MinLinesProperty); set => SetValue(MinLinesProperty, value); }

    public ErpTextAreaField()
    {
        InitializeComponent();
    }

    private void RefreshValidation()
    {
        if (IsRequired && string.IsNullOrWhiteSpace(Text) && ShowValidation)
        {
            ErrorText.Visibility = Visibility.Visible;
            ErrorMessage = $"Le champ « {Label} » est obligatoire.";
            InputBorder.BorderBrush = ErpFieldValidation.GetBrush("ErpInputErrorBrush");
            return;
        }

        ErrorText.Visibility = Visibility.Collapsed;
        InputBorder.BorderBrush = ErpFieldValidation.GetBrush("ErpInputBorderBrush");
    }

    private void InputTextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        ShowValidation = true;
        RefreshValidation();
    }
}
