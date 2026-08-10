using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Setup.DTOs;

public sealed record InitialSetupStatusDto(
    bool NeedsSetup,
    bool HasPermissions,
    string Message);

public sealed record CompleteInitialSetupRequest(
    string SchoolName,
    string? LegalName,
    string? Address,
    string? City,
    string? Province,
    string? Phone,
    string? Email,
    Currency DefaultCurrency,
    string? LogoFileName,
    string? LogoBase64,
    string AcademicYearLabel,
    DateOnly AcademicYearStart,
    DateOnly AcademicYearEnd,
    string AdminUserName,
    string AdminEmail,
    string AdminPassword,
    string AdminFirstName,
    string AdminLastName,
    IReadOnlyList<InitialFeeTypeRequest>? FeeTypes,
    IReadOnlyList<string>? InstallmentNames,
    IReadOnlyList<string>? PricingCategoryNames);

public sealed record InitialFeeTypeRequest(
    string Name,
    Currency Currency,
    bool IsMandatory);

public sealed record CompleteInitialSetupResultDto(
    Guid SchoolId,
    Guid AcademicYearId,
    Guid AdminUserId,
    string SchoolName,
    string AdminUserName,
    bool BootstrapSyncPending = false,
    string? BootstrapSyncMessage = null,
    Guid? EstablishmentCredentialId = null,
    int? EstablishmentCredentialVersion = null,
    string? EstablishmentQrPayload = null);
