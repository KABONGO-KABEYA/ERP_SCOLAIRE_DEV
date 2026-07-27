using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.CurrencyManagement.DTOs;
using SchoolManagement.Application.CurrencyManagement.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.CurrencyManagement.Services;

/// <summary>
/// Point d'entrée unique pour devises et conversions.
/// Aucun écran / contrôleur ne doit convertir hors de ce service.
/// </summary>
public sealed class CurrencyService : ICurrencyService
{
    private readonly IRepository<CurrencyDefinition> _currencies;
    private readonly IRepository<SchoolCurrency> _schoolCurrencies;
    private readonly IRepository<ExchangeRateType> _rateTypes;
    private readonly IRepository<ExchangeRate> _rates;
    private readonly IRepository<ExchangeRateHistory> _histories;
    private readonly IUnitOfWork _unitOfWork;

    public CurrencyService(
        IRepository<CurrencyDefinition> currencies,
        IRepository<SchoolCurrency> schoolCurrencies,
        IRepository<ExchangeRateType> rateTypes,
        IRepository<ExchangeRate> rates,
        IRepository<ExchangeRateHistory> histories,
        IUnitOfWork unitOfWork)
    {
        _currencies = currencies;
        _schoolCurrencies = schoolCurrencies;
        _rateTypes = rateTypes;
        _rates = rates;
        _histories = histories;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<CurrencyDefinitionDto>> SearchCurrenciesAsync(
        string? search = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _currencies.GetAllAsync(cancellationToken);
        IEnumerable<CurrencyDefinition> query = rows;
        if (activeOnly == true)
            query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Symbol.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderByDescending(x => x.IsSystemLocal)
            .ThenBy(x => x.Code)
            .Select(MapCurrency)
            .ToList();
    }

    public async Task<CurrencyDefinitionDto> CreateCurrencyAsync(
        SaveCurrencyDefinitionRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.Code);
        var existing = await _currencies.FindAsync(x => x.Code == code, cancellationToken);
        if (existing.Count > 0)
            throw new DomainException($"La devise '{code}' existe déjà.");

        if (request.IsSystemLocal)
            await ClearSystemLocalAsync(cancellationToken);

        var entity = new CurrencyDefinition
        {
            Code = code,
            Name = request.Name.Trim(),
            Symbol = string.IsNullOrWhiteSpace(request.Symbol) ? code : request.Symbol.Trim(),
            DecimalPlaces = Math.Clamp(request.DecimalPlaces, 0, 6),
            IsSystemLocal = request.IsSystemLocal,
            IsActive = request.IsActive,
            CreatedBy = userId == Guid.Empty ? null : userId
        };
        await _currencies.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapCurrency(entity);
    }

    public async Task<CurrencyDefinitionDto> UpdateCurrencyAsync(
        Guid id,
        SaveCurrencyDefinitionRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _currencies.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Devise introuvable.");

        var code = NormalizeCode(request.Code);
        var clash = await _currencies.FindAsync(x => x.Code == code && x.Id != id, cancellationToken);
        if (clash.Count > 0)
            throw new DomainException($"La devise '{code}' existe déjà.");

        if (request.IsSystemLocal && !entity.IsSystemLocal)
            await ClearSystemLocalAsync(cancellationToken);

        entity.Code = code;
        entity.Name = request.Name.Trim();
        entity.Symbol = string.IsNullOrWhiteSpace(request.Symbol) ? code : request.Symbol.Trim();
        entity.DecimalPlaces = Math.Clamp(request.DecimalPlaces, 0, 6);
        entity.IsSystemLocal = request.IsSystemLocal;
        entity.IsActive = request.IsActive;
        entity.UpdatedBy = userId == Guid.Empty ? null : userId;
        await _currencies.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapCurrency(entity);
    }

