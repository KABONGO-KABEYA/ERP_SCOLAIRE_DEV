namespace SchoolManagement.Application.Security;

using SchoolManagement.Application.Security.DTOs;

/// <summary>Résultat du calcul des permissions effectives (codes uniquement pour JWT).</summary>
public sealed record EffectivePermissionResult(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> PermissionCodes,
    bool IsPlatformSuperAdmin);

public interface ISecurityCatalogCache
{
    /// <summary>
    /// Invalide le cache catalogue (deps + permissions actives).
    /// Préférer l’invalidation automatique via l’interceptor EF ;
    /// n’appeler manuellement que pour des chemins hors ChangeTracker (SQL brut, etc.).
    /// </summary>
    void Invalidate();
}

public interface IPermissionDependencyService
{
    Task<IReadOnlySet<string>> GetRequiredClosureAsync(string permissionCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetPrerequisiteMapAsync(CancellationToken cancellationToken = default);
}

public interface IEffectivePermissionService
{
    Task<EffectivePermissionResult> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aperçu admin : permissions pertinentes avec origines (Role / Grant / Deny / Dependency).
    /// </summary>
    Task<EffectivePermissionExplanationDto> ExplainAsync(Guid userId, CancellationToken cancellationToken = default);
}
