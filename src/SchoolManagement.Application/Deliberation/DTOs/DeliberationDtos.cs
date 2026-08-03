using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Deliberation.DTOs;

public sealed record DeliberationSheetDto(
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid ClassRoomId,
    string ClassDisplayName,
    Guid AcademicPeriodId,
    string PeriodLabel,
    ResultValidationStatus ValidationStatus,
    string ValidationStatusLabel,
    DateTime? ValidatedAtUtc,
    string? ValidatedByUserName,
    DeliberationPeriodContextDto PeriodContext,
    DeliberationSummaryDto Summary,
    IReadOnlyList<DeliberationStudentRowDto> Students,
    IReadOnlyList<ConductOptionDto> ConductOptions,
    IReadOnlyList<DeliberationCourseOptionDto> CourseOptions,
    DeliberationSpecialCasesDto SpecialCases);

/// <summary>Contexte UI dérivé automatiquement de la période (aucun choix utilisateur).</summary>
public sealed record DeliberationPeriodContextDto(
    DeliberationPeriodMode Mode,
    string ModeLabel,
    bool IsYearEnd,
    bool CanSetFinalDecision,
    bool CanOfferRepechage,
    bool CanAddBonusPoints,
    bool CanSetConduct,
    bool CanValidateClass,
    bool CanCancelValidation,
    bool IsReadOnly,
    IReadOnlyList<FinalCouncilDecisionOptionDto> AvailableDecisions);

public sealed record FinalCouncilDecisionOptionDto(
    FinalCouncilDecision Value,
    string Label);

public sealed record DeliberationSummaryDto(
    int StudentCount,
    int AdmittedCount,
    int DeferredCount,
    int ExcludedCount,
    int PendingDecisionCount,
    int MissingConductCount,
    decimal? ClassAverage,
    string ClassAverageDisplay,
    decimal? SuccessRatePercent,
    string SuccessRateDisplay);

public sealed record DeliberationStudentRowDto(
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    int Rank,
    decimal Average,
    decimal Percentage,
    string AverageDisplay,
    string PercentageDisplay,
    string? Mention,
    Guid? ConductDefinitionId,
    string? ConductLabel,
    ClassCouncilDecision ProposedDecision,
    string ProposedDecisionLabel,
    FinalCouncilDecision? FinalDecision,
    string FinalDecisionLabel,
    string? Observation,
    decimal BonusPointsTotal,
    ResultValidationStatus ValidationStatus,
    string ValidationStatusLabel);

public sealed record ConductOptionDto(Guid Id, string Label, int SortOrder);

public sealed record DeliberationSpecialCasesDto(
    IReadOnlyList<DeliberationSpecialCaseItemDto> Deferred,
    IReadOnlyList<DeliberationSpecialCaseItemDto> Excluded,
    IReadOnlyList<DeliberationSpecialCaseItemDto> JustifiedAbsence,
    IReadOnlyList<DeliberationSpecialCaseItemDto> UnjustifiedAbsence,
    IReadOnlyList<DeliberationSpecialCaseItemDto> ParticularDecision);

public sealed record DeliberationSpecialCaseItemDto(
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    string CategoryCode,
    string CategoryLabel,
    string Detail);

public sealed record DeliberationMinutesDto(
    Guid? Id,
    Guid AcademicYearId,
    Guid ClassRoomId,
    Guid AcademicPeriodId,
    string? GeneralObservations,
    string? CouncilDecisions,
    string? PedagogicalRecommendations,
    DateTime? RecordedAtUtc,
    string? RecordedByUserName,
    string RecordedAtDisplay,
    bool Exists);

public sealed record SaveDeliberationMinutesRequest(
    Guid AcademicYearId,
    Guid ClassRoomId,
    Guid AcademicPeriodId,
    string? GeneralObservations,
    string? CouncilDecisions,
    string? PedagogicalRecommendations);

