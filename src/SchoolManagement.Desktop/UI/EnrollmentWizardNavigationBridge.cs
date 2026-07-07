namespace SchoolManagement.Desktop.UI;

public enum EnrollmentWizardEntryMode
{
    NouvelleInscription,
    Reinscription
}

public static class EnrollmentWizardNavigationBridge
{
    private static EnrollmentWizardEntryMode? _pendingMode;

    public static void Request(EnrollmentWizardEntryMode mode) => _pendingMode = mode;

    public static EnrollmentWizardEntryMode ConsumeMode()
    {
        var mode = _pendingMode ?? EnrollmentWizardEntryMode.NouvelleInscription;
        _pendingMode = null;
        return mode;
    }
}
