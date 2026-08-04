namespace SchoolManagement.Application.ParentActivation;

public static class ActivationTokenConstants
{
    public const string TokenTypeClaim = "typ";
    public const string TokenTypeValue = "parent_activation";
    public const string DeepLinkScheme = "erp-scolaire";
    public const int DefaultValidityMinutes = 15;
    public const int SessionTtlMinutes = 15;
}
