namespace SchoolManagement.Infrastructure.Auth;

using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Auth.DTOs;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Security;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;

public sealed class AuthService : IAuthService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEffectivePermissionService _effectivePermissions;
    private readonly SchoolDbContext _context;

    public AuthService(
        IUserAccountRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IEffectivePermissionService effectivePermissions,
        SchoolDbContext context)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _effectivePermissions = effectivePermissions;
        _context = context;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        // Multi-tenant (Cloud / login distant) : si SchoolId fourni (établissement lié au QR),
        // résoudre le compte dans cette école — évite de renvoyer un homonyme d'une autre école.
        var userQuery = _context.UserAccounts
            .IgnoreQueryFilters()
            .Where(u => u.UserName == request.UserName && u.IsActive && !u.IsDeleted);

        if (request.SchoolId is Guid schoolId && schoolId != Guid.Empty)
        {
            userQuery = userQuery.Where(u => u.SchoolId == schoolId);
        }

        var user = await userQuery.FirstOrDefaultAsync(cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await LogLoginAsync(user, request.UserName, false, "Identifiants invalides", ipAddress, cancellationToken);
            throw new UnauthorizedAccessException("Nom d'utilisateur ou mot de passe incorrect.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await LogLoginAsync(user, user.UserName, true, null, ipAddress, cancellationToken);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken)
            ?? throw new UnauthorizedAccessException("Refresh token invalide.");

        if (storedToken.IsRevoked || storedToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token expiré ou révoqué.");
        }

        var user = await _context.UserAccounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == storedToken.UserId && !u.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedAccessException("Utilisateur introuvable.");

        if (!user.IsActive)
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Compte désactivé.");
        }

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var storedToken = await _refreshTokenRepository.GetByTokenAsync(refreshToken, cancellationToken);
        if (storedToken is null)
        {
            return;
        }

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.UserAccounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var effective = await _effectivePermissions.ResolveAsync(userId, cancellationToken);
        return MapProfile(user, effective.Roles, effective.PermissionCodes);
    }

    public async Task<AuthResponse> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.UserAccounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Utilisateur introuvable.");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new DomainException("Mot de passe actuel incorrect.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        // Entité déjà trackée : ne pas appeler Update() (marquerait SchoolId comme modifié).
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var effective = await _effectivePermissions.ResolveAsync(user.Id, cancellationToken);

        var accessToken = _tokenService.GenerateAccessToken(
            user.Id,
            user.SchoolId,
            user.UserName,
            $"{user.FirstName} {user.LastName}",
            effective.Roles,
            effective.PermissionCodes,
            effective.IsPlatformSuperAdmin);

        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            accessToken,
            refreshTokenValue,
            _tokenService.GetAccessTokenExpiration(),
            MapProfile(user, effective.Roles, effective.PermissionCodes));
    }

    private static UserProfileDto MapProfile(
        UserAccount user,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions) =>
        new(
            user.Id,
            user.SchoolId,
            user.UserName,
            user.Email,
            $"{user.FirstName} {user.LastName}",
            user.MustChangePassword,
            user.TeacherId,
            roles,
            permissions);

    private async Task LogLoginAsync(
        UserAccount? user,
        string userName,
        bool success,
        string? failureReason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var schoolId = user?.SchoolId
            ?? await LocalSchoolResolver.TryResolvePrimarySchoolIdAsync(_context, cancellationToken);
        if (schoolId is null || schoolId == Guid.Empty)
        {
            return;
        }

        var previousIgnore = _context.IgnoreSchoolScope;
        _context.IgnoreSchoolScope = true;
        try
        {
            _context.LoginHistory.Add(new LoginHistory
            {
                SchoolId = schoolId.Value,
                UserId = user?.Id,
                UserName = userName,
                IsSuccessful = success,
                FailureReason = failureReason,
                IpAddress = ipAddress,
                LoginAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _context.IgnoreSchoolScope = previousIgnore;
        }
    }
}
