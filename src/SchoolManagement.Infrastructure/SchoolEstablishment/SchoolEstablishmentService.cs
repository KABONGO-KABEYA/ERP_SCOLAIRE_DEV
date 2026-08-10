using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.SchoolEstablishment;
using SchoolManagement.Application.ServerIdentity;
using SchoolManagement.Domain.Entities.SchoolEstablishment;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

namespace SchoolManagement.Infrastructure.SchoolEstablishment;

public sealed class SchoolEstablishmentService : ISchoolEstablishmentService
{
    private readonly SchoolDbContext _db;
    private readonly IBootstrapSchoolRegistryClient _registryClient;
    private readonly SchoolBootstrapPublishUrls _publishUrls;
    private readonly IServerIdentityProvider _identity;
    private readonly ILogger<SchoolEstablishmentService> _logger;

    public SchoolEstablishmentService(
        SchoolDbContext db,
        IBootstrapSchoolRegistryClient registryClient,
        SchoolBootstrapPublishUrls publishUrls,
        IServerIdentityProvider identity,
        ILogger<SchoolEstablishmentService> logger)
    {
        _db = db;
        _registryClient = registryClient;
        _publishUrls = publishUrls;
        _identity = identity;
        _logger = logger;
    }

    public async Task<SchoolEstablishmentQrDto> ProvisionForNewSchoolAsync(
        Guid schoolId,
        string schoolName,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.SchoolEstablishmentCredentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => !c.IsDeleted
                     && c.SchoolId == schoolId
                     && c.Status == SchoolEstablishmentCredentialStatuses.Active,
                cancellationToken);

        if (existing is not null)
        {
            return await BuildQrAsync(existing, schoolName, cancellationToken);
        }

        var credential = CreateActiveCredential(schoolId, version: 1, createdByUserId: null);
        _db.SchoolEstablishmentCredentials.Add(credential);
        await _db.SaveChangesAsync(cancellationToken);

