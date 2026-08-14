using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchoolManagement.Desktop.Controls;

public partial class ErpGridDatePicker
{
    private static readonly string[] AcceptedFormats =
    [
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd/M/yyyy",
        "d/MM/yyyy"
    ];

    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(
            nameof(SelectedDate),
            typeof(DateTime?),
            typeof(ErpGridDatePicker),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

    public static readonly DependencyProperty DisplayDateStartProperty =
        DependencyProperty.Register(
            nameof(DisplayDateStart),
            typeof(DateTime?),
            typeof(ErpGridDatePicker),
            new PropertyMetadata(null, OnRangeChanged));

    public static readonly DependencyProperty DisplayDateEndProperty =
        DependencyProperty.Register(
            nameof(DisplayDateEnd),
            typeof(DateTime?),
            typeof(ErpGridDatePicker),
            new PropertyMetadata(null, OnRangeChanged));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(ErpGridDatePicker),
            new PropertyMetadata(false, OnIsReadOnlyChanged));

    private bool _syncingText;

    public ErpGridDatePicker()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            SyncCalendarFromSelectedDate();
            SyncTextFromSelectedDate();
            ApplyReadOnlyState();
        };
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public DateTime? DisplayDateStart
    {
        get => (DateTime?)GetValue(DisplayDateStartProperty);
        set => SetValue(DisplayDateStartProperty, value);
    }

    public DateTime? DisplayDateEnd
    {
        get => (DateTime?)GetValue(DisplayDateEndProperty);
        set => SetValue(DisplayDateEndProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpGridDatePicker picker)
        {
            picker.SyncCalendarFromSelectedDate();
            picker.SyncTextFromSelectedDate();
        }
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpGridDatePicker picker)
        {
            picker.ApplyCalendarConstraints();
        }
    }

    private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpGridDatePicker picker)
        {
            picker.ApplyReadOnlyState();
        }
    }

    private void OpenCalendarButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (IsReadOnly)
        {
            return;
        }

        // Valide d'abord une saisie clavier en cours.
        TryCommitTypedDate(revertOnInvalid: true);

        ApplyCalendarConstraints();
        SyncCalendarFromSelectedDate();
        CalendarPopup.IsOpen = !CalendarPopup.IsOpen;
    }

    private void CalendarControl_OnSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CalendarControl.SelectedDate is not DateTime selected)
        {
            return;
        }

        if (!IsDateAllowed(selected))
        {
            SyncCalendarFromSelectedDate();
            return;
        }

        SelectedDate = selected.Date;
        CalendarPopup.IsOpen = false;
        SyncTextFromSelectedDate();
    }

    private void DateTextBox_OnLostFocus(object sender, RoutedEventArgs e) =>
        TryCommitTypedDate(revertOnInvalid: true);

    private void DateTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryCommitTypedDate(revertOnInvalid: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SyncTextFromSelectedDate();
            e.Handled = true;
        }
    }

    private void DateTextBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (IsReadOnly)
        {
            return;
        }

        // Facilite la correction d'une date existante.
        DateTextBox.SelectAll();
    }

    /// <summary>
    /// Interprète le texte saisi au format <c>dd/MM/yyyy</c>.
    /// En cas d'entrée invalide : pas d'exception — restauration de l'affichage précédent.
    /// </summary>
    private void TryCommitTypedDate(bool revertOnInvalid)
    {
        if (IsReadOnly || _syncingText)
        {
            return;
        }

        var text = (DateTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text) || text == "—")
        {
            if (SelectedDate is not null)
            {
                SelectedDate = null;
            }

            SyncTextFromSelectedDate();
            return;
        }

        if (DateTime.TryParseExact(
                text,
                AcceptedFormats,
                Fr,
                DateTimeStyles.None,
                out var parsed)
            && IsDateAllowed(parsed))
        {
            var normalized = parsed.Date;
            if (SelectedDate?.Date != normalized)
            {
                SelectedDate = normalized;
            }
            else
            {
                SyncTextFromSelectedDate();
            }

            return;
        }

        if (revertOnInvalid)
        {
            SyncTextFromSelectedDate();
        }
    }

    private void SyncCalendarFromSelectedDate()
    {
        CalendarControl.SelectedDate = SelectedDate?.Date;
        CalendarControl.DisplayDate = SelectedDate?.Date ?? DateTime.Today;
    }

    private void SyncTextFromSelectedDate()
    {
        if (!IsLoaded)
        {
            return;
        }

        _syncingText = true;
        try
        {
            DateTextBox.Text = SelectedDate is DateTime d
                ? d.ToString("dd/MM/yyyy", Fr)
                : string.Empty;
        }
        finally
        {
            _syncingText = false;
        }
    }

    private void ApplyCalendarConstraints()
    {
        CalendarControl.BlackoutDates.Clear();

        var min = DisplayDateStart?.Date;
        var max = DisplayDateEnd?.Date;

        if (min is not null && max is not null && min > max)
        {
            return;
        }

        if (min is not null)
        {
            CalendarControl.BlackoutDates.Add(new CalendarDateRange(DateTime.MinValue, min.Value.AddDays(-1)));
        }

        if (max is not null)
        {
            CalendarControl.BlackoutDates.Add(new CalendarDateRange(max.Value.AddDays(1), DateTime.MaxValue));
        }
    }

    private bool IsDateAllowed(DateTime date)
    {
        var value = date.Date;
        var min = DisplayDateStart?.Date;
        var max = DisplayDateEnd?.Date;

        if (min is not null && max is not null && min > max)
        {
            return true;
        }

        if (min is not null && value < min)
        {
            return false;
        }

        if (max is not null && value > max)
        {
            return false;
        }

        return true;
    }

    private void ApplyReadOnlyState()
    {
        OpenCalendarButton.IsEnabled = !IsReadOnly;
        DateTextBox.IsReadOnly = IsReadOnly;
        DateTextBox.IsHitTestVisible = !IsReadOnly;
        HostBorder.Opacity = IsReadOnly ? 0.75 : 1;
    }

    public void ApplyBorderBrush(System.Windows.Media.Brush brush)
    {
        HostBorder.BorderBrush = brush;
    }
}
