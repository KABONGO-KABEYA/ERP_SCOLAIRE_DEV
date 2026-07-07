namespace SchoolManagement.Application.Auth.Interfaces;

using SchoolManagement.Application.Auth.DTOs;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
}

public interface ITokenService
{
    string GenerateAccessToken(Guid userId, Guid schoolId, string userName, string fullName, IEnumerable<string> roles, IEnumerable<string> permissions);

    DateTime GetAccessTokenExpiration();

    string GenerateRefreshToken();
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
