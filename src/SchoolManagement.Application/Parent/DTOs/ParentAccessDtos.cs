namespace SchoolManagement.Application.Parent.DTOs;

/// <summary>
/// Identifiants d'accès application mobile créés (ou déjà existants) pour un tuteur.
/// </summary>
public sealed record ParentAppAccessCredentialDto(
    Guid GuardianId,
    string GuardianFullName,
    string UserName,
    string? TemporaryPassword,
    bool WasCreated,
    bool MustChangePassword);
