namespace SchoolManagement.Application.Academic.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record SectionDto(Guid Id, string Code, string Name, EducationCycle Cycle);

public sealed record ClassRoomDto(
    Guid Id,
    string Code,
    string Name,
    string FullDisplayName,
    Guid AcademicYearId,
    Guid SectionId,
    string SectionName,
    Guid? PedagogicalClassId,
    int Level,
    int? MaxCapacity,
    string? Observations,
    bool IsActive);

public sealed record CreateClassRoomRequest(
    Guid AcademicYearId,
    Guid SectionId,
    string Code,
    string Name,
    int Level,
    int? MaxCapacity);

public sealed record CourseDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ClassRoomId,
    decimal Coefficient,
    int MaxScore);

public sealed record CreateCourseRequest(
    Guid? ClassRoomId,
    string Code,
    string Name,
    decimal Coefficient,
    int MaxScore);

public sealed record EnrollmentDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string RegistrationNumber,
    Guid ClassRoomId,
    string ClassName,
    Guid AcademicYearId,
    EnrollmentStatus Status,
    bool IsActive);

public sealed record CreateEnrollmentRequest(
    Guid StudentId,
    Guid AcademicYearId,
    Guid ClassRoomId,
    DateOnly EnrollmentDate);
