namespace SchoolManagement.Application.RevenueAllocation.Services;

using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.RevenueAllocation.DTOs;
using SchoolManagement.Application.RevenueAllocation.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class RevenueAllocationService : IRevenueAllocationService
{
    private readonly IRepository<RevenueAllocationDestination> _destinationRepository;
    private readonly IRepository<RevenueAllocationKey> _keyRepository;
    private readonly IRepository<RevenueAllocationKeyDetail> _detailRepository;
    private readonly IRepository<RevenueAllocationEntry> _entryRepository;
    private readonly IRepository<ExpensePayment> _expensePaymentRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentLine> _paymentLineRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<Section> _sectionRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<UserAccount> _userRepository;
    private readonly IRevenueAllocationEngine _engine;
    private readonly IUnitOfWork _unitOfWork;

    public RevenueAllocationService(
        IRepository<RevenueAllocationDestination> destinationRepository,
        IRepository<RevenueAllocationKey> keyRepository,
        IRepository<RevenueAllocationKeyDetail> detailRepository,
        IRepository<RevenueAllocationEntry> entryRepository,
        IRepository<ExpensePayment> expensePaymentRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<Section> sectionRepository,
        IRepository<Student> studentRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<UserAccount> userRepository,
        IRevenueAllocationEngine engine,
        IUnitOfWork unitOfWork)
    {
        _destinationRepository = destinationRepository;
        _keyRepository = keyRepository;
        _detailRepository = detailRepository;
        _entryRepository = entryRepository;
        _expensePaymentRepository = expensePaymentRepository;
        _paymentRepository = paymentRepository;
        _paymentLineRepository = paymentLineRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRoomRepository = classRoomRepository;
        _sectionRepository = sectionRepository;
        _studentRepository = studentRepository;
        _yearRepository = yearRepository;
        _feeTypeRepository = feeTypeRepository;
        _userRepository = userRepository;
        _engine = engine;
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
        var feeType = await GetFeeTypeAsync(schoolId, request.FeeTypeId, cancellationToken);
        await ValidateDetailsAsync(schoolId, request.Details, cancellationToken);

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

        var key = new RevenueAllocationKey
        {
            SchoolId = schoolId,
            AcademicYearId = request.AcademicYearId,
            FeeTypeId = feeType.Id,
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? $"Répartition {feeType.Name}"
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
            .GroupBy(k => k.FeeTypeId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(k => k.StartDate).ThenByDescending(k => k.CreatedAt).First());

        var lines = paymentLines.Where(l => l.Amount > 0).ToList();
        if (lines.Count == 0)
        {
            throw new DomainException("Aucun montant à répartir sur ce paiement.");
        }

        var now = DateTime.UtcNow;
        foreach (var paymentLine in lines)
        {
            if (!keysByFeeType.TryGetValue(paymentLine.FeeTypeId, out var key))
            {
                var feeType = (await _feeTypeRepository.FindAsync(
                    f => f.Id == paymentLine.FeeTypeId && f.SchoolId == schoolId,
                    cancellationToken)).FirstOrDefault();
                var label = feeType?.Name ?? paymentLine.FeeTypeId.ToString();
                throw new DomainException(
                    $"Aucune répartition ouverte pour le type de frais « {label} » à la date du paiement. Configurez une clé de répartition (pourcentages) avant d'encaisser.");
            }

            var details = await LoadDetailsWithDestinationsAsync(key.Id, cancellationToken);
            if (details.Count == 0)
            {
                throw new DomainException($"La clé de répartition « {key.Name} » ne contient aucune ligne.");
            }

            var calculated = _engine.Calculate(paymentLine.Amount, details);
            foreach (var item in calculated.Where(c => c.Amount != 0))
            {
                await _entryRepository.AddAsync(new RevenueAllocationEntry
                {
                    SchoolId = schoolId,
                    PaymentId = payment.Id,
                    AllocationKeyId = key.Id,
                    DestinationId = item.DestinationId,
                    FeeTypeId = paymentLine.FeeTypeId,
                    AcademicYearId = payment.AcademicYearId,
                    Amount = item.Amount,
                    AppliedPercentage = item.AppliedPercentage,
                    CalculationType = AllocationCalculationType.Pourcentage,
                    AllocatedAt = now,
                    AllocatedByUserId = userId
                }, cancellationToken);
            }
        }
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

        return filtered
            .Where(e => e.FeeTypeId.HasValue)
            .GroupBy(e => e.FeeTypeId!.Value)
            .Select(feeGroup =>
            {
                feeTypes.TryGetValue(feeGroup.Key, out var feeType);
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
                            Math.Round(percentage, 2),
                            amount);
                    })
                    .OrderByDescending(r => r.AllocatedAmount)
                    .ThenBy(r => r.DestinationName)
                    .ToList();

                return new FeeTypeAllocationSummaryGroupDto(
                    feeGroup.Key,
                    feeType?.Code ?? "—",
                    feeType?.Name ?? "—",
                    feeTotal,
                    destinationRows);
            })
            .OrderBy(g => g.FeeTypeName)
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

        var entries = await _entryRepository.FindAsync(e => e.SchoolId == schoolId, cancellationToken);
        var expenses = await _expensePaymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        var destinations = (await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);

        var scopedPaymentIds = await ResolveScopedPaymentIdsAsync(schoolId, request, cancellationToken);

        IEnumerable<RevenueAllocationEntry> FilterEntries(IEnumerable<RevenueAllocationEntry> source, DateOnly? from, DateOnly? to)
        {
            var query = source;
            if (request.AcademicYearId.HasValue)
            {
                query = query.Where(e => e.AcademicYearId == request.AcademicYearId);
            }

            if (request.FeeTypeId.HasValue)
            {
                query = query.Where(e => e.FeeTypeId == request.FeeTypeId);
            }

            if (request.DestinationId.HasValue)
            {
                query = query.Where(e => e.DestinationId == request.DestinationId);
            }

            if (scopedPaymentIds is not null)
            {
                query = query.Where(e => scopedPaymentIds.Contains(e.PaymentId));
            }

            if (from.HasValue)
            {
                query = query.Where(e => DateOnly.FromDateTime(e.AllocatedAt) >= from);
            }

            if (to.HasValue)
            {
                query = query.Where(e => DateOnly.FromDateTime(e.AllocatedAt) <= to);
            }

            return query;
        }

        IEnumerable<ExpensePayment> FilterExpenses(IEnumerable<ExpensePayment> source, DateOnly? from, DateOnly? to)
        {
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

        var periodEntries = FilterEntries(entries, fromDate, toDate).ToList();
        var periodExpenses = FilterExpenses(expenses, fromDate, toDate).ToList();
        var openingEntries = FilterEntries(entries, null, fromDate.AddDays(-1)).ToList();
        var openingExpenses = FilterExpenses(expenses, null, fromDate.AddDays(-1)).ToList();

        var destinationIds = periodEntries.Select(e => e.DestinationId)
            .Concat(periodExpenses.Select(p => p.DestinationId))
            .Concat(openingEntries.Select(e => e.DestinationId))
            .Concat(openingExpenses.Select(p => p.DestinationId))
            .Distinct()
            .ToList();

        if (request.DestinationId.HasValue && !destinationIds.Contains(request.DestinationId.Value))
        {
            destinationIds.Add(request.DestinationId.Value);
        }

        AllocationCashFlowRowDto BuildRow(Guid destinationId, decimal j1Enc, decimal j1Dep, decimal enc, decimal dep)
        {
            destinations.TryGetValue(destinationId, out var destination);
            var periodJ1 = j1Enc - j1Dep;
            return new AllocationCashFlowRowDto(
                destinationId,
                destination?.Code ?? "—",
                destination?.Name ?? "—",
                periodJ1,
                enc,
                dep,
                periodJ1 + enc - dep);
        }

        var globalRows = destinationIds
            .Select(id => BuildRow(
                id,
                openingEntries.Where(e => e.DestinationId == id).Sum(e => e.Amount),
                openingExpenses.Where(p => p.DestinationId == id).Sum(p => p.Amount),
                periodEntries.Where(e => e.DestinationId == id).Sum(e => e.Amount),
                periodExpenses.Where(p => p.DestinationId == id).Sum(p => p.Amount)))
            .OrderBy(r => r.DestinationName)
            .ToList();

        var totals = new AllocationCashFlowRowDto(
            Guid.Empty,
            "TOTAL",
            "Total général",
            globalRows.Sum(r => r.PeriodJ1),
            globalRows.Sum(r => r.Encaissement),
            globalRows.Sum(r => r.DepenseP),
            globalRows.Sum(r => r.PeriodeP));

        var dailyGroups = new List<AllocationCashFlowDailyGroupDto>();
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            var dayEntries = FilterEntries(entries, date, date).ToList();
            var dayExpenses = FilterExpenses(expenses, date, date).ToList();
            var dayDestinationIds = dayEntries.Select(e => e.DestinationId)
                .Concat(dayExpenses.Select(p => p.DestinationId))
                .Distinct()
                .ToList();

            if (dayDestinationIds.Count == 0)
            {
                continue;
            }

            var dayBefore = date.AddDays(-1);
            var rows = dayDestinationIds
                .Select(id => BuildRow(
                    id,
                    FilterEntries(entries, null, dayBefore).Where(e => e.DestinationId == id).Sum(e => e.Amount),
                    FilterExpenses(expenses, null, dayBefore).Where(p => p.DestinationId == id).Sum(p => p.Amount),
                    dayEntries.Where(e => e.DestinationId == id).Sum(e => e.Amount),
                    dayExpenses.Where(p => p.DestinationId == id).Sum(p => p.Amount)))
                .OrderBy(r => r.DestinationName)
                .ToList();

            dailyGroups.Add(new AllocationCashFlowDailyGroupDto(date, rows));
        }

        return new AllocationCashFlowResultDto(globalRows, dailyGroups, totals);
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

        var byDest = entries
            .GroupBy(e => e.DestinationId)
            .Select(g =>
            {
                destinations.TryGetValue(g.Key, out var dest);
                return new DestinationTotalDto(g.Key, dest?.Code ?? "—", dest?.Name ?? "—", g.Sum(x => x.Amount));
            })
            .OrderByDescending(t => t.Total)
            .ToList();

        var byFee = entries
            .Where(e => e.FeeTypeId.HasValue)
            .GroupBy(e => e.FeeTypeId!.Value)
            .Select(g =>
            {
                feeTypes.TryGetValue(g.Key, out var fee);
                return new FeeTypeTotalDto(g.Key, fee?.Code ?? "—", fee?.Name ?? "—", g.Sum(x => x.Amount));
            })
            .OrderByDescending(t => t.Total)
            .ToList();

        return new RevenueAllocationTotalsDto(entries.Sum(e => e.Amount), byDest, byFee);
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
            "Reçu", "Élève", "Montant payé", "Destination", "Code", "Montant réparti",
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
            sheet.Cell(row, 6).Value = item.AllocatedAmount;
            sheet.Cell(row, 7).Value = item.AppliedPercentage;
            sheet.Cell(row, 8).Value = item.CalculationType.ToString();
            sheet.Cell(row, 9).Value = item.AllocationKeyName;
            sheet.Cell(row, 10).Value = item.AcademicYearLabel;
            sheet.Cell(row, 11).Value = item.FeeTypeName;
            sheet.Cell(row, 12).Value = item.AllocatedAt;
            sheet.Cell(row, 13).Value = item.AllocatedBy;
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
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.5f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Reçu").SemiBold();
                        header.Cell().Text("Élève").SemiBold();
                        header.Cell().Text("Destination").SemiBold();
                        header.Cell().AlignRight().Text("Montant").SemiBold();
                        header.Cell().Text("Date").SemiBold();
                    });

                    foreach (var item in result.Items)
                    {
                        table.Cell().Text(item.ReceiptNumber);
                        table.Cell().Text(item.StudentName);
                        table.Cell().Text(item.DestinationName);
                        table.Cell().AlignRight().Text($"{item.AllocatedAmount:N2}");
                        table.Cell().Text($"{item.AllocatedAt:dd/MM/yyyy}");
                    }
                });

                page.Footer().AlignRight().Text($"Total : {result.Totals.GrandTotal:N2}");
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

        if (request.FromDate.HasValue)
        {
            query = query.Where(e => DateOnly.FromDateTime(e.AllocatedAt) >= request.FromDate);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(e => DateOnly.FromDateTime(e.AllocatedAt) <= request.ToDate);
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

        if (request.StudentId.HasValue)
        {
            var paymentIds = (await _paymentRepository.FindAsync(
                p => p.SchoolId == schoolId && p.StudentId == request.StudentId, cancellationToken))
                .Select(p => p.Id)
                .ToHashSet();
            query = query.Where(e => paymentIds.Contains(e.PaymentId));
        }

        var scopedPaymentIds = await ResolveScopedPaymentIdsAsync(schoolId, request, cancellationToken);
        if (scopedPaymentIds is not null)
        {
            query = query.Where(e => scopedPaymentIds.Contains(e.PaymentId));
        }

        return query.ToList();
    }

    /// <summary>Filtre section/classe aligné sur le rapport recettes réalisées.</summary>
    private async Task<HashSet<Guid>?> ResolveScopedPaymentIdsAsync(
        Guid schoolId,
        RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.SectionId.HasValue && !request.ClassRoomId.HasValue)
        {
            return null;
        }

        var payments = (await _paymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken)).ToList();
        if (payments.Count == 0)
        {
            return [];
        }

        if (request.AcademicYearId.HasValue)
        {
            payments = payments.Where(p => p.AcademicYearId == request.AcademicYearId.Value).ToList();
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

        var studentIds = payments.Select(p => p.StudentId).Distinct().ToList();
        var enrollments = studentIds.Count == 0
            ? []
            : await _enrollmentRepository.FindAsync(
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

        return payments.Select(p => p.Id).ToHashSet();
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

        return entries.Select(e =>
        {
            payments.TryGetValue(e.PaymentId, out var payment);
            students.TryGetValue(payment?.StudentId ?? Guid.Empty, out var student);
            destinations.TryGetValue(e.DestinationId, out var dest);
            keys.TryGetValue(e.AllocationKeyId, out var key);
            years.TryGetValue(e.AcademicYearId, out var year);
            feeTypes.TryGetValue(e.FeeTypeId ?? Guid.Empty, out var feeType);
            users.TryGetValue(e.AllocatedByUserId ?? Guid.Empty, out var user);

            return new RevenueAllocationEntryDto(
                e.Id,
                e.PaymentId,
                payment?.ReceiptNumber ?? "—",
                payment?.StudentId ?? Guid.Empty,
                student is null ? "—" : $"{student.LastName} {student.FirstName}",
                payment?.TotalAmount ?? 0,
                e.DestinationId,
                dest?.Code ?? "—",
                dest?.Name ?? "—",
                e.Amount,
                e.AppliedPercentage,
                e.CalculationType,
                e.AllocationKeyId,
                key?.Name ?? "—",
                e.AcademicYearId,
                year?.Label ?? "—",
                e.FeeTypeId,
                feeType?.Name,
                e.AllocatedAt,
                user is null ? null : $"{user.LastName} {user.FirstName}");
        }).ToList();
    }

    private async Task<RevenueAllocationKeyDto> MapKeyAsync(
        Guid schoolId,
        RevenueAllocationKey key,
        CancellationToken cancellationToken)
    {
        var year = await GetYearAsync(schoolId, key.AcademicYearId, cancellationToken);
        var feeType = await GetFeeTypeAsync(schoolId, key.FeeTypeId, cancellationToken);
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
            key.FeeTypeId,
            feeType.Code,
            feeType.Name,
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

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Le code de destination est obligatoire.");
        }

        return code.Trim().ToUpperInvariant();
    }
}
