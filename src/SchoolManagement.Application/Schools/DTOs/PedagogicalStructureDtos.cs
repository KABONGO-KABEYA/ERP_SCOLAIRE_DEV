namespace SchoolManagement.Application.Schools.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record PedagogicalClassDto(
    Guid Id,
    string TemplateCode,
    SchoolProgram Program,
    string ProgramLabel,
    int LevelOrder,
    string DisplayName,
    string? HumanitiesSection,
    string? StudyOption,
    int? MinAge,
    int? MaxAge,
    bool IsEnabled,
    int LocalCount);

public sealed record ClassLocalDto(
    Guid Id,
    Guid PedagogicalClassId,
    Guid AcademicYearId,
    string PedagogicalClassName,
    string LocalName,
    string Code,
    string FullDisplayName,
    int? MaxCapacity,
    string? Observations,
    bool IsActive);

public sealed record UpdatePedagogicalClassRequest(
    bool IsEnabled,
    int? MinAge,
    int? MaxAge);

public sealed record BulkUpdatePedagogicalClassesRequest(
    IReadOnlyList<BulkPedagogicalClassItem> Classes);

public sealed record BulkPedagogicalClassItem(
    Guid Id,
    bool IsEnabled,
    int? MinAge,
    int? MaxAge);

public sealed record CreateClassLocalRequest(
    Guid PedagogicalClassId,
    Guid AcademicYearId,
    string LocalName,
    int? MaxCapacity,
    string? Observations);

public sealed record UpdateClassLocalRequest(
    string LocalName,
    int? MaxCapacity,
    string? Observations,
    bool IsActive);

public sealed record PedagogicalStructureSummaryDto(
    int TotalClasses,
    int EnabledClasses,
    int TotalLocals);