    public async Task SetCurrencyActiveAsync(Guid id, bool isActive, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _currencies.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Devise introuvable.");
        entity.IsActive = isActive;
        entity.UpdatedBy = userId == Guid.Empty ? null : userId;
        await _currencies.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<CurrencyDefinitionDto> GetMainCurrencyAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var schoolRows = await _schoolCurrencies.FindAsync(x => x.SchoolId == schoolId && x.IsPrimary, cancellationToken);
        var primaryLink = schoolRows.FirstOrDefault();
        if (primaryLink is not null)
        {
            var currency = await _currencies.GetByIdAsync(primaryLink.CurrencyId, cancellationToken);
            if (currency is not null)
                return MapCurrency(currency);
        }

        var all = await _currencies.GetAllAsync(cancellationToken);
        var systemLocal = all.FirstOrDefault(x => x.IsSystemLocal && x.IsActive)
            ?? all.FirstOrDefault(x => x.Code == "CDF" && x.IsActive)
            ?? throw new KeyNotFoundException("Aucune devise principale configurée.");
        return MapCurrency(systemLocal);
    }

    public async Task<IReadOnlyList<SchoolCurrencyDto>> GetAllowedCurrenciesAsync(
        Guid schoolId,
        bool paymentOnly = false,
        CancellationToken cancellationToken = default)
    {
        var links = await _schoolCurrencies.FindAsync(x => x.SchoolId == schoolId, cancellationToken);
        var currencies = (await _currencies.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);

        return links
            .Where(x => currencies.TryGetValue(x.CurrencyId, out var c) && c.IsActive)
            .Where(x => !paymentOnly || x.AllowPayment)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => currencies[x.CurrencyId].Code)
            .Select(x => MapSchoolCurrency(x, currencies[x.CurrencyId]))
            .ToList();
    }

    public Task<IReadOnlyList<SchoolCurrencyDto>> GetSchoolCurrenciesAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
        => GetAllowedCurrenciesAsync(schoolId, paymentOnly: false, cancellationToken);

    public async Task<SchoolCurrencyDto> UpsertSchoolCurrencyAsync(
        Guid schoolId,
        SaveSchoolCurrencyRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await ValidateCurrencyAsync(request.CurrencyId, cancellationToken);

        if (request.IsPrimary)
        {
            var others = await _schoolCurrencies.FindAsync(
                x => x.SchoolId == schoolId && x.IsPrimary && x.CurrencyId != request.CurrencyId,
                cancellationToken);
            foreach (var other in others)
            {
                var tracked = await _schoolCurrencies.GetByIdAsync(other.Id, cancellationToken);
                if (tracked is null) continue;
                tracked.IsPrimary = false;
                tracked.UpdatedBy = userId == Guid.Empty ? null : userId;
                await _schoolCurrencies.UpdateAsync(tracked, cancellationToken);
            }
        }

        var existingList = await _schoolCurrencies.FindAsync(
            x => x.SchoolId == schoolId && x.CurrencyId == request.CurrencyId,
            cancellationToken);
        var existing = existingList.FirstOrDefault();

        if (existing is null)
        {
            existing = new SchoolCurrency
            {
                SchoolId = schoolId,
                CurrencyId = request.CurrencyId,
                IsPrimary = request.IsPrimary,
                AllowPayment = request.AllowPayment,
                CreatedBy = userId == Guid.Empty ? null : userId
            };
            await _schoolCurrencies.AddAsync(existing, cancellationToken);
        }
        else
        {
            var tracked = await _schoolCurrencies.GetByIdAsync(existing.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Devise d'établissement introuvable.");
            tracked.IsPrimary = request.IsPrimary;
            tracked.AllowPayment = request.AllowPayment;
            tracked.UpdatedBy = userId == Guid.Empty ? null : userId;
            await _schoolCurrencies.UpdateAsync(tracked, cancellationToken);
            existing = tracked;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var currency = await _currencies.GetByIdAsync(existing.CurrencyId, cancellationToken)
            ?? throw new KeyNotFoundException("Devise introuvable.");
        return MapSchoolCurrency(existing, currency);
    }

    public async Task RemoveSchoolCurrencyAsync(Guid schoolId, Guid schoolCurrencyId, CancellationToken cancellationToken = default)
    {
        var entity = await _schoolCurrencies.GetByIdAsync(schoolCurrencyId, cancellationToken)
            ?? throw new KeyNotFoundException("Devise d'établissement introuvable.");
        if (entity.SchoolId != schoolId)
            throw new KeyNotFoundException("Devise d'établissement introuvable.");
        if (entity.IsPrimary)
            throw new DomainException("Impossible de retirer la devise principale. Définissez d'abord une autre devise principale.");

        await _schoolCurrencies.DeleteAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExchangeRateTypeDto>> GetRateTypesAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var rows = await _rateTypes.GetAllAsync(cancellationToken);
        IEnumerable<ExchangeRateType> query = rows;
        if (activeOnly)
            query = query.Where(x => x.IsActive);
        return query.OrderBy(x => x.Name).Select(MapRateType).ToList();
    }

    public async Task<ExchangeRateTypeDto> CreateRateTypeAsync(
        SaveExchangeRateTypeRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.Code);
        if ((await _rateTypes.FindAsync(x => x.Code == code, cancellationToken)).Count > 0)
            throw new DomainException($"Le type de taux '{code}' existe déjà.");

        var entity = new ExchangeRateType
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive,
            CreatedBy = userId == Guid.Empty ? null : userId
        };
        await _rateTypes.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapRateType(entity);
    }

    public async Task<ExchangeRateTypeDto> UpdateRateTypeAsync(
        Guid id,
        SaveExchangeRateTypeRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _rateTypes.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Type de taux introuvable.");
        var code = NormalizeCode(request.Code);
        if ((await _rateTypes.FindAsync(x => x.Code == code && x.Id != id, cancellationToken)).Count > 0)
            throw new DomainException($"Le type de taux '{code}' existe déjà.");

        entity.Code = code;
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedBy = userId == Guid.Empty ? null : userId;
        await _rateTypes.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapRateType(entity);
    }

    public async Task SetRateTypeActiveAsync(Guid id, bool isActive, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _rateTypes.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Type de taux introuvable.");
        entity.IsActive = isActive;
        entity.UpdatedBy = userId == Guid.Empty ? null : userId;
        await _rateTypes.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExchangeRateDto>> SearchExchangeRatesAsync(
        Guid? sourceCurrencyId = null,
        Guid? targetCurrencyId = null,
        Guid? rateTypeId = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var rates = await _rates.GetAllAsync(cancellationToken);
        IEnumerable<ExchangeRate> query = rates;
        if (sourceCurrencyId.HasValue)
            query = query.Where(x => x.SourceCurrencyId == sourceCurrencyId);
        if (targetCurrencyId.HasValue)
            query = query.Where(x => x.TargetCurrencyId == targetCurrencyId);
        if (rateTypeId.HasValue)
            query = query.Where(x => x.RateTypeId == rateTypeId);
        if (activeOnly == true)
            query = query.Where(x => x.IsActive);

        var currencies = (await _currencies.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);
        var types = (await _rateTypes.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);

        return query
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.EffectiveDate)
            .ThenBy(x => currencies.GetValueOrDefault(x.SourceCurrencyId)?.Code ?? string.Empty)
            .Select(x => MapRate(x, currencies, types))
            .Where(x => x is not null)
            .Cast<ExchangeRateDto>()
            .ToList();
    }

    public async Task<ExchangeRateDto> GetRateByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _rates.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Taux de change introuvable.");
        var currencies = (await _currencies.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);
        var types = (await _rateTypes.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);
        return MapRate(entity, currencies, types)
            ?? throw new KeyNotFoundException("Taux de change introuvable.");
    }

    public async Task<ExchangeRateDto?> GetActiveExchangeRateAsync(
        Guid sourceCurrencyId,
        Guid targetCurrencyId,
        Guid? rateTypeId = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceCurrencyId == targetCurrencyId)
            return null;

        var direct = await FindActiveRateAsync(sourceCurrencyId, targetCurrencyId, rateTypeId, cancellationToken);
        if (direct is not null)
            return direct;

        // Inverse automatique : 1 USD = 2290 CDF ⇒ 1 CDF = 1/2290 USD
        var inverse = await FindActiveRateAsync(targetCurrencyId, sourceCurrencyId, rateTypeId, cancellationToken);
        if (inverse is null)
            return null;

        return InvertRate(inverse);
    }

    public async Task<ExchangeRateDto?> GetRateForDateAsync(
        Guid sourceCurrencyId,
        Guid targetCurrencyId,
        DateOnly asOfDate,
        Guid? rateTypeId = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceCurrencyId == targetCurrencyId)
            return null;

        var direct = await FindRateForDateAsync(sourceCurrencyId, targetCurrencyId, asOfDate, rateTypeId, cancellationToken);
        if (direct is not null)
            return direct;

        var inverse = await FindRateForDateAsync(targetCurrencyId, sourceCurrencyId, asOfDate, rateTypeId, cancellationToken);
        return inverse is null ? null : InvertRate(inverse);
    }

    private async Task<ExchangeRateDto?> FindActiveRateAsync(
        Guid sourceCurrencyId,
        Guid targetCurrencyId,
        Guid? rateTypeId,
        CancellationToken cancellationToken)
    {
        var rates = await _rates.FindAsync(
            x => x.IsActive &&
                 x.SourceCurrencyId == sourceCurrencyId &&
                 x.TargetCurrencyId == targetCurrencyId &&
                 (!rateTypeId.HasValue || x.RateTypeId == rateTypeId.Value),
            cancellationToken);

        var entity = rates.OrderByDescending(x => x.EffectiveDate).FirstOrDefault();
        if (entity is null)
            return null;

        var currencies = (await _currencies.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);
        var types = (await _rateTypes.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);
        return MapRate(entity, currencies, types);
    }

    private async Task<ExchangeRateDto?> FindRateForDateAsync(
        Guid sourceCurrencyId,
        Guid targetCurrencyId,
        DateOnly asOfDate,
        Guid? rateTypeId,
        CancellationToken cancellationToken)
    {
        var rates = await _rates.FindAsync(
            x => x.SourceCurrencyId == sourceCurrencyId &&
                 x.TargetCurrencyId == targetCurrencyId &&
                 x.EffectiveDate <= asOfDate &&
                 (!rateTypeId.HasValue || x.RateTypeId == rateTypeId.Value),
            cancellationToken);

        var entity = rates
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.EffectiveDate)
            .FirstOrDefault();
        if (entity is null)
            return null;

        var currencies = (await _currencies.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);
        var types = (await _rateTypes.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);
        return MapRate(entity, currencies, types);
    }

    private static ExchangeRateDto InvertRate(ExchangeRateDto rate) =>
        new(
            rate.Id,
            rate.TargetCurrencyId,
            rate.TargetCurrencyCode,
            rate.SourceCurrencyId,
            rate.SourceCurrencyCode,
            rate.RateTypeId,
            rate.RateTypeCode,
            rate.RateTypeName,
            rate.EffectiveDate,
            Math.Round(1m / rate.Rate, 10, MidpointRounding.AwayFromZero),
            rate.IsActive,
            rate.Notes);

    public async Task<ExchangeRateDto> CreateExchangeRateAsync(
        SaveExchangeRateRequest request,
        Guid userId,
        string? machineName,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await ValidateRateRequestAsync(request, cancellationToken);

        if (request.IsActive)
            await DeactivateActiveRatesAsync(request.SourceCurrencyId, request.TargetCurrencyId, request.RateTypeId, null, userId, cancellationToken);

        var entity = new ExchangeRate
        {
            SourceCurrencyId = request.SourceCurrencyId,
            TargetCurrencyId = request.TargetCurrencyId,
            RateTypeId = request.RateTypeId,
            EffectiveDate = request.EffectiveDate,
            Rate = request.Rate,
            IsActive = request.IsActive,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedBy = userId == Guid.Empty ? null : userId
        };
        await _rates.AddAsync(entity, cancellationToken);
        await AppendHistoryAsync(
            entity,
            oldRate: null,
            newRate: entity.Rate,
            action: request.IsActive ? "CreateActive" : "Create",
            userId,
            machineName,
            ipAddress,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetRateByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ExchangeRateDto> UpdateExchangeRateAsync(
        Guid id,
        SaveExchangeRateRequest request,
        Guid userId,
        string? machineName,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await ValidateRateRequestAsync(request, cancellationToken);
        var entity = await _rates.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Taux de change introuvable.");

        var oldRate = entity.Rate;
        if (request.IsActive)
            await DeactivateActiveRatesAsync(request.SourceCurrencyId, request.TargetCurrencyId, request.RateTypeId, id, userId, cancellationToken);

        entity.SourceCurrencyId = request.SourceCurrencyId;
        entity.TargetCurrencyId = request.TargetCurrencyId;
        entity.RateTypeId = request.RateTypeId;
        entity.EffectiveDate = request.EffectiveDate;
        entity.Rate = request.Rate;
        entity.IsActive = request.IsActive;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.UpdatedBy = userId == Guid.Empty ? null : userId;
        await _rates.UpdateAsync(entity, cancellationToken);
        await AppendHistoryAsync(entity, oldRate, entity.Rate, "Update", userId, machineName, ipAddress, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetRateByIdAsync(entity.Id, cancellationToken);
    }

    public async Task ActivateExchangeRateAsync(
        Guid id,
        Guid userId,
        string? machineName,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var entity = await _rates.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Taux de change introuvable.");

        await DeactivateActiveRatesAsync(entity.SourceCurrencyId, entity.TargetCurrencyId, entity.RateTypeId, id, userId, cancellationToken);
        entity.IsActive = true;
        entity.UpdatedBy = userId == Guid.Empty ? null : userId;
        await _rates.UpdateAsync(entity, cancellationToken);
        await AppendHistoryAsync(entity, entity.Rate, entity.Rate, "Activate", userId, machineName, ipAddress, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteExchangeRateAsync(
        Guid id,
        Guid userId,
        string? machineName,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var entity = await _rates.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Taux de change introuvable.");

        var oldRate = entity.Rate;
        entity.IsActive = false;
        entity.UpdatedBy = userId == Guid.Empty ? null : userId;
        await _rates.UpdateAsync(entity, cancellationToken);
        await AppendHistoryAsync(entity, oldRate, oldRate, "Deactivate", userId, machineName, ipAddress, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExchangeRateHistoryDto>> GetExchangeRateHistoryAsync(
        Guid? exchangeRateId = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, 1000);
        var rows = exchangeRateId.HasValue
            ? await _histories.FindAsync(x => x.ExchangeRateId == exchangeRateId.Value, cancellationToken)
            : await _histories.GetAllAsync(cancellationToken);

        var currencies = (await _currencies.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);
        var types = (await _rateTypes.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);

        return rows
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .Select(x => new ExchangeRateHistoryDto(
                x.Id,
                x.ExchangeRateId,
                currencies.GetValueOrDefault(x.SourceCurrencyId)?.Code ?? "?",
                currencies.GetValueOrDefault(x.TargetCurrencyId)?.Code ?? "?",
                types.GetValueOrDefault(x.RateTypeId)?.Code ?? "?",
                x.OldRate,
                x.NewRate,
                x.Action,
                x.UserId,
                x.MachineName,
                x.IpAddress,
                x.OccurredAt))
            .ToList();
    }

    public async Task ValidateCurrencyAsync(Guid currencyId, CancellationToken cancellationToken = default)
    {
        var entity = await _currencies.GetByIdAsync(currencyId, cancellationToken);
        if (entity is null || !entity.IsActive)
            throw new DomainException("Devise invalide ou inactive.");
    }

    public async Task ValidateExchangeRateAsync(Guid exchangeRateId, CancellationToken cancellationToken = default)
    {
        var entity = await _rates.GetByIdAsync(exchangeRateId, cancellationToken);
        if (entity is null || !entity.IsActive)
            throw new DomainException("Taux de change invalide ou inactif.");
    }

    public Task<CurrencyConversionResultDto> CalculateEquivalentAsync(
        CurrencyConversionRequest request,
        CancellationToken cancellationToken = default)
        => ConvertAsync(request, cancellationToken);

    public async Task<CurrencyConversionResultDto> ConvertAsync(
        CurrencyConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount < 0)
            throw new DomainException("Le montant à convertir ne peut pas être négatif.");

        await ValidateCurrencyAsync(request.SourceCurrencyId, cancellationToken);
        await ValidateCurrencyAsync(request.TargetCurrencyId, cancellationToken);

        var source = await _currencies.GetByIdAsync(request.SourceCurrencyId, cancellationToken)
            ?? throw new KeyNotFoundException("Devise source introuvable.");
        var target = await _currencies.GetByIdAsync(request.TargetCurrencyId, cancellationToken)
            ?? throw new KeyNotFoundException("Devise destination introuvable.");

        if (request.SourceCurrencyId == request.TargetCurrencyId)
        {
            return new CurrencyConversionResultDto(
                source.Id, source.Code, target.Id, target.Code,
                request.Amount, Round(request.Amount, target.DecimalPlaces),
                1m, null, null);
        }

        decimal rate;
        Guid? rateId = null;
        DateOnly? effective = null;

        if (request.OverrideRate.HasValue)
        {
            if (request.OverrideRate.Value <= 0)
                throw new DomainException("Le taux forcé doit être strictement positif.");
            rate = request.OverrideRate.Value;
        }
        else
        {
            ExchangeRateDto? found = request.AsOfDate.HasValue
                ? await GetRateForDateAsync(request.SourceCurrencyId, request.TargetCurrencyId, request.AsOfDate.Value, request.RateTypeId, cancellationToken)
                : await GetActiveExchangeRateAsync(request.SourceCurrencyId, request.TargetCurrencyId, request.RateTypeId, cancellationToken);

            if (found is null)
            {
                var inverse = request.AsOfDate.HasValue
                    ? await GetRateForDateAsync(request.TargetCurrencyId, request.SourceCurrencyId, request.AsOfDate.Value, request.RateTypeId, cancellationToken)
                    : await GetActiveExchangeRateAsync(request.TargetCurrencyId, request.SourceCurrencyId, request.RateTypeId, cancellationToken);

                if (inverse is null)
                    throw new DomainException($"Aucun taux de change actif pour {source.Code} → {target.Code}.");

                rate = 1m / inverse.Rate;
                rateId = inverse.Id;
                effective = inverse.EffectiveDate;
            }
            else
            {
                rate = found.Rate;
                rateId = found.Id;
                effective = found.EffectiveDate;
            }
        }

        var converted = Round(request.Amount * rate, target.DecimalPlaces);
        return new CurrencyConversionResultDto(
            source.Id, source.Code, target.Id, target.Code,
            request.Amount, converted, rate, rateId, effective);
    }

    /// <summary>Résout une devise référentiel à partir de l'enum historique (rétrocompatibilité).</summary>
    public async Task<CurrencyDefinitionDto?> ResolveByEnumCodeAsync(
        string currencyCode,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(currencyCode);
        var rows = await _currencies.FindAsync(x => x.Code == code && x.IsActive, cancellationToken);
        var entity = rows.FirstOrDefault();
        return entity is null ? null : MapCurrency(entity);
    }

    private async Task ValidateRateRequestAsync(SaveExchangeRateRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceCurrencyId == request.TargetCurrencyId)
            throw new DomainException("Les devises source et destination doivent être différentes.");
        if (request.Rate <= 0)
            throw new DomainException("Le taux doit être strictement positif.");

        await ValidateCurrencyAsync(request.SourceCurrencyId, cancellationToken);
        await ValidateCurrencyAsync(request.TargetCurrencyId, cancellationToken);

        var type = await _rateTypes.GetByIdAsync(request.RateTypeId, cancellationToken);
        if (type is null || !type.IsActive)
            throw new DomainException("Type de taux invalide ou inactif.");
    }

    private async Task DeactivateActiveRatesAsync(
        Guid sourceId,
        Guid targetId,
        Guid rateTypeId,
        Guid? excludeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var actives = await _rates.FindAsync(
            x => x.IsActive &&
                 x.SourceCurrencyId == sourceId &&
                 x.TargetCurrencyId == targetId &&
                 x.RateTypeId == rateTypeId &&
                 (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

        foreach (var row in actives)
        {
            var tracked = await _rates.GetByIdAsync(row.Id, cancellationToken);
            if (tracked is null) continue;
            tracked.IsActive = false;
            tracked.UpdatedBy = userId == Guid.Empty ? null : userId;
            await _rates.UpdateAsync(tracked, cancellationToken);
        }
    }

    private async Task AppendHistoryAsync(
        ExchangeRate rate,
        decimal? oldRate,
        decimal newRate,
        string action,
        Guid userId,
        string? machineName,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await _histories.AddAsync(new ExchangeRateHistory
        {
            ExchangeRateId = rate.Id,
            SourceCurrencyId = rate.SourceCurrencyId,
            TargetCurrencyId = rate.TargetCurrencyId,
            RateTypeId = rate.RateTypeId,
            OldRate = oldRate,
            NewRate = newRate,
            Action = action,
            UserId = userId == Guid.Empty ? null : userId,
            MachineName = string.IsNullOrWhiteSpace(machineName) ? Environment.MachineName : machineName,
            IpAddress = ipAddress,
            OccurredAt = DateTime.UtcNow,
            CreatedBy = userId == Guid.Empty ? null : userId
        }, cancellationToken);
    }

    private async Task ClearSystemLocalAsync(CancellationToken cancellationToken)
    {
        var locals = await _currencies.FindAsync(x => x.IsSystemLocal, cancellationToken);
        foreach (var local in locals)
        {
            var tracked = await _currencies.GetByIdAsync(local.Id, cancellationToken);
            if (tracked is null) continue;
            tracked.IsSystemLocal = false;
            await _currencies.UpdateAsync(tracked, cancellationToken);
        }
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Le code devise est obligatoire.");
        return code.Trim().ToUpperInvariant();
    }

    private static decimal Round(decimal amount, int decimals)
        => Math.Round(amount, Math.Clamp(decimals, 0, 6), MidpointRounding.AwayFromZero);

    private static CurrencyDefinitionDto MapCurrency(CurrencyDefinition x) => new(
        x.Id, x.Code, x.Name, x.Symbol, x.DecimalPlaces, x.IsSystemLocal, x.IsActive);

    private static SchoolCurrencyDto MapSchoolCurrency(SchoolCurrency x, CurrencyDefinition c) => new(
        x.Id, x.CurrencyId, c.Code, c.Name, c.Symbol, x.IsPrimary, x.AllowPayment);

    private static ExchangeRateTypeDto MapRateType(ExchangeRateType x) => new(
        x.Id, x.Code, x.Name, x.Description, x.IsActive);

    private static ExchangeRateDto? MapRate(
        ExchangeRate x,
        IReadOnlyDictionary<Guid, CurrencyDefinition> currencies,
        IReadOnlyDictionary<Guid, ExchangeRateType> types)
    {
        if (!currencies.TryGetValue(x.SourceCurrencyId, out var source) ||
            !currencies.TryGetValue(x.TargetCurrencyId, out var target) ||
            !types.TryGetValue(x.RateTypeId, out var type))
            return null;

        return new ExchangeRateDto(
            x.Id,
            x.SourceCurrencyId, source.Code,
            x.TargetCurrencyId, target.Code,
            x.RateTypeId, type.Code, type.Name,
            x.EffectiveDate, x.Rate, x.IsActive, x.Notes);
    }
}
