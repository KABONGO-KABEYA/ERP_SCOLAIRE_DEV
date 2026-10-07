namespace SchoolManagement.Application.ControllerControl.DTOs;

using SchoolManagement.Domain.Enums;

public enum ControllerCheckMode
{
    Annual = 1,
    Installment = 2
}

public enum ControllerPaymentStatus
{
    UpToDate = 1,
    Partial = 2,
    Unpaid = 3,
    Overdue = 4,
    Credit = 5
}

public sealed record ControllerAcademicYearDto(
    Guid Id,
    string Label,
    bool IsCurrent,
    bool IsClosed);

public sealed record ControllerFeeTypeDto(
    Guid Id,
    string Code,
    string Name,
    Currency Currency);

public sealed record ControllerSetupDto(
    IReadOnlyList<ControllerAcademicYearDto> AcademicYears,
    Guid? CurrentAcademicYearId,
    IReadOnlyList<ControllerFeeTypeDto> FeeTypes);

public sealed record ControllerInstallmentDto(
    Guid Id,
    string Name,
    int SortOrder);

public sealed record ControllerStudentSearchItemDto(
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    string ClassName,
    string? PhotoPath);

public sealed record ControllerStudentSearchResultDto(
    IReadOnlyList<ControllerStudentSearchItemDto> Items);

public sealed record ControllerCheckByQrRequest(
    string QrPayload,
    Guid AcademicYearId,
    Guid FeeTypeId,
    ControllerCheckMode Mode,
    Guid? FeeInstallmentId = null);

public sealed record ControllerCheckByStudentRequest(
    Guid StudentId,
    Guid AcademicYearId,
    Guid FeeTypeId,
    ControllerCheckMode Mode,
    Guid? FeeInstallmentId = null);

public sealed record ControllerCheckResultDto(
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    string ClassName,
    string? PhotoPath,
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid FeeTypeId,
    string FeeTypeName,
    Currency Currency,
    ControllerCheckMode Mode,
    Guid? FeeInstallmentId,
    string? InstallmentName,
    DateOnly? DueDate,
    decimal AmountExpected,
    decimal AmountPaid,
    decimal Balance,
    ControllerPaymentStatus Status,
    string StatusLabel,
    Guid? CardId,
    string? CardNumber);
