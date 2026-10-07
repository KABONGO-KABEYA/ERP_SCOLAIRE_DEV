namespace SchoolManagement.Domain.Entities.Sync;

/// <summary>Destination courante dont la reprise complète est durablement enfilée.</summary>
public sealed class SyncDestination
{
    public Guid SchoolId { get; set; }
    public string RemoteKey { get; set; } = string.Empty;
    public DateTime PreparedAt { get; set; }
}
