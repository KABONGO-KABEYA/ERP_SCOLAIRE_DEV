namespace SchoolManagement.Application.Schools.DTOs;

using SchoolManagement.Domain.Enums;

public sealed record SchoolDto(
    Guid Id,
    string Name,
    string? LegalName,
    string? Address,
    string? City,
    string? Province,
    string? Phone,
    string? Email,
    Currency DefaultCurrency,
    Guid? DefaultFeeTypeId,
    string? DefaultFeeTypeName,
    bool IsActive);

public sealed record AcademicYearDto(
    Guid Id,
    Guid SchoolId,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsCurrent,
    bool IsClosed);

public sealed record UpdateSchoolRequest(
    string Name,
    string? LegalName,
    string? Address,
    string? City,
    string? Province,
    string? Phone,
    string? Email,
    Guid? DefaultFeeTypeId);

public sealed record CreateAcademicYearRequest(
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    bool SetAsCurrent);

public sealed record SchoolRegulationDto(
    string Content,
    DateTime? UpdatedAt);

public sealed record UpdateSchoolRegulationRequest(
    string Content);
