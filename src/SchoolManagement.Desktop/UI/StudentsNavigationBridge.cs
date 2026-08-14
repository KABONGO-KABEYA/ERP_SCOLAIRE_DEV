namespace SchoolManagement.Desktop.UI;

/// <summary>Préréglages de navigation vers la liste élèves (ex. depuis le dashboard).</summary>
public sealed record StudentsListPreset(
    bool OpenFiltersExpanded = true,
    bool IncludeInscritsOnly = true,
    bool AutoLoadCurrentYear = true);

public static class StudentsNavigationBridge
{
    private static StudentsListPreset? _pendingPreset;

    public static void RequestFromDashboard(StudentsListPreset? preset = null)
    {
        _pendingPreset = preset ?? new StudentsListPreset();
    }

    public static StudentsListPreset? ConsumePreset()
    {
        var preset = _pendingPreset;
        _pendingPreset = null;
        return preset;
    }
}
