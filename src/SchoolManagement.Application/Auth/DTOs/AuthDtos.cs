namespace SchoolManagement.Application.Auth.DTOs;

public sealed record LoginRequest(string UserName, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    UserProfileDto User);

public sealed record UserProfileDto(
    Guid Id,
    Guid SchoolId,
    string UserName,
    string Email,
    string FullName,
    bool MustChangePassword,
    Guid? TeacherId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
