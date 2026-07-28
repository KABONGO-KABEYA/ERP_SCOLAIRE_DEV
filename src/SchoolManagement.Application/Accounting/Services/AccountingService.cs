namespace SchoolManagement.Application.Accounting.Services;

using SchoolManagement.Application.Accounting.DTOs;
using SchoolManagement.Application.Accounting.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.CurrencyManagement.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class AccountingService : IAccountingService
{
    private readonly IRepository<ExpenseRequest> _requestRepository;
    private readonly IRepository<ExpensePayment> _paymentRepository;
    private readonly IRepository<ExpensePaymentAllocation> _allocationRepository;
    private readonly IRepository<RevenueAllocationEntry> _entryRepository;
    private readonly IRepository<RevenueAllocationKey> _keyRepository;
    private readonly IRepository<RevenueAllocationKeyDetail> _keyDetailRepository;
    private readonly IRepository<RevenueAllocationDestination> _destinationRepository;
    private readonly IRepository<Payment> _tuitionPaymentRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<CurrencyDefinition> _currencyRepository;
    private readonly ICurrencyService _currencyService;
    private readonly IUnitOfWork _unitOfWork;

    public AccountingService(
        IRepository<ExpenseRequest> requestRepository,
        IRepository<ExpensePayment> paymentRepository,
        IRepository<ExpensePaymentAllocation> allocationRepository,
        IRepository<RevenueAllocationEntry> entryRepository,
        IRepository<RevenueAllocationKey> keyRepository,
        IRepository<RevenueAllocationKeyDetail> keyDetailRepository,
        IRepository<RevenueAllocationDestination> destinationRepository,
        IRepository<Payment> tuitionPaymentRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<CurrencyDefinition> currencyRepository,
        ICurrencyService currencyService,
        IUnitOfWork unitOfWork)
    {
        _requestRepository = requestRepository;
        _paymentRepository = paymentRepository;
        _allocationRepository = allocationRepository;
        _entryRepository = entryRepository;
        _keyRepository = keyRepository;
        _keyDetailRepository = keyDetailRepository;
        _destinationRepository = destinationRepository;
        _tuitionPaymentRepository = tuitionPaymentRepository;
        _yearRepository = yearRepository;
        _currencyRepository = currencyRepository;
        _currencyService = currencyService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ExpenseRequestSearchResultDto> SearchExpenseRequestsAsync(
        Guid schoolId,
        ExpenseSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var items = await FilterRequestsAsync(schoolId, request, cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var pageItems = items
            .OrderByDescending(r => r.RequestDate)
            .ThenByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new ExpenseRequestSearchResultDto(
            await MapRequestsAsync(schoolId, pageItems, cancellationToken),
            items.Count);
    }

    public async Task<ExpensePaymentSearchResultDto> SearchExpensePaymentsAsync(
        Guid schoolId,
        ExpenseSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var items = await FilterPaymentsAsync(schoolId, request, cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var pageItems = items
            .OrderByDescending(p => p.ExpenseDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new ExpensePaymentSearchResultDto(
            await MapPaymentsAsync(schoolId, pageItems, cancellationToken),
            items.Count,
            items.Sum(p => p.Amount));
    }

    public async Task<IReadOnlyList<ExpenseDestinationBalanceDto>> GetExpenseBalancesAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        await EnsureYearAsync(schoolId, academicYearId, cancellationToken);

        var keyedDestinationIds = await GetKeyedDestinationIdsAsync(schoolId, academicYearId, cancellationToken);
        if (keyedDestinationIds.Count == 0)
        {
            return [];
        }

        var destinations = (await _destinationRepository.FindAsync(
                d => d.SchoolId == schoolId && d.IsActive && keyedDestinationIds.Contains(d.Id),
                cancellationToken))
            .ToDictionary(d => d.Id);

        var allocationEntries = (await _entryRepository.FindAsync(
                e => e.SchoolId == schoolId && e.AcademicYearId == academicYearId,
                cancellationToken))
            .Where(e => keyedDestinationIds.Contains(e.DestinationId))
            .ToList();

        var expensePayments = (await _paymentRepository.FindAsync(
                p => p.SchoolId == schoolId && p.AcademicYearId == academicYearId,
                cancellationToken))
            .Where(p => keyedDestinationIds.Contains(p.DestinationId))
            .ToList();

        // Compléter CurrencyId manquant sur anciennes écritures via le paiement.
        await BackfillEntryCurrencyFromPaymentsAsync(allocationEntries, cancellationToken);

        var currencyIds = allocationEntries
            .Where(e => e.CurrencyId.HasValue)
            .Select(e => e.CurrencyId!.Value)
            .Distinct()
            .ToList();
        var currencyLabels = currencyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _currencyRepository.FindAsync(c => currencyIds.Contains(c.Id), cancellationToken))
                .ToDictionary(c => c.Id, c => c.Code);

        var expenseCurrencyMap = await ResolveExpenseCurrencyIdsAsync(schoolId, expensePayments, cancellationToken);
        foreach (var id in expenseCurrencyMap.Values.Where(v => v.HasValue).Select(v => v!.Value))
        {
            if (!currencyLabels.ContainsKey(id))
            {
                currencyIds.Add(id);
            }
        }

        if (currencyIds.Count > currencyLabels.Count)
        {
            var missing = currencyIds.Where(id => !currencyLabels.ContainsKey(id)).Distinct().ToList();
            foreach (var c in await _currencyRepository.FindAsync(x => missing.Contains(x.Id), cancellationToken))
            {
                currencyLabels[c.Id] = c.Code;
            }
        }

        static string CodeOf(Guid? currencyId, IReadOnlyDictionary<Guid, string> labels) =>
            currencyId.HasValue && labels.TryGetValue(currencyId.Value, out var code)
                ? code
                : currencyId.HasValue ? "?" : "—";

        var allocated = allocationEntries
            .GroupBy(e => (e.DestinationId, e.CurrencyId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var spentLines = await ExpandExpenseSpendLinesAsync(
            schoolId,
            expensePayments,
            expenseCurrencyMap,
            cancellationToken);
        foreach (var id in spentLines.Where(l => l.CurrencyId.HasValue).Select(l => l.CurrencyId!.Value))
        {
            if (!currencyLabels.ContainsKey(id))
            {
                var c = await _currencyRepository.GetByIdAsync(id, cancellationToken);
                if (c is not null)
                {
                    currencyLabels[c.Id] = c.Code;
                }
            }
        }

        var spent = spentLines
            .GroupBy(l => (l.DestinationId, l.CurrencyId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var keys = allocated.Keys
            .Concat(spent.Keys)
            .Distinct()
            .Where(k => destinations.ContainsKey(k.DestinationId))
            .ToList();

        // Garantit une ligne par compte actif même sans mouvement (devise principale).
        Guid? mainCurrencyId = null;
        try
        {
            mainCurrencyId = (await _currencyService.GetMainCurrencyAsync(schoolId, cancellationToken)).Id;
            if (mainCurrencyId.HasValue && !currencyLabels.ContainsKey(mainCurrencyId.Value))
            {
                var main = await _currencyRepository.GetByIdAsync(mainCurrencyId.Value, cancellationToken);
                if (main is not null)
                {
                    currencyLabels[main.Id] = main.Code;
                }
            }
        }
        catch
        {
            // ignore
        }

        foreach (var destinationId in destinations.Keys)
        {
            if (!keys.Any(k => k.DestinationId == destinationId))
            {
                keys.Add((destinationId, mainCurrencyId));
            }
        }

        return keys
            .Select(k =>
            {
                var dest = destinations[k.DestinationId];
                allocated.TryGetValue(k, out var alloc);
                spent.TryGetValue(k, out var spentAmount);
                return new ExpenseDestinationBalanceDto(
                    dest.Id,
                    dest.Code,
                    dest.Name,
                    k.CurrencyId,
                    CodeOf(k.CurrencyId, currencyLabels),
                    alloc,
                    spentAmount,
                    alloc - spentAmount);
            })
            .OrderBy(b => b.DestinationName)
            .ThenBy(b => b.Currency)
            .ToList();
    }

    private async Task BackfillEntryCurrencyFromPaymentsAsync(
        IReadOnlyList<RevenueAllocationEntry> entries,
        CancellationToken cancellationToken)
    {
        var missing = entries.Where(e => !e.CurrencyId.HasValue).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var paymentIds = missing.Select(e => e.PaymentId).Distinct().ToList();
        var payments = (await _tuitionPaymentRepository.FindAsync(p => paymentIds.Contains(p.Id), cancellationToken))
            .ToDictionary(p => p.Id);

        foreach (var entry in missing)
        {
            if (!payments.TryGetValue(entry.PaymentId, out var payment))
            {
                continue;
            }

            entry.CurrencyId = payment.FeeCurrencyId
                ?? payment.PaymentCurrencyId;
            if (!entry.CurrencyId.HasValue)
            {
                var resolved = await _currencyService.ResolveByEnumCodeAsync(
                    payment.Currency.ToString(),
                    cancellationToken);
                entry.CurrencyId = resolved?.Id;
            }
        }
    }

    private async Task<Dictionary<Guid, Guid?>> ResolveExpenseCurrencyIdsAsync(
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

    public async Task<ExpenseRequestDto> CreateExpenseRequestAsync(
        Guid schoolId,
        CreateExpenseRequestRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDestinationAsync(schoolId, request.DestinationId, cancellationToken);
        await EnsureYearAsync(schoolId, request.AcademicYearId, cancellationToken);

        var entity = new ExpenseRequest
        {
            SchoolId = schoolId,
            AcademicYearId = request.AcademicYearId,
            DestinationId = request.DestinationId,
            Reference = await GenerateRequestReferenceAsync(schoolId, cancellationToken),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            RequestedAmount = request.RequestedAmount,
            Currency = request.Currency,
            RequestDate = request.RequestDate,
            Status = ExpenseRequestStatus.Brouillon,
            CreatedBy = userId
        };

        await _requestRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await MapRequestsAsync(schoolId, [entity], cancellationToken)).Single();
    }

    public async Task<ExpenseRequestDto> SubmitExpenseRequestAsync(
        Guid schoolId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetRequestAsync(schoolId, requestId, cancellationToken);
        if (entity.Status != ExpenseRequestStatus.Brouillon)
        {
            throw new DomainException("Seule une demande en brouillon peut être soumise.");
        }

        entity.Status = ExpenseRequestStatus.Soumise;
        entity.SubmittedAt = DateTime.UtcNow;
        await _requestRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await MapRequestsAsync(schoolId, [entity], cancellationToken)).Single();
    }

    public async Task<ExpenseRequestDto> ApproveExpenseRequestAsync(
        Guid schoolId,
        Guid requestId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetRequestAsync(schoolId, requestId, cancellationToken);
        if (entity.Status != ExpenseRequestStatus.Soumise)
        {
            throw new DomainException("Seule une demande soumise peut être approuvée.");
        }

        entity.Status = ExpenseRequestStatus.Approuvee;
        entity.ApprovedAt = DateTime.UtcNow;
        entity.ApprovedByUserId = userId;
        await _requestRepository.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await MapRequestsAsync(schoolId, [entity], cancellationToken)).Single();
    }

    public async Task<ExpensePaymentDto> CreateExpensePaymentAsync(
        Guid schoolId,
        CreateExpensePaymentRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
        {
            throw new DomainException("Le libellé de la dépense est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.BeneficiaryName))
        {
            throw new DomainException("Le nom du bénéficiaire est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.AuthorizedByName))
        {
            throw new DomainException("Le nom de la personne ayant autorisé la dépense est obligatoire.");
        }

        if (request.Amount <= 0)
        {
            throw new DomainException("Le montant de la dépense doit être supérieur à zéro.");
        }

        await EnsureDestinationAsync(schoolId, request.DestinationId, cancellationToken);
        await EnsureYearAsync(schoolId, request.AcademicYearId, cancellationToken);

        var primaryCurrencyId = await ResolvePrimaryCurrencyIdAsync(
            schoolId,
            request.Currency,
            request.PrimaryCurrencyId,
            cancellationToken);
        var primaryCode = request.Currency.ToString();
        if (primaryCurrencyId.HasValue)
        {
            var primaryDef = await _currencyRepository.GetByIdAsync(primaryCurrencyId.Value, cancellationToken);
            if (primaryDef is not null)
            {
                primaryCode = primaryDef.Code;
            }
        }

        var balances = await GetExpenseBalancesAsync(schoolId, request.AcademicYearId, cancellationToken);
        var forDestination = balances.Where(b => b.DestinationId == request.DestinationId).ToList();
        if (forDestination.Count == 0)
        {
            throw new DomainException(
                "Ce compte n'apparaît dans aucune clé de répartition active pour l'année scolaire sélectionnée.");
        }

        var allocationLines = (request.CurrencyAllocations ?? [])
            .Where(l => l.Amount > 0)
            .ToList();

        if (allocationLines.Count == 0)
        {
            // Flux mono-devise classique.
            var balance = forDestination.FirstOrDefault(b =>
                (primaryCurrencyId.HasValue && b.CurrencyId == primaryCurrencyId)
                || string.Equals(b.Currency, primaryCode, StringComparison.OrdinalIgnoreCase));
            if (balance is null)
            {
                var available = string.Join(", ",
                    forDestination.Select(b => $"{b.AvailableAmount:N2} {b.Currency}"));
                throw new DomainException(
                    $"Aucun solde en {primaryCode} sur le compte « {forDestination[0].DestinationName} ». " +
                    $"Soldes disponibles : {available}.");
            }

            if (request.Amount > balance.AvailableAmount)
            {
                throw new DomainException(
                    $"Montant supérieur au solde disponible ({balance.AvailableAmount:N2} {balance.Currency}) " +
                    $"sur le compte « {balance.DestinationName} ». Utilisez la répartition multi-devises.");
            }

            allocationLines =
            [
                new CreateExpensePaymentAllocationLine(
                    balance.CurrencyId ?? primaryCurrencyId
                        ?? throw new DomainException($"Devise {primaryCode} introuvable."),
                    request.Amount)
            ];
        }

        if (allocationLines.Select(l => l.CurrencyId).Distinct().Count() != allocationLines.Count)
        {
            throw new DomainException("Une devise ne peut apparaître qu'une seule fois dans la répartition.");
        }

        var resolvedAllocations = new List<(
            Guid CurrencyId,
            string CurrencyCode,
            decimal Amount,
            Guid? ExchangeRateId,
            decimal AppliedRate,
            decimal Equivalent)>();
        decimal totalEquivalent = 0m;

        foreach (var line in allocationLines)
        {
            var currencyDef = await _currencyRepository.GetByIdAsync(line.CurrencyId, cancellationToken)
                ?? throw new DomainException("Devise de répartition introuvable.");

            var balance = forDestination.FirstOrDefault(b => b.CurrencyId == line.CurrencyId)
                ?? forDestination.FirstOrDefault(b =>
                    string.Equals(b.Currency, currencyDef.Code, StringComparison.OrdinalIgnoreCase));

            if (balance is null)
            {
                throw new DomainException(
                    $"Aucun solde en {currencyDef.Code} sur le compte sélectionné.");
            }

            if (line.Amount > balance.AvailableAmount + 0.009m)
            {
                throw new DomainException(
                    $"Montant utilisé en {currencyDef.Code} ({line.Amount:N2}) supérieur au disponible ({balance.AvailableAmount:N2}).");
            }

            decimal appliedRate;
            decimal equivalent;
            Guid? exchangeRateId;

            if (!primaryCurrencyId.HasValue || line.CurrencyId == primaryCurrencyId.Value)
            {
                appliedRate = 1m;
                equivalent = decimal.Round(line.Amount, 2, MidpointRounding.AwayFromZero);
                exchangeRateId = null;
            }
            else
            {
                var conversion = await _currencyService.ConvertAsync(
                    new CurrencyManagement.DTOs.CurrencyConversionRequest(
                        line.CurrencyId,
                        primaryCurrencyId.Value,
                        line.Amount,
                        AsOfDate: request.ExpenseDate,
                        OverrideRate: line.OverrideRate),
                    cancellationToken);
                appliedRate = conversion.AppliedRate;
                equivalent = decimal.Round(conversion.TargetAmount, 2, MidpointRounding.AwayFromZero);
                exchangeRateId = conversion.ExchangeRateId;
            }

            totalEquivalent += equivalent;
            resolvedAllocations.Add((
                line.CurrencyId,
                currencyDef.Code,
                decimal.Round(line.Amount, 2, MidpointRounding.AwayFromZero),
                exchangeRateId,
                appliedRate,
                equivalent));
        }

        if (Math.Abs(totalEquivalent - request.Amount) > 0.05m)
        {
            throw new DomainException(
                $"La répartition multi-devises couvre {totalEquivalent:N2} {primaryCode} " +
                $"au lieu de {request.Amount:N2} {primaryCode}. Ajustez les montants utilisés.");
        }

        if (totalEquivalent + 0.009m < request.Amount)
        {
            throw new DomainException(
                $"Solde cumulé du compte insuffisant pour couvrir {request.Amount:N2} {primaryCode} " +
                $"(couverture : {totalEquivalent:N2} {primaryCode}). Aucun mouvement enregistré.");
        }

        ExpenseRequest? linkedRequest = null;
        if (request.ExpenseRequestId.HasValue)
        {
            linkedRequest = await GetRequestAsync(schoolId, request.ExpenseRequestId.Value, cancellationToken);
            if (linkedRequest.Status is not (ExpenseRequestStatus.Approuvee or ExpenseRequestStatus.Payee))
            {
                throw new DomainException("La demande liée doit être approuvée avant paiement.");
            }
        }

        var entity = new ExpensePayment
        {
            SchoolId = schoolId,
            AcademicYearId = request.AcademicYearId,
            DestinationId = request.DestinationId,
            ExpenseRequestId = request.ExpenseRequestId,
            Reference = await GeneratePaymentReferenceAsync(schoolId, cancellationToken),
            Label = request.Label.Trim(),
            BeneficiaryName = request.BeneficiaryName.Trim(),
            AuthorizedByName = request.AuthorizedByName.Trim(),
            Amount = request.Amount,
            Currency = request.Currency,
            PrimaryCurrencyId = primaryCurrencyId,
            ExpenseDate = request.ExpenseDate,
            ExternalReference = string.IsNullOrWhiteSpace(request.ExternalReference)
                ? null
                : request.ExternalReference.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim().ToLowerInvariant(),
            Observations = string.IsNullOrWhiteSpace(request.Observations) ? null : request.Observations.Trim(),
            AttachmentFileName = string.IsNullOrWhiteSpace(request.AttachmentFileName)
                ? null
                : request.AttachmentFileName.Trim(),
            AttachmentStoragePath = string.IsNullOrWhiteSpace(request.AttachmentStoragePath)
                ? null
                : request.AttachmentStoragePath.Trim(),
            CreatedBy = userId
        };

        await _paymentRepository.AddAsync(entity, cancellationToken);

        var sort = 0;
        foreach (var line in resolvedAllocations.OrderBy(l => l.CurrencyCode == primaryCode ? 0 : 1)
                     .ThenBy(l => l.CurrencyCode))
        {
            await _allocationRepository.AddAsync(new ExpensePaymentAllocation
            {
                SchoolId = schoolId,
                ExpensePaymentId = entity.Id,
                CurrencyId = line.CurrencyId,
                Amount = line.Amount,
                ExchangeRateId = line.ExchangeRateId,
                AppliedExchangeRate = line.AppliedRate,
                EquivalentInPrimaryCurrency = line.Equivalent,
                SortOrder = sort++,
                CreatedBy = userId
            }, cancellationToken);
        }

        if (linkedRequest is not null)
        {
            linkedRequest.Status = ExpenseRequestStatus.Payee;
            await _requestRepository.UpdateAsync(linkedRequest, cancellationToken);
        }

        // Persister avant relecture : FindAsync(AsNoTracking) ne voit pas les entités encore en mémoire.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetExpensePaymentByIdAsync(schoolId, entity.Id, cancellationToken);
    }

    public async Task<ExpensePaymentDto> GetExpensePaymentByIdAsync(
        Guid schoolId,
        Guid expensePaymentId,
        CancellationToken cancellationToken = default)
    {
        var entity = (await _paymentRepository.FindAsync(
                p => p.SchoolId == schoolId && p.Id == expensePaymentId,
                cancellationToken))
            .FirstOrDefault()
            ?? throw new DomainException("Dépense introuvable.");

        return (await MapPaymentsAsync(schoolId, [entity], cancellationToken, includeAllocations: true)).Single();
    }

    private async Task<Guid?> ResolvePrimaryCurrencyIdAsync(
        Guid schoolId,
        Currency currencyEnum,
        Guid? requestedCurrencyId,
        CancellationToken cancellationToken)
    {
        if (requestedCurrencyId.HasValue)
        {
            var def = await _currencyRepository.GetByIdAsync(requestedCurrencyId.Value, cancellationToken);
            if (def is null || !def.IsActive)
            {
                throw new DomainException("Devise principale introuvable.");
            }

            return def.Id;
        }

        var byEnum = await _currencyService.ResolveByEnumCodeAsync(currencyEnum.ToString(), cancellationToken);
        if (byEnum is not null)
        {
            return byEnum.Id;
        }

        try
        {
            return (await _currencyService.GetMainCurrencyAsync(schoolId, cancellationToken)).Id;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<(Guid DestinationId, Guid? CurrencyId, decimal Amount)>> ExpandExpenseSpendLinesAsync(
        Guid schoolId,
        IReadOnlyList<ExpensePayment> expensePayments,
        IReadOnlyDictionary<Guid, Guid?> expenseCurrencyMap,
        CancellationToken cancellationToken)
    {
        var result = new List<(Guid DestinationId, Guid? CurrencyId, decimal Amount)>();
        if (expensePayments.Count == 0)
        {
            return result;
        }

        var paymentIds = expensePayments.Select(p => p.Id).ToList();
        var allocations = (await _allocationRepository.FindAsync(
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
                    result.Add((payment.DestinationId, line.CurrencyId, line.Amount));
                }
            }
            else
            {
                result.Add((
                    payment.DestinationId,
                    expenseCurrencyMap.GetValueOrDefault(payment.Id) ?? payment.PrimaryCurrencyId,
                    payment.Amount));
            }
        }

        return result;
    }

    private async Task<HashSet<Guid>> GetKeyedDestinationIdsAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var activeKeyIds = (await _keyRepository.FindAsync(
                k => k.SchoolId == schoolId
                     && k.AcademicYearId == academicYearId
                     && k.IsActive
                     && k.EndDate == null,
                cancellationToken))
            .Select(k => k.Id)
            .ToHashSet();

        if (activeKeyIds.Count == 0)
        {
            return [];
        }

        return (await _keyDetailRepository.FindAsync(
                d => activeKeyIds.Contains(d.AllocationKeyId),
                cancellationToken))
            .Select(d => d.DestinationId)
            .ToHashSet();
    }

    private async Task<List<ExpenseRequest>> FilterRequestsAsync(
        Guid schoolId,
        ExpenseSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = (await _requestRepository.FindAsync(r => r.SchoolId == schoolId, cancellationToken)).AsEnumerable();
        if (request.AcademicYearId.HasValue)
        {
            query = query.Where(r => r.AcademicYearId == request.AcademicYearId);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(r => r.RequestDate >= request.FromDate);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(r => r.RequestDate <= request.ToDate);
        }

        if (request.DestinationId.HasValue)
        {
            query = query.Where(r => r.DestinationId == request.DestinationId);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(r => r.Status == request.Status);
        }

        return query.ToList();
    }

    private async Task<List<ExpensePayment>> FilterPaymentsAsync(
        Guid schoolId,
        ExpenseSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = (await _paymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken)).AsEnumerable();
        if (request.AcademicYearId.HasValue)
        {
            query = query.Where(p => p.AcademicYearId == request.AcademicYearId);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(p => p.ExpenseDate >= request.FromDate);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(p => p.ExpenseDate <= request.ToDate);
        }

        if (request.DestinationId.HasValue)
        {
            query = query.Where(p => p.DestinationId == request.DestinationId);
        }

        return query.ToList();
    }

    private async Task<ExpenseRequest> GetRequestAsync(Guid schoolId, Guid requestId, CancellationToken cancellationToken)
    {
        var entity = (await _requestRepository.FindAsync(r => r.Id == requestId && r.SchoolId == schoolId, cancellationToken))
            .FirstOrDefault();
        return entity ?? throw new DomainException("Demande de paiement introuvable.");
    }

    private async Task EnsureDestinationAsync(Guid schoolId, Guid destinationId, CancellationToken cancellationToken)
    {
        var exists = (await _destinationRepository.FindAsync(
            d => d.Id == destinationId && d.SchoolId == schoolId && d.IsActive,
            cancellationToken)).Any();
        if (!exists)
        {
            throw new DomainException("Compte bénéficiaire introuvable ou inactif.");
        }
    }

    private async Task EnsureYearAsync(Guid schoolId, Guid yearId, CancellationToken cancellationToken)
    {
        var exists = (await _yearRepository.FindAsync(y => y.Id == yearId && y.SchoolId == schoolId, cancellationToken)).Any();
        if (!exists)
        {
            throw new DomainException("Année scolaire introuvable.");
        }
    }

    private async Task<string> GenerateRequestReferenceAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var count = (await _requestRepository.FindAsync(r => r.SchoolId == schoolId, cancellationToken)).Count;
        return $"DP-{DateTime.UtcNow:yyyyMMdd}-{count + 1:D4}";
    }

    private async Task<string> GeneratePaymentReferenceAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var count = (await _paymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken)).Count;
        return $"DEP-{DateTime.UtcNow:yyyyMMdd}-{count + 1:D4}";
    }

    private async Task<IReadOnlyList<ExpenseRequestDto>> MapRequestsAsync(
        Guid schoolId,
        IReadOnlyList<ExpenseRequest> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var destinations = (await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);
        var years = (await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken))
            .ToDictionary(y => y.Id);

        return items.Select(r =>
        {
            destinations.TryGetValue(r.DestinationId, out var destination);
            years.TryGetValue(r.AcademicYearId, out var year);
            return new ExpenseRequestDto(
                r.Id,
                r.Reference,
                r.Title,
                r.Description,
                r.RequestedAmount,
                r.Currency.ToString(),
                r.RequestDate,
                r.Status,
                FormatRequestStatus(r.Status),
                r.DestinationId,
                destination?.Code ?? "—",
                destination?.Name ?? "—",
                r.AcademicYearId,
                year?.Label ?? "—",
                r.SubmittedAt,
                r.ApprovedAt);
        }).ToList();
    }

    private async Task<IReadOnlyList<ExpensePaymentDto>> MapPaymentsAsync(
        Guid schoolId,
        IReadOnlyList<ExpensePayment> items,
        CancellationToken cancellationToken,
        bool includeAllocations = false)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var destinations = (await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);
        var years = (await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken))
            .ToDictionary(y => y.Id);

        Dictionary<Guid, List<ExpensePaymentAllocation>> allocationsByPayment;
        Dictionary<Guid, string> currencyCodes = new();
        {
            var paymentIds = items.Select(p => p.Id).ToList();
            var allocations = (await _allocationRepository.FindAsync(
                    a => a.SchoolId == schoolId && paymentIds.Contains(a.ExpensePaymentId),
                    cancellationToken))
                .ToList();
            allocationsByPayment = allocations
                .GroupBy(a => a.ExpensePaymentId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortOrder).ToList());

            var currencyIds = allocations.Select(a => a.CurrencyId)
                .Concat(items.Where(i => i.PrimaryCurrencyId.HasValue).Select(i => i.PrimaryCurrencyId!.Value))
                .Distinct()
                .ToList();
            if (currencyIds.Count > 0)
            {
                currencyCodes = (await _currencyRepository.FindAsync(c => currencyIds.Contains(c.Id), cancellationToken))
                    .ToDictionary(c => c.Id, c => c.Code);
            }
        }

        return items.Select(p =>
        {
            destinations.TryGetValue(p.DestinationId, out var destination);
            years.TryGetValue(p.AcademicYearId, out var year);
            allocationsByPayment.TryGetValue(p.Id, out var allocs);
            allocs ??= [];
            var primaryCode = p.PrimaryCurrencyId.HasValue
                              && currencyCodes.TryGetValue(p.PrimaryCurrencyId.Value, out var pc)
                ? pc
                : p.Currency.ToString();

            var allocationDtos = allocs.Select(a =>
            {
                var code = currencyCodes.TryGetValue(a.CurrencyId, out var c) ? c : "?";
                return new ExpensePaymentAllocationDto(
                    a.Id,
                    a.CurrencyId,
                    code,
                    a.Amount,
                    a.ExchangeRateId,
                    a.AppliedExchangeRate,
                    a.EquivalentInPrimaryCurrency,
                    a.SortOrder,
                    $"1 {code} = {a.AppliedExchangeRate:N8} {primaryCode}");
            }).ToList();

            return new ExpensePaymentDto(
                p.Id,
                p.Reference,
                p.Label,
                p.BeneficiaryName,
                p.AuthorizedByName,
                p.Amount,
                primaryCode,
                p.PrimaryCurrencyId,
                p.ExpenseDate,
                p.DestinationId,
                destination?.Code ?? "—",
                destination?.Name ?? "—",
                p.ExpenseRequestId,
                p.AcademicYearId,
                year?.Label ?? "—",
                allocationDtos.Count > 1,
                allocationDtos,
                p.ExternalReference,
                p.Category,
                FormatExpenseCategory(p.Category),
                p.Observations,
                p.AttachmentFileName,
                !string.IsNullOrWhiteSpace(p.AttachmentFileName) || !string.IsNullOrWhiteSpace(p.AttachmentStoragePath));
        }).ToList();
    }

    private static string? FormatExpenseCategory(string? category) => category?.Trim().ToLowerInvariant() switch
    {
        "fonctionnement" => "Fonctionnement",
        "pedagogie" => "Pédagogie",
        "salaires" => "Salaires / Prestations",
        "infrastructure" => "Infrastructure",
        "autre" => "Autre",
        null or "" => null,
        _ => category
    };

    private static string FormatRequestStatus(ExpenseRequestStatus status) => status switch
    {
        ExpenseRequestStatus.Brouillon => "Brouillon",
        ExpenseRequestStatus.Soumise => "Soumise",
        ExpenseRequestStatus.Approuvee => "Approuvée",
        ExpenseRequestStatus.Payee => "Payée",
        ExpenseRequestStatus.Annulee => "Annulée",
        _ => status.ToString()
    };
}
