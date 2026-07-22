namespace SchoolManagement.Application.CloudSync.DTOs;

public sealed record CloudSyncStatusDto(
    bool CloudConfigured,
    bool CloudEnabled,
    bool CloudReachable,
    string? CloudServer,
    DateTime? LastSuccessUtc,
    DateTime? LastAttemptUtc,
    string? LastMessage,
    int PendingUnits,
    int PendingCriticalUnits,
    int FailedUnits,
    int DeadLetterUnits,
    double? AverageDurationMs,
    IReadOnlyList<CloudSyncJournalLineDto> RecentJournal);

public sealed record CloudSyncJournalLineDto(
    Guid Id,
    DateTime StartedAt,
    int DurationMs,
    bool Success,
    bool Skipped,
    int UnitsSucceeded,
    int UnitsFailed,
    int RecordsSucceeded,
    int RecordsFailed,
    string? TablesTouched,
    string? ErrorSummary);

public sealed record CloudSyncRunResultDto(
    bool Skipped,
    bool Success,
    string Message,
    int UnitsSucceeded,
    int UnitsFailed,
    int RecordsSucceeded,
    int RecordsFailed,
    int DurationMs);
