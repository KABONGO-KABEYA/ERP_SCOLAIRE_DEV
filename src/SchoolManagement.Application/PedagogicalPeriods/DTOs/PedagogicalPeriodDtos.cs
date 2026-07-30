namespace SchoolManagement.Application.PedagogicalPeriods.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record PedagogicalPeriodStructureDto(
    Guid AcademicYearId,
    string AcademicYearLabel,
    IReadOnlyList<PedagogicalCycleStructureDto> Cycles);

public sealed record PedagogicalCycleStructureDto(
    PedagogicalCycleGroup CycleGroup,
    string CycleGroupLabel,
    IReadOnlyList<PedagogicalMainPeriodDto> MainPeriods);

public sealed record PedagogicalMainPeriodDto(
    Guid Id,
    string Name,
    AcademicPeriodType PeriodType,
    int OrderIndex,
    IReadOnlyList<PedagogicalSubPeriodDto> SubPeriods);

public sealed record PedagogicalSubPeriodDto(
    Guid Id,
    Guid MainPeriodId,
    string Name,
    string MainPeriodName,
    PedagogicalCycleGroup CycleGroup,
    AcademicPeriodType PeriodType,
    AcademicSubPeriodKind Kind,
    string KindLabel,
    AcademicSubPeriodStatus Status,
    string StatusLabel,
    int OrderIndex,
    int SequenceIndex,
    int MaxScore,
    int? MaxEvaluationCount,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTime? OpenedAt,
    DateOnly? PlannedCloseDate,
    DateTime? ClosedAt,
    bool IsActive);

public sealed record ActiveSubPeriodDto(
    Guid Id,
    string Name,
    string MainPeriodName,
    PedagogicalCycleGroup CycleGroup,
    AcademicSubPeriodKind Kind,
    AcademicSubPeriodStatus Status,
    string StatusLabel,
    int MaxScore,
    int? MaxEvaluationCount,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime? OpenedAt,
    DateOnly? PlannedCloseDate);

public sealed record CreatePedagogicalStructureRequest(
    Guid AcademicYearId,
    bool ReplaceExisting = false);

public sealed record UpdateSubPeriodSettingsRequest(
    DateOnly StartDate,
    DateOnly EndDate);

/// <summary>Dates obligatoires à l'ouverture — absentes tant que la période est « À venir ».</summary>
public sealed record OpenSubPeriodRequest(
    DateOnly StartDate,
    DateOnly EndDate);
