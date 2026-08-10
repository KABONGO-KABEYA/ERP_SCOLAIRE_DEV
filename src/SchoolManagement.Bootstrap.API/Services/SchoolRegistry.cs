using Microsoft.Extensions.Options;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;

namespace SchoolManagement.Bootstrap.API.Services;

/// <summary>
/// Résolution école pour ParentActivation relay — <b>SQL d'abord</b>, legacy env optionnel (Phase 8).
/// </summary>
public sealed class SchoolRegistry
{
    public static readonly Guid EcoleTestSchoolId =
        Guid.Parse("71635f62-b975-479d-9e6e-fbacd05e4996");

    private readonly IBootstrapSchoolRegistryRepository _repository;
    private readonly BootstrapOptions _options;

    public SchoolRegistry(
        IBootstrapSchoolRegistryRepository repository,
        IOptions<BootstrapOptions> options)
    {
        _repository = repository;
        _options = options.Value;
    }

    public async Task<SchoolRegistryEntryOptions> ResolveAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var sql = await _repository.GetBySchoolIdAsync(schoolId, cancellationToken);
        if (sql is not null && sql.IsActive)
        {
            return MapSql(sql);
        }

        if (_options.AllowLegacyEnvSchoolRegistry)
        {
            var legacy = _options.Schools.FirstOrDefault(s => s.SchoolId == schoolId);
            if (legacy is not null && !string.IsNullOrWhiteSpace(legacy.ActivationBaseUrl))
            {
                return legacy;
            }
        }

        throw new InvalidOperationException(
            $"École {schoolId:D} introuvable dans le registre Bootstrap SQL" +
            (_options.AllowLegacyEnvSchoolRegistry
                ? " (legacy env également vide)."
                : " (legacy env désactivé)."));
    }

    /// <summary>Compat sync — préférer <see cref="ResolveAsync"/>.</summary>
    public SchoolRegistryEntryOptions Resolve(Guid schoolId) =>
        ResolveAsync(schoolId).GetAwaiter().GetResult();

    private static SchoolRegistryEntryOptions MapSql(BootstrapSchoolRegistryEntry sql) =>
        new()
        {
            SchoolId = sql.SchoolId,
            ActivationBaseUrl = sql.ActivationBaseUrl,
            CloudBaseUrl = sql.CloudBaseUrl,
            PublicKeyFingerprint = sql.PublicKeyFingerprint,
            KeyVersion = sql.KeyVersion,
            ServerInstanceId = sql.ServerInstanceId?.ToString("D"),
        };
}
