using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Security;

/// <summary>Module fonctionnel global (catalogue plateforme).</summary>
public class SecurityModule : AuditableEntity, IAggregateRoot
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<SecurityFunction> Functions { get; set; } = [];
}

/// <summary>Fonction métier sous un module.</summary>
public class SecurityFunction : AuditableEntity, IAggregateRoot
{
    public Guid ModuleId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public SecurityModule Module { get; set; } = null!;

    public ICollection<SecurityPage> Pages { get; set; } = [];
}

/// <summary>Page navigable multi-canal.</summary>
public class SecurityPage : AuditableEntity, IAggregateRoot
{
    public Guid FunctionId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public string? RequiredPermissionCode { get; set; }

    public string? DesktopViewKey { get; set; }

    public string? WebRoute { get; set; }

    public string? MobileScreenKey { get; set; }

    public string? DeepLink { get; set; }

    public bool IsAvailableOnDesktop { get; set; } = true;

    public bool IsAvailableOnWeb { get; set; }

    public bool IsAvailableOnMobile { get; set; }

    public SecurityFunction Function { get; set; } = null!;

    public ICollection<SecurityAction> Actions { get; set; } = [];
}

/// <summary>Action UI/API rattachée à une page (sans FK inverse Permission).</summary>
public class SecurityAction : AuditableEntity, IAggregateRoot
{
    public Guid PageId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsAvailableOnDesktop { get; set; } = true;

    public bool IsAvailableOnWeb { get; set; }

    public bool IsAvailableOnMobile { get; set; }

    public SecurityPage Page { get; set; } = null!;

    public ICollection<Permission> Permissions { get; set; } = [];
}

/// <summary>Prérequis : PermissionId dépend de RequiresPermissionId.</summary>
public class PermissionDependency : AuditableEntity, IAggregateRoot
{
    public Guid PermissionId { get; set; }

    public Guid RequiresPermissionId { get; set; }

    public bool IsActive { get; set; } = true;

    public Permission Permission { get; set; } = null!;

    public Permission RequiresPermission { get; set; } = null!;
}

/// <summary>Exception Grant/Deny datée par utilisateur (school-scoped).</summary>
public class UserPermissionException : AuditableEntity, IAggregateRoot, ISchoolScoped
{
    public Guid SchoolId { get; set; }

    public Guid UserId { get; set; }

    public Guid PermissionId { get; set; }

    public PermissionExceptionEffect Effect { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime? ValidTo { get; set; }

    public string? Reason { get; set; }

    public Guid? GrantedByUserId { get; set; }

    public UserAccount User { get; set; } = null!;

    public Permission Permission { get; set; } = null!;

    public UserAccount? GrantedByUser { get; set; }
}

/// <summary>Journal d'audit dédié aux opérations de sécurité.</summary>
public class SecurityAuditLog : AuditableEntity, IAggregateRoot
{
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? SchoolId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string ActorUserName { get; set; } = string.Empty;

    public SecurityAuditActorKind ActorKind { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string? TargetEntityType { get; set; }

    public Guid? TargetEntityId { get; set; }

    public string? TargetUserName { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public Guid? CorrelationId { get; set; }
}
