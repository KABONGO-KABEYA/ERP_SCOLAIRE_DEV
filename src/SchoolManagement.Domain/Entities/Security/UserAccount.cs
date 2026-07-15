using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Security;

public class UserAccount : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public Guid? AddressId { get; set; }

    public PostalAddress? ResidenceAddress { get; set; }

    public Guid? TeacherId { get; set; }

    public Guid? GuardianId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserRoleAssignment> Roles { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

public class Role : AuditableEntity, IAggregateRoot
{
    public Guid SchoolId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public UserRole SystemRole { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = [];

    public ICollection<UserRoleAssignment> Users { get; set; } = [];
}

public class Permission : AuditableEntity, IAggregateRoot
{
    public string Code { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public PermissionAction Action { get; set; }

    public string Description { get; set; } = string.Empty;

    public ICollection<RolePermission> Roles { get; set; } = [];
}

public class RolePermission : AuditableEntity
{
    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public Role Role { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}

public class UserRoleAssignment : AuditableEntity
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public UserAccount User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}

public class AuditEntry : AuditableEntity, IAggregateRoot
{
    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class LoginHistory : AuditableEntity, IAggregateRoot
{
    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public bool IsSuccessful { get; set; }

    public string? FailureReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime LoginAt { get; set; } = DateTime.UtcNow;
}

public class RefreshToken : AuditableEntity, IAggregateRoot
{
    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; }

    public UserAccount User { get; set; } = null!;
}