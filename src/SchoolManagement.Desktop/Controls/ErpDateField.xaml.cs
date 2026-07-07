using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SchoolManagement.Desktop.Controls;

public partial class ErpDateField : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ErpDateField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty IsRequiredProperty =
        DependencyProperty.Register(nameof(IsRequired), typeof(bool), typeof(ErpDateField), new PropertyMetadata(false));
    public static readonly DependencyProperty FieldWidthProperty =
        DependencyProperty.Register(nameof(FieldWidth), typeof(double), typeof(ErpDateField), new PropertyMetadata(220d));
    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(ErpDateField), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty ShowValidationProperty =
        DependencyProperty.Register(nameof(ShowValidation), typeof(bool), typeof(ErpDateField), new PropertyMetadata(false));
    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime?), typeof(ErpDateField),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateChanged));
    public static readonly DependencyProperty AgeProperty =
        DependencyProperty.Register(nameof(Age), typeof(int), typeof(ErpDateField), new PropertyMetadata(0));
    public static readonly DependencyProperty AgeCategoryProperty =
        DependencyProperty.Register(nameof(AgeCategory), typeof(string), typeof(ErpDateField), new PropertyMetadata("—"));
    public static readonly DependencyProperty CompatibilityMessageProperty =
        DependencyProperty.Register(nameof(CompatibilityMessage), typeof(string), typeof(ErpDateField),
            new PropertyMetadata(string.Empty, OnCompatibilityChanged));
    public static readonly DependencyProperty IsCompatibilityOkProperty =
        DependencyProperty.Register(nameof(IsCompatibilityOk), typeof(bool), typeof(ErpDateField),
            new PropertyMetadata(true, OnCompatibilityChanged));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    public double FieldWidth { get => (double)GetValue(FieldWidthProperty); set => SetValue(FieldWidthProperty, value); }
    public string ErrorMessage { get => (string)GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public bool ShowValidation { get => (bool)GetValue(ShowValidationProperty); set => SetValue(ShowValidationProperty, value); }
    public DateTime? SelectedDate { get => (DateTime?)GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    public int Age { get => (int)GetValue(AgeProperty); set => SetValue(AgeProperty, value); }
    public string AgeCategory { get => (string)GetValue(AgeCategoryProperty); set => SetValue(AgeCategoryProperty, value); }
    public string CompatibilityMessage { get => (string)GetValue(CompatibilityMessageProperty); set => SetValue(CompatibilityMessageProperty, value); }
    public bool IsCompatibilityOk { get => (bool)GetValue(IsCompatibilityOkProperty); set => SetValue(IsCompatibilityOkProperty, value); }

    public ErpDateField()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateDerivedValues();
    }

    private static void OnDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpDateField field)
        {
            field.ShowValidation = true;
            field.UpdateDerivedValues();
        }
    }

    private static void OnCompatibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpDateField field)
        {
            field.UpdateCompatibilityBadge();
        }
    }

    private void UpdateDerivedValues()
    {
        if (SelectedDate is null)
        {
            Age = 0;
            AgeCategory = "—";
            AgeBadge.Visibility = Visibility.Collapsed;
            CategoryBadge.Visibility = Visibility.Collapsed;
            RefreshValidation();
            return;
        }

        var birth = DateOnly.FromDateTime(SelectedDate.Value);
        Age = CalculateAge(birth, DateOnly.FromDateTime(DateTime.Today));
        AgeCategory = Age < 18 ? "Mineur" : "Majeur";
        AgeBadge.Visibility = Visibility.Visible;
        CategoryBadge.Visibility = Visibility.Visible;
        UpdateCompatibilityBadge();
        ApplyVisualState("Valid");
        RefreshValidation();
    }

    private void UpdateCompatibilityBadge()
    {
        if (string.IsNullOrWhiteSpace(CompatibilityMessage))
        {
            CompatibilityBadge.Visibility = Visibility.Collapsed;
            return;
        }

        CompatibilityBadge.Visibility = Visibility.Visible;
        CompatibilityBadge.Style = IsCompatibilityOk
            ? (Style)FindResource("ErpBadgeInfo")
            : (Style)FindResource("ErpBadgeWarning");
        CompatibilityText.Style = IsCompatibilityOk
            ? (Style)FindResource("ErpBadgeInfoText")
            : (Style)FindResource("ErpBadgeWarningText");
        CompatibilityText.Text = CompatibilityMessage;
    }

    private void RefreshValidation()
    {
        if (!IsLoaded)
        {
            return;
        }

        if (IsRequired && SelectedDate is null)
        {
            ApplyVisualState(ShowValidation ? "Error" : "Normal");
            ErrorMessage = ShowValidation ? "La date de naissance est obligatoire." : string.Empty;
            return;
        }

        if (SelectedDate >= DateTime.Today)
        {
            ApplyVisualState("Error");
            ErrorMessage = "La date de naissance doit être dans le passé.";
            return;
        }

        ErrorMessage = string.Empty;
        ApplyVisualState(SelectedDate.HasValue ? "Valid" : "Normal");
    }

    private void ApplyVisualState(string state)
    {
        SuccessIcon.Visibility = state == "Valid" ? Visibility.Visible : Visibility.Collapsed;
        ErrorIcon.Visibility = state == "Error" ? Visibility.Visible : Visibility.Collapsed;
        ErrorText.Visibility = state == "Error" ? Visibility.Visible : Visibility.Collapsed;
        InputBorder.BorderBrush = state switch
        {
            "Error" => ErpFieldValidation.GetBrush("ErpInputErrorBrush"),
            "Valid" => ErpFieldValidation.GetBrush("ErpSuccessBrush"),
            _ => ErpFieldValidation.GetBrush("ErpInputBorderBrush")
        };
    }

    private static int CalculateAge(DateOnly birth, DateOnly reference)
    {
        var age = reference.Year - birth.Year;
        if (birth > reference.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
