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
        DependencyProperty.Register(nameof(FieldWidth), typeof(double), typeof(ErpDateField),
            new PropertyMetadata(220d, OnLayoutPropertyChanged));
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
    public static readonly DependencyProperty ShowBadgesProperty =
        DependencyProperty.Register(nameof(ShowBadges), typeof(bool), typeof(ErpDateField), new PropertyMetadata(true, OnShowBadgesChanged));
    public static readonly DependencyProperty DateValidationModeProperty =
        DependencyProperty.Register(nameof(DateValidationMode), typeof(ErpDateValidationMode), typeof(ErpDateField),
            new PropertyMetadata(ErpDateValidationMode.BirthDate, OnValidationModeChanged));

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
    public bool ShowBadges { get => (bool)GetValue(ShowBadgesProperty); set => SetValue(ShowBadgesProperty, value); }
    public ErpDateValidationMode DateValidationMode
    {
        get => (ErpDateValidationMode)GetValue(DateValidationModeProperty);
        set => SetValue(DateValidationModeProperty, value);
    }

    public ErpDateField()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateLayoutSizing();
            UpdateDerivedValues();
            InputDatePicker.IsReadOnly = !IsEnabled;
        };
        IsEnabledChanged += (_, _) => InputDatePicker.IsReadOnly = !IsEnabled;
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
        if (d is ErpDateField field)
        {
            field.UpdateLayoutSizing();
        }
    }

    private void UpdateLayoutSizing()
    {
        ErpFieldLayout.ApplyResponsiveWidth(this, FieldWidth);
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

    private static void OnShowBadgesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpDateField field)
        {
            field.UpdateBadgeVisibility();
        }
    }

    private static void OnValidationModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpDateField field)
        {
            field.UpdateDerivedValues();
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
            UpdateBadgeVisibility();
            RefreshValidation();
            return;
        }

        if (DateValidationMode == ErpDateValidationMode.BirthDate)
        {
            var birth = DateOnly.FromDateTime(SelectedDate.Value);
            Age = CalculateAge(birth, DateOnly.FromDateTime(DateTime.Today));
            AgeCategory = Age < 18 ? "Mineur" : "Majeur";
        }
        else
        {
            Age = 0;
            AgeCategory = "—";
            AgeBadge.Visibility = Visibility.Collapsed;
            CategoryBadge.Visibility = Visibility.Collapsed;
        }

        UpdateBadgeVisibility();
        UpdateCompatibilityBadge();
        RefreshValidation();
    }

    private void UpdateCompatibilityBadge()
    {
        if (!ShowBadges)
        {
            CompatibilityBadge.Visibility = Visibility.Collapsed;
            return;
        }

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

    private void UpdateBadgeVisibility()
    {
        if (!ShowBadges || SelectedDate is null)
        {
            AgeBadge.Visibility = Visibility.Collapsed;
            CategoryBadge.Visibility = Visibility.Collapsed;
            if (!ShowBadges)
            {
                CompatibilityBadge.Visibility = Visibility.Collapsed;
            }

            return;
        }

        AgeBadge.Visibility = Visibility.Visible;
        CategoryBadge.Visibility = Visibility.Visible;
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
            ErrorMessage = ShowValidation
                ? DateValidationMode switch
                {
                    ErpDateValidationMode.EnrollmentDate => "La date d'inscription est obligatoire.",
                    ErpDateValidationMode.BirthDate => "La date de naissance est obligatoire.",
                    _ => $"Le champ « {Label} » est obligatoire."
                }
                : string.Empty;
            return;
        }

        if (SelectedDate is not null)
        {
            if (DateValidationMode == ErpDateValidationMode.BirthDate && SelectedDate >= DateTime.Today)
            {
                ApplyVisualState("Error");
                ErrorMessage = "La date de naissance doit être dans le passé.";
                return;
            }

            if (DateValidationMode == ErpDateValidationMode.EnrollmentDate && SelectedDate.Value.Date > DateTime.Today)
            {
                ApplyVisualState("Error");
                ErrorMessage = "La date d'inscription ne peut pas être dans le futur.";
                return;
            }
        }

        ErrorMessage = string.Empty;
        ApplyVisualState(SelectedDate.HasValue ? "Valid" : "Normal");
    }

    private void ApplyVisualState(string state)
    {
        SuccessIcon.Visibility = state == "Valid" ? Visibility.Visible : Visibility.Collapsed;
        ErrorIcon.Visibility = state == "Error" ? Visibility.Visible : Visibility.Collapsed;
        ErrorText.Visibility = state == "Error" ? Visibility.Visible : Visibility.Collapsed;
        InputDatePicker.ApplyBorderBrush(state switch
        {
            "Error" => ErpFieldValidation.GetBrush("ErpInputErrorBrush"),
            "Valid" => ErpFieldValidation.GetBrush("ErpSuccessBrush"),
            _ => ErpFieldValidation.GetBrush("ErpInputBorderBrush")
        });
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
