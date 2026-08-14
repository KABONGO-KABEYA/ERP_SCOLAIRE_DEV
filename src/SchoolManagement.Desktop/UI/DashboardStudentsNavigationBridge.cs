namespace SchoolManagement.Desktop.UI;

public static class DashboardStudentsNavigationBridge
{
    private static Guid? _pendingStudentId;

    public static void RequestConsultation(Guid studentId) => _pendingStudentId = studentId;

    public static Guid? ConsumeConsultationStudentId()
    {
        var id = _pendingStudentId;
        _pendingStudentId = null;
        return id;
    }
}
