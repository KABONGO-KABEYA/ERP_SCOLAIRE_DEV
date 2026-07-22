namespace SchoolManagement.Domain.Enums;

/// <summary>Priorité d'une unité de synchronisation local → cloud.</summary>
public enum SyncPriority
{
    /// <summary>Paiements, encaissements, reçus — drain quasi immédiat.</summary>
    Critical = 1,

    /// <summary>Données métier standards (élèves, classes…).</summary>
    Normal = 5,

    /// <summary>Paramètres / référentiels peu urgents.</summary>
    Low = 10
}

/// <summary>État d'une unité ou d'un item dans la file outbox.</summary>
public enum SyncOutboxStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    DeadLetter = 5
}

/// <summary>Type d'opération à appliquer sur le cloud.</summary>
public enum SyncOperationType
{
    Insert = 1,
    Update = 2,
    Delete = 3
}
