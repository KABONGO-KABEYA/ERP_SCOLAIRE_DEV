using SchoolManagement.Application.CurrencyManagement.DTOs;

namespace SchoolManagement.Application.CurrencyManagement.Interfaces;

/// <summary>
/// Point d'entrée unique pour devises et conversions — aucun écran/contrôleur ne convertit hors de ce service.
/// </summary>
public interface ICurrencyService
{
    Task<IReadOnlyList<CurrencyDefinitionDto>> SearchCurrenciesAsync(
        string? search = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<CurrencyDefinitionDto> CreateCurrencyAsync(
        SaveCurrencyDefinitionRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CurrencyDefinitionDto> UpdateCurrencyAsync(
        Guid id,
        SaveCurrencyDefinitionRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SetCurrencyActiveAsync(Guid id, bool isActive, Guid userId, CancellationToken cancellationToken = default);

    Task<CurrencyDefinitionDto> GetMainCurrencyAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolCurrencyDto>> GetAllowedCurrenciesAsync(
        Guid schoolId,
        bool paymentOnly = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolCurrencyDto>> GetSchoolCurrenciesAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<SchoolCurrencyDto> UpsertSchoolCurrencyAsync(
        Guid schoolId,
        SaveSchoolCurrencyRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task RemoveSchoolCurrencyAsync(Guid schoolId, Guid schoolCurrencyId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExchangeRateTypeDto>> GetRateTypesAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<ExchangeRateTypeDto> CreateRateTypeAsync(
        SaveExchangeRateTypeRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ExchangeRateTypeDto> UpdateRateTypeAsync(
        Guid id,
        SaveExchangeRateTypeRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SetRateTypeActiveAsync(Guid id, bool isActive, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExchangeRateDto>> SearchExchangeRatesAsync(
        Guid? sourceCurrencyId = null,
        Guid? targetCurrencyId = null,
        Guid? rateTypeId = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<ExchangeRateDto> GetRateByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ExchangeRateDto?> GetActiveExchangeRateAsync(
        Guid sourceCurrencyId,
        Guid targetCurrencyId,
        Guid? rateTypeId = null,
        CancellationToken cancellationToken = default);

    Task<ExchangeRateDto?> GetRateForDateAsync(
        Guid sourceCurrencyId,
        Guid targetCurrencyId,
        DateOnly asOfDate,
        Guid? rateTypeId = null,
        CancellationToken cancellationToken = default);

    Task<ExchangeRateDto> CreateExchangeRateAsync(
        SaveExchangeRateRequest request,
        Guid userId,
        string? machineName,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ExchangeRateDto> UpdateExchangeRateAsync(
        Guid id,
        SaveExchangeRateRequest request,
        Guid userId,
        string? machineName,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task ActivateExchangeRateAsync(
        Guid id,
        Guid userId,
        string? machineName,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task SoftDeleteExchangeRateAsync(
        Guid id,
        Guid userId,
        string? machineName,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExchangeRateHistoryDto>> GetExchangeRateHistoryAsync(
        Guid? exchangeRateId = null,
        int take = 200,
        CancellationToken cancellationToken = default);

    Task ValidateCurrencyAsync(Guid currencyId, CancellationToken cancellationToken = default);

    Task ValidateExchangeRateAsync(Guid exchangeRateId, CancellationToken cancellationToken = default);

    Task<CurrencyConversionResultDto> ConvertAsync(
        CurrencyConversionRequest request,
        CancellationToken cancellationToken = default);

    Task<CurrencyConversionResultDto> CalculateEquivalentAsync(
        CurrencyConversionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Pont vers l'enum historique Currency (CDF/USD) pour rétrocompatibilité.</summary>
    Task<CurrencyDefinitionDto?> ResolveByEnumCodeAsync(
        string currencyCode,
        CancellationToken cancellationToken = default);
}
