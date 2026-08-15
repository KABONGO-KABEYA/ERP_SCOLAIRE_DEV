using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolManagement.Bootstrap.API.Contracts;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Persistence.Entities;
using SchoolManagement.Bootstrap.API.Security;

namespace SchoolManagement.Bootstrap.API.Services;

public interface IUpdateAgentCredentialService
{
    Task<UpdateAgentCredentialSecretResponse> CreateAsync(
        CreateUpdateAgentCredentialRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UpdateAgentCredentialListItem>> ListAsync(
        Guid schoolId,
        CancellationToken cancellationToken);

    Task<UpdateAgentCredentialSecretResponse> RotateAsync(
        Guid clientId,
        string? reason,
        CancellationToken cancellationToken);

    Task<UpdateAgentCredentialListItem> RevokeAsync(
        Guid clientId,
        string? reason,
        CancellationToken cancellationToken);

    Task<UpdateAgentTokenResponse> IssueTokenAsync(
        UpdateAgentTokenRequest request,
        CancellationToken cancellationToken);

    Task<UpdateAgentAuthContext> AuthenticateBearerAsync(
        string token,
        CancellationToken cancellationToken);
}

public sealed class UpdateAgentCredentialService : IUpdateAgentCredentialService
{
    private readonly BootstrapDbContext _db;
    private readonly BootstrapOptions _options;

    public UpdateAgentCredentialService(BootstrapDbContext db, IOptions<BootstrapOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<UpdateAgentCredentialSecretResponse> CreateAsync(
        CreateUpdateAgentCredentialRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.SchoolId == Guid.Empty)
        {
            throw new AgentException(StatusCodes.Status400BadRequest, "SchoolId requis.");
        }

        var school = await LoadSchoolAsync(request.SchoolId, cancellationToken);
        EnsureSchoolActive(school);

        var existingActive = await GetActiveAsync(request.SchoolId, cancellationToken);
        if (existingActive is not null)
        {
            throw new AgentException(
                StatusCodes.Status409Conflict,
                "Un credential agent Active existe déjà pour cette école. Utilisez rotate.");
        }

        var created = CreateActiveRow(
            request.SchoolId,
            version: 1,
            request.CreatedBy,
            out var clientSecret);

        _db.UpdateAgentCredentials.Add(created);
        await _db.SaveChangesAsync(cancellationToken);
        return MapSecret(created, clientSecret);
    }

    public async Task<IReadOnlyList<UpdateAgentCredentialListItem>> ListAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        if (schoolId == Guid.Empty)
        {
            throw new AgentException(StatusCodes.Status400BadRequest, "SchoolId requis.");
        }

        var rows = await _db.UpdateAgentCredentials
            .AsNoTracking()
            .Where(c => c.SchoolId == schoolId)
            .OrderByDescending(c => c.CredentialVersion)
            .ToListAsync(cancellationToken);

        return rows.Select(MapList).ToList();
    }

    public async Task<UpdateAgentCredentialSecretResponse> RotateAsync(
        Guid clientId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var current = await GetByIdAsync(clientId, cancellationToken)
                      ?? throw new AgentException(StatusCodes.Status404NotFound, "Credential agent introuvable.");

        if (!string.Equals(current.Status, UpdateAgentCredentialStatuses.Active, StringComparison.Ordinal))
        {
            throw new AgentException(StatusCodes.Status409Conflict, "Seul un credential Active peut être roté.");
        }

        var school = await LoadSchoolAsync(current.SchoolId, cancellationToken);
        EnsureSchoolActive(school);

        var now = DateTime.UtcNow;
        current.Status = UpdateAgentCredentialStatuses.Revoked;
        current.RevokedAtUtc = now;
        current.RevokedReason = string.IsNullOrWhiteSpace(reason) ? "Rotation credential agent" : reason.Trim();

        var next = CreateActiveRow(
            current.SchoolId,
            version: current.CredentialVersion + 1,
            createdBy: current.CreatedBy,
            out var clientSecret);

        _db.UpdateAgentCredentials.Add(next);
        await _db.SaveChangesAsync(cancellationToken);
        return MapSecret(next, clientSecret);
    }

    public async Task<UpdateAgentCredentialListItem> RevokeAsync(
        Guid clientId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var current = await GetByIdAsync(clientId, cancellationToken)
                      ?? throw new AgentException(StatusCodes.Status404NotFound, "Credential agent introuvable.");

        if (string.Equals(current.Status, UpdateAgentCredentialStatuses.Revoked, StringComparison.Ordinal))
        {
            return MapList(current);
        }

        current.Status = UpdateAgentCredentialStatuses.Revoked;
        current.RevokedAtUtc = DateTime.UtcNow;
        current.RevokedReason = string.IsNullOrWhiteSpace(reason) ? "Révocation credential agent" : reason.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return MapList(current);
    }

    public async Task<UpdateAgentTokenResponse> IssueTokenAsync(
        UpdateAgentTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!UpdateAgentJwt.TryValidateSigningKey(_options.AgentJwtSigningKey, out var keyError))
        {
            throw new AgentException(StatusCodes.Status503ServiceUnavailable, keyError!);
        }

        if (request is null || request.ClientId == Guid.Empty || string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            throw new AgentException(StatusCodes.Status401Unauthorized, "Identifiants agent invalides.");
        }

        var credential = await GetByIdAsync(request.ClientId, cancellationToken);
        if (credential is null || !UpdateAgentSecret.Matches(request.ClientSecret, credential.SecretHash))
        {
            throw new AgentException(StatusCodes.Status401Unauthorized, "Identifiants agent invalides.");
        }

