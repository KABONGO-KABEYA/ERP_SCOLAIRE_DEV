using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Domain.Entities.Sync;

/// <summary>
/// Unité transactionnelle de sync local → cloud.
/// Tous les items d'une unité sont poussés dans une seule transaction distante.
/// </summary>
public class SyncOutboxUnit : AuditableEntity, IAggregateRoot
{
    public Guid? SchoolId { get; set; }

    /// <summary>Ex. Payment, Student, Enrollment, GenericBatch.</summary>
    public string AggregateType { get; set; } = "GenericBatch";

    /// <summary>Id de l'agrégat métier (PaymentId, StudentId…) si applicable.</summary>
    public Guid? AggregateId { get; set; }

    public SyncPriority Priority { get; set; } = SyncPriority.Normal;

    public SyncOutboxStatus Status { get; set; } = SyncOutboxStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public string? LastError { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Nombre d'items attendus (intégrité).</summary>
    public int ExpectedItemCount { get; set; }

    public ICollection<SyncOutboxItem> Items { get; set; } = [];
}

/// <summary>Ligne d'outbox : une entité à synchroniser.</summary>
public class SyncOutboxItem : AuditableEntity
{
    public Guid UnitId { get; set; }

    public string TableName { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public SyncOperationType Operation { get; set; }

    public SyncOutboxStatus Status { get; set; } = SyncOutboxStatus.Pending;

    /// <summary>Ordre d'application au sein de l'unité (FK parents d'abord).</summary>
    public int Sequence { get; set; }

    public string? LastError { get; set; }

    public SyncOutboxUnit Unit { get; set; } = null!;
}

/// <summary>Journal d'une exécution de synchronisation (diagnostic).</summary>
public class SyncJournalEntry : AuditableEntity, IAggregateRoot
{
    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public int DurationMs { get; set; }

    public bool Success { get; set; }

    public bool Skipped { get; set; }

    public int UnitsAttempted { get; set; }

    public int UnitsSucceeded { get; set; }

    public int UnitsFailed { get; set; }

    public int RecordsSent { get; set; }

    public int RecordsSucceeded { get; set; }

    public int RecordsFailed { get; set; }

    public string? TablesTouched { get; set; }

    public string? ErrorSummary { get; set; }

    public string? DetailJson { get; set; }
}

/// <summary>Filigrane catch-up par table (sécurité si outbox a manqué un changement).</summary>
public class SyncWatermark : AuditableEntity, IAggregateRoot
{
    public string TableName { get; set; } = string.Empty;

    public DateTime LastSyncedAt { get; set; } = DateTime.UnixEpoch;

    public Guid? LastSyncedEntityId { get; set; }
}
