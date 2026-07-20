namespace SchoolManagement.Application.Finance.DTOs;

using SchoolManagement.Domain.Enums;

public enum PaymentSituationStatus
{
    AJour = 1,
    EnRetard = 2,
    Impaye = 3,
    Credit = 4
}

public sealed record StudentPaymentSituationDto(
    Guid EnrollmentId,
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    string GenderLabel,
    string ClassName,
    string? SectionName,
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid FeePricingCategoryId,
    string FeePricingCategoryCode,
    string FeePricingCategoryName,
    Guid? FeeTypeId,
    string FeeTypeCode,
    string FeeTypeName,
    decimal AmountPaid,
    decimal AmountExpected,
    decimal Balance,
    PaymentSituationStatus PaymentStatus,
    string PaymentStatusLabel,
    Currency Currency,
    string? PhotoPath = null)
{
    public string AmountPaidExpectedDisplay => $"{AmountPaid:N0} / {AmountExpected:N0}";

    public string BalanceDisplay => Balance.ToString("N0");

    public bool HasOutstandingBalance => Balance > 0;

    public bool IsCredit => PaymentStatus == PaymentSituationStatus.Credit;
}

public sealed record StudentPaymentSituationSearchRequest(
    Guid? AcademicYearId = null,
    Guid? SectionId = null,
    Guid? PedagogicalClassId = null,
    Guid? ClassRoomId = null,
    Guid? FeePricingCategoryId = null,
    Guid? FeeTypeId = null,
    PaymentSituationStatus? PaymentStatus = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50);

public sealed record StudentPaymentSituationSearchResultDto(
    IReadOnlyList<StudentPaymentSituationDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record StudentPricingAssignmentDto(
    Guid EnrollmentId,
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    string ClassName,
    string? SectionName,
    Guid AcademicYearId,
    string AcademicYearLabel,
    Guid FeePricingCategoryId,
    string FeePricingCategoryCode,
    string FeePricingCategoryName,
    DateOnly AssignedAt,
    DateTime? UpdatedAt,
    Guid? PedagogicalClassId = null)
{
    public string AssignedAtDisplay => AssignedAt.ToString("dd/MM/yyyy");

    public string PricingCategoryName => FeePricingCategoryName;
}

public sealed record StudentPricingAssignmentSearchRequest(
    Guid? AcademicYearId = null,
    Guid? SectionId = null,
    Guid? PedagogicalClassId = null,
    Guid? ClassRoomId = null,
    Guid? FeePricingCategoryId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50);

public sealed record StudentPricingAssignmentSearchResultDto(
    IReadOnlyList<StudentPricingAssignmentDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record UpdateEnrollmentPricingCategoryRequest(Guid FeePricingCategoryId, string? Notes = null);

public sealed record PricingCategoryHistoryLineDto(
    DateTime ChangedAt,
    string? PreviousCategoryName,
    string NewCategoryName,
    string? Notes);

public sealed record StudentApplicableFeeLineDto(
    string FeeTypeName,
    string InstallmentName,
    int SortOrder,
    decimal Amount,
    string Currency);

public sealed record StudentApplicableFeesDto(
    Guid EnrollmentId,
    string StudentName,
    string ClassName,
    string PricingCategoryName,
    string AcademicYearLabel,
    IReadOnlyList<StudentApplicableFeeLineDto> Lines,
    decimal TotalAmount,
    string Currency);

public sealed record InstallmentPaymentPlanLineDto(
    Guid FeeInstallmentId,
    string InstallmentName,
    int SortOrder,
    decimal AmountExpected,
    decimal AmountPaid,
    decimal Remaining,
    DateOnly? DueDate);

public sealed record StudentInstallmentPaymentPlanDto(
    Guid StudentId,
    Guid EnrollmentId,
    Guid AcademicYearId,
    Guid FeeTypeId,
    string FeeTypeName,
    Guid FeePricingCategoryId,
    Guid? PedagogicalClassId,
    Currency Currency,
    IReadOnlyList<InstallmentPaymentPlanLineDto> Lines);
