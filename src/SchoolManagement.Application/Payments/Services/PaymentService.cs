namespace SchoolManagement.Application.Payments.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Application.Payments.Interfaces;
using SchoolManagement.Application.RevenueAllocation.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class PaymentService : IPaymentService
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentLine> _paymentLineRepository;
    private readonly IRepository<PaymentReversal> _reversalRepository;
    private readonly IRepository<CashMovement> _cashMovementRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;
    private readonly IRepository<ClassFeeAmount> _classFeeAmountRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<FeeInstallment> _installmentRepository;
    private readonly IRepository<RevenueAllocationEntry> _allocationEntryRepository;
    private readonly IRevenueAllocationService _revenueAllocationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentService(
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<PaymentReversal> reversalRepository,
        IRepository<CashMovement> cashMovementRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<ClassFeeAmount> classFeeAmountRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<Student> studentRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<FeeInstallment> installmentRepository,
        IRepository<RevenueAllocationEntry> allocationEntryRepository,
        IRevenueAllocationService revenueAllocationService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _paymentLineRepository = paymentLineRepository;
        _reversalRepository = reversalRepository;
        _cashMovementRepository = cashMovementRepository;
        _balanceRepository = balanceRepository;
        _classFeeAmountRepository = classFeeAmountRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRoomRepository = classRoomRepository;
        _studentRepository = studentRepository;
        _yearRepository = yearRepository;
        _feeTypeRepository = feeTypeRepository;
        _installmentRepository = installmentRepository;
        _allocationEntryRepository = allocationEntryRepository;
        _revenueAllocationService = revenueAllocationService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<PaymentDto> CreatePaymentAsync(
        Guid schoolId,
        Guid userId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var student = (await _studentRepository.FindAsync(
            s => s.Id == request.StudentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository,
            schoolId,
            request.AcademicYearId,
            cancellationToken);

        var totalAmount = request.Lines.Sum(l => l.Amount);
        var receiptNumber = $"REC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

        var payment = new Payment
        {
            SchoolId = schoolId,
            StudentId = request.StudentId,
            AcademicYearId = request.AcademicYearId,
            CashRegisterId = null,
            BankId = request.BankId,
            ReceiptNumber = receiptNumber,
            PaymentDate = request.PaymentDate.HasValue
                ? DateTime.SpecifyKind(request.PaymentDate.Value.Date, DateTimeKind.Utc)
                : DateTime.UtcNow,
            TotalAmount = totalAmount,
            Currency = request.Currency,
            Status = PaymentStatus.Complet,
            PaymentMethod = null,
            Notes = request.Notes,
            ReceivedByUserId = userId
        };

        await _paymentRepository.AddAsync(payment, cancellationToken);

        var paymentLines = new List<PaymentLine>();
        foreach (var line in request.Lines)
        {
            var paymentLine = new PaymentLine
            {
                PaymentId = payment.Id,
                FeeTypeId = line.FeeTypeId,
                FeeInstallmentId = line.FeeInstallmentId,
                Amount = line.Amount,
                Currency = line.Currency,
                Description = line.Description,
                PhysicalReceiptNumber = string.IsNullOrWhiteSpace(line.PhysicalReceiptNumber)
                    ? null
                    : line.PhysicalReceiptNumber.Trim()
            };
            await _paymentLineRepository.AddAsync(paymentLine, cancellationToken);
            paymentLines.Add(paymentLine);
        }

        foreach (var group in request.Lines.GroupBy(l => (l.FeeTypeId, l.FeeInstallmentId)))
        {
            var line = group.First();
            if (!line.FeeInstallmentId.HasValue)
            {
                throw new DomainException(
                    "Chaque ligne de paiement doit préciser la tranche (FeeInstallmentId) pour mettre à jour le solde élève.");
            }

            await UpdateStudentBalanceAsync(
                schoolId,
                request.StudentId,
                request.AcademicYearId,
                line.FeeTypeId,
                line.FeeInstallmentId.Value,
                group.Sum(l => l.Amount),
                line.Currency,
                cancellationToken);
        }

        // Pas de mouvement de caisse : les caisses ne sont plus gérées (CashRegisterId = null).

        // Répartition atomique via SaveChanges (transaction implicite EF Core).
        // Note d'intégration future « Retenues » :
        // 1) Calculer MontantNet = MontantBrut - TotalRetenues via IWithholdingService.CalculateForPaymentLineAsync
        // 2) Transmettre MontantNet (et non le brut) à ApplyAllocationForPaymentAsync
        await _revenueAllocationService.ApplyAllocationForPaymentAsync(
            schoolId,
            payment,
            paymentLines,
            userId,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapPaymentDto(payment, $"{student.LastName} {student.FirstName}");
    }

    public async Task<PaymentListDto> SearchAsync(Guid schoolId, PaymentSearchRequest request, CancellationToken cancellationToken = default)
    {
        var payments = await _paymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);

        var query = payments.AsEnumerable();

        if (request.StudentId.HasValue)
        {
            query = query.Where(p => p.StudentId == request.StudentId);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(p => DateOnly.FromDateTime(p.PaymentDate) >= request.FromDate);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(p => DateOnly.FromDateTime(p.PaymentDate) <= request.ToDate);
        }

        var total = query.Count();
        var items = query
            .OrderByDescending(p => p.PaymentDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p =>
            {
                studentMap.TryGetValue(p.StudentId, out var student);
                var name = student is null ? "—" : $"{student.LastName} {student.FirstName}";
                return MapPaymentDto(p, name);
            })
            .ToList();

        return new PaymentListDto { Items = items, Page = request.Page, PageSize = request.PageSize, TotalCount = total };
    }

    public async Task<PaymentDetailDto?> GetByIdAsync(Guid schoolId, Guid paymentId, CancellationToken cancellationToken = default)
    {
        var payment = (await _paymentRepository.FindAsync(
            p => p.Id == paymentId && p.SchoolId == schoolId, cancellationToken)).FirstOrDefault();

        if (payment is null)
        {
            return null;
        }

        return await MapDetailAsync(payment, cancellationToken);
    }

    public async Task<StudentFinancialSummaryDto?> GetStudentFinancialSummaryAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository,
            schoolId,
            academicYearId,
            cancellationToken);

        var student = (await _studentRepository.FindAsync(
            s => s.Id == studentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault();

        if (student is null)
        {
            return null;
        }

        var balances = await _balanceRepository.FindAsync(
            b => b.StudentId == studentId,
            cancellationToken);
        var yearTariffIds = (await _classFeeAmountRepository.FindAsync(
            a => a.SchoolId == schoolId && a.AcademicYearId == academicYearId,
            cancellationToken)).Select(a => a.Id).ToHashSet();
        balances = balances.Where(b => yearTariffIds.Contains(b.ClassFeeAmountId)).ToList();

        var totalDue = balances.Sum(b => b.AmountDue);
        var totalPaid = balances.Sum(b => b.AmountPaid);
        var currency = balances.FirstOrDefault()?.Currency ?? Currency.CDF;

        return new StudentFinancialSummaryDto(
            studentId,
            $"{student.LastName} {student.FirstName}",
            totalDue,
            totalPaid,
            totalDue - totalPaid,
            currency);
    }

    public async Task CancelPaymentAsync(
        Guid schoolId,
        Guid userId,
        Guid paymentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Le motif d'annulation est obligatoire.");
        }

        PaymentMutationPolicy.EnsureAdministrator(_currentUser);

        var payment = (await _paymentRepository.FindAsync(
            p => p.Id == paymentId && p.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Paiement introuvable.");

        if (payment.Status != PaymentStatus.Complet)
        {
            throw new DomainException("Seuls les paiements complets peuvent être annulés.");
        }

        var existingReversal = (await _reversalRepository.FindAsync(
            r => r.PaymentId == paymentId, cancellationToken)).FirstOrDefault();
        if (existingReversal is not null)
        {
            throw new DomainException("Ce paiement a déjà été annulé.");
        }

        await EnsureRetrogradeMutationAllowedAsync(schoolId, payment, cancellationToken);

        var lines = await _paymentLineRepository.FindAsync(l => l.PaymentId == paymentId, cancellationToken);
        foreach (var group in lines.GroupBy(l => (l.FeeTypeId, l.FeeInstallmentId)))
        {
            var line = group.First();
            if (!line.FeeInstallmentId.HasValue)
            {
                continue;
            }

            await ReverseStudentBalanceAsync(
                schoolId,
                payment.StudentId,
                payment.AcademicYearId,
                line.FeeTypeId,
                line.FeeInstallmentId.Value,
                group.Sum(l => l.Amount),
                cancellationToken);
        }

        // Mouvement OUT uniquement si une caisse historique était liée au paiement.
        if (payment.CashRegisterId is Guid cashRegisterId)
        {
            var lastMovement = (await _cashMovementRepository.FindAsync(
                m => m.CashRegisterId == cashRegisterId, cancellationToken))
                .OrderByDescending(m => m.MovementDate)
                .FirstOrDefault();

            var balanceAfter = (lastMovement?.BalanceAfter ?? 0) - payment.TotalAmount;

            await _cashMovementRepository.AddAsync(new CashMovement
            {
                CashRegisterId = cashRegisterId,
                PaymentId = payment.Id,
                MovementDate = DateTime.UtcNow,
                MovementType = "OUT",
                Amount = payment.TotalAmount,
                Currency = payment.Currency,
                BalanceAfter = balanceAfter,
                Description = $"Annulation {payment.ReceiptNumber}",
                UserId = userId
            }, cancellationToken);
        }

        payment.Status = PaymentStatus.Annule;
        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        await _reversalRepository.AddAsync(new PaymentReversal
        {
            PaymentId = payment.Id,
            Reason = reason.Trim(),
            ReversedAt = DateTime.UtcNow,
            ReversedByUserId = userId,
            IsApproved = true,
            ApprovedByUserId = userId
        }, cancellationToken);

        // Les écritures de répartition restent en place ; le statut Annule suffit pour l'instant.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaymentDetailDto> UpdatePaymentNotesAsync(
        Guid schoolId,
        Guid paymentId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        PaymentMutationPolicy.EnsureAdministrator(_currentUser);

        var payment = (await _paymentRepository.FindAsync(
            p => p.Id == paymentId && p.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Paiement introuvable.");

        if (payment.Status == PaymentStatus.Annule)
        {
            throw new DomainException("Impossible de modifier un paiement annulé.");
        }

        await EnsureRetrogradeMutationAllowedAsync(schoolId, payment, cancellationToken);

        payment.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await _paymentRepository.UpdateAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapDetailAsync(payment, cancellationToken);
    }

    public async Task<PaymentDetailDto> UpdatePaymentAmountAsync(
        Guid schoolId,
        Guid userId,
        Guid paymentId,
        UpdatePaymentAmountRequest request,
        CancellationToken cancellationToken = default)
    {
        PaymentMutationPolicy.EnsureAdministrator(_currentUser);

        if (request.NewAmount <= 0)
        {
            throw new DomainException("Le nouveau montant doit être supérieur à zéro.");
        }

        var payment = (await _paymentRepository.FindAsync(
            p => p.Id == paymentId && p.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Paiement introuvable.");

        if (payment.Status != PaymentStatus.Complet)
        {
            throw new DomainException("Seuls les paiements complets peuvent être modifiés.");
        }

        await EnsureRetrogradeMutationAllowedAsync(schoolId, payment, cancellationToken);

        var lines = (await _paymentLineRepository.FindAsync(l => l.PaymentId == payment.Id, cancellationToken))
            .OrderBy(l => l.CreatedAt)
            .ToList();
        if (lines.Count == 0)
        {
            throw new DomainException("Ce paiement n'a aucune ligne à modifier.");
        }

        var oldTotal = payment.TotalAmount;
        var newTotal = decimal.Round(request.NewAmount, 2, MidpointRounding.AwayFromZero);
        if (oldTotal == newTotal)
        {
            if (request.Notes is not null)
            {
                payment.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return await MapDetailAsync(payment, cancellationToken);
        }

        var newLineAmounts = DistributeAmount(lines.Select(l => l.Amount).ToList(), oldTotal, newTotal);

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var newLineAmount = newLineAmounts[i];
            var delta = newLineAmount - line.Amount;

            if (delta != 0 && line.FeeInstallmentId.HasValue)
            {
                await ApplyBalanceDeltaAsync(
                    schoolId,
                    payment.StudentId,
                    payment.AcademicYearId,
                    line.FeeTypeId,
                    line.FeeInstallmentId.Value,
                    delta,
                    payment.Currency,
                    cancellationToken);
            }

            line.Amount = newLineAmount;
            await _paymentLineRepository.UpdateAsync(line, cancellationToken);
        }

        payment.TotalAmount = newTotal;
        if (request.Notes is not null)
        {
            payment.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        }

        await _paymentRepository.UpdateAsync(payment, cancellationToken);

        var existingEntries = await _allocationEntryRepository.FindAsync(
            e => e.PaymentId == payment.Id, cancellationToken);
        foreach (var entry in existingEntries)
        {
            await _allocationEntryRepository.DeleteAsync(entry, cancellationToken);
        }

        await _revenueAllocationService.ApplyAllocationForPaymentAsync(
            schoolId,
            payment,
            lines,
            userId,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(payment, cancellationToken);
    }

    private static List<decimal> DistributeAmount(IReadOnlyList<decimal> oldAmounts, decimal oldTotal, decimal newTotal)
    {
        if (oldAmounts.Count == 0)
        {
            return [];
        }

        if (oldTotal <= 0)
        {
            var list = Enumerable.Repeat(0m, oldAmounts.Count).ToList();
            list[^1] = newTotal;
            return list;
        }

        var result = new List<decimal>(oldAmounts.Count);
        decimal allocated = 0;
        for (var i = 0; i < oldAmounts.Count; i++)
        {
            if (i == oldAmounts.Count - 1)
            {
                result.Add(decimal.Round(newTotal - allocated, 2, MidpointRounding.AwayFromZero));
            }
            else
            {
                var share = decimal.Round(oldAmounts[i] / oldTotal * newTotal, 2, MidpointRounding.AwayFromZero);
                result.Add(share);
                allocated += share;
            }
        }

        return result;
    }

    private async Task ApplyBalanceDeltaAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        Guid feeInstallmentId,
        decimal delta,
        Currency currency,
        CancellationToken cancellationToken)
    {
        if (delta > 0)
        {
            await UpdateStudentBalanceAsync(
                schoolId, studentId, academicYearId, feeTypeId, feeInstallmentId, delta, currency, cancellationToken);
            return;
        }

        if (delta < 0)
        {
            await ReverseStudentBalanceAsync(
                schoolId, studentId, academicYearId, feeTypeId, feeInstallmentId, -delta, cancellationToken);
        }
    }

    private async Task EnsureRetrogradeMutationAllowedAsync(
        Guid schoolId,
        Payment payment,
        CancellationToken cancellationToken)
    {
        var siblingPayments = (await _paymentRepository.FindAsync(
            p => p.SchoolId == schoolId
                && p.StudentId == payment.StudentId
                && p.AcademicYearId == payment.AcademicYearId,
            cancellationToken)).ToList();

        var lines = (await _paymentLineRepository.FindAsync(l => l.PaymentId == payment.Id, cancellationToken)).ToList();
        var feeTypeIds = lines.Select(l => l.FeeTypeId).Distinct().ToHashSet();

        var completedSiblings = siblingPayments
            .Where(p => p.Status == PaymentStatus.Complet)
            .ToList();
        var completedIds = completedSiblings.Select(p => p.Id).ToHashSet();

        // Dernier versement = dernier paiement Complet du même type de frais (pas tous les frais).
        IReadOnlyList<Payment> relevantForLatest = completedSiblings;
        if (feeTypeIds.Count > 0)
        {
            var relatedLines = await _paymentLineRepository.FindAsync(
                l => completedIds.Contains(l.PaymentId) && feeTypeIds.Contains(l.FeeTypeId),
                cancellationToken);
            var relatedPaymentIds = relatedLines.Select(l => l.PaymentId).ToHashSet();
            relevantForLatest = completedSiblings
                .Where(p => relatedPaymentIds.Contains(p.Id))
                .ToList();
        }

        PaymentMutationPolicy.EnsureIsLatestCompletedPayment(payment, relevantForLatest);

        if (feeTypeIds.Count == 0)
        {
            return;
        }

        var allLines = (await _paymentLineRepository.FindAsync(
            l => completedIds.Contains(l.PaymentId) && feeTypeIds.Contains(l.FeeTypeId),
            cancellationToken)).ToList();

        var otherPaidByInstallment = allLines
            .Where(l => l.PaymentId != payment.Id && l.FeeInstallmentId.HasValue)
            .GroupBy(l => l.FeeInstallmentId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var installments = await _installmentRepository.FindAsync(i => i.SchoolId == schoolId, cancellationToken);
        var orders = installments
            .Select(i => (InstallmentId: i.Id, SortOrder: i.SortOrder))
            .ToList();

        PaymentMutationPolicy.EnsureNoLaterInstallmentsPaid(lines, orders, otherPaidByInstallment);
    }

    private async Task ReverseStudentBalanceAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        Guid feeInstallmentId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var classFeeAmountId = await ResolveClassFeeAmountIdAsync(
            schoolId, studentId, academicYearId, feeTypeId, feeInstallmentId, cancellationToken);
        if (classFeeAmountId is null)
        {
            return;
        }

        var balance = await FindStudentFeeBalanceAsync(studentId, classFeeAmountId.Value, cancellationToken);
        if (balance is null)
        {
            return;
        }

        balance.AmountPaid = Math.Max(0, balance.AmountPaid - amount);
        await _balanceRepository.UpdateAsync(balance, cancellationToken);
    }

    private async Task UpdateStudentBalanceAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        Guid feeInstallmentId,
        decimal amountPaid,
        Currency currency,
        CancellationToken cancellationToken)
    {
        var classFeeAmountId = await ResolveClassFeeAmountIdAsync(
            schoolId, studentId, academicYearId, feeTypeId, feeInstallmentId, cancellationToken)
            ?? throw new DomainException(
                "Impossible de rattacher le paiement à une ligne de tarif (ClassFeeAmount). Vérifiez la catégorie tarifaire et la configuration des frais.");

        var balance = await FindStudentFeeBalanceAsync(studentId, classFeeAmountId, cancellationToken);
        if (balance is null)
        {
            // Sécurité : solde absent (catégorie non provisionnée) — crée avec AmountDue figé au tarif courant.
            var tariff = (await _classFeeAmountRepository.FindAsync(
                a => a.Id == classFeeAmountId, cancellationToken)).FirstOrDefault();

            balance = new StudentFeeBalance
            {
                StudentId = studentId,
                ClassFeeAmountId = classFeeAmountId,
                AmountDue = tariff?.Amount ?? amountPaid,
                AmountPaid = amountPaid,
                Currency = currency
            };
            await _balanceRepository.AddAsync(balance, cancellationToken);
        }
        else
        {
            balance.AmountPaid += amountPaid;
            await _balanceRepository.UpdateAsync(balance, cancellationToken);
        }
    }

    private async Task<Guid?> ResolveClassFeeAmountIdAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        Guid feeInstallmentId,
        CancellationToken cancellationToken)
    {
        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.StudentId == studentId
                 && e.AcademicYearId == academicYearId
                 && e.IsActive,
            cancellationToken)).FirstOrDefault();
        if (enrollment is null)
        {
            return null;
        }

        var classRoom = (await _classRoomRepository.FindAsync(
            c => c.Id == enrollment.ClassRoomId, cancellationToken)).FirstOrDefault();
        if (classRoom?.PedagogicalClassId is not Guid pedagogicalClassId)
        {
            return null;
        }

        var tariff = (await _classFeeAmountRepository.FindAsync(
            a => a.SchoolId == schoolId
                 && a.AcademicYearId == academicYearId
                 && a.PedagogicalClassId == pedagogicalClassId
                 && a.FeePricingCategoryId == enrollment.FeePricingCategoryId
                 && a.FeeTypeId == feeTypeId
                 && a.FeeInstallmentId == feeInstallmentId,
            cancellationToken)).FirstOrDefault();

        return tariff?.Id;
    }

    private async Task<StudentFeeBalance?> FindStudentFeeBalanceAsync(
        Guid studentId,
        Guid classFeeAmountId,
        CancellationToken cancellationToken)
    {
        var balance = (await _balanceRepository.FindAsync(
            b => b.StudentId == studentId && b.ClassFeeAmountId == classFeeAmountId,
            cancellationToken)).FirstOrDefault();

        if (balance is not null)
        {
            return balance;
        }

        balance = (await _balanceRepository.FindIncludingDeletedAsync(
            b => b.StudentId == studentId && b.ClassFeeAmountId == classFeeAmountId,
            cancellationToken)).FirstOrDefault();

        if (balance is null)
        {
            return null;
        }

        balance.IsDeleted = false;
        balance.DeletedAt = null;
        balance.DeletedBy = null;
        return balance;
    }

    private async Task<PaymentDetailDto> MapDetailAsync(Payment payment, CancellationToken cancellationToken)
    {
        var student = (await _studentRepository.FindAsync(s => s.Id == payment.StudentId, cancellationToken)).FirstOrDefault();
        var name = student is null ? "—" : $"{student.LastName} {student.FirstName}";
        var lines = await _paymentLineRepository.FindAsync(l => l.PaymentId == payment.Id, cancellationToken);
        var feeTypes = await _feeTypeRepository.FindAsync(_ => true, cancellationToken);
        var feeMap = feeTypes.ToDictionary(f => f.Id, f => f.Name);

        var lineDtos = lines
            .Select(l => new PaymentLineDto(
                l.Id,
                l.FeeTypeId,
                feeMap.TryGetValue(l.FeeTypeId, out var ftName) ? ftName : null,
                l.Amount,
                l.Currency,
                l.Description,
                l.FeeInstallmentId,
                null,
                l.PhysicalReceiptNumber))
            .ToList();

        return new PaymentDetailDto(
            payment.Id,
            payment.ReceiptNumber,
            payment.StudentId,
            name,
            payment.AcademicYearId,
            payment.PaymentDate,
            payment.TotalAmount,
            payment.Currency,
            payment.Status,
            payment.Notes,
            lineDtos);
    }

    private static PaymentDto MapPaymentDto(Payment payment, string studentName) =>
        new(
            payment.Id,
            payment.ReceiptNumber,
            payment.StudentId,
            studentName,
            payment.PaymentDate,
            payment.TotalAmount,
            payment.Currency,
            payment.Status,
            payment.Notes);
}
