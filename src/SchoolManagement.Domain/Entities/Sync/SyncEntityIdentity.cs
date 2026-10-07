namespace SchoolManagement.Domain.Entities.Sync;

/// <summary>Correspondances locales vers une base cloud ; métadonnées, jamais synchronisées.</summary>
public sealed class SyncEntityIdentity
{
    public string RemoteKey { get; set; } = string.Empty;
    public Guid SchoolId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public Guid LocalId { get; set; }
    public Guid CloudId { get; set; }
}
