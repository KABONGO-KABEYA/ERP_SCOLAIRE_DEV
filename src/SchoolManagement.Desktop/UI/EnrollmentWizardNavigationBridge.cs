namespace SchoolManagement.Desktop.UI;

public enum EnrollmentWizardEntryMode
{
    NouvelleInscription,
    Reinscription,
    Modification
}

public static class EnrollmentWizardNavigationBridge
{
    private static EnrollmentWizardEntryMode? _pendingMode;
    private static Guid? _pendingModificationStudentId;

    public static void Request(EnrollmentWizardEntryMode mode)
    {
        _pendingMode = mode;
        if (mode != EnrollmentWizardEntryMode.Modification)
        {
            _pendingModificationStudentId = null;
        }
    }

    public static void RequestModification(Guid studentId)
    {
        _pendingMode = EnrollmentWizardEntryMode.Modification;
        _pendingModificationStudentId = studentId;
    }

    public static EnrollmentWizardEntryMode ConsumeMode()
    {
        var mode = _pendingMode ?? EnrollmentWizardEntryMode.NouvelleInscription;
        _pendingMode = null;
        return mode;
    }

    public static Guid? ConsumeModificationStudentId()
    {
        var id = _pendingModificationStudentId;
        _pendingModificationStudentId = null;
        return id;
    }
}
