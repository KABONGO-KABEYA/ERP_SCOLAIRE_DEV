using Microsoft.EntityFrameworkCore;
using SchoolManagement.Bootstrap.API.Persistence.Entities;

namespace SchoolManagement.Bootstrap.API.Persistence;

public sealed class EfBootstrapSchoolRegistryRepository : IBootstrapSchoolRegistryRepository
{
    private readonly BootstrapDbContext _db;

    public EfBootstrapSchoolRegistryRepository(BootstrapDbContext db)
    {
        _db = db;
    }

    public Task<BootstrapSchoolRegistryEntry?> GetBySchoolIdAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default) =>
        _db.SchoolRegistry.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SchoolId == schoolId, cancellationToken);

    public Task<BootstrapSchoolEstablishmentCredential?> GetActiveCredentialAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default) =>
        _db.EstablishmentCredentials.AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.SchoolId == schoolId && c.Status == EstablishmentCredentialStatuses.Active,
                cancellationToken);

    public Task<BootstrapSchoolEstablishmentCredential?> GetCredentialByIdAsync(
        Guid credentialId,
        CancellationToken cancellationToken = default) =>
        _db.EstablishmentCredentials.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == credentialId, cancellationToken);

    public async Task<BootstrapSchoolRegistryEntry> UpsertSchoolAsync(
        BootstrapSchoolRegistryUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SchoolName))
        {
            throw new ArgumentException("SchoolName requis.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ActivationBaseUrl) ||
            string.IsNullOrWhiteSpace(request.CloudBaseUrl))
        {
            throw new ArgumentException("ActivationBaseUrl et CloudBaseUrl requis.", nameof(request));
        }

        var now = DateTime.UtcNow;
        var entry = await _db.SchoolRegistry
            .FirstOrDefaultAsync(s => s.SchoolId == request.SchoolId, cancellationToken);

        if (entry is null)
        {
            entry = new BootstrapSchoolRegistryEntry
            {
                SchoolId = request.SchoolId,
                RegisteredAtUtc = now,
            };
            _db.SchoolRegistry.Add(entry);
        }

        entry.SchoolName = request.SchoolName.Trim();
        entry.ActivationBaseUrl = request.ActivationBaseUrl.Trim().TrimEnd('/');
        entry.CloudBaseUrl = request.CloudBaseUrl.Trim().TrimEnd('/');
        entry.PublicKeyFingerprint = NullIfWhiteSpace(request.PublicKeyFingerprint);
        entry.KeyVersion = request.KeyVersion;
        entry.ServerInstanceId = request.ServerInstanceId;
        entry.LicenseId = request.LicenseId;
        entry.IsActive = true;
        entry.UpdatedAtUtc = now;

        if (request.Credential is not null)
        {
            await ActivateCredentialCoreAsync(request.SchoolId, request.Credential, revokeReason: null, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<(BootstrapSchoolEstablishmentCredential Revoked, BootstrapSchoolEstablishmentCredential Active)> RotateCredentialAsync(
        Guid schoolId,
        BootstrapCredentialUpsert newCredential,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newCredential);

        var school = await _db.SchoolRegistry
            .FirstOrDefaultAsync(s => s.SchoolId == schoolId, cancellationToken)
            ?? throw new InvalidOperationException($"École {schoolId:D} introuvable dans le registre Bootstrap.");

        var previous = await _db.EstablishmentCredentials
            .FirstOrDefaultAsync(
                c => c.SchoolId == schoolId && c.Status == EstablishmentCredentialStatuses.Active,
                cancellationToken);

        if (previous is null)
        {
            throw new InvalidOperationException(
                $"Aucun credential Active à révoquer pour l'école {schoolId:D}.");
        }

        var revokeReason = string.IsNullOrWhiteSpace(reason) ? "Rotation credential" : reason.Trim();
        var active = await ActivateCredentialCoreAsync(
            schoolId,
            newCredential,
            revokeReason,
            cancellationToken);

        school.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (previous, active);
    }

    public async Task<BootstrapEstablishmentSession> CreateSessionAsync(
        Guid schoolId,
        Guid credentialId,
        string deviceId,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("DeviceId requis.", nameof(deviceId));
        }

        var session = new BootstrapEstablishmentSession
        {
            SchoolId = schoolId,
            CredentialId = credentialId,
            DeviceId = deviceId.Trim(),
            Status = EstablishmentSessionStatuses.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
        };
        _db.EstablishmentSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public Task<BootstrapEstablishmentSession?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        _db.EstablishmentSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

    public async Task MarkSessionCompletedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.EstablishmentSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session d'établissement introuvable.");

        session.Status = EstablishmentSessionStatuses.Completed;
        session.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<BootstrapSchoolEstablishmentCredential> ActivateCredentialCoreAsync(
        Guid schoolId,
        BootstrapCredentialUpsert credential,
        string? revokeReason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credential.SecretHash))
        {
            throw new ArgumentException("SecretHash requis.", nameof(credential));
        }

        if (credential.CredentialVersion < 1)
        {
            throw new ArgumentException("CredentialVersion doit être >= 1.", nameof(credential));
        }

        var existingActive = await _db.EstablishmentCredentials
            .Where(c => c.SchoolId == schoolId && c.Status == EstablishmentCredentialStatuses.Active)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var old in existingActive)
        {
            if (old.Id == credential.CredentialId)
            {
                continue;
            }

            old.Status = EstablishmentCredentialStatuses.Revoked;
            old.RevokedAtUtc = now;
            old.RevokedReason = revokeReason ?? "Remplacé par upsert credential";
        }

        var row = await _db.EstablishmentCredentials
            .FirstOrDefaultAsync(c => c.Id == credential.CredentialId, cancellationToken);

        if (row is null)
        {
            row = new BootstrapSchoolEstablishmentCredential
            {
                Id = credential.CredentialId,
                SchoolId = schoolId,
                CreatedAtUtc = now,
            };
            _db.EstablishmentCredentials.Add(row);
        }

        row.CredentialVersion = credential.CredentialVersion;
        row.TokenType = string.IsNullOrWhiteSpace(credential.TokenType)
            ? EstablishmentTokenTypes.SchoolEstablishment
            : credential.TokenType.Trim();
        row.SecretHash = credential.SecretHash.Trim();
        row.Status = EstablishmentCredentialStatuses.Active;
        row.RevokedAtUtc = null;
        row.RevokedReason = null;
        row.CreatedBy = NullIfWhiteSpace(credential.CreatedBy);
        return row;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
