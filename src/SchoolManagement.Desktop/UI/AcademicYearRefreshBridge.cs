namespace SchoolManagement.Desktop.UI;

public static class AcademicYearRefreshBridge
{
    public static event Action? CurrentYearChanged;

    public static void NotifyCurrentYearChanged() => CurrentYearChanged?.Invoke();
}
