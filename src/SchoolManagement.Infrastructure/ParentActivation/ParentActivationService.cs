using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.ParentActivation;
using SchoolManagement.Application.ServerIdentity;
using SchoolManagement.Domain.Entities.ParentActivation;
using SchoolManagement.Infrastructure.Auth;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Shared.Constants;

namespace SchoolManagement.Infrastructure.ParentActivation;

public sealed class ParentActivationService : IParentActivationService
{
    private readonly SchoolDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerIdentityProvider _identity;
    private readonly IConfiguration _configuration;
    private readonly JwtSettings _jwtSettings;

    public ParentActivationService(
        SchoolDbContext db,
        IUnitOfWork unitOfWork,
        IServerIdentityProvider identity,
        IConfiguration configuration,
        IOptions<JwtSettings> jwtSettings)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _identity = identity;
        _configuration = configuration;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<IssueParentActivationTokenResponse> IssueTokenAsync(
        Guid schoolId,
        Guid issuedByUserId,
        IssueParentActivationTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var validityMinutes = request.ValidityMinutes is > 0 and <= 120
            ? request.ValidityMinutes.Value
            : ActivationTokenConstants.DefaultValidityMinutes;

        var expires = DateTime.UtcNow.AddMinutes(validityMinutes);
        var tokenEntity = new ParentActivationToken
        {
            SchoolId = schoolId,
            ExpiresAtUtc = expires,
            SuggestedUserName = string.IsNullOrWhiteSpace(request.SuggestedUserName)
                ? null
                : request.SuggestedUserName.Trim(),
            IssuedByUserId = issuedByUserId
        };

        await _db.ParentActivationTokens.AddAsync(tokenEntity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var jwt = CreateActivationJwt(tokenEntity, expires);
        var deepLink =
            $"{ActivationTokenConstants.DeepLinkScheme}://activate?token={Uri.EscapeDataString(jwt)}";

        return new IssueParentActivationTokenResponse(
            jwt,
            tokenEntity.Id,
            expires,
            deepLink,
            deepLink);
    }

    public async Task<ActivationSessionDto> StartAsync(
        ActivationStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var principal = ValidateActivationJwt(request.Token);
        var tokenId = Guid.Parse(principal.FindFirst(JwtRegisteredClaimNames.Jti)!.Value);
        var schoolId = Guid.Parse(principal.FindFirst(ClaimTypesCustom.SchoolId)!.Value);

        var tokenEntity = await _db.ParentActivationTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.SchoolId == schoolId, cancellationToken)
            ?? throw new InvalidOperationException("Token d'activation inconnu.");

        if (tokenEntity.ConsumedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Token d'activation déjà utilisé.");
        }

        if (tokenEntity.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Token d'activation expiré.");
        }

        var session = new ParentActivationSession
        {
            SchoolId = schoolId,
            ActivationTokenId = tokenId,
            DeviceId = request.DeviceId.Trim(),
            BootstrapSessionId = request.BootstrapSessionId,
            Status = ParentActivationSessionStatus.Pending,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(ActivationTokenConstants.SessionTtlMinutes)
        };

        await _db.ParentActivationSessions.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapSession(session, request.ClientHints);
    }

    public async Task<SchoolBindingDto> CompleteAsync(
        ActivationCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ParentActivationSessions
            .FirstOrDefaultAsync(s => s.Id == request.ActivationSessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session d'activation introuvable.");

        if (!string.Equals(session.DeviceId, request.DeviceId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("DeviceId incompatible avec la session.");
        }

        if (session.Status != ParentActivationSessionStatus.Pending)
        {
            throw new InvalidOperationException("Session d'activation déjà finalisée ou invalide.");
        }

        if (session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Session d'activation expirée.");
        }

        var tokenEntity = await _db.ParentActivationTokens
            .FirstOrDefaultAsync(t => t.Id == session.ActivationTokenId, cancellationToken)
            ?? throw new InvalidOperationException("Token d'activation introuvable.");

        if (tokenEntity.ConsumedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Token d'activation déjà consommé.");
        }

        tokenEntity.ConsumedAtUtc = DateTime.UtcNow;
        session.Status = ParentActivationSessionStatus.Completed;
        session.CompletedAtUtc = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BuildBinding(tokenEntity, session);
    }

    private SchoolBindingDto BuildBinding(ParentActivationToken token, ParentActivationSession session)
    {
        var snapshot = _identity.Current;
        var cloudBaseUrl = (_configuration["Activation:CloudBaseUrl"]
                            ?? _configuration["Activation:CloudPublicUrl"]
                            ?? string.Empty).Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(cloudBaseUrl))
        {
            cloudBaseUrl = "https://cloud.example.invalid";
        }

        return new SchoolBindingDto(
            token.SchoolId,
            snapshot.SchoolName,
            cloudBaseUrl,
            snapshot.ServerInstanceId,
            snapshot.LicenseId,
            DateTime.UtcNow,
            token.Id,
            session.Id,
            session.DeviceId,
            ConnectionProtocolConstants.ProtocolVersion,
            token.SuggestedUserName,
            null,
            null);
    }

    private string CreateActivationJwt(ParentActivationToken tokenEntity, DateTime expires)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, tokenEntity.Id.ToString("D")),
            new(ClaimTypesCustom.SchoolId, tokenEntity.SchoolId.ToString("D")),
            new(ActivationTokenConstants.TokenTypeClaim, ActivationTokenConstants.TokenTypeValue)
        };

        if (!string.IsNullOrWhiteSpace(tokenEntity.SuggestedUserName))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, tokenEntity.SuggestedUserName));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private ClaimsPrincipal ValidateActivationJwt(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        var principal = handler.ValidateToken(token, parameters, out var validated);
        if (validated is not JwtSecurityToken jwt
            || jwt.Claims.FirstOrDefault(c => c.Type == ActivationTokenConstants.TokenTypeClaim)?.Value
            != ActivationTokenConstants.TokenTypeValue)
        {
            throw new InvalidOperationException("Type de token invalide.");
        }

        return principal;
    }

    private static ActivationSessionDto MapSession(
        ParentActivationSession session,
        Dictionary<string, object?>? clientHints) =>
        new(
            session.Id,
            session.ActivationTokenId,
            session.DeviceId,
            session.SchoolId,
            session.Status.ToString().ToLowerInvariant(),
            session.CreatedAt,
            session.ExpiresAtUtc,
            clientHints);
}
