namespace SchoolManagement.Application.Students.DTOs;

using SchoolManagement.Application.Students;
using SchoolManagement.Domain.Enums;

public sealed record StudentDto(
    Guid Id,
    string RegistrationNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    Gender Gender,
    DateOnly DateOfBirth,
    string? Phone,
    string? Email,
    bool IsArchived,
    bool IsEnrolledCurrentYear = false,
    string? CurrentYearClassName = null,
    EnrollmentStatus? CurrentYearStatus = null,
    string? WithdrawalReason = null,
    DateOnly? WithdrawalDate = null)
{
    public string FullName => $"{LastName} {FirstName}";
}

public sealed record StudentEnrollmentHistoryDto(
    Guid EnrollmentId,
    Guid AcademicYearId,
    string AcademicYearLabel,
    bool IsYearClosed,
    bool IsCurrentYear,
    string ClassDisplayName,
    string? SectionName,
    string? StudyOption,
    string LocalName,
    DateOnly EnrollmentDate,
    EnrollmentStatus Status,
    bool IsActive);

public sealed record StudentProfileDto(
    StudentDto Student,
    IReadOnlyList<StudentEnrollmentHistoryDto> Enrollments);

public sealed record CreateStudentRequest(
    string RegistrationNumber,
    string FirstName,
    string LastName,
    string? MiddleName,
    Gender Gender,
    DateOnly DateOfBirth,
    string? PlaceOfBirth,
    string? Address,
    string? Phone,
    string? Email);

public sealed record UpdateStudentRequest(
    string FirstName,
    string LastName,
    string? MiddleName,
    Gender Gender,
    DateOnly DateOfBirth,
    string? PlaceOfBirth,
    string? Address,
    string? Phone,
    string? Email);

public sealed record StudentSearchRequest(
    string? Search,
    Guid? AcademicYearId,
    Guid? SectionId,
    Guid? PedagogicalClassId,
    Guid? ClassRoomId,
    string? StudyOption,
    bool ApplyFilters = false,
    bool IncludeAll = false,
    bool IncludeInscrits = true,
    bool IncludeExcluded = false,
    bool IncludeAbandoned = false,
    int Page = 1,
    int PageSize = 20);

public sealed record WithdrawFromCurrentYearRequest(
    StudentWithdrawalType WithdrawalType,
    string ReasonCode,
    string? CustomReason = null);

public sealed class StudentListDto
{
    public required IReadOnlyList<StudentDto> Items { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
