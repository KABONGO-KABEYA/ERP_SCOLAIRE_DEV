namespace SchoolManagement.Desktop.Services;

using SchoolManagement.Application.Auth.DTOs;

public interface IAuthSessionService
{
    string? AccessToken { get; }

    string? RefreshToken { get; }

    UserProfileDto? CurrentUser { get; }

    bool IsAuthenticated { get; }

    /// <summary>Administrateur (rôle ADMIN ou permission admin.full).</summary>
    bool IsAdministrator { get; }

    void SetSession(AuthResponse response);

    void Clear();
}

public interface ISchoolApiService
{
    Task<SchoolManagement.Application.Schools.DTOs.SchoolDto?> GetCurrentSchoolAsync(CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.SchoolDto> UpdateSchoolAsync(
        SchoolManagement.Application.Schools.DTOs.UpdateSchoolRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.AcademicYearDto>> GetAcademicYearsAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.AcademicYearDto> CreateAcademicYearAsync(
        SchoolManagement.Application.Schools.DTOs.CreateAcademicYearRequest request,
        CancellationToken cancellationToken = default);

    Task SetCurrentAcademicYearAsync(
        Guid yearId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.SchoolLookupsDto> GetLookupsAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.SchoolRegulationDto> GetRegulationAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.SchoolRegulationDto> UpdateRegulationAsync(
        SchoolManagement.Application.Schools.DTOs.UpdateSchoolRegulationRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.PedagogicalStructureSummaryDto> GetPedagogicalSummaryAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto>> GetPedagogicalClassesAsync(
        string? search = null,
        SchoolManagement.Domain.Enums.SchoolProgram? program = null,
        bool? enabledOnly = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto> UpdatePedagogicalClassAsync(
        Guid classId,
        SchoolManagement.Application.Schools.DTOs.UpdatePedagogicalClassRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto>> BulkUpdatePedagogicalClassesAsync(
        SchoolManagement.Application.Schools.DTOs.BulkUpdatePedagogicalClassesRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.ClassLocalDto>> GetClassLocalsAsync(
        Guid pedagogicalClassId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.ClassLocalDto> CreateClassLocalAsync(
        SchoolManagement.Application.Schools.DTOs.CreateClassLocalRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.ClassLocalDto> UpdateClassLocalAsync(
        Guid localId,
        SchoolManagement.Application.Schools.DTOs.UpdateClassLocalRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteClassLocalAsync(Guid localId, CancellationToken cancellationToken = default);

    Task InitializePedagogicalStructureAsync(CancellationToken cancellationToken = default);
}

public interface IPaymentApiService
{
    Task<SchoolManagement.Application.Payments.DTOs.PaymentListDto> SearchAsync(
        SchoolManagement.Application.Payments.DTOs.PaymentSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Payments.DTOs.PaymentDto> CreateAsync(
        SchoolManagement.Application.Payments.DTOs.CreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Payments.DTOs.PaymentDetailDto> GetByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Payments.DTOs.FeeTypeStatementDto> GetFeeTypeStatementAsync(
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Payments.DTOs.FeeTypeStatementDto> GetFeeTypeStatementForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportFeeTypeStatementPdfAsync(
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportFeeTypeStatementPdfForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Payments.DTOs.StudentFinancialSummaryDto> GetStudentFinancialSummaryAsync(
        Guid studentId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Payments.DTOs.PaymentMutationGateDto> GetMutationGateAsync(
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        Guid paymentId,
        SchoolManagement.Application.Payments.DTOs.CancelPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Payments.DTOs.PaymentDetailDto> UpdateNotesAsync(
        Guid paymentId,
        SchoolManagement.Application.Payments.DTOs.UpdatePaymentNotesRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Payments.DTOs.PaymentDetailDto> UpdateAmountAsync(
        Guid paymentId,
        SchoolManagement.Application.Payments.DTOs.UpdatePaymentAmountRequest request,
        CancellationToken cancellationToken = default);
}

public interface IRevenueAllocationApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueDestinationDto>> GetDestinationsAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueDestinationDto> CreateDestinationAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.SaveRevenueDestinationRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueDestinationDto> UpdateDestinationAsync(
        Guid id,
        SchoolManagement.Application.RevenueAllocation.DTOs.SaveRevenueDestinationRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateDestinationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationKeyDto>> GetKeysAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationKeyDto> CreateKeyAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.CreateRevenueAllocationKeyRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationKeyDto> UpdateKeyAsync(
        Guid id,
        SchoolManagement.Application.RevenueAllocation.DTOs.UpdateRevenueAllocationKeyRequest request,
        CancellationToken cancellationToken = default);

    Task ActivateKeyAsync(Guid id, CancellationToken cancellationToken = default);

    Task CloseKeyAsync(Guid id, DateOnly? endDate = null, CancellationToken cancellationToken = default);

    Task DeactivateKeyAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteKeyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchResultDto> SearchEntriesAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.RevenueAllocation.DTOs.FeeTypeAllocationSummaryGroupDto>> GetAllocationSummaryByFeeTypeAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.RevenueAllocation.DTOs.AllocationCashFlowResultDto> GetAllocationCashFlowAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.RevenueAllocation.DTOs.WithholdingReportResultDto> GetWithholdingReportAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportExcelAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportPdfAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWithholdingApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Withholdings.DTOs.WithholdingTypeDto>> GetTypesAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingTypeDto> CreateTypeAsync(
        SchoolManagement.Application.Withholdings.DTOs.SaveWithholdingTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingTypeDto> UpdateTypeAsync(
        Guid id,
        SchoolManagement.Application.Withholdings.DTOs.SaveWithholdingTypeRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateTypeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationSearchResultDto> SearchConfigurationsAsync(
        SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationDto> CreateConfigurationAsync(
        SchoolManagement.Application.Withholdings.DTOs.SaveWithholdingConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationDto> UpdateConfigurationAsync(
        Guid id,
        SchoolManagement.Application.Withholdings.DTOs.SaveWithholdingConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateConfigurationAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteConfigurationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingCalculationResult> CalculateAsync(
        SchoolManagement.Application.Withholdings.DTOs.WithholdingCalculateRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportExcelAsync(
        SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportPdfAsync(
        SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICurrencyApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyDefinitionDto>> SearchCurrenciesAsync(
        string? search = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyDefinitionDto> CreateCurrencyAsync(
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveCurrencyDefinitionRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyDefinitionDto> UpdateCurrencyAsync(
        Guid id,
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveCurrencyDefinitionRequest request,
        CancellationToken cancellationToken = default);

    Task SetCurrencyActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.SchoolCurrencyDto>> GetSchoolCurrenciesAsync(
        bool paymentOnly = false,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.CurrencyManagement.DTOs.SchoolCurrencyDto> UpsertSchoolCurrencyAsync(
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveSchoolCurrencyRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveSchoolCurrencyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateTypeDto>> GetRateTypesAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateTypeDto> CreateRateTypeAsync(
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveExchangeRateTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateDto>> SearchExchangeRatesAsync(
        Guid? sourceCurrencyId = null,
        Guid? targetCurrencyId = null,
        Guid? rateTypeId = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateDto> CreateExchangeRateAsync(
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveExchangeRateRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateDto> UpdateExchangeRateAsync(
        Guid id,
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveExchangeRateRequest request,
        CancellationToken cancellationToken = default);

    Task ActivateExchangeRateAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeactivateExchangeRateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateHistoryDto>> GetHistoryAsync(
        Guid? exchangeRateId = null,
        int take = 200,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyConversionResultDto> ConvertAsync(
        SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyConversionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IStudentCardApiService
{
    Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDashboardDto> GetDashboardAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Shared.Models.PagedResult<SchoolManagement.Application.StudentCards.DTOs.StudentCardListItemDto>> SearchAsync(
        SchoolManagement.Application.StudentCards.DTOs.StudentCardSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> GetByIdAsync(
        Guid cardId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> CreateAsync(
        SchoolManagement.Application.StudentCards.DTOs.CreateStudentCardRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.BulkCreateStudentCardsResult> BulkCreateAsync(
        SchoolManagement.Application.StudentCards.DTOs.BulkCreateStudentCardsRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.PrintStudentCardsResult> PrintAsync(
        SchoolManagement.Application.StudentCards.DTOs.PrintStudentCardsRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> ReprintAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.ReprintStudentCardRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> RenewAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.RenewStudentCardRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> DeclareLostAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.DeclareCardIncidentRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> DeclareStolenAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.DeclareCardIncidentRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> DeactivateAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.DeactivateStudentCardRequest request,
        CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Guid cardId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.StudentCards.DTOs.CardTemplateDto>> ListTemplatesAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.CardTemplateDto> CreateTemplateAsync(
        SchoolManagement.Application.StudentCards.DTOs.SaveCardTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.CardTemplateDto> UpdateTemplateAsync(
        Guid templateId,
        SchoolManagement.Application.StudentCards.DTOs.SaveCardTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.CardSchoolSettingsDto> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.StudentCards.DTOs.CardSchoolSettingsDto> SaveSettingsAsync(
        SchoolManagement.Application.StudentCards.DTOs.SaveCardSchoolSettingsRequest request,
        CancellationToken cancellationToken = default);
}

public interface IFinanceApiService
{
    Task<SchoolManagement.Application.Finance.DTOs.StudentPaymentSituationSearchResultDto> SearchPaymentSituationsAsync(
        SchoolManagement.Application.Finance.DTOs.StudentPaymentSituationSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Finance.DTOs.StudentInstallmentPaymentPlanDto> GetInstallmentPaymentPlanAsync(
        Guid enrollmentId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Finance.DTOs.StudentPricingAssignmentSearchResultDto> SearchPricingAssignmentsAsync(
        SchoolManagement.Application.Finance.DTOs.StudentPricingAssignmentSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Finance.DTOs.StudentPricingAssignmentDto> UpdatePricingAssignmentAsync(
        Guid enrollmentId,
        SchoolManagement.Application.Finance.DTOs.UpdateEnrollmentPricingCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Finance.DTOs.PricingCategoryHistoryLineDto>> GetPricingCategoryHistoryAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Finance.DTOs.StudentApplicableFeesDto> GetApplicableFeesAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken = default);
}

public interface IGradeApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.EvaluationDto>> GetEvaluationsAsync(
        Guid classRoomId, Guid academicPeriodId, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Grades.DTOs.EvaluationDto> CreateEvaluationAsync(
        SchoolManagement.Application.Grades.DTOs.CreateEvaluationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.PeriodResultDto>> CalculateResultsAsync(
        SchoolManagement.Application.Grades.DTOs.CalculatePeriodResultsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.GradeEntryDto>> GetGradeEntriesAsync(
        Guid evaluationId,
        CancellationToken cancellationToken = default);

    Task SubmitGradesAsync(
        SchoolManagement.Application.Grades.DTOs.SubmitGradesRequest request,
        CancellationToken cancellationToken = default);
}

public interface IStudentApiService
{
    Task<SchoolManagement.Application.Students.DTOs.StudentListDto> SearchAsync(
        SchoolManagement.Application.Students.DTOs.StudentSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Students.DTOs.StudentProfileDto> GetProfileAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Students.DTOs.StudentDto> CreateAsync(
        SchoolManagement.Application.Students.DTOs.CreateStudentRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Students.DTOs.StudentDto> UpdateAsync(
        Guid studentId,
        SchoolManagement.Application.Students.DTOs.UpdateStudentRequest request,
        CancellationToken cancellationToken = default);

    Task WithdrawFromCurrentYearAsync(
        Guid studentId,
        SchoolManagement.Application.Students.DTOs.WithdrawFromCurrentYearRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Students.WithdrawalReasonsDto> GetWithdrawalReasonsAsync(
        CancellationToken cancellationToken = default);

    Task ExcludeFromCurrentYearAsync(Guid studentId, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Students.DTOs.StudentDossierFileDto>> ListDossierFilesAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);
}

public interface IEnrollmentWizardApiService
{
    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentPrerequisitesDto> GetPrerequisitesAsync(
        CancellationToken cancellationToken = default);

    Task<string> GenerateRegistrationNumberAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentStudentSearchResultDto>> SearchStudentsAsync(
        string search,
        bool forReinscription = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentGuardianSearchResultDto>> SearchGuardiansAsync(
        string search,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.StoredEnrollmentFileDto> StoreEnrollmentFileAsync(
        string lastName,
        string firstName,
        string registrationNumber,
        string academicYearLabel,
        string documentType,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentStructureOptionsDto> GetStructureOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.ClassCapacityDto> GetClassCapacityAsync(
        Guid classRoomId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentFeeSummaryDto> CalculateFeesAsync(
        Guid? pedagogicalClassId = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentValidationResultDto> ValidateAsync(
        SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentResultDto> CompleteAsync(
        SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentFormDocumentDto> GetEnrollmentFormAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.StudentDossierEditDto> GetStudentDossierForEditAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentValidationResultDto> ValidateStudentDossierUpdateAsync(
        Guid enrollmentId,
        SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.UpdateStudentDossierResultDto> UpdateStudentDossierAsync(
        Guid enrollmentId,
        SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default);
}

public interface IGeographyApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCountriesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetProvincesAsync(
        Guid countryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCitiesAsync(
        Guid provinceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCommunesAsync(
        Guid cityId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Geography.DTOs.AddressDto?> GetAddressAsync(
        Guid addressId,
        CancellationToken cancellationToken = default);
}

public interface IGeographyAdminApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCountriesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetProvincesAsync(
        Guid countryId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCitiesAsync(
        Guid provinceId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCommunesAsync(
        Guid cityId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Geography.DTOs.GeographyItemDto> SaveCountryAsync(
        SchoolManagement.Application.Geography.DTOs.UpsertGeographyItemRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Geography.DTOs.GeographyItemDto> SaveProvinceAsync(
        SchoolManagement.Application.Geography.DTOs.CreateProvinceRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Geography.DTOs.GeographyItemDto> SaveCityAsync(
        SchoolManagement.Application.Geography.DTOs.CreateCityRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Geography.DTOs.GeographyItemDto> SaveCommuneAsync(
        SchoolManagement.Application.Geography.DTOs.CreateCommuneRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default);

    Task DeactivateCountryAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeactivateProvinceAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeactivateCityAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeactivateCommuneAsync(Guid id, CancellationToken cancellationToken = default);

    Task<byte[]> DownloadImportTemplateAsync(CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Geography.DTOs.GeographyImportResultDto> ImportExcelAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

public interface IAcademicApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.SectionDto>> GetSectionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.ClassRoomDto>> GetClassRoomsAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Academic.DTOs.ClassRoomDto> CreateClassRoomAsync(
        SchoolManagement.Application.Academic.DTOs.CreateClassRoomRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.CourseDto>> GetCoursesAsync(
        Guid? classRoomId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Academic.DTOs.CourseDto> CreateCourseAsync(
        SchoolManagement.Application.Academic.DTOs.CreateCourseRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.EnrollmentDto>> GetEnrollmentsAsync(
        Guid? classRoomId = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Academic.DTOs.EnrollmentDto> CreateEnrollmentAsync(
        SchoolManagement.Application.Academic.DTOs.CreateEnrollmentRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDocumentApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Documents.DTOs.StudentDocumentDto>> ListAsync(
        Guid? studentId = null,
        CancellationToken cancellationToken = default);

    Task UploadAsync(Guid studentId, string documentType, string filePath, CancellationToken cancellationToken = default);

    Task DownloadAsync(Guid documentId, string destinationPath, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);
}

public interface IReportApiService
{
    Task<SchoolManagement.Application.Reports.DTOs.DashboardStatsDto> GetDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Reports.DTOs.EnrollmentByClassDto>> GetEnrollmentByClassAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Reports.DTOs.ClassAverageReportDto>> GetClassAveragesAsync(
        Guid? academicPeriodId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Reports.DTOs.FinancialSummaryDto> GetFinancialSummaryAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Reports.DTOs.RealizedReceiptsResultDto> GetRealizedReceiptsAsync(
        SchoolManagement.Application.Reports.DTOs.RealizedReceiptsRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportRealizedReceiptsPdfAsync(
        SchoolManagement.Application.Reports.DTOs.RealizedReceiptsRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportRealizedReceiptsExcelAsync(
        SchoolManagement.Application.Reports.DTOs.RealizedReceiptsRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Reports.DTOs.PaymentSituationReportResultDto> GetPaymentSituationReportAsync(
        SchoolManagement.Application.Reports.DTOs.PaymentSituationReportRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportPaymentSituationReportPdfAsync(
        SchoolManagement.Application.Reports.DTOs.PaymentSituationReportRequest request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportPaymentSituationReportExcelAsync(
        SchoolManagement.Application.Reports.DTOs.PaymentSituationReportRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAdminApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.UserAccountDto>> GetUsersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.RoleDto>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Admin.DTOs.UserAccountDto> CreateUserAsync(
        SchoolManagement.Application.Admin.DTOs.CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Admin.DTOs.UserAccountDto> UpdateUserAsync(
        Guid userId,
        SchoolManagement.Application.Admin.DTOs.UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Admin.DTOs.UserAccountDto> SetUserRolesAsync(
        Guid userId,
        SchoolManagement.Application.Admin.DTOs.SetUserRolesRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.TeacherAdminDto>> GetTeachersAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Admin.DTOs.TeacherAdminDto> CreateTeacherAsync(
        SchoolManagement.Application.Admin.DTOs.CreateTeacherAdminRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Admin.DTOs.TeacherAdminDto> UpdateTeacherAsync(
        Guid teacherId,
        SchoolManagement.Application.Admin.DTOs.UpdateTeacherAdminRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDocumentBrandingApiService
{
    Task<SchoolManagement.Application.DocumentBranding.DTOs.DocumentBrandingConfigurationDto> GetConfigurationAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.DocumentBranding.DTOs.DocumentBrandingLookupDto> GetLookupsAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolLogoDto> CreateLogoAsync(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolLogoRequest request,
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolLogoDto> UpdateLogoAsync(
        Guid logoId,
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolLogoRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default);

    Task DeleteLogoAsync(Guid logoId, CancellationToken cancellationToken = default);

    Task SetPrimaryLogoAsync(Guid logoId, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolDocumentHeaderDto> CreateHeaderAsync(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolDocumentHeaderRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolDocumentHeaderDto> UpdateHeaderAsync(
        Guid headerId,
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolDocumentHeaderRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default);

    Task DeleteHeaderAsync(Guid headerId, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolSignatureDto> CreateSignatureAsync(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolSignatureRequest request,
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolSignatureDto> UpdateSignatureAsync(
        Guid signatureId,
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolSignatureRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default);

    Task DeleteSignatureAsync(Guid signatureId, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolStampDto> CreateStampAsync(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolStampRequest request,
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolStampDto> UpdateStampAsync(
        Guid stampId,
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolStampRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default);

    Task DeleteStampAsync(Guid stampId, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolDocumentFooterDto> SaveFooterAsync(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolDocumentFooterRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISchoolFeeApiService
{
    Task<SchoolManagement.Application.SchoolFees.DTOs.SchoolFeeCatalogDto> GetCatalogAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeDto> CreateFeeTypeAsync(
        SchoolManagement.Application.SchoolFees.DTOs.CreateFeeTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeDto> UpdateFeeTypeAsync(
        Guid feeTypeId,
        SchoolManagement.Application.SchoolFees.DTOs.UpdateFeeTypeRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteFeeTypeAsync(Guid feeTypeId, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.FeePricingCategoryDto> CreatePricingCategoryAsync(
        SchoolManagement.Application.SchoolFees.DTOs.CreateFeePricingCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.FeePricingCategoryDto> UpdatePricingCategoryAsync(
        Guid categoryId,
        SchoolManagement.Application.SchoolFees.DTOs.UpdateFeePricingCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task DeletePricingCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.FeeInstallmentDto> CreateInstallmentAsync(
        SchoolManagement.Application.SchoolFees.DTOs.SaveFeeInstallmentRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.FeeInstallmentDto> UpdateInstallmentAsync(
        Guid installmentId,
        SchoolManagement.Application.SchoolFees.DTOs.SaveFeeInstallmentRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteInstallmentAsync(Guid installmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeInstallmentDto>> GetFeeTypeInstallmentsAsync(
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeInstallmentDto>> SaveFeeTypeInstallmentsAsync(
        Guid feeTypeId,
        SchoolManagement.Application.SchoolFees.DTOs.SaveFeeTypeInstallmentsRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.ClassFeeScheduleDto> GetScheduleAsync(
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid feePricingCategoryId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.SchoolFees.DTOs.ClassFeeScheduleSignatureDto>> GetScheduleSignaturesAsync(
        Guid academicYearId,
        Guid feePricingCategoryId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.ClassFeeScheduleDto> SaveScheduleAsync(
        SchoolManagement.Application.SchoolFees.DTOs.SaveClassFeeScheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.SaveClassFeeScheduleBulkResult> SaveScheduleBulkAsync(
        SchoolManagement.Application.SchoolFees.DTOs.SaveClassFeeScheduleBulkRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.CopyClassFeeScheduleResult> CopyScheduleFromPreviousAsync(
        SchoolManagement.Application.SchoolFees.DTOs.CopyClassFeeScheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.SchoolFees.DTOs.CopyClassFeeScheduleBulkResult> CopyScheduleFromPreviousBulkAsync(
        SchoolManagement.Application.SchoolFees.DTOs.CopyClassFeeScheduleBulkRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAccountingApiService
{
    Task<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestSearchResultDto> SearchExpenseRequestsAsync(
        SchoolManagement.Application.Accounting.DTOs.ExpenseSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Accounting.DTOs.ExpensePaymentSearchResultDto> SearchExpensePaymentsAsync(
        SchoolManagement.Application.Accounting.DTOs.ExpenseSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestDto> CreateExpenseRequestAsync(
        SchoolManagement.Application.Accounting.DTOs.CreateExpenseRequestRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestDto> SubmitExpenseRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestDto> ApproveExpenseRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Accounting.DTOs.ExpensePaymentDto> CreateExpensePaymentAsync(
        SchoolManagement.Application.Accounting.DTOs.CreateExpensePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Accounting.DTOs.ExpenseDestinationBalanceDto>> GetExpenseBalancesAsync(
        Guid academicYearId,
        CancellationToken cancellationToken = default);
}

public interface ICloudSyncApiService
{
    Task<SchoolManagement.Application.CloudSync.DTOs.CloudSyncStatusDto> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.CloudSync.DTOs.CloudSyncRunResultDto> SynchronizeNowAsync(
        bool criticalOnly = false,
        CancellationToken cancellationToken = default);
}