public sealed record DeliberationCourseOptionDto(
    Guid CourseId,
    Guid? CourseAssignmentId,
    string CourseName,
    bool IsSelected);

public sealed record DeliberationDecisionDialogDto(
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    Guid ClassRoomId,
    string ClassDisplayName,
    Guid AcademicPeriodId,
    string PeriodLabel,
    Guid AcademicYearId,
    decimal Average,
    decimal Percentage,
    string AverageDisplay,
    string PercentageDisplay,
    string? Mention,
    ClassCouncilDecision ProposedDecision,
    string ProposedDecisionLabel,
    FinalCouncilDecision? FinalDecision,
    string FinalDecisionLabel,
    string? Observation,
    DateTime? DecidedAtUtc,
    string? DecidedByUserName,
    string DecidedAtDisplay,
    bool CanSetFinalDecision,
    bool CanOfferRepechage,
    IReadOnlyList<FinalCouncilDecisionOptionDto> AvailableDecisions,
    IReadOnlyList<DeliberationCourseOptionDto> Courses,
    IReadOnlyList<Guid> RemedialCourseIds,
    IReadOnlyList<DeliberationExemptionItemDto> Exemptions,
    string? ExemptionMotive,
    string? ExemptionObservation);

public sealed record DeliberationExemptionItemDto(
    Guid CourseId,
    string CourseName,
    string Motive,
    string? Observation);

public sealed record SaveDeliberationDecisionRequest(
    Guid AcademicYearId,
    Guid ClassRoomId,
    Guid AcademicPeriodId,
    Guid StudentId,
    FinalCouncilDecision FinalDecision,
    string? Observation,
    IReadOnlyList<Guid>? RemedialCourseIds,
    IReadOnlyList<Guid>? ExemptionCourseIds,
    string? ExemptionMotive,
    string? ExemptionObservation);

public sealed record SaveStudentConductRequest(
    Guid AcademicYearId,
    Guid ClassRoomId,
    Guid AcademicPeriodId,
    Guid StudentId,
    Guid ConductDefinitionId,
    string? Observation);

public sealed record SavePedagogicalBonusRequest(
    Guid AcademicYearId,
    Guid ClassRoomId,
    Guid AcademicPeriodId,
    Guid StudentId,
    Guid CourseId,
    Guid? CourseAssignmentId,
    decimal PointsAdded,
    string Motive);

public sealed record PedagogicalBonusDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid CourseId,
    string CourseName,
    decimal PointsAdded,
    string Motive,
    string RecordedByUserName,
    DateTime RecordedAtUtc,
    string RecordedAtDisplay);

/// <summary>Contexte d'ajout de points pour un élève (note avant / reste ajoutable).</summary>
public sealed record PedagogicalBonusDialogDto(
    Guid StudentId,
    string StudentName,
    decimal StudentBonusTotal,
    string StudentBonusTotalDisplay,
    decimal MaxPointsPerOperation,
    IReadOnlyList<PedagogicalBonusCourseContextDto> Courses);

public sealed record PedagogicalBonusCourseContextDto(
    Guid CourseId,
    Guid? CourseAssignmentId,
    string CourseName,
    decimal? BaseScore,
    decimal? CurrentScore,
    decimal? Maximum,
    string BaseScoreDisplay,
    string CurrentScoreDisplay,
    string MaximumDisplay,
    decimal ExistingBonusPoints,
    string ExistingBonusDisplay,
    decimal RemainingAddable,
    string RemainingAddableDisplay);

public sealed record ValidateDeliberationClassRequest(
    Guid AcademicYearId,
    Guid ClassRoomId,
    Guid AcademicPeriodId,
    string? Observation);

public sealed record ValidateDeliberationClassResultDto(
    bool Success,
    string Message,
    ResultValidationStatus ValidationStatus,
    string ValidationStatusLabel,
    Guid? RemedialPeriodId,
    string? RemedialPeriodName,
    int RemedialStudentCount);
