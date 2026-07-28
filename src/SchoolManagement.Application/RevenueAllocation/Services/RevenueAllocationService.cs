namespace SchoolManagement.Application.RevenueAllocation.Services;

using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.CurrencyManagement.Interfaces;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Application.RevenueAllocation.Interfaces;
using SchoolManagement.Application.Withholdings.DTOs;
using SchoolManagement.Application.Withholdings.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class RevenueAllocationService : IRevenueAllocationService
{
    public const string PrincipalDestinationCode = "PRN";

    private readonly IRepository<RevenueAllocationDestination> _destinationRepository;
    private readonly IRepository<RevenueAllocationKey> _keyRepository;
    private readonly IRepository<RevenueAllocationKeyDetail> _detailRepository;
    private readonly IRepository<RevenueAllocationEntry> _entryRepository;
    private readonly IRepository<ExpensePayment> _expensePaymentRepository;
    private readonly IRepository<ExpensePaymentAllocation> _expenseAllocationRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentLine> _paymentLineRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<Section> _sectionRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<WithholdingType> _withholdingTypeRepository;
    private readonly IRepository<UserAccount> _userRepository;
    private readonly IRepository<CurrencyDefinition> _currencyRepository;
    private readonly ICurrencyService _currencyService;
    private readonly IRevenueAllocationEngine _engine;
    private readonly IWithholdingService _withholdingService;
    private readonly IUnitOfWork _unitOfWork;

    public RevenueAllocationService(
        IRepository<RevenueAllocationDestination> destinationRepository,
        IRepository<RevenueAllocationKey> keyRepository,
        IRepository<RevenueAllocationKeyDetail> detailRepository,
        IRepository<RevenueAllocationEntry> entryRepository,
        IRepository<ExpensePayment> expensePaymentRepository,
        IRepository<ExpensePaymentAllocation> expenseAllocationRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<Section> sectionRepository,
        IRepository<Student> studentRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<WithholdingType> withholdingTypeRepository,
        IRepository<UserAccount> userRepository,
        IRepository<CurrencyDefinition> currencyRepository,
        ICurrencyService currencyService,
        IRevenueAllocationEngine engine,
        IWithholdingService withholdingService,
        IUnitOfWork unitOfWork)
    {
        _destinationRepository = destinationRepository;
        _keyRepository = keyRepository;
        _detailRepository = detailRepository;
        _entryRepository = entryRepository;
        _expensePaymentRepository = expensePaymentRepository;
        _expenseAllocationRepository = expenseAllocationRepository;
        _paymentRepository = paymentRepository;
        _paymentLineRepository = paymentLineRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRoomRepository = classRoomRepository;
        _sectionRepository = sectionRepository;
        _studentRepository = studentRepository;
        _yearRepository = yearRepository;
        _feeTypeRepository = feeTypeRepository;
        _withholdingTypeRepository = withholdingTypeRepository;
        _userRepository = userRepository;
        _currencyRepository = currencyRepository;
        _currencyService = currencyService;
        _engine = engine;
        _withholdingService = withholdingService;
        _unitOfWork = unitOfWork;
    }

    public async Task EnsureDefaultDestinationsAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var existing = await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken);
        var changed = false;

        foreach (var duplicates in existing
                     .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            var keep = duplicates
                .OrderByDescending(d => d.IsActive)
                .ThenBy(d => d.CreatedAt)
                .ThenBy(d => d.Id)
                .First();
            foreach (var duplicate in duplicates.Where(d => d.Id != keep.Id))
            {
                await _destinationRepository.DeleteAsync(duplicate, cancellationToken);
                changed = true;
            }
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            existing = await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken);
        }

        var existingCodes = existing
            .Select(d => d.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var defaults = new (string Code, string Name, string Description)[]
        {
            (PrincipalDestinationCode, "Compte principal", "Compte par défaut — reçoit 100 % tant qu'aucune clé de répartition n'est configurée"),
            ("SAL", "Salaire", "Masse salariale"),
            ("FON", "Fonctionnement", "Charges de fonctionnement"),
            ("INV", "Investissement", "Investissements et équipements"),
            ("SOC", "Fonds social", "Caisse / fonds social"),
            ("RES", "Réserve", "Réserve de l'établissement")
        };

        var added = false;
        foreach (var item in defaults)
        {
            if (existingCodes.Contains(item.Code))
            {
                continue;
            }

            await _destinationRepository.AddAsync(new RevenueAllocationDestination
            {
                SchoolId = schoolId,
                Code = item.Code,
                Name = item.Name,
                Description = item.Description,
                IsActive = true
            }, cancellationToken);
            existingCodes.Add(item.Code);
            added = true;
        }

        if (added)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<RevenueDestinationDto>> GetDestinationsAsync(
        Guid schoolId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultDestinationsAsync(schoolId, cancellationToken);
        var items = await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken);
        if (activeOnly)
        {
            items = items.Where(d => d.IsActive).ToList();
        }

        // Garde-fou : une seule ligne par code (évite les doublons issus d'anciens seeds concurrentiels).
        return items
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(d => d.IsActive)
                .ThenBy(d => d.CreatedAt)
                .ThenBy(d => d.Id)
                .First())
            .OrderBy(d => d.Code)
            .Select(d => new RevenueDestinationDto(d.Id, d.Code, d.Name, d.Description, d.IsActive))
            .ToList();
    }

    public async Task<RevenueDestinationDto> CreateDestinationAsync(
        Guid schoolId,
        SaveRevenueDestinationRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.Code);
        var duplicate = await _destinationRepository.FindAsync(
            d => d.SchoolId == schoolId && d.Code == code, cancellationToken);
        if (duplicate.Count > 0)
        {
            throw new DomainException($"Une destination avec le code « {code} » existe déjà.");
        }

        var entity = new RevenueAllocationDestination
        {
            SchoolId = schoolId,
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = request.IsActive
        };
        await _destinationRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new RevenueDestinationDto(entity.Id, entity.Code, entity.Name, entity.Description, entity.IsActive);
    }

    public async Task<RevenueDestinationDto> UpdateDestinationAsync(
        Guid schoolId,
        Guid destinationId,
        SaveRevenueDestinationRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetDestinationEntityAsync(schoolId, destinationId, cancellationToken);
        var code = NormalizeCode(request.Code);
        var duplicate = (await _destinationRepository.FindAsync(
            d => d.SchoolId == schoolId && d.Code == code && d.Id != destinationId, cancellationToken)).Any();
        if (duplicate)
        {
            throw new DomainException($"Une destination avec le code « {code} » existe déjà.");
        }

        entity.Code = code;
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.IsActive = request.IsActive;
        await _destinationRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new RevenueDestinationDto(entity.Id, entity.Code, entity.Name, entity.Description, entity.IsActive);
    }

    public async Task DeactivateDestinationAsync(Guid schoolId, Guid destinationId, CancellationToken cancellationToken = default)
    {
        var entity = await GetDestinationEntityAsync(schoolId, destinationId, cancellationToken);
        entity.IsActive = false;
        await _destinationRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RevenueAllocationKeyDto>> GetKeysAsync(
        Guid schoolId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var keys = await _keyRepository.FindAsync(
            k => k.SchoolId == schoolId && (!academicYearId.HasValue || k.AcademicYearId == academicYearId),
            cancellationToken);

        var result = new List<RevenueAllocationKeyDto>();
        foreach (var key in keys
                     .OrderByDescending(k => k.IsActive)
                     .ThenByDescending(k => k.StartDate)
                     .ThenBy(k => k.Name))
        {
            result.Add(await MapKeyAsync(schoolId, key, cancellationToken));
        }

        return result;
    }

    public async Task<RevenueAllocationKeyDto?> GetKeyByIdAsync(
        Guid schoolId,
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        var key = (await _keyRepository.FindAsync(k => k.Id == keyId && k.SchoolId == schoolId, cancellationToken))
            .FirstOrDefault();
        return key is null ? null : await MapKeyAsync(schoolId, key, cancellationToken);
    }

    public async Task<RevenueAllocationKeyDto> CreateKeyAsync(
        Guid schoolId,
        CreateRevenueAllocationKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await GetYearAsync(schoolId, request.AcademicYearId, cancellationToken);
        await ValidateDetailsAsync(schoolId, request.Details, cancellationToken);

        var hasFee = request.FeeTypeId.HasValue;
        var hasWithholding = request.WithholdingTypeId.HasValue;
        if (hasFee == hasWithholding)
        {
            throw new DomainException(
                "Indiquez soit un type de frais, soit un type de retenue (un seul des deux).");
        }

        FeeType? feeType = null;
        WithholdingType? withholdingType = null;
        if (hasFee)
        {
            feeType = await GetFeeTypeAsync(schoolId, request.FeeTypeId!.Value, cancellationToken);
            var existing = await _keyRepository.FindAsync(
                k => k.SchoolId == schoolId
                     && k.AcademicYearId == request.AcademicYearId
                     && k.FeeTypeId == request.FeeTypeId,
                cancellationToken);
            if (existing.Count > 0)
            {
                throw new DomainException(
                    $"Une clé de répartition existe déjà pour « {feeType.Name} » sur cette année scolaire. Vous ne pouvez que la modifier.");
            }
        }
        else
        {
            withholdingType = await GetWithholdingTypeAsync(schoolId, request.WithholdingTypeId!.Value, cancellationToken);
            var existing = await _keyRepository.FindAsync(
                k => k.SchoolId == schoolId
                     && k.AcademicYearId == request.AcademicYearId
                     && k.WithholdingTypeId == request.WithholdingTypeId,
                cancellationToken);
            if (existing.Count > 0)
            {
                throw new DomainException(
                    $"Une clé de répartition existe déjà pour la retenue « {withholdingType.Name} » sur cette année scolaire. Vous ne pouvez que la modifier.");
            }
        }

        var sourceLabel = feeType?.Name ?? withholdingType!.Name;
        var key = new RevenueAllocationKey
        {
            SchoolId = schoolId,
            AcademicYearId = request.AcademicYearId,
            FeeTypeId = feeType?.Id,
            WithholdingTypeId = withholdingType?.Id,
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? $"Répartition {sourceLabel}"
                : request.Name.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            StartDate = request.StartDate,
            EndDate = null,
            IsActive = true
        };
        await _keyRepository.AddAsync(key, cancellationToken);
        await ReplaceDetailsAsync(key.Id, request.Details, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await GetKeyByIdAsync(schoolId, key.Id, cancellationToken))!;
    }

    public async Task<RevenueAllocationKeyDto> UpdateKeyAsync(
        Guid schoolId,
        Guid keyId,
        UpdateRevenueAllocationKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = await GetKeyEntityAsync(schoolId, keyId, cancellationToken);
        await ValidateDetailsAsync(schoolId, request.Details, cancellationToken);

        key.Name = request.Name.Trim();
        key.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        key.StartDate = request.StartDate;
        await _keyRepository.UpdateAsync(key, cancellationToken);

        var existingDetails = await _detailRepository.FindAsync(d => d.AllocationKeyId == keyId, cancellationToken);
        foreach (var detail in existingDetails)
        {
            await _detailRepository.DeleteAsync(detail, cancellationToken);
        }

        await ReplaceDetailsAsync(keyId, request.Details, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await GetKeyByIdAsync(schoolId, keyId, cancellationToken))!;
    }

    public async Task ActivateKeyAsync(Guid schoolId, Guid keyId, CancellationToken cancellationToken = default)
    {
        var key = await GetKeyEntityAsync(schoolId, keyId, cancellationToken);
        var details = await LoadDetailsWithDestinationsAsync(keyId, cancellationToken);
        var errors = _engine.ValidateKeyForActivation(details);
        if (errors.Count > 0)
        {
            throw new DomainException(string.Join(" ", errors));
        }

        key.EndDate = null;
        key.IsActive = true;
        await _keyRepository.UpdateAsync(key, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseKeyAsync(
        Guid schoolId,
        Guid keyId,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var key = await GetKeyEntityAsync(schoolId, keyId, cancellationToken);
        if (!key.IsOpen)
        {
            throw new DomainException("Cette répartition est déjà clôturée.");
        }

        var closingDate = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (closingDate < key.StartDate)
        {
            throw new DomainException("La date de fin ne peut pas être antérieure à la date de début.");
        }

        key.EndDate = closingDate;
        key.IsActive = false;
        await _keyRepository.UpdateAsync(key, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateKeyAsync(Guid schoolId, Guid keyId, CancellationToken cancellationToken = default) =>
        await CloseKeyAsync(schoolId, keyId, null, cancellationToken);

    public async Task DeleteKeyAsync(Guid schoolId, Guid keyId, CancellationToken cancellationToken = default)
    {
        var key = await GetKeyEntityAsync(schoolId, keyId, cancellationToken);
        var historyCount = (await _entryRepository.FindAsync(e => e.AllocationKeyId == keyId, cancellationToken)).Count;
        if (historyCount > 0)
        {
            throw new DomainException(
                "Cette clé a déjà servi à des paiements. L'historique est conservé : vous ne pouvez que la modifier, pas la supprimer.");
        }

        var details = await _detailRepository.FindAsync(d => d.AllocationKeyId == keyId, cancellationToken);
        foreach (var detail in details)
        {
            await _detailRepository.DeleteAsync(detail, cancellationToken);
        }

        await _keyRepository.DeleteAsync(key, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyAllocationForPaymentAsync(
        Guid schoolId,
        Payment payment,
        IReadOnlyList<PaymentLine> paymentLines,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultDestinationsAsync(schoolId, cancellationToken);
        var principal = await GetPrincipalDestinationAsync(schoolId, cancellationToken);

        var paymentDate = DateOnly.FromDateTime(payment.PaymentDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(payment.PaymentDate, DateTimeKind.Utc)
            : payment.PaymentDate.ToUniversalTime());

        var openKeys = await _keyRepository.FindAsync(
            k => k.SchoolId == schoolId
                 && k.AcademicYearId == payment.AcademicYearId
                 && k.StartDate <= paymentDate
                 && (k.EndDate == null || k.EndDate >= paymentDate),
            cancellationToken);

        var keysByFeeType = openKeys
            .Where(k => k.FeeTypeId.HasValue)
            .GroupBy(k => k.FeeTypeId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(k => k.StartDate).ThenByDescending(k => k.CreatedAt).First());

        var keysByWithholding = openKeys
            .Where(k => k.WithholdingTypeId.HasValue)
            .GroupBy(k => k.WithholdingTypeId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(k => k.StartDate).ThenByDescending(k => k.CreatedAt).First());

        var lines = paymentLines.Where(l => l.Amount > 0).ToList();
        if (lines.Count == 0)
        {
            throw new DomainException("Aucun montant à répartir sur ce paiement.");
        }

        var pricingCategoryId = (await _enrollmentRepository.FindAsync(
                e => e.IsActive
                     && e.StudentId == payment.StudentId
                     && e.AcademicYearId == payment.AcademicYearId,
                cancellationToken))
            .OrderByDescending(e => e.EnrollmentDate)
            .Select(e => (Guid?)e.FeePricingCategoryId)
            .FirstOrDefault();

        var now = DateTime.UtcNow;

        // Modification de montant : mémoriser les retenues fixes déjà liées à chaque ligne
        // avant de recalculer (sinon elles seraient perdues car ce n'est plus le 1er versement).
        var preserveFixedByLine = await _withholdingService
            .GetFixedApplicationConfigurationIdsByLineAsync(schoolId, payment.Id, cancellationToken);
        await _withholdingService.RemoveApplicationsForPaymentAsync(schoolId, payment.Id, cancellationToken);

        foreach (var paymentLine in lines)
        {
            preserveFixedByLine.TryGetValue(paymentLine.Id, out var preserveFixedForLine);
            var withholdingResult = await _withholdingService.CalculateForPaymentLineAsync(
                schoolId,
                paymentLine.Amount,
                new WithholdingResolveContext(
                    payment.AcademicYearId,
                    paymentLine.FeeTypeId,
                    paymentLine.FeeInstallmentId,
                    pricingCategoryId,
                    payment.StudentId,
                    BalanceIncludesCurrentPayment: true,
                    PreserveFixedConfigurationIds: preserveFixedForLine),
                cancellationToken);

            await _withholdingService.RecordApplicationsAsync(
                schoolId,
                payment.StudentId,
                payment.AcademicYearId,
                payment.Id,
                paymentLine.Id,
                withholdingResult,
                cancellationToken);

            // Montant net (après retenues) → répartition type de frais, sinon Compte principal 100 %.
            keysByFeeType.TryGetValue(paymentLine.FeeTypeId, out var feeKey);
            await WriteAllocationEntriesAsync(
                schoolId,
                payment,
                userId,
                now,
                withholdingResult.NetAmount,
                feeKey,
                principal,
                feeTypeId: paymentLine.FeeTypeId,
                withholdingTypeId: null,
                cancellationToken);

            // Chaque retenue → répartition type de retenue, sinon Compte principal 100 %.
            foreach (var withheld in withholdingResult.Lines.Where(l => l.WithheldAmount > 0))
            {
                keysByWithholding.TryGetValue(withheld.WithholdingTypeId, out var withholdingKey);
                await WriteAllocationEntriesAsync(
                    schoolId,
                    payment,
                    userId,
                    now,
                    withheld.WithheldAmount,
                    withholdingKey,
                    principal,
                    feeTypeId: paymentLine.FeeTypeId,
                    withholdingTypeId: withheld.WithholdingTypeId,
                    cancellationToken);
            }
        }
    }

    private async Task WriteAllocationEntriesAsync(
        Guid schoolId,
        Payment payment,
        Guid userId,
        DateTime allocatedAt,
        decimal amount,
        RevenueAllocationKey? key,
        RevenueAllocationDestination principal,
        Guid? feeTypeId,
        Guid? withholdingTypeId,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return;
        }

        var currencyId = await ResolveAllocationCurrencyIdAsync(schoolId, payment, cancellationToken);

        if (key is null)
        {
            await _entryRepository.AddAsync(new RevenueAllocationEntry
            {
                SchoolId = schoolId,
                PaymentId = payment.Id,
                AllocationKeyId = null,
                DestinationId = principal.Id,
                FeeTypeId = feeTypeId,
                WithholdingTypeId = withholdingTypeId,
                AcademicYearId = payment.AcademicYearId,
                CurrencyId = currencyId,
                Amount = amount,
                AppliedPercentage = 100m,
                CalculationType = AllocationCalculationType.Pourcentage,
                AllocatedAt = allocatedAt,
                AllocatedByUserId = userId
            }, cancellationToken);
            return;
        }

        var details = await LoadDetailsWithDestinationsAsync(key.Id, cancellationToken);
        if (details.Count == 0)
        {
            throw new DomainException($"La clé de répartition « {key.Name} » ne contient aucune ligne.");
        }

        var calculated = _engine.Calculate(amount, details);
        foreach (var item in calculated.Where(c => c.Amount != 0))
        {
            await _entryRepository.AddAsync(new RevenueAllocationEntry
            {
                SchoolId = schoolId,
                PaymentId = payment.Id,
                AllocationKeyId = key.Id,
                DestinationId = item.DestinationId,
                FeeTypeId = feeTypeId,
                WithholdingTypeId = withholdingTypeId,
                AcademicYearId = payment.AcademicYearId,
                CurrencyId = currencyId,
                Amount = item.Amount,
                AppliedPercentage = item.AppliedPercentage,
                CalculationType = AllocationCalculationType.Pourcentage,
                AllocatedAt = allocatedAt,
                AllocatedByUserId = userId
            }, cancellationToken);
        }
    }

    /// <summary>Devise du montant réparti = devise du frais (snapshot paiement).</summary>
    private async Task<Guid?> ResolveAllocationCurrencyIdAsync(
        Guid schoolId,
        Payment payment,
        CancellationToken cancellationToken)
    {
        if (payment.FeeCurrencyId.HasValue)
        {
            return payment.FeeCurrencyId;
        }

        if (payment.PaymentCurrencyId.HasValue)
        {
            return payment.PaymentCurrencyId;
        }

        var fromEnum = await _currencyService.ResolveByEnumCodeAsync(payment.Currency.ToString(), cancellationToken);
        if (fromEnum is not null)
        {
            return fromEnum.Id;
        }

        try
        {
            var main = await _currencyService.GetMainCurrencyAsync(schoolId, cancellationToken);
            return main.Id;
        }
        catch
        {
            return null;
        }
    }

    private async Task<RevenueAllocationDestination> GetPrincipalDestinationAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        var principal = (await _destinationRepository.FindAsync(
            d => d.SchoolId == schoolId && d.Code == PrincipalDestinationCode && d.IsActive,
            cancellationToken)).FirstOrDefault();
        return principal
            ?? throw new DomainException(
                "Le Compte principal (PRN) est introuvable. Vérifiez les destinations de répartition.");
    }

    public async Task<RevenueAllocationSearchResultDto> SearchAllocationsAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var entries = await _entryRepository.FindAsync(e => e.SchoolId == schoolId, cancellationToken);
        var filtered = await FilterEntriesAsync(schoolId, entries, request, cancellationToken);

        var totals = await BuildTotalsAsync(schoolId, filtered, cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var items = filtered
            .OrderByDescending(e => e.AllocatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtos = await MapEntriesAsync(schoolId, items, cancellationToken);
        return new RevenueAllocationSearchResultDto(dtos, page, pageSize, filtered.Count, totals);
    }

    public async Task<IReadOnlyList<FeeTypeAllocationSummaryGroupDto>> GetAllocationSummaryByFeeTypeAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var entries = await _entryRepository.FindAsync(e => e.SchoolId == schoolId, cancellationToken);
        var filtered = await FilterEntriesAsync(schoolId, entries, request, cancellationToken);

        var destinations = (await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);
        var feeTypes = (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken))
            .ToDictionary(f => f.Id);
        var currencyLabels = await BuildCurrencyLabelMapAsync(filtered, cancellationToken);

        return filtered
            .Where(e => e.FeeTypeId.HasValue)
            .GroupBy(e => (FeeTypeId: e.FeeTypeId!.Value, CurrencyId: e.CurrencyId))
            .Select(feeGroup =>
            {
                feeTypes.TryGetValue(feeGroup.Key.FeeTypeId, out var feeType);
                var currencyCode = ResolveCurrencyCode(feeGroup.Key.CurrencyId, currencyLabels);
                var feeTotal = feeGroup.Sum(e => e.Amount);
                var destinationRows = feeGroup
                    .GroupBy(e => e.DestinationId)
                    .Select(destGroup =>
                    {
                        destinations.TryGetValue(destGroup.Key, out var dest);
                        var amount = destGroup.Sum(e => e.Amount);
                        var withPercentage = destGroup.Where(e => e.AppliedPercentage.HasValue).ToList();
                        var percentage = withPercentage.Count > 0
                            ? withPercentage.Sum(e => e.AppliedPercentage!.Value * e.Amount) / amount
                            : feeTotal > 0
                                ? amount / feeTotal * 100m
                                : 0m;

                        return new FeeTypeAllocationDestinationSummaryDto(
                            destGroup.Key,
                            dest?.Code ?? "—",
                            dest?.Name ?? "—",
                            feeGroup.Key.CurrencyId,
                            currencyCode,
                            Math.Round(percentage, 2),
                            amount);
                    })
                    .OrderByDescending(r => r.AllocatedAmount)
                    .ThenBy(r => r.DestinationName)
                    .ToList();

                return new FeeTypeAllocationSummaryGroupDto(
                    feeGroup.Key.FeeTypeId,
                    feeType?.Code ?? "—",
                    feeType?.Name ?? "—",
                    feeGroup.Key.CurrencyId,
                    currencyCode,
                    feeTotal,
                    destinationRows);
            })
            .OrderBy(g => g.FeeTypeName)
            .ThenBy(g => g.CurrencyCode)
            .ToList();
    }

    public async Task<AllocationCashFlowResultDto> GetAllocationCashFlowAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var fromDate = request.FromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var toDate = request.ToDate ?? fromDate;
        if (toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        // Même base que le rapport recettes : date de paiement (PaymentDate), pas AllocatedAt.
        // On charge tout le périmètre (sans dates) puis on découpe période / J-1.
        var scopeRequest = request with { FromDate = null, ToDate = null };
        var matchingPayments = await ResolveMatchingPaymentsAsync(schoolId, scopeRequest, cancellationToken);
        var periodPaymentIds = matchingPayments
            .Where(p =>
            {
                var date = DateOnly.FromDateTime(p.PaymentDate);
                return date >= fromDate && date <= toDate;
            })
            .Select(p => p.Id)
            .ToHashSet();
        var openingPaymentIds = matchingPayments
            .Where(p => DateOnly.FromDateTime(p.PaymentDate) < fromDate)
            .Select(p => p.Id)
            .ToHashSet();

        var entries = await _entryRepository.FindAsync(e => e.SchoolId == schoolId, cancellationToken);
        var expenses = await _expensePaymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        var destinations = (await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);

        IEnumerable<RevenueAllocationEntry> BaseEntries(IEnumerable<RevenueAllocationEntry> source)
        {
            var query = source;
            if (request.AcademicYearId.HasValue)
            {
                query = query.Where(e => e.AcademicYearId == request.AcademicYearId);
            }

            // Le filtre type de frais passe par les paiements (PaymentLines), pas FeeTypeId
            // sur l'écriture : les parts retenues doivent rester dans le cash-flow.

            if (request.DestinationId.HasValue)
            {
                query = query.Where(e => e.DestinationId == request.DestinationId);
            }

            if (request.CurrencyId.HasValue)
            {
                query = query.Where(e => e.CurrencyId == request.CurrencyId);
            }

            return query;
        }

        // Dépenses : hors filtre section/classe (pas liées à un élève) pour ne pas
        // fausser le partage d'encaissement du périmètre sélectionné.
        var applyExpenses = !request.SectionId.HasValue && !request.ClassRoomId.HasValue;

        IEnumerable<ExpensePayment> FilterExpenses(IEnumerable<ExpensePayment> source, DateOnly? from, DateOnly? to)
        {
            if (!applyExpenses)
            {
                return [];
            }

            var query = source;
            if (request.AcademicYearId.HasValue)
            {
                query = query.Where(p => p.AcademicYearId == request.AcademicYearId);
            }

            if (request.DestinationId.HasValue)
            {
                query = query.Where(p => p.DestinationId == request.DestinationId);
            }

            if (from.HasValue)
            {
                query = query.Where(p => p.ExpenseDate >= from);
            }

            if (to.HasValue)
            {
                query = query.Where(p => p.ExpenseDate <= to);
            }

            return query;
        }

        var baseEntries = BaseEntries(entries).ToList();
        var periodEntries = baseEntries.Where(e => periodPaymentIds.Contains(e.PaymentId)).ToList();
        var openingEntries = baseEntries.Where(e => openingPaymentIds.Contains(e.PaymentId)).ToList();
        var periodExpenses = FilterExpenses(expenses, fromDate, toDate).ToList();
        var openingExpenses = FilterExpenses(expenses, null, fromDate.AddDays(-1)).ToList();

        var currencyLabels = await BuildCurrencyLabelMapAsync(
            baseEntries.Concat(periodEntries).Concat(openingEntries).ToList(),
            cancellationToken);
        var expenseCurrencyMap = await BuildExpenseCurrencyIdMapAsync(schoolId, periodExpenses.Concat(openingExpenses).ToList(), cancellationToken);
        var expenseSpendLines = (await ExpandExpenseSpendLinesAsync(
            schoolId,
            periodExpenses.Concat(openingExpenses).ToList(),
            expenseCurrencyMap,
            cancellationToken)).ToList();
        foreach (var currencyId in expenseSpendLines.Where(l => l.CurrencyId.HasValue).Select(l => l.CurrencyId!.Value).Distinct())
        {
            if (!currencyLabels.ContainsKey(currencyId))
            {
                var def = await _currencyRepository.GetByIdAsync(currencyId, cancellationToken);
                if (def is not null)
                {
                    currencyLabels[def.Id] = def.Code;
                }
            }
        }

        static (Guid DestinationId, Guid? CurrencyId) KeyOf(Guid destinationId, Guid? currencyId) =>
            (destinationId, currencyId);

        var keys = periodEntries.Select(e => KeyOf(e.DestinationId, e.CurrencyId))
            .Concat(openingEntries.Select(e => KeyOf(e.DestinationId, e.CurrencyId)))
            .Concat(expenseSpendLines.Select(l => KeyOf(l.DestinationId, l.CurrencyId)))
            .Distinct()
            .ToList();

        if (request.DestinationId.HasValue
            && !keys.Any(k => k.DestinationId == request.DestinationId.Value))
        {
            keys.Add(KeyOf(request.DestinationId.Value, request.CurrencyId));
        }

        AllocationCashFlowRowDto BuildRow(
            Guid destinationId,
            Guid? currencyId,
            decimal j1Enc,
            decimal j1Dep,
            decimal enc,
            decimal dep)
        {
            destinations.TryGetValue(destinationId, out var destination);
            var periodJ1 = j1Enc - j1Dep;
            return new AllocationCashFlowRowDto(
                destinationId,
                destination?.Code ?? "—",
                destination?.Name ?? "—",
                currencyId,
                ResolveCurrencyCode(currencyId, currencyLabels),
                periodJ1,
                enc,
                dep,
                periodJ1 + enc - dep);
        }

        decimal SumEntries(IEnumerable<RevenueAllocationEntry> source, Guid destinationId, Guid? currencyId) =>
            source.Where(e => e.DestinationId == destinationId && e.CurrencyId == currencyId).Sum(e => e.Amount);

        decimal SumExpenseLines(
            IEnumerable<(Guid DestinationId, Guid? CurrencyId, decimal Amount, Guid PaymentId)> source,
            Guid destinationId,
            Guid? currencyId,
            IReadOnlySet<Guid> paymentIds) =>
            source.Where(l =>
                    paymentIds.Contains(l.PaymentId)
                    && l.DestinationId == destinationId
                    && l.CurrencyId == currencyId)
                .Sum(l => l.Amount);

        var openingExpenseIds = openingExpenses.Select(p => p.Id).ToHashSet();
        var periodExpenseIds = periodExpenses.Select(p => p.Id).ToHashSet();

        var globalRows = keys
            .Select(k => BuildRow(
                k.DestinationId,
                k.CurrencyId,
                SumEntries(openingEntries, k.DestinationId, k.CurrencyId),
                SumExpenseLines(expenseSpendLines, k.DestinationId, k.CurrencyId, openingExpenseIds),
                SumEntries(periodEntries, k.DestinationId, k.CurrencyId),
                SumExpenseLines(expenseSpendLines, k.DestinationId, k.CurrencyId, periodExpenseIds)))
            .OrderBy(r => r.CurrencyCode)
            .ThenBy(r => r.DestinationName)
            .ToList();

        var totalsByCurrency = globalRows
            .GroupBy(r => (r.CurrencyId, r.CurrencyCode))
            .Select(g => new AllocationCashFlowRowDto(
                Guid.Empty,
                "TOTAL",
                $"Total {g.Key.CurrencyCode}",
                g.Key.CurrencyId,
                g.Key.CurrencyCode,
                g.Sum(r => r.PeriodJ1),
                g.Sum(r => r.Encaissement),
                g.Sum(r => r.DepenseP),
                g.Sum(r => r.PeriodeP)))
            .OrderBy(r => r.CurrencyCode)
            .ToList();

        var dailyGroups = new List<AllocationCashFlowDailyGroupDto>();
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            var dayPaymentIds = matchingPayments
                .Where(p => DateOnly.FromDateTime(p.PaymentDate) == date)
                .Select(p => p.Id)
                .ToHashSet();
            var dayEntries = baseEntries.Where(e => dayPaymentIds.Contains(e.PaymentId)).ToList();
            var dayExpenses = FilterExpenses(expenses, date, date).ToList();
            var dayExpenseIds = dayExpenses.Select(p => p.Id).ToHashSet();
            var expensesBeforeDay = FilterExpenses(expenses, null, date.AddDays(-1)).ToList();
            var expensesBeforeDayIds = expensesBeforeDay.Select(p => p.Id).ToHashSet();

            // Compléter les lignes de dépense pour les paiements du jour / veille non encore chargés.
            var missingExpenseIds = dayExpenseIds
                .Concat(expensesBeforeDayIds)
                .Where(id => expenseSpendLines.All(l => l.PaymentId != id))
                .ToList();
            if (missingExpenseIds.Count > 0)
            {
                var missingPayments = dayExpenses.Concat(expensesBeforeDay)
                    .Where(p => missingExpenseIds.Contains(p.Id))
                    .GroupBy(p => p.Id)
                    .Select(g => g.First())
                    .ToList();
                var map = await BuildExpenseCurrencyIdMapAsync(schoolId, missingPayments, cancellationToken);
                foreach (var pair in map)
                {
                    expenseCurrencyMap[pair.Key] = pair.Value;
                }

                expenseSpendLines.AddRange(await ExpandExpenseSpendLinesAsync(
                    schoolId, missingPayments, expenseCurrencyMap, cancellationToken));
            }

            var dayKeys = dayEntries.Select(e => KeyOf(e.DestinationId, e.CurrencyId))
                .Concat(expenseSpendLines
                    .Where(l => dayExpenseIds.Contains(l.PaymentId))
                    .Select(l => KeyOf(l.DestinationId, l.CurrencyId)))
                .Distinct()
                .ToList();

            if (dayKeys.Count == 0)
            {
                continue;
            }

            var openingBeforeDayPaymentIds = matchingPayments
                .Where(p => DateOnly.FromDateTime(p.PaymentDate) < date)
                .Select(p => p.Id)
                .ToHashSet();
            var openingBeforeDay = baseEntries
                .Where(e => openingBeforeDayPaymentIds.Contains(e.PaymentId))
                .ToList();

            var rows = dayKeys
                .Select(k => BuildRow(
                    k.DestinationId,
                    k.CurrencyId,
                    SumEntries(openingBeforeDay, k.DestinationId, k.CurrencyId),
                    SumExpenseLines(expenseSpendLines, k.DestinationId, k.CurrencyId, expensesBeforeDayIds),
                    SumEntries(dayEntries, k.DestinationId, k.CurrencyId),
                    SumExpenseLines(expenseSpendLines, k.DestinationId, k.CurrencyId, dayExpenseIds)))
                .OrderBy(r => r.CurrencyCode)
                .ThenBy(r => r.DestinationName)
                .ToList();

            dailyGroups.Add(new AllocationCashFlowDailyGroupDto(date, rows));
        }

        return new AllocationCashFlowResultDto(globalRows, dailyGroups, totalsByCurrency);
    }

    public async Task<WithholdingReportResultDto> GetWithholdingReportAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var matchingPayments = await ResolveMatchingPaymentsAsync(schoolId, request, cancellationToken);
        var paymentMap = matchingPayments.ToDictionary(p => p.Id);
        var paymentIds = paymentMap.Keys.ToHashSet();
        if (paymentIds.Count == 0)
        {
            return new WithholdingReportResultDto([], 0m, 0);
        }

        var entries = (await _entryRepository.FindAsync(
                e => e.SchoolId == schoolId && e.WithholdingTypeId != null,
                cancellationToken))
            .Where(e => paymentIds.Contains(e.PaymentId))
            .ToList();

        if (request.AcademicYearId.HasValue)
        {
            entries = entries.Where(e => e.AcademicYearId == request.AcademicYearId.Value).ToList();
        }

        if (entries.Count == 0)
        {
            return new WithholdingReportResultDto([], 0m, 0);
        }

        var typeIds = entries
            .Where(e => e.WithholdingTypeId.HasValue)
            .Select(e => e.WithholdingTypeId!.Value)
            .Distinct()
            .ToList();
        var types = (await _withholdingTypeRepository.FindAsync(t => typeIds.Contains(t.Id), cancellationToken))
            .ToDictionary(t => t.Id);

        var studentIds = paymentMap.Values.Select(p => p.StudentId).Distinct().ToList();
        var students = (await _studentRepository.FindAsync(s => studentIds.Contains(s.Id), cancellationToken))
            .ToDictionary(s => s.Id);

        var groups = entries
            .Where(e => e.WithholdingTypeId.HasValue && paymentMap.ContainsKey(e.PaymentId))
            .GroupBy(e => e.WithholdingTypeId!.Value)
            .Select(typeGroup =>
            {
                types.TryGetValue(typeGroup.Key, out var type);
                var studentLines = typeGroup
                    .GroupBy(e => e.PaymentId)
                    .Select(paymentGroup =>
                    {
                        var payment = paymentMap[paymentGroup.Key];
                        students.TryGetValue(payment.StudentId, out var student);
                        var name = student is null
                            ? "—"
                            : StudentDisplayName.Format(student);
                        return new WithholdingReportStudentLineDto(
                            payment.StudentId,
                            name,
                            payment.Id,
                            DateOnly.FromDateTime(payment.PaymentDate),
                            paymentGroup.Sum(e => e.Amount));
                    })
                    .OrderBy(l => l.StudentName)
                    .ThenBy(l => l.PaymentDate)
                    .ToList();

                return new WithholdingReportTypeGroupDto(
                    typeGroup.Key,
                    type?.Code ?? "—",
                    type?.Name ?? "—",
                    studentLines.Sum(l => l.Amount),
                    studentLines);
            })
            .OrderByDescending(g => g.TypeTotal)
            .ThenBy(g => g.WithholdingTypeName)
            .ToList();

        return new WithholdingReportResultDto(
            groups,
            groups.Sum(g => g.TypeTotal),
            entries.Select(e => e.PaymentId).Distinct().Count());
    }

    private async Task<RevenueAllocationTotalsDto> BuildTotalsAsync(
        Guid schoolId,
        IReadOnlyList<RevenueAllocationEntry> entries,
        CancellationToken cancellationToken)
    {
        var destinations = (await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);
        var feeTypes = (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken))
            .ToDictionary(f => f.Id);
        var currencyLabels = await BuildCurrencyLabelMapAsync(entries, cancellationToken);

        var byCurrency = entries
            .GroupBy(e => e.CurrencyId)
            .Select(g => new CurrencyTotalDto(
                g.Key,
                ResolveCurrencyCode(g.Key, currencyLabels),
                g.Sum(x => x.Amount)))
            .OrderBy(t => t.CurrencyCode)
            .ToList();

        var byDest = entries
            .GroupBy(e => (e.DestinationId, e.CurrencyId))
            .Select(g =>
            {
                destinations.TryGetValue(g.Key.DestinationId, out var dest);
                return new DestinationTotalDto(
                    g.Key.DestinationId,
                    dest?.Code ?? "—",
                    dest?.Name ?? "—",
                    g.Key.CurrencyId,
                    ResolveCurrencyCode(g.Key.CurrencyId, currencyLabels),
                    g.Sum(x => x.Amount));
            })
            .OrderBy(t => t.CurrencyCode)
            .ThenByDescending(t => t.Total)
            .ToList();

        var byFee = entries
            .Where(e => e.FeeTypeId.HasValue)
            .GroupBy(e => (FeeTypeId: e.FeeTypeId!.Value, e.CurrencyId))
            .Select(g =>
            {
                feeTypes.TryGetValue(g.Key.FeeTypeId, out var fee);
                return new FeeTypeTotalDto(
                    g.Key.FeeTypeId,
                    fee?.Code ?? "—",
                    fee?.Name ?? "—",
                    g.Key.CurrencyId,
                    ResolveCurrencyCode(g.Key.CurrencyId, currencyLabels),
                    g.Sum(x => x.Amount));
            })
            .OrderBy(t => t.CurrencyCode)
            .ThenByDescending(t => t.Total)
            .ToList();

        // GrandTotal uniquement si une seule devise (évite de mélanger CDF + USD).
        var grandTotal = byCurrency.Count == 1 ? byCurrency[0].Total : 0m;
        return new RevenueAllocationTotalsDto(grandTotal, byCurrency, byDest, byFee);
    }

    public async Task<byte[]> ExportAllocationsExcelAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var search = request with { Page = 1, PageSize = 50_000 };
        var result = await SearchAllocationsAsync(schoolId, search, cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Répartitions");
        var headers = new[]
        {
            "Reçu", "Élève", "Montant payé", "Destination", "Code", "Devise", "Montant réparti",
            "Pourcentage", "Type", "Clé", "Année", "Type frais", "Date", "Utilisateur"
        };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var item in result.Items)
        {
            sheet.Cell(row, 1).Value = item.ReceiptNumber;
            sheet.Cell(row, 2).Value = item.StudentName;
            sheet.Cell(row, 3).Value = item.PaymentAmount;
            sheet.Cell(row, 4).Value = item.DestinationName;
            sheet.Cell(row, 5).Value = item.DestinationCode;
            sheet.Cell(row, 6).Value = item.CurrencyCode;
            sheet.Cell(row, 7).Value = item.AllocatedAmount;
            sheet.Cell(row, 8).Value = item.AppliedPercentage;
            sheet.Cell(row, 9).Value = item.CalculationType.ToString();
            sheet.Cell(row, 10).Value = item.AllocationKeyName;
            sheet.Cell(row, 11).Value = item.AcademicYearLabel;
            sheet.Cell(row, 12).Value = item.FeeTypeName;
            sheet.Cell(row, 13).Value = item.AllocatedAt;
            sheet.Cell(row, 14).Value = item.AllocatedBy;
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportAllocationsPdfAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var search = request with { Page = 1, PageSize = 2_000 };
        var result = await SearchAllocationsAsync(schoolId, search, cancellationToken);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Text("Répartition des recettes").SemiBold().FontSize(16);
                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Reçu").SemiBold();
                        header.Cell().Text("Élève").SemiBold();
                        header.Cell().Text("Destination").SemiBold();
                        header.Cell().Text("Devise").SemiBold();
                        header.Cell().AlignRight().Text("Montant").SemiBold();
                        header.Cell().Text("Date").SemiBold();
                    });

                    foreach (var item in result.Items)
                    {
                        table.Cell().Text(item.ReceiptNumber);
                        table.Cell().Text(item.StudentName);
                        table.Cell().Text(item.DestinationName);
                        table.Cell().Text(item.CurrencyCode);
                        table.Cell().AlignRight().Text($"{item.AllocatedAmount:N2}");
                        table.Cell().Text($"{item.AllocatedAt:dd/MM/yyyy}");
                    }
                });

                var totalsText = result.Totals.ByCurrency.Count == 0
                    ? "Total : 0"
                    : string.Join("  ·  ", result.Totals.ByCurrency.Select(c => $"{c.CurrencyCode} {c.Total:N2}"));
                page.Footer().AlignRight().Text(totalsText);
            });
        });

        return document.GeneratePdf();
    }

    private async Task<List<RevenueAllocationEntry>> FilterEntriesAsync(
        Guid schoolId,
        IReadOnlyList<RevenueAllocationEntry> entries,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken)
    {
        IEnumerable<RevenueAllocationEntry> query = entries;

        if (request.AcademicYearId.HasValue)
        {
            query = query.Where(e => e.AcademicYearId == request.AcademicYearId);
        }

        if (request.PaymentId.HasValue)
        {
            query = query.Where(e => e.PaymentId == request.PaymentId);
        }

        if (request.DestinationId.HasValue)
        {
            query = query.Where(e => e.DestinationId == request.DestinationId);
        }

        if (request.FeeTypeId.HasValue)
        {
            query = query.Where(e => e.FeeTypeId == request.FeeTypeId);
        }

        if (request.CurrencyId.HasValue)
        {
            query = query.Where(e => e.CurrencyId == request.CurrencyId);
        }

        if (request.StudentId.HasValue)
        {
            var paymentIds = (await _paymentRepository.FindAsync(
                p => p.SchoolId == schoolId && p.StudentId == request.StudentId, cancellationToken))
                .Select(p => p.Id)
                .ToHashSet();
            query = query.Where(e => paymentIds.Contains(e.PaymentId));
        }

        // Dates / section / classe : même logique que le rapport recettes (PaymentDate).
        if (request.FromDate.HasValue
            || request.ToDate.HasValue
            || request.SectionId.HasValue
            || request.ClassRoomId.HasValue)
        {
            var matchingIds = (await ResolveMatchingPaymentsAsync(schoolId, request, cancellationToken))
                .Select(p => p.Id)
                .ToHashSet();
            query = query.Where(e => matchingIds.Contains(e.PaymentId));
        }

        return query.ToList();
    }

    /// <summary>
    /// Paiements validés alignés sur le rapport recettes (année, type de frais, section, classe, dates).
    /// </summary>
    private async Task<List<Payment>> ResolveMatchingPaymentsAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken)
    {
        var payments = (await _paymentRepository.FindAsync(
            p => p.SchoolId == schoolId && p.Status == PaymentStatus.Complet,
            cancellationToken)).ToList();

        if (request.AcademicYearId.HasValue)
        {
            payments = payments.Where(p => p.AcademicYearId == request.AcademicYearId.Value).ToList();
        }

        if (request.FromDate.HasValue || request.ToDate.HasValue)
        {
            payments = payments.Where(p =>
            {
                var date = DateOnly.FromDateTime(p.PaymentDate);
                if (request.FromDate.HasValue && date < request.FromDate.Value)
                {
                    return false;
                }

                if (request.ToDate.HasValue && date > request.ToDate.Value)
                {
                    return false;
                }

                return true;
            }).ToList();
        }

        if (request.FeeTypeId.HasValue)
        {
            var paymentIdsWithFee = (await _paymentLineRepository.FindAsync(
                    l => l.FeeTypeId == request.FeeTypeId.Value,
                    cancellationToken))
                .Select(l => l.PaymentId)
                .ToHashSet();
            payments = payments.Where(p => paymentIdsWithFee.Contains(p.Id)).ToList();
        }

        if (request.StudentId.HasValue)
        {
            payments = payments.Where(p => p.StudentId == request.StudentId.Value).ToList();
        }

        if (request.PaymentId.HasValue)
        {
            payments = payments.Where(p => p.Id == request.PaymentId.Value).ToList();
        }

        if (!request.SectionId.HasValue && !request.ClassRoomId.HasValue)
        {
            return payments;
        }

        if (payments.Count == 0)
        {
            return payments;
        }

        var studentIds = payments.Select(p => p.StudentId).Distinct().ToList();
        var enrollments = await _enrollmentRepository.FindAsync(
            e => e.IsActive && studentIds.Contains(e.StudentId),
            cancellationToken);
        var yearIds = payments.Select(p => p.AcademicYearId).Distinct().ToList();
        if (yearIds.Count > 0)
        {
            enrollments = enrollments.Where(e => yearIds.Contains(e.AcademicYearId)).ToList();
        }

        var studentYearClass = enrollments
            .GroupBy(e => (e.StudentId, e.AcademicYearId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EnrollmentDate).First().ClassRoomId);

        var classIds = studentYearClass.Values.Distinct().ToList();
        var classes = classIds.Count == 0
            ? []
            : await _classRoomRepository.FindAsync(c => classIds.Contains(c.Id), cancellationToken);
        var classMap = classes.ToDictionary(c => c.Id);

        Guid? ResolveClassId(Payment p) =>
            studentYearClass.TryGetValue((p.StudentId, p.AcademicYearId), out var classId) ? classId : null;

        Guid? ResolveSectionId(Guid? classId) =>
            classId.HasValue && classMap.TryGetValue(classId.Value, out var cr) ? cr.SectionId : null;

        if (request.SectionId.HasValue)
        {
            var selectedSection = (await _sectionRepository.FindAsync(
                    s => s.Id == request.SectionId.Value && s.SchoolId == schoolId,
                    cancellationToken))
                .FirstOrDefault();

            if (selectedSection is not null)
            {
                var matchingSectionIds = (await _sectionRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken))
                    .Where(s => string.Equals(s.Name.Trim(), selectedSection.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.Id)
                    .ToHashSet();

                payments = payments
                    .Where(p =>
                    {
                        var sectionId = ResolveSectionId(ResolveClassId(p));
                        return sectionId.HasValue && matchingSectionIds.Contains(sectionId.Value);
                    })
                    .ToList();
            }
        }

        if (request.ClassRoomId.HasValue)
        {
            payments = payments.Where(p => ResolveClassId(p) == request.ClassRoomId.Value).ToList();
        }

        return payments;
    }

    private async Task<IReadOnlyList<RevenueAllocationEntryDto>> MapEntriesAsync(
        Guid schoolId,
        IReadOnlyList<RevenueAllocationEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var paymentIds = entries.Select(e => e.PaymentId).Distinct().ToList();
        var payments = (await _paymentRepository.FindAsync(p => paymentIds.Contains(p.Id), cancellationToken))
            .ToDictionary(p => p.Id);
        var studentIds = payments.Values.Select(p => p.StudentId).Distinct().ToList();
        var students = (await _studentRepository.FindAsync(s => studentIds.Contains(s.Id), cancellationToken))
            .ToDictionary(s => s.Id);
        var destinations = (await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);
        var keys = (await _keyRepository.FindAsync(k => k.SchoolId == schoolId, cancellationToken))
            .ToDictionary(k => k.Id);
        var years = (await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken))
            .ToDictionary(y => y.Id);
        var feeTypes = (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken))
            .ToDictionary(f => f.Id);
        var userIds = entries.Where(e => e.AllocatedByUserId.HasValue).Select(e => e.AllocatedByUserId!.Value).Distinct().ToList();
        var users = userIds.Count == 0
            ? new Dictionary<Guid, UserAccount>()
            : (await _userRepository.FindAsync(u => userIds.Contains(u.Id), cancellationToken)).ToDictionary(u => u.Id);
        var currencyLabels = await BuildCurrencyLabelMapAsync(entries, cancellationToken);

        return entries.Select(e =>
        {
            payments.TryGetValue(e.PaymentId, out var payment);
            students.TryGetValue(payment?.StudentId ?? Guid.Empty, out var student);
            destinations.TryGetValue(e.DestinationId, out var dest);
            RevenueAllocationKey? key = null;
            if (e.AllocationKeyId.HasValue)
            {
                keys.TryGetValue(e.AllocationKeyId.Value, out key);
            }

            years.TryGetValue(e.AcademicYearId, out var year);
            feeTypes.TryGetValue(e.FeeTypeId ?? Guid.Empty, out var feeType);
            users.TryGetValue(e.AllocatedByUserId ?? Guid.Empty, out var user);

            return new RevenueAllocationEntryDto(
                e.Id,
                e.PaymentId,
                payment?.ReceiptNumber ?? "—",
                payment?.StudentId ?? Guid.Empty,
                StudentDisplayName.FormatOrDefault(student),
                payment?.TotalAmount ?? 0,
                e.DestinationId,
                dest?.Code ?? "—",
                dest?.Name ?? "—",
                e.CurrencyId,
                ResolveCurrencyCode(e.CurrencyId, currencyLabels),
                e.Amount,
                e.AppliedPercentage,
                e.CalculationType,
                e.AllocationKeyId,
                key?.Name ?? (e.AllocationKeyId is null ? "Compte principal (défaut)" : "—"),
                e.AcademicYearId,
                year?.Label ?? "—",
                e.FeeTypeId,
                feeType?.Name,
                e.AllocatedAt,
                user is null ? null : $"{user.LastName} {user.FirstName}");
        }).ToList();
    }

    private async Task<Dictionary<Guid, string>> BuildCurrencyLabelMapAsync(
        IReadOnlyList<RevenueAllocationEntry> entries,
        CancellationToken cancellationToken)
    {
        var ids = entries.Where(e => e.CurrencyId.HasValue).Select(e => e.CurrencyId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return (await _currencyRepository.FindAsync(c => ids.Contains(c.Id), cancellationToken))
            .ToDictionary(c => c.Id, c => c.Code);
    }

    private static string ResolveCurrencyCode(Guid? currencyId, IReadOnlyDictionary<Guid, string> labels)
    {
        if (currencyId.HasValue && labels.TryGetValue(currencyId.Value, out var code))
        {
            return code;
        }

        return currencyId.HasValue ? "?" : "—";
    }

    private async Task<List<(Guid DestinationId, Guid? CurrencyId, decimal Amount, Guid PaymentId)>> ExpandExpenseSpendLinesAsync(
        Guid schoolId,
        IReadOnlyList<ExpensePayment> expensePayments,
        IReadOnlyDictionary<Guid, Guid?> expenseCurrencyMap,
        CancellationToken cancellationToken)
    {
        var result = new List<(Guid DestinationId, Guid? CurrencyId, decimal Amount, Guid PaymentId)>();
        if (expensePayments.Count == 0)
        {
            return result;
        }

        var paymentIds = expensePayments.Select(p => p.Id).ToList();
        var allocations = (await _expenseAllocationRepository.FindAsync(
                a => a.SchoolId == schoolId && paymentIds.Contains(a.ExpensePaymentId),
                cancellationToken))
            .ToList();
        var byPayment = allocations.GroupBy(a => a.ExpensePaymentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var payment in expensePayments)
        {
            if (byPayment.TryGetValue(payment.Id, out var lines) && lines.Count > 0)
            {
                foreach (var line in lines)
                {
                    result.Add((payment.DestinationId, line.CurrencyId, line.Amount, payment.Id));
                }
            }
            else
            {
                result.Add((
                    payment.DestinationId,
                    expenseCurrencyMap.GetValueOrDefault(payment.Id) ?? payment.PrimaryCurrencyId,
                    payment.Amount,
                    payment.Id));
            }
        }

        return result;
    }

    private async Task<Dictionary<Guid, Guid?>> BuildExpenseCurrencyIdMapAsync(
        Guid schoolId,
        IReadOnlyList<ExpensePayment> expenses,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<Guid, Guid?>();
        if (expenses.Count == 0)
        {
            return map;
        }

        var cache = new Dictionary<string, Guid?>(StringComparer.OrdinalIgnoreCase);
        foreach (var expense in expenses)
        {
            var code = expense.Currency.ToString();
            if (!cache.TryGetValue(code, out var currencyId))
            {
                var resolved = await _currencyService.ResolveByEnumCodeAsync(code, cancellationToken);
                currencyId = resolved?.Id;
                if (currencyId is null)
                {
                    try
                    {
                        currencyId = (await _currencyService.GetMainCurrencyAsync(schoolId, cancellationToken)).Id;
                    }
                    catch
                    {
                        currencyId = null;
                    }
                }

                cache[code] = currencyId;
            }

            map[expense.Id] = currencyId;
        }

        return map;
    }

    private async Task<RevenueAllocationKeyDto> MapKeyAsync(
        Guid schoolId,
        RevenueAllocationKey key,
        CancellationToken cancellationToken)
    {
        var year = await GetYearAsync(schoolId, key.AcademicYearId, cancellationToken);
        FeeType? feeType = null;
        WithholdingType? withholdingType = null;
        if (key.FeeTypeId.HasValue)
        {
            feeType = await GetFeeTypeAsync(schoolId, key.FeeTypeId.Value, cancellationToken);
        }
        else if (key.WithholdingTypeId.HasValue)
        {
            withholdingType = await GetWithholdingTypeAsync(schoolId, key.WithholdingTypeId.Value, cancellationToken);
        }

        var details = await LoadDetailsWithDestinationsAsync(key.Id, cancellationToken);
        var detailDtos = details
            .OrderBy(d => d.SortOrder)
            .Select(d => new RevenueAllocationKeyDetailDto(
                d.Id,
                d.DestinationId,
                d.Destination.Code,
                d.Destination.Name,
                AllocationCalculationType.Pourcentage,
                d.Value,
                d.SortOrder))
            .ToList();

        var hasHistory = (await _entryRepository.FindAsync(e => e.AllocationKeyId == key.Id, cancellationToken)).Count > 0;

        return new RevenueAllocationKeyDto(
            key.Id,
            key.AcademicYearId,
            year.Label,
            key.SourceKind,
            key.FeeTypeId,
            feeType?.Code,
            feeType?.Name,
            key.WithholdingTypeId,
            withholdingType?.Code,
            withholdingType?.Name,
            key.Name,
            key.Notes,
            key.StartDate,
            key.EndDate,
            key.IsActive && key.EndDate is null,
            hasHistory,
            !hasHistory,
            detailDtos,
            detailDtos.Sum(d => d.Value));
    }

    private async Task<List<RevenueAllocationKeyDetail>> LoadDetailsWithDestinationsAsync(
        Guid keyId,
        CancellationToken cancellationToken)
    {
        var details = (await _detailRepository.FindAsync(d => d.AllocationKeyId == keyId, cancellationToken)).ToList();
        var destinationIds = details.Select(d => d.DestinationId).Distinct().ToList();
        var destinations = (await _destinationRepository.FindAsync(d => destinationIds.Contains(d.Id), cancellationToken))
            .ToDictionary(d => d.Id);

        foreach (var detail in details)
        {
            if (destinations.TryGetValue(detail.DestinationId, out var destination))
            {
                detail.Destination = destination;
            }

            detail.CalculationType = AllocationCalculationType.Pourcentage;
        }

        return details;
    }

    private async Task ReplaceDetailsAsync(
        Guid keyId,
        IReadOnlyList<SaveRevenueAllocationKeyDetailRequest> details,
        CancellationToken cancellationToken)
    {
        var sort = 1;
        foreach (var item in details)
        {
            await _detailRepository.AddAsync(new RevenueAllocationKeyDetail
            {
                AllocationKeyId = keyId,
                DestinationId = item.DestinationId,
                CalculationType = AllocationCalculationType.Pourcentage,
                Value = item.Value,
                SortOrder = item.SortOrder > 0 ? item.SortOrder : sort
            }, cancellationToken);
            sort++;
        }
    }

    private async Task ValidateDetailsAsync(
        Guid schoolId,
        IReadOnlyList<SaveRevenueAllocationKeyDetailRequest> details,
        CancellationToken cancellationToken)
    {
        if (details.Count == 0)
        {
            throw new DomainException("Ajoutez au moins une ligne de répartition.");
        }

        if (details.GroupBy(d => d.DestinationId).Any(g => g.Count() > 1))
        {
            throw new DomainException("Une destination ne peut apparaître qu'une seule fois dans la clé.");
        }

        var destinations = (await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);

        var tempDetails = new List<RevenueAllocationKeyDetail>();
        foreach (var item in details)
        {
            if (!destinations.TryGetValue(item.DestinationId, out var destination))
            {
                throw new DomainException("Destination introuvable.");
            }

            tempDetails.Add(new RevenueAllocationKeyDetail
            {
                DestinationId = item.DestinationId,
                Destination = destination,
                CalculationType = AllocationCalculationType.Pourcentage,
                Value = item.Value,
                SortOrder = item.SortOrder
            });
        }

        var errors = _engine.ValidateKeyForActivation(tempDetails);
        if (errors.Count > 0)
        {
            throw new DomainException(string.Join(" ", errors));
        }
    }

    private async Task<RevenueAllocationDestination> GetDestinationEntityAsync(
        Guid schoolId,
        Guid destinationId,
        CancellationToken cancellationToken) =>
        (await _destinationRepository.FindAsync(d => d.Id == destinationId && d.SchoolId == schoolId, cancellationToken))
            .FirstOrDefault()
        ?? throw new KeyNotFoundException("Destination introuvable.");

    private async Task<RevenueAllocationKey> GetKeyEntityAsync(
        Guid schoolId,
        Guid keyId,
        CancellationToken cancellationToken) =>
        (await _keyRepository.FindAsync(k => k.Id == keyId && k.SchoolId == schoolId, cancellationToken))
            .FirstOrDefault()
        ?? throw new KeyNotFoundException("Clé de répartition introuvable.");

    private async Task<AcademicYear> GetYearAsync(Guid schoolId, Guid yearId, CancellationToken cancellationToken) =>
        (await _yearRepository.FindAsync(y => y.Id == yearId && y.SchoolId == schoolId, cancellationToken))
            .FirstOrDefault()
        ?? throw new KeyNotFoundException("Année scolaire introuvable.");

    private async Task<FeeType> GetFeeTypeAsync(Guid schoolId, Guid feeTypeId, CancellationToken cancellationToken) =>
        (await _feeTypeRepository.FindAsync(f => f.Id == feeTypeId && f.SchoolId == schoolId, cancellationToken))
            .FirstOrDefault()
        ?? throw new KeyNotFoundException("Type de frais introuvable.");

    private async Task<WithholdingType> GetWithholdingTypeAsync(
        Guid schoolId,
        Guid withholdingTypeId,
        CancellationToken cancellationToken) =>
        (await _withholdingTypeRepository.FindAsync(
            w => w.Id == withholdingTypeId && w.SchoolId == schoolId,
            cancellationToken))
            .FirstOrDefault()
        ?? throw new KeyNotFoundException("Type de retenue introuvable.");

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Le code de destination est obligatoire.");
        }

        return code.Trim().ToUpperInvariant();
    }
}
