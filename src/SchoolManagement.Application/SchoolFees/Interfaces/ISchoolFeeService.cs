namespace SchoolManagement.Application.SchoolFees.Interfaces;

using SchoolManagement.Application.SchoolFees.DTOs;

public interface ISchoolFeeService
{
    Task<SchoolFeeCatalogDto> GetCatalogAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeeTypeDto>> GetFeeTypesAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<FeeTypeDto> CreateFeeTypeAsync(Guid schoolId, CreateFeeTypeRequest request, CancellationToken cancellationToken = default);

    Task<FeeTypeDto> UpdateFeeTypeAsync(Guid schoolId, Guid feeTypeId, UpdateFeeTypeRequest request, CancellationToken cancellationToken = default);

    Task DeleteFeeTypeAsync(Guid schoolId, Guid feeTypeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeePricingCategoryDto>> GetPricingCategoriesAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<FeePricingCategoryDto> CreatePricingCategoryAsync(Guid schoolId, CreateFeePricingCategoryRequest request, CancellationToken cancellationToken = default);

    Task<FeePricingCategoryDto> UpdatePricingCategoryAsync(Guid schoolId, Guid categoryId, UpdateFeePricingCategoryRequest request, CancellationToken cancellationToken = default);

    Task DeletePricingCategoryAsync(Guid schoolId, Guid categoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeeInstallmentDto>> GetInstallmentsAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<FeeInstallmentDto> CreateInstallmentAsync(Guid schoolId, SaveFeeInstallmentRequest request, CancellationToken cancellationToken = default);

    Task<FeeInstallmentDto> UpdateInstallmentAsync(Guid schoolId, Guid installmentId, SaveFeeInstallmentRequest request, CancellationToken cancellationToken = default);

    Task DeleteInstallmentAsync(Guid schoolId, Guid installmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeeTypeInstallmentDto>> GetFeeTypeInstallmentsAsync(
        Guid schoolId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeeTypeInstallmentDto>> SaveFeeTypeInstallmentsAsync(
        Guid schoolId,
        Guid feeTypeId,
        SaveFeeTypeInstallmentsRequest request,
        CancellationToken cancellationToken = default);

    Task<ClassFeeScheduleDto> GetScheduleAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid feePricingCategoryId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassFeeScheduleSignatureDto>> GetScheduleSignaturesAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid feePricingCategoryId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<ClassFeeScheduleDto> SaveScheduleAsync(
        Guid schoolId,
        SaveClassFeeScheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<SaveClassFeeScheduleBulkResult> SaveScheduleBulkAsync(
        Guid schoolId,
        SaveClassFeeScheduleBulkRequest request,
        CancellationToken cancellationToken = default);

    Task<CopyClassFeeScheduleResult> CopyScheduleFromPreviousYearAsync(
        Guid schoolId,
        CopyClassFeeScheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<CopyClassFeeScheduleBulkResult> CopyScheduleFromPreviousYearBulkAsync(
        Guid schoolId,
        CopyClassFeeScheduleBulkRequest request,
        CancellationToken cancellationToken = default);

    Task<decimal> ResolveAnnualAmountAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);
}
