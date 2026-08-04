using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Entities.ParentActivation;

/// <summary>Token d'activation parent (métier école — jamais stocké côté Bootstrap).</summary>
public sealed class ParentActivationToken : AuditableEntity
{
    public Guid SchoolId { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }

    public string? SuggestedUserName { get; set; }

    public Guid? IssuedByUserId { get; set; }
}