        if (request.SchoolId is { } claimed && claimed != Guid.Empty && claimed != credential.SchoolId)
        {
            throw new AgentException(StatusCodes.Status401Unauthorized, "Identifiants agent invalides.");
        }

        if (!string.Equals(credential.Status, UpdateAgentCredentialStatuses.Active, StringComparison.Ordinal))
        {
            throw new AgentException(StatusCodes.Status401Unauthorized, "Credential agent révoqué.");
        }

        var school = await LoadSchoolAsync(credential.SchoolId, cancellationToken);
        if (!school.IsActive)
        {
            throw new AgentException(StatusCodes.Status403Forbidden, "École inactive.");
        }

        var lifetime = ResolveLifetime();
        var expiresUtc = DateTime.UtcNow.Add(lifetime);
        var jwt = UpdateAgentJwt.Create(
            _options.AgentJwtSigningKey,
            credential.Id,
            credential.SchoolId,
            school.ServerInstanceId,
            expiresUtc);

        return new UpdateAgentTokenResponse
        {
            AccessToken = jwt,
            TokenType = "Bearer",
            ExpiresIn = (int)Math.Ceiling(lifetime.TotalSeconds),
            SchoolId = credential.SchoolId,
            ClientId = credential.Id,
        };
    }

    public async Task<UpdateAgentAuthContext> AuthenticateBearerAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (!UpdateAgentJwt.TryValidateSigningKey(_options.AgentJwtSigningKey, out var keyError))
        {
            throw new AgentException(StatusCodes.Status503ServiceUnavailable, keyError!);
        }

        UpdateAgentJwtClaims claims;
        try
        {
            claims = UpdateAgentJwt.Validate(token, _options.AgentJwtSigningKey);
        }
        catch (SecurityTokenExpiredException)
        {
            throw new AgentException(StatusCodes.Status401Unauthorized, "Jeton agent expiré.");
        }
        catch (SecurityTokenException)
        {
            throw new AgentException(StatusCodes.Status401Unauthorized, "Jeton agent invalide.");
        }

        var credential = await GetByIdAsync(claims.ClientId, cancellationToken);
        if (credential is null)
        {
            throw new AgentException(StatusCodes.Status401Unauthorized, "Jeton agent invalide.");
        }

        if (!string.Equals(credential.Status, UpdateAgentCredentialStatuses.Active, StringComparison.Ordinal))
        {
            throw new AgentException(StatusCodes.Status401Unauthorized, "Credential agent révoqué.");
        }

        if (claims.SchoolId != credential.SchoolId)
        {
            throw new AgentException(StatusCodes.Status401Unauthorized, "Jeton agent invalide.");
        }

        var school = await LoadSchoolAsync(credential.SchoolId, cancellationToken);
        if (!school.IsActive)
        {
            throw new AgentException(StatusCodes.Status403Forbidden, "École inactive.");
        }

        return new UpdateAgentAuthContext
        {
            ClientId = credential.Id,
            SchoolId = credential.SchoolId,
            JwtId = claims.JwtId,
            ServerInstanceId = school.ServerInstanceId,
        };
    }

    private async Task<BootstrapSchoolRegistryEntry> LoadSchoolAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        var school = await _db.SchoolRegistry
            .FirstOrDefaultAsync(s => s.SchoolId == schoolId, cancellationToken);
        if (school is null)
        {
            throw new AgentException(StatusCodes.Status404NotFound, "École introuvable dans le registre Bootstrap.");
        }

        return school;
    }

    private static void EnsureSchoolActive(BootstrapSchoolRegistryEntry school)
    {
        if (!school.IsActive)
        {
            throw new AgentException(StatusCodes.Status403Forbidden, "École inactive.");
        }
    }

    private Task<UpdateAgentCredential?> GetByIdAsync(Guid clientId, CancellationToken cancellationToken) =>
        _db.UpdateAgentCredentials.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);

    private Task<UpdateAgentCredential?> GetActiveAsync(Guid schoolId, CancellationToken cancellationToken) =>
        _db.UpdateAgentCredentials.FirstOrDefaultAsync(
            c => c.SchoolId == schoolId && c.Status == UpdateAgentCredentialStatuses.Active,
            cancellationToken);

    private static UpdateAgentCredential CreateActiveRow(
        Guid schoolId,
        int version,
        string? createdBy,
        out string clientSecret)
    {
        var generated = UpdateAgentSecret.Generate();
        clientSecret = generated.ClientSecret;
        return new UpdateAgentCredential
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolId,
            CredentialVersion = version,
            SecretHash = generated.SecretHash,
            Status = UpdateAgentCredentialStatuses.Active,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim(),
        };
    }

    private TimeSpan ResolveLifetime()
    {
        var minutes = _options.AgentJwtMinutes;
        if (minutes < 5)
        {
            minutes = 5;
        }
        else if (minutes > 60)
        {
            minutes = 60;
        }

        return TimeSpan.FromMinutes(minutes);
    }

    private static UpdateAgentCredentialSecretResponse MapSecret(UpdateAgentCredential row, string clientSecret) =>
        new()
        {
            ClientId = row.Id,
            SchoolId = row.SchoolId,
            CredentialVersion = row.CredentialVersion,
            Status = row.Status,
            ClientSecret = clientSecret,
            CreatedAtUtc = row.CreatedAtUtc,
        };

    private static UpdateAgentCredentialListItem MapList(UpdateAgentCredential row) =>
        new()
        {
            ClientId = row.Id,
            SchoolId = row.SchoolId,
            CredentialVersion = row.CredentialVersion,
            Status = row.Status,
            CreatedAtUtc = row.CreatedAtUtc,
            RevokedAtUtc = row.RevokedAtUtc,
            RevokedReason = row.RevokedReason,
        };
}
