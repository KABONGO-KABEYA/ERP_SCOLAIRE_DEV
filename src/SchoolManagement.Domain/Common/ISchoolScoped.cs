namespace SchoolManagement.Domain.Common;

/// <summary>
/// Entité appartenant à un établissement (isolation multi-tenant).
/// </summary>
public interface ISchoolScoped
{
    Guid SchoolId { get; set; }
}