        await TryPublishUpsertAsync(credential, schoolName, cancellationToken);
        return await BuildQrAsync(credential, schoolName, cancellationToken);
    }

    public async Task<SchoolEstablishmentQrDto> GetCurrentQrAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var active = await GetActiveRequiredAsync(schoolId, cancellationToken);
        var schoolName = await ResolveSchoolNameAsync(schoolId, cancellationToken);
        return await BuildQrAsync(active, schoolName, cancellationToken);
    }

    public async Task<SchoolEstablishmentQrDto> RotateAsync(
        Guid schoolId,
        Guid? rotatedByUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var previous = await GetActiveRequiredAsync(schoolId, cancellationToken);
        var schoolName = await ResolveSchoolNameAsync(schoolId, cancellationToken);
        var now = DateTime.UtcNow;

        previous.Status = SchoolEstablishmentCredentialStatuses.Revoked;
        previous.RevokedAtUtc = now;
        previous.RevokedReason = string.IsNullOrWhiteSpace(reason) ? "Rotation credential" : reason.Trim();
        previous.UpdatedAt = now;

        var next = CreateActiveCredential(
            schoolId,
            version: previous.CredentialVersion + 1,
            createdByUserId: rotatedByUserId);
        _db.SchoolEstablishmentCredentials.Add(next);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _registryClient.RotateCredentialAsync(
                schoolId,
                ToPayload(next),
                previous.RevokedReason,
                cancellationToken);
            MarkSynced(next);
        }
        catch (Exception ex) when (ex is InvalidOperationException or BootstrapRegistryClientException or HttpRequestException or TaskCanceledException)
        {
            // École / nouveau credential locaux conservés — sync pending + retry admin.
            MarkFailed(next, ex);
            _logger.LogWarning(
                ex,
                "Rotation locale OK mais publication Bootstrap en échec — école {SchoolId}, credential {CredentialId}",
                schoolId,
                next.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await BuildQrAsync(next, schoolName, cancellationToken);
    }

    public async Task<BootstrapSyncRetryResult> RetryBootstrapSyncAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var active = await GetActiveRequiredAsync(schoolId, cancellationToken);
        var schoolName = await ResolveSchoolNameAsync(schoolId, cancellationToken);

        try
        {
            // Upsert idempotent : recrée/maj registre + active le credential courant (révoque l'ancien côté Bootstrap).
            await PublishUpsertCoreAsync(active, schoolName, cancellationToken);
            MarkSynced(active);
            await _db.SaveChangesAsync(cancellationToken);
            var qr = await BuildQrAsync(active, schoolName, cancellationToken);
            return new BootstrapSyncRetryResult(
                Success: true,
                BootstrapSyncPending: false,
                BootstrapSyncStatus: SchoolEstablishmentBootstrapSyncStatuses.Synced,
                Message: "Registre Bootstrap synchronisé.",
                Qr: qr);
        }
        catch (Exception ex) when (ex is InvalidOperationException or BootstrapRegistryClientException or HttpRequestException or TaskCanceledException)
        {
            MarkFailed(active, ex);
            await _db.SaveChangesAsync(cancellationToken);
            var qr = await BuildQrAsync(active, schoolName, cancellationToken);
            return new BootstrapSyncRetryResult(
                Success: false,
                BootstrapSyncPending: true,
                BootstrapSyncStatus: active.BootstrapSyncStatus,
                Message: SafeErrorMessage(ex),
                Qr: qr);
        }
    }

    private async Task TryPublishUpsertAsync(
        SchoolEstablishmentCredential credential,
        string schoolName,
        CancellationToken cancellationToken)
    {
        try
        {
            await PublishUpsertCoreAsync(credential, schoolName, cancellationToken);
            MarkSynced(credential);
        }
        catch (Exception ex) when (ex is InvalidOperationException or BootstrapRegistryClientException or HttpRequestException or TaskCanceledException)
        {
            MarkFailed(credential, ex);
            _logger.LogWarning(
                ex,
                "École {SchoolId} créée localement — sync Bootstrap pending (credential {CredentialId})",
                credential.SchoolId,
                credential.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task PublishUpsertCoreAsync(
        SchoolEstablishmentCredential credential,
        string schoolName,
        CancellationToken cancellationToken)
    {
        var activationBaseUrl = _publishUrls.ActivationBaseUrl;
        var cloudBaseUrl = _publishUrls.CloudBaseUrl;
        if (string.IsNullOrWhiteSpace(activationBaseUrl) || string.IsNullOrWhiteSpace(cloudBaseUrl))
        {
            throw new InvalidOperationException(
                "Bootstrap:ActivationBaseUrl et CloudBaseUrl (ou Activation:CloudBaseUrl) sont requis pour publier le registre.");
        }

        var snapshot = _identity.Current;
        await _registryClient.UpsertSchoolAsync(
            new BootstrapRegistryUpsertPayload(
                credential.SchoolId,
                schoolName,
                activationBaseUrl,
                cloudBaseUrl,
                snapshot.PublicKeyFingerprint,
                snapshot.KeyVersion,
                snapshot.ServerInstanceId,
                snapshot.LicenseId,
                ToPayload(credential)),
            cancellationToken);
    }

    private static SchoolEstablishmentCredential CreateActiveCredential(
        Guid schoolId,
        int version,
        Guid? createdByUserId)
    {
        var secretHash = SchoolEstablishmentCrypto.CreateSecretHash();
        return new SchoolEstablishmentCredential
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            CredentialVersion = version,
            TokenType = SchoolEstablishmentTokenConstants.TokenTypeValue,
            SecretHash = secretHash,
            Status = SchoolEstablishmentCredentialStatuses.Active,
            CreatedByUserId = createdByUserId,
            BootstrapSyncPending = true,
            BootstrapSyncStatus = SchoolEstablishmentBootstrapSyncStatuses.Pending,
        };
    }

    private async Task<SchoolEstablishmentCredential> GetActiveRequiredAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        return await _db.SchoolEstablishmentCredentials
                   .IgnoreQueryFilters()
                   .FirstOrDefaultAsync(
                       c => !c.IsDeleted
                            && c.SchoolId == schoolId
                            && c.Status == SchoolEstablishmentCredentialStatuses.Active,
                       cancellationToken)
               ?? throw new DomainException("Aucun credential établissement Active pour cette école.");
    }

    private async Task<string> ResolveSchoolNameAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var name = await _db.Schools.IgnoreQueryFilters().AsNoTracking()
            .Where(s => !s.IsDeleted && s.Id == schoolId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(name) ? _identity.Current.SchoolName : name;
    }

    private Task<SchoolEstablishmentQrDto> BuildQrAsync(
        SchoolEstablishmentCredential credential,
        string schoolName,
        CancellationToken cancellationToken)
    {
        _ = schoolName;
        _ = cancellationToken;
        // JWT public (QR) — signé depuis SecretHash ; le secret brut n'existe plus.
        var token = SchoolEstablishmentCrypto.CreateSignedJwt(
            credential.SchoolId,
            credential.Id,
            credential.CredentialVersion,
            credential.SecretHash);
        var deepLink = SchoolEstablishmentCrypto.BuildDeepLink(token);
        return Task.FromResult(new SchoolEstablishmentQrDto(
            credential.SchoolId,
            credential.Id,
            credential.CredentialVersion,
            token,
            deepLink,
            deepLink,
            credential.BootstrapSyncPending,
            credential.BootstrapSyncStatus,
            credential.LastBootstrapSyncError));
    }

    private static BootstrapRegistryCredentialPayload ToPayload(SchoolEstablishmentCredential c) =>
        new(c.Id, c.CredentialVersion, c.SecretHash, c.TokenType);

    private static void MarkSynced(SchoolEstablishmentCredential credential)
    {
        var now = DateTime.UtcNow;
        credential.BootstrapSyncPending = false;
        credential.BootstrapSyncStatus = SchoolEstablishmentBootstrapSyncStatuses.Synced;
        credential.LastBootstrapSyncError = null;
        credential.LastBootstrapSyncAttemptUtc = now;
        credential.BootstrapSyncedAtUtc = now;
        credential.UpdatedAt = now;
    }

    private static void MarkFailed(SchoolEstablishmentCredential credential, Exception ex)
    {
        var now = DateTime.UtcNow;
        credential.BootstrapSyncPending = true;
        credential.BootstrapSyncStatus = SchoolEstablishmentBootstrapSyncStatuses.Failed;
        credential.LastBootstrapSyncError = SafeErrorMessage(ex);
        credential.LastBootstrapSyncAttemptUtc = now;
        credential.UpdatedAt = now;
    }

    private static string SafeErrorMessage(Exception ex) =>
        ex switch
        {
            BootstrapRegistryClientException b => b.Message,
            InvalidOperationException i => i.Message,
            TaskCanceledException => "Délai dépassé lors de l'appel Bootstrap.",
            HttpRequestException => "Bootstrap injoignable.",
            _ => "Échec de synchronisation Bootstrap.",
        };
}
