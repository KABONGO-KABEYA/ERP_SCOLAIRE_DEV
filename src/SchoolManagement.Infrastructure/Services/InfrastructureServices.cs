namespace SchoolManagement.Infrastructure.Services;

using SchoolManagement.Application.Common.Interfaces;

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class CurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; set; }

    public Guid? SchoolId { get; set; }

    public string? UserName { get; set; }

    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)
        || Permissions.Contains("admin.full", StringComparer.OrdinalIgnoreCase);

    public bool IsAdministrator =>
        HasPermission("admin.full")
        || Roles.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase)
            || r.Contains("ADMIN", StringComparison.OrdinalIgnoreCase));
}
