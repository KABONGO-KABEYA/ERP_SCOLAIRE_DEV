namespace SchoolManagement.Application.CourseConfiguration.DTOs;

public sealed record BranchOptionDto(
    Guid Id,
    string Name);

public sealed record AvailableCourseDto(
    Guid CourseId,
    string Code,
    string Name,
    Guid? BranchId,
    string? BranchName,
    int MaxPerPeriod);

public sealed record AvailableCourseBranchGroupDto(
    Guid? BranchId,
    string BranchName,
    IReadOnlyList<AvailableCourseDto> Courses);

public sealed record CourseConfigurationItemDto(
    Guid? AssignmentId,
    Guid CourseId,
    string CourseCode,
    string CourseName,
    Guid? BranchId,
    string? BranchName,
    Guid? TeacherId,
    string? TeacherName,
    bool IsActive,
    int MaxPerPeriod);

public sealed record CourseConfigurationDto(
    bool IsConfigured,
    bool IsPrimaryLevel,
    IReadOnlyList<CourseConfigurationItemDto> Items);

public sealed record SaveCourseConfigurationItemRequest(
    Guid CourseId,
    Guid? TeacherId,
    bool IsActive,
    int Maximum);

public sealed record SaveCourseConfigurationRequest(
    Guid AcademicYearId,
    Guid PedagogicalClassId,
    Guid ClassRoomId,
    IReadOnlyList<SaveCourseConfigurationItemRequest> Items);

public sealed record CreateCatalogCourseRequest(
    Guid PedagogicalClassId,
    string Name,
    Guid? BranchId,
    int MaxScore);

public sealed record CreateCatalogCourseResultDto(
    Guid CourseId,
    string Code,
    string Name,
    Guid? BranchId,
    string? BranchName,
    int MaxPerPeriod);
