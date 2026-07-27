namespace SchoolManagement.Application.CurrencyManagement.DTOs;

public sealed record CurrencyDefinitionDto(
    Guid Id,
    string Code,
    string Name,
    string Symbol,
    int DecimalPlaces,
    bool IsSystemLocal,
    bool IsActive);

public sealed record SaveCurrencyDefinitionRequest(
    string Code,
    string Name,
    string Symbol,
    int DecimalPlaces = 2,
    bool IsSystemLocal = false,
    bool IsActive = true);

public sealed record SchoolCurrencyDto(
    Guid Id,
    Guid CurrencyId,
    string CurrencyCode,
    string CurrencyName,
    string Symbol,
    bool IsPrimary,
    bool AllowPayment);

public sealed record SaveSchoolCurrencyRequest(
    Guid CurrencyId,
    bool IsPrimary,
    bool AllowPayment = true);

public sealed record ExchangeRateTypeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record SaveExchangeRateTypeRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive = true);

public sealed record ExchangeRateDto(
    Guid Id,
    Guid SourceCurrencyId,
    string SourceCurrencyCode,
    Guid TargetCurrencyId,
    string TargetCurrencyCode,
    Guid RateTypeId,
    string RateTypeCode,
    string RateTypeName,
    DateOnly EffectiveDate,
    decimal Rate,
    bool IsActive,
    string? Notes);

public sealed record SaveExchangeRateRequest(
    Guid SourceCurrencyId,
    Guid TargetCurrencyId,
    Guid RateTypeId,
    DateOnly EffectiveDate,
    decimal Rate,
    bool IsActive = true,
    string? Notes = null);

public sealed record ExchangeRateHistoryDto(
    Guid Id,
    Guid ExchangeRateId,
    string SourceCurrencyCode,
    string TargetCurrencyCode,
    string RateTypeCode,
    decimal? OldRate,
    decimal NewRate,
    string Action,
    Guid? UserId,
    string? MachineName,
    string? IpAddress,
    DateTime OccurredAt);

public sealed record CurrencyConversionRequest(
    Guid SourceCurrencyId,
    Guid TargetCurrencyId,
    decimal Amount,
    Guid? RateTypeId = null,
    DateOnly? AsOfDate = null,
    decimal? OverrideRate = null);

public sealed record CurrencyConversionResultDto(
    Guid SourceCurrencyId,
    string SourceCurrencyCode,
    Guid TargetCurrencyId,
    string TargetCurrencyCode,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal AppliedRate,
    Guid? ExchangeRateId,
    DateOnly? EffectiveDate);
