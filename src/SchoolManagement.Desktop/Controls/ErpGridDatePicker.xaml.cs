using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SchoolManagement.Desktop.Controls;

public partial class ErpGridDatePicker
{
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

    public ErpGridDatePicker()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            SyncCalendarFromSelectedDate();
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
    }

    private void SyncCalendarFromSelectedDate()
    {
        CalendarControl.SelectedDate = SelectedDate?.Date;
        CalendarControl.DisplayDate = SelectedDate?.Date ?? DateTime.Today;
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
        HostBorder.Opacity = IsReadOnly ? 0.75 : 1;
    }
}
