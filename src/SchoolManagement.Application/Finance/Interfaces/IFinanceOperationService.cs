namespace SchoolManagement.Application.Finance.Interfaces;

using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.Reports.DTOs;

public interface IFinanceOperationService
{
    Task<StudentPaymentSituationSearchResultDto> SearchPaymentSituationsAsync(
        Guid schoolId,
        StudentPaymentSituationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentSituationReportResultDto> GetPaymentSituationReportAsync(
        Guid schoolId,
        PaymentSituationReportRequest request,
        CancellationToken cancellationToken = default);

    Task<StudentInstallmentPaymentPlanDto> GetInstallmentPaymentPlanAsync(
        Guid schoolId,
        Guid enrollmentId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<StudentPricingAssignmentSearchResultDto> SearchPricingAssignmentsAsync(
        Guid schoolId,
        StudentPricingAssignmentSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<StudentPricingAssignmentDto> UpdateEnrollmentPricingCategoryAsync(
        Guid schoolId,
        Guid enrollmentId,
        UpdateEnrollmentPricingCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PricingCategoryHistoryLineDto>> GetPricingCategoryHistoryAsync(
        Guid schoolId,
        Guid enrollmentId,
        CancellationToken cancellationToken = default);

    Task<StudentApplicableFeesDto> GetApplicableFeesAsync(
        Guid schoolId,
        Guid enrollmentId,
        CancellationToken cancellationToken = default);
}
