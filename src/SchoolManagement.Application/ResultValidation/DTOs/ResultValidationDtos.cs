using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.ResultValidation.DTOs;

public sealed record ResultValidationActionRequest(
    Guid AcademicYearId,
    Guid ClassRoomId,
    Guid AcademicPeriodId,
    string? Observations);

public sealed record ResultValidationSheetDto(
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid ClassRoomId,
    string ClassDisplayName,
    Guid AcademicPeriodId,
    string PeriodLabel,
    ResultValidationStatus Status,
    string StatusLabel,
    ResultValidationSummaryDto Summary,
    IReadOnlyList<ResultValidationStudentRowDto> Students,
    IReadOnlyList<ResultValidationEventDto> Events,
    ResultValidationReadinessDto Readiness,
    bool CanValidate,
    bool CanCancelValidation,
    bool CanLock,
    bool CanUnlock);

public sealed record ResultValidationSummaryDto(
    int StudentCount,
    int AdmittedCount,
    int DeferredCount,
    int ExcludedCount,
    int PendingDecisionCount,
    decimal? ClassAverage,
    string ClassAverageDisplay,
    decimal? SuccessRatePercent,
    string SuccessRateDisplay,
    DateTime? CalculatedAtUtc,
    DateTime? LastUpdatedAtUtc);

public sealed record ResultValidationStudentRowDto(
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    int Rank,
    decimal Average,
    decimal Percentage,
    string AverageDisplay,
    string PercentageDisplay,
    string? Mention,
    ClassCouncilDecision Decision,
    string DecisionLabel,
    ResultValidationStatus ValidationStatus,
    string ValidationStatusLabel);

public sealed record ResultValidationEventDto(
    Guid Id,
    ResultValidationOperation Operation,
    string OperationLabel,
    DateTime OccurredAtUtc,
    string UserName,
    string? Observations);

public sealed record ResultValidationReadinessDto(
    bool IsReady,
    bool HasCalculatedResults,
    IReadOnlyList<ResultValidationIssueDto> Issues);

public sealed record ResultValidationIssueDto(
    string Code,
    string Severity,
    string Message,
    Guid? StudentId = null,
    Guid? CourseId = null);
