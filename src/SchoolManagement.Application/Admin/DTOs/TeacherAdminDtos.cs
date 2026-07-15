namespace SchoolManagement.Application.Admin.DTOs;

using SchoolManagement.Application.Geography.DTOs;

public sealed record TeacherAdminDto(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string FullName,
    string? Phone,
    string? Email,
    string? Specialization,
    DateOnly? HireDate,
    bool IsActive,
    Guid? AddressId,
    string? AddressLine);

public sealed record CreateTeacherAdminRequest(
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    string? Specialization,
    DateOnly? HireDate,
    AddressInputDto? ResidenceAddress);

public sealed record UpdateTeacherAdminRequest(
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    string? Specialization,
    DateOnly? HireDate,
    bool IsActive,
    AddressInputDto? ResidenceAddress,
    bool UpdateAddress = false);
