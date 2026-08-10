namespace SchoolManagement.Application.ParentActivation;

public static class ActivationTokenConstants
{
    /// <summary>Claim historique parent (<c>typ</c>).</summary>
    public const string TokenTypeClaim = "typ";

    /// <summary>Claim aligné contrat établissement / Bootstrap (<c>token_type</c>).</summary>
    public const string TokenTypeClaimModern = "token_type";

    public const string TokenTypeValue = "parent_activation";
    public const string DeepLinkScheme = "erp-scolaire";
    public const int DefaultValidityMinutes = 15;
    public const int SessionTtlMinutes = 15;
}
