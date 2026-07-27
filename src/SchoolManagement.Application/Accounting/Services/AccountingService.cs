namespace SchoolManagement.Application.Accounting.Services;

using SchoolManagement.Application.Accounting.DTOs;
using SchoolManagement.Application.Accounting.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class AccountingService : IAccountingService
{
    private readonly IRepository<ExpenseRequest> _requestRepository;
    private readonly IRepository<ExpensePayment> _paymentRepository;
    private readonly IRepository<RevenueAllocationEntry> _entryRepository;
    private readonly IRepository<RevenueAllocationKey> _keyRepository;
    private readonly IRepository<RevenueAllocationKeyDetail> _keyDetailRepository;
    private readonly IRepository<RevenueAllocationDestination> _destinationRepository;
    private readonly IRepository<Payment> _tuitionPaymentRepository;
    private readonly IRepository<AcademicYear> _yearRepository;

    public AccountingService(
        IRepository<ExpenseRequest> requestRepository,
        IRepository<ExpensePayment> paymentRepository,
        IRepository<RevenueAllocationEntry> entryRepository,
        IRepository<RevenueAllocationKey> keyRepository,
        IRepository<RevenueAllocationKeyDetail> keyDetailRepository,
        IRepository<RevenueAllocationDestination> destinationRepository,
        IRepository<Payment> tuitionPaymentRepository,
        IRepository<AcademicYear> yearRepository)
    {
        _requestRepository = requestRepository;
        _paymentRepository = paymentRepository;
        _entryRepository = entryRepository;
        _keyRepository = keyRepository;
        _keyDetailRepository = keyDetailRepository;
        _destinationRepository = destinationRepository;
        _tuitionPaymentRepository = tuitionPaymentRepository;
        _yearRepository = yearRepository;
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
            .OrderBy(d => d.Name)
            .ToList();

        var allocationEntries = (await _entryRepository.FindAsync(
                e => e.SchoolId == schoolId && e.AcademicYearId == academicYearId,
                cancellationToken))
            .ToList();

        var allocatedByDestination = allocationEntries
            .GroupBy(e => e.DestinationId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var expensePayments = (await _paymentRepository.FindAsync(
                p => p.SchoolId == schoolId && p.AcademicYearId == academicYearId,
                cancellationToken))
            .ToList();

        var spentByDestination = expensePayments
            .GroupBy(p => p.DestinationId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        // Devise du compte = majoritaire sur les recettes allouées, sinon sur les dépenses.
        var tuitionPaymentIds = allocationEntries
            .Select(e => e.PaymentId)
            .Distinct()
            .ToList();
        var tuitionPayments = tuitionPaymentIds.Count == 0
            ? []
            : (await _tuitionPaymentRepository.FindAsync(
                    p => tuitionPaymentIds.Contains(p.Id),
                    cancellationToken))
                .ToDictionary(p => p.Id);

        var currencyByDestination = new Dictionary<Guid, Currency>();
        foreach (var group in allocationEntries.GroupBy(e => e.DestinationId))
        {
            var currencies = group
                .Select(e => tuitionPayments.TryGetValue(e.PaymentId, out var pay) ? pay.Currency : (Currency?)null)
                .Where(c => c.HasValue)
                .Select(c => c!.Value)
                .ToList();
            if (currencies.Count == 0)
                continue;

            currencyByDestination[group.Key] = currencies
                .GroupBy(c => c)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key)
                .Select(x => x.Key)
                .First();
        }

        foreach (var group in expensePayments.GroupBy(p => p.DestinationId))
        {
            if (currencyByDestination.ContainsKey(group.Key))
                continue;

            currencyByDestination[group.Key] = group
                .GroupBy(p => p.Currency)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key)
                .Select(x => x.Key)
                .First();
        }

        var schoolDefaultCurrency = tuitionPayments.Values
            .Select(p => p.Currency)
            .Concat(expensePayments.Select(p => p.Currency))
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .DefaultIfEmpty(Currency.CDF)
            .First();

        return destinations.Select(d =>
        {
            allocatedByDestination.TryGetValue(d.Id, out var allocated);
            spentByDestination.TryGetValue(d.Id, out var spent);
            var currency = currencyByDestination.TryGetValue(d.Id, out var cur)
                ? cur
                : schoolDefaultCurrency;
            return new ExpenseDestinationBalanceDto(
                d.Id,
                d.Code,
                d.Name,
                allocated,
                spent,
                allocated - spent,
                currency.ToString());
        }).ToList();
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

        var balances = await GetExpenseBalancesAsync(schoolId, request.AcademicYearId, cancellationToken);
        var balance = balances.FirstOrDefault(b => b.DestinationId == request.DestinationId);
        if (balance is null)
        {
            throw new DomainException(
                "Ce compte n'apparaît dans aucune clé de répartition active pour l'année scolaire sélectionnée.");
        }

        if (request.Amount > balance.AvailableAmount)
        {
            throw new DomainException(
                $"Montant supérieur au solde disponible ({balance.AvailableAmount:N2} {request.Currency}) sur le compte « {balance.DestinationName} ».");
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
            ExpenseDate = request.ExpenseDate,
            CreatedBy = userId
        };

        await _paymentRepository.AddAsync(entity, cancellationToken);

        if (linkedRequest is not null)
        {
            linkedRequest.Status = ExpenseRequestStatus.Payee;
            await _requestRepository.UpdateAsync(linkedRequest, cancellationToken);
        }

        return (await MapPaymentsAsync(schoolId, [entity], cancellationToken)).Single();
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

        return items.Select(p =>
        {
            destinations.TryGetValue(p.DestinationId, out var destination);
            years.TryGetValue(p.AcademicYearId, out var year);
            return new ExpensePaymentDto(
                p.Id,
                p.Reference,
                p.Label,
                p.BeneficiaryName,
                p.AuthorizedByName,
                p.Amount,
                p.Currency.ToString(),
                p.ExpenseDate,
                p.DestinationId,
                destination?.Code ?? "—",
                destination?.Name ?? "—",
                p.ExpenseRequestId,
                p.AcademicYearId,
                year?.Label ?? "—");
        }).ToList();
    }

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
