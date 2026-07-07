namespace SchoolManagement.Infrastructure.Auth;

using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Auth.DTOs;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
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
    private readonly SchoolDbContext _context;

    public AuthService(
        IUserAccountRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        SchoolDbContext context)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _context.UserAccounts
            .Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.Permissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.UserName == request.UserName && u.IsActive, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await LogLoginAsync(user?.Id, request.UserName, false, "Identifiants invalides", ipAddress, cancellationToken);
            throw new UnauthorizedAccessException("Nom d'utilisateur ou mot de passe incorrect.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await LogLoginAsync(user.Id, user.UserName, true, null, ipAddress, cancellationToken);

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

        var user = await _userRepository.GetWithRolesAndPermissionsAsync(storedToken.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Utilisateur introuvable.");

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
        var user = await _userRepository.GetWithRolesAndPermissionsAsync(userId, cancellationToken);
        return user is null ? null : MapProfile(user);
    }

    public async Task<AuthResponse> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetWithRolesAndPermissionsAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("Utilisateur introuvable.");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new DomainException("Mot de passe actuel incorrect.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, cancellationToken);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var roles = user.Roles.Select(r => r.Role.Code).Distinct().ToList();
        var permissions = user.Roles
            .SelectMany(r => r.Role.Permissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        if (roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase))
        {
            permissions = permissions.Union(Shared.Constants.Permissions.All).Distinct().ToList();
        }

        var accessToken = _tokenService.GenerateAccessToken(
            user.Id, user.SchoolId, user.UserName, $"{user.FirstName} {user.LastName}", roles, permissions);

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
            MapProfile(user, roles, permissions));
    }

    private static UserProfileDto MapProfile(
        UserAccount user,
        IReadOnlyList<string>? roles = null,
        IReadOnlyList<string>? permissions = null)
    {
        roles ??= user.Roles.Select(r => r.Role.Code).ToList();
        permissions ??= user.Roles
            .SelectMany(r => r.Role.Permissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToList();

        return new UserProfileDto(
            user.Id,
            user.SchoolId,
            user.UserName,
            user.Email,
            $"{user.FirstName} {user.LastName}",
            user.MustChangePassword,
            roles,
            permissions);
    }

    private async Task LogLoginAsync(
        Guid? userId,
        string userName,
        bool success,
        string? failureReason,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        _context.LoginHistory.Add(new LoginHistory
        {
            UserId = userId,
            UserName = userName,
            IsSuccessful = success,
            FailureReason = failureReason,
            IpAddress = ipAddress,
            LoginAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
    }
}
