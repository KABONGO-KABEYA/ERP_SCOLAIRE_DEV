namespace SchoolManagement.Application.Students.DTOs;

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
    bool IsArchived)
{
    public string FullName => $"{LastName} {FirstName}";
}

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
    Guid? ClassRoomId,
    int Page = 1,
    int PageSize = 20);

public sealed class StudentListDto
{
    public required IReadOnlyList<StudentDto> Items { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
