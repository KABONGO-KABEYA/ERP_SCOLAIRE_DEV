using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.DocumentBranding.Interfaces;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Application.Payments.Interfaces;
using SchoolManagement.Application.SchoolFees.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Payments.Services;

public sealed class FeeTypeStatementService : IFeeTypeStatementService
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentLine> _paymentLineRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<Domain.Entities.Students.Enrollment> _enrollmentRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<FeeInstallment> _installmentRepository;
    private readonly IRepository<ClassFeeAmount> _classFeeAmountRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;
    private readonly IRepository<School> _schoolRepository;
    private readonly IRepository<StudentGuardian> _studentGuardianRepository;
    private readonly IRepository<Guardian> _guardianRepository;
    private readonly IRepository<UserAccount> _userRepository;
    private readonly IRepository<CurrencyDefinition> _currencyRepository;
    private readonly ISchoolFeeService _schoolFeeService;
    private readonly IDocumentPrintBrandingResolver _brandingResolver;
    private readonly IDocumentBrandingStorageService _brandingStorage;

    public FeeTypeStatementService(
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<Student> studentRepository,
        IRepository<Domain.Entities.Students.Enrollment> enrollmentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<FeeInstallment> installmentRepository,
        IRepository<ClassFeeAmount> classFeeAmountRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<School> schoolRepository,
        IRepository<StudentGuardian> studentGuardianRepository,
        IRepository<Guardian> guardianRepository,
        IRepository<UserAccount> userRepository,
        IRepository<CurrencyDefinition> currencyRepository,
        ISchoolFeeService schoolFeeService,
        IDocumentPrintBrandingResolver brandingResolver,
        IDocumentBrandingStorageService brandingStorage)
    {
        _paymentRepository = paymentRepository;
        _paymentLineRepository = paymentLineRepository;
        _studentRepository = studentRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _yearRepository = yearRepository;
        _feeTypeRepository = feeTypeRepository;
        _installmentRepository = installmentRepository;
        _classFeeAmountRepository = classFeeAmountRepository;
        _balanceRepository = balanceRepository;
        _schoolRepository = schoolRepository;
        _studentGuardianRepository = studentGuardianRepository;
        _guardianRepository = guardianRepository;
        _userRepository = userRepository;
        _currencyRepository = currencyRepository;
        _schoolFeeService = schoolFeeService;
        _brandingResolver = brandingResolver;
        _brandingStorage = brandingStorage;
    }

    public async Task<FeeTypeStatementDto> GetStatementAsync(
        Guid schoolId,
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var payment = (await _paymentRepository.FindAsync(
            p => p.Id == paymentId && p.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Paiement introuvable.");

        var paymentLines = await _paymentLineRepository.FindAsync(
            l => l.PaymentId == payment.Id, cancellationToken);

        var resolvedFeeTypeId = feeTypeId
            ?? paymentLines.Select(l => l.FeeTypeId).FirstOrDefault();
        if (resolvedFeeTypeId == Guid.Empty)
        {
            throw new DomainException("Impossible de déterminer le type de frais pour ce relevé.");
        }

        return await BuildForStudentAsync(
            schoolId,
            payment.StudentId,
            payment.AcademicYearId,
            resolvedFeeTypeId,
            payment,
            cancellationToken);
    }

    public async Task<FeeTypeStatementDto> GetStatementForStudentAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        // Dernier paiement du type (s'il existe) pour n° reçu / caissier ; sinon relevé à zéro versement.
        var payments = (await _paymentRepository.FindAsync(
                p => p.SchoolId == schoolId
                     && p.StudentId == studentId
                     && p.AcademicYearId == academicYearId
                     && p.Status == PaymentStatus.Complet,
                cancellationToken))
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();

        Payment? anchorPayment = null;
        foreach (var payment in payments)
        {
            var lines = await _paymentLineRepository.FindAsync(
                l => l.PaymentId == payment.Id && l.FeeTypeId == feeTypeId, cancellationToken);
            if (lines.Count > 0)
            {
                anchorPayment = payment;
                break;
            }
        }

        return await BuildForStudentAsync(
            schoolId,
            studentId,
            academicYearId,
            feeTypeId,
            anchorPayment,
            cancellationToken);
    }

    private async Task<FeeTypeStatementDto> BuildForStudentAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        Payment? anchorPayment,
        CancellationToken cancellationToken)
    {
        var feeType = (await _feeTypeRepository.FindAsync(
            f => f.Id == feeTypeId && f.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Type de frais introuvable.");

        var student = (await _studentRepository.FindAsync(
            s => s.Id == studentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var year = (await _yearRepository.FindAsync(
            y => y.Id == academicYearId && y.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Année scolaire introuvable.");

        var school = (await _schoolRepository.FindAsync(
            s => s.Id == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("École introuvable.");

        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.StudentId == student.Id
                 && e.AcademicYearId == academicYearId
                 && e.IsActive,
            cancellationToken)).FirstOrDefault();

        var className = "—";
        Guid? pedagogicalClassId = null;
        Guid pricingCategoryId = Guid.Empty;

        if (enrollment is not null)
        {
            pricingCategoryId = enrollment.FeePricingCategoryId;
            var classRoom = (await _classRoomRepository.FindAsync(
                c => c.Id == enrollment.ClassRoomId, cancellationToken)).FirstOrDefault();
            pedagogicalClassId = classRoom?.PedagogicalClassId;
            if (classRoom?.PedagogicalClassId is Guid pedId)
            {
                var ped = (await _pedagogicalClassRepository.FindAsync(
                    p => p.Id == pedId, cancellationToken)).FirstOrDefault();
                className = ped is null
                    ? classRoom.Name
                    : $"{ped.DisplayName} {classRoom.Name}".Trim();
            }
            else if (classRoom is not null)
            {
                className = classRoom.Name;
            }
        }

        var installments = (await _installmentRepository.FindAsync(
            i => i.SchoolId == schoolId, cancellationToken)).ToList();
        var installmentNames = installments.ToDictionary(i => i.Id, i => i.Name);
        var installmentSortOrders = installments.ToDictionary(i => i.Id, i => i.SortOrder);

        var history = await BuildPaymentHistoryAsync(
            schoolId,
            student.Id,
            academicYearId,
            feeTypeId,
            installmentNames,
            installmentSortOrders,
            cancellationToken);

        var situations = await BuildInstallmentSituationsAsync(
            schoolId,
            student.Id,
            academicYearId,
            feeTypeId,
            pedagogicalClassId,
            pricingCategoryId,
            installmentNames,
            cancellationToken);

        var branding = await _brandingResolver.ResolveAsync(
            schoolId,
            DocumentBrandingType.Recu,
            cancellationToken);

        var (parentName, parentPhone) = await ResolveParentAsync(student.Id, cancellationToken);
        var cashierName = anchorPayment is null
            ? null
            : await ResolveCashierNameAsync(anchorPayment.ReceivedByUserId, cancellationToken);

        var address = branding.Footer?.Address
            ?? string.Join(", ", new[] { school.Address, school.City, school.Province }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        var phone = branding.Footer?.Phone ?? school.Phone;
        var email = branding.Footer?.Email ?? school.Email;
        var motto = branding.Footer?.SchoolMotto;

        var periodFrom = history.Count > 0
            ? DateOnly.FromDateTime(history.Min(h => h.PaymentDate).ToLocalTime())
            : year.StartDate;
        var periodTo = history.Count > 0
            ? DateOnly.FromDateTime(history.Max(h => h.PaymentDate).ToLocalTime())
            : DateOnly.FromDateTime(DateTime.Now);
        if (periodTo < periodFrom)
        {
            periodTo = periodFrom;
        }

        var paymentDate = anchorPayment?.PaymentDate ?? DateTime.UtcNow;
        var statementNumber = BuildStatementNumber(
            paymentDate,
            anchorPayment?.Id ?? student.Id,
            feeTypeId);
        var receiptNumber = anchorPayment?.ReceiptNumber ?? "—";

        return new FeeTypeStatementDto(
            anchorPayment?.Id ?? Guid.Empty,
            receiptNumber,
            statementNumber,
            paymentDate,
            DateTime.Now,
            student.Id,
            string.Join(" ", new[] { student.LastName, student.MiddleName, student.FirstName }
                .Where(x => !string.IsNullOrWhiteSpace(x))),
            student.LastName,
            student.MiddleName,
            student.FirstName,
            student.RegistrationNumber,
            className,
            parentName,
            parentPhone,
            cashierName,
            periodFrom,
            periodTo,
            year.Id,
            year.Label,
            feeType.Id,
            feeType.Name,
            feeType.Currency,
            await ResolveCurrencyCodeAsync(anchorPayment?.FeeCurrencyId, feeType.Currency.ToString(), cancellationToken),
            await ResolveCurrencyCodeAsync(anchorPayment?.PaymentCurrencyId, feeType.Currency.ToString(), cancellationToken),
            anchorPayment?.FeeCurrencyAmount,
            anchorPayment?.PaymentCurrencyAmount,
            anchorPayment?.AppliedExchangeRate,
            school.Name,
            motto,
            string.IsNullOrWhiteSpace(address) ? null : address,
            phone,
            email,
            branding,
            history,
            situations,
            situations.Sum(s => s.AmountExpected),
            situations.Sum(s => s.AmountPaid),
            situations.Sum(s => s.Remaining));
    }

    private async Task<string?> ResolveCurrencyCodeAsync(
        Guid? currencyId,
        string fallback,
        CancellationToken cancellationToken)
    {
        if (!currencyId.HasValue)
            return fallback;
        var entity = await _currencyRepository.GetByIdAsync(currencyId.Value, cancellationToken);
        return entity?.Code ?? fallback;
    }

    private static string BuildStatementNumber(DateTime paymentDate, Guid primaryId, Guid? secondaryId = null)
    {
        var seq = Math.Abs(HashCode.Combine(primaryId, secondaryId ?? Guid.Empty)) % 1_000_000;
        return $"RS-{paymentDate:yyyy}-{seq:D6}";
    }

    public async Task<byte[]> ExportPdfAsync(
        Guid schoolId,
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var statement = await GetStatementAsync(schoolId, paymentId, feeTypeId, cancellationToken);
        return FeeTypeStatementPdfGenerator.BuildPdfBytes(statement, LoadBrandingImage);
    }

    public async Task<byte[]> ExportPdfForStudentAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        var statement = await GetStatementForStudentAsync(
            schoolId, studentId, academicYearId, feeTypeId, cancellationToken);
        return FeeTypeStatementPdfGenerator.BuildPdfBytes(statement, LoadBrandingImage);
    }

    private byte[]? LoadBrandingImage(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !_brandingStorage.FileExists(relativePath))
        {
            return null;
        }

        try
        {
            var absolute = _brandingStorage.ResolveAbsolutePath(relativePath);
            return File.Exists(absolute) ? File.ReadAllBytes(absolute) : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<FeeTypeStatementPaymentHistoryLineDto>> BuildPaymentHistoryAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        IReadOnlyDictionary<Guid, string> installmentNames,
        IReadOnlyDictionary<Guid, int> installmentSortOrders,
        CancellationToken cancellationToken)
    {
        var payments = (await _paymentRepository.FindAsync(
                p => p.SchoolId == schoolId
                     && p.StudentId == studentId
                     && p.AcademicYearId == academicYearId
                     && p.Status == PaymentStatus.Complet,
                cancellationToken))
            .ToList();

        if (payments.Count == 0)
        {
            return [];
        }

        var paymentIds = payments.Select(p => p.Id).ToHashSet();
        var paymentMap = payments.ToDictionary(p => p.Id);

        var lines = await _paymentLineRepository.FindAsync(
            l => paymentIds.Contains(l.PaymentId) && l.FeeTypeId == feeTypeId,
            cancellationToken);

        // Ordre de versement : chronologique (date → reçu → tranche → création ligne).
        var ordered = lines
            .OrderBy(l => paymentMap[l.PaymentId].PaymentDate)
            .ThenBy(l => paymentMap[l.PaymentId].CreatedAt)
            .ThenBy(l => paymentMap[l.PaymentId].ReceiptNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.FeeInstallmentId.HasValue
                && installmentSortOrders.TryGetValue(l.FeeInstallmentId.Value, out var sort)
                ? sort
                : int.MaxValue)
            .ThenBy(l => l.CreatedAt)
            .ToList();

        var result = new List<FeeTypeStatementPaymentHistoryLineDto>(ordered.Count);
        var n = 1;
        foreach (var line in ordered)
        {
            var payment = paymentMap[line.PaymentId];
            var installmentName = line.FeeInstallmentId.HasValue
                && installmentNames.TryGetValue(line.FeeInstallmentId.Value, out var name)
                ? name
                : "—";
            var receipt = !string.IsNullOrWhiteSpace(line.PhysicalReceiptNumber)
                ? line.PhysicalReceiptNumber.Trim()
                : payment.ReceiptNumber;

            result.Add(new FeeTypeStatementPaymentHistoryLineDto(
                n++,
                installmentName,
                payment.PaymentDate,
                line.Amount,
                receipt));
        }

        return result;
    }

    private async Task<IReadOnlyList<FeeTypeStatementInstallmentLineDto>> BuildInstallmentSituationsAsync(
        Guid schoolId,
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        Guid? pedagogicalClassId,
        Guid pricingCategoryId,
        IReadOnlyDictionary<Guid, string> installmentNames,
        CancellationToken cancellationToken)
    {
        if (!pedagogicalClassId.HasValue || pricingCategoryId == Guid.Empty)
        {
            return [];
        }

        var schedule = await _schoolFeeService.GetScheduleAsync(
            schoolId,
            academicYearId,
            pedagogicalClassId.Value,
            pricingCategoryId,
            feeTypeId,
            cancellationToken);

        var classFeeAmounts = (await _classFeeAmountRepository.FindAsync(
            a => a.SchoolId == schoolId
                 && a.AcademicYearId == academicYearId
                 && a.PedagogicalClassId == pedagogicalClassId.Value
                 && a.FeePricingCategoryId == pricingCategoryId
                 && a.FeeTypeId == feeTypeId,
            cancellationToken)).ToList();

        var classFeeByInstallment = classFeeAmounts
            .GroupBy(a => a.FeeInstallmentId)
            .ToDictionary(g => g.Key, g => g.First());

        var balances = await _balanceRepository.FindAsync(
            b => b.StudentId == studentId, cancellationToken);
        var balanceByClassFeeId = balances.ToDictionary(b => b.ClassFeeAmountId);

        var completedPaymentIds = (await _paymentRepository.FindAsync(
                p => p.SchoolId == schoolId
                     && p.StudentId == studentId
                     && p.AcademicYearId == academicYearId
                     && p.Status == PaymentStatus.Complet,
                cancellationToken))
            .Select(p => p.Id)
            .ToHashSet();

        var paidByInstallment = new Dictionary<Guid, decimal>();
        if (completedPaymentIds.Count > 0)
        {
            var paidLines = await _paymentLineRepository.FindAsync(
                l => completedPaymentIds.Contains(l.PaymentId)
                     && l.FeeTypeId == feeTypeId
                     && l.FeeInstallmentId != null,
                cancellationToken);

            paidByInstallment = paidLines
                .GroupBy(l => l.FeeInstallmentId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
        }

        IEnumerable<(Guid FeeInstallmentId, string InstallmentName, int SortOrder, decimal ScheduleAmount)> source;
        if (schedule.Lines.Count > 0)
        {
            source = schedule.Lines
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.InstallmentName)
                .Select(l => (l.FeeInstallmentId, l.InstallmentName, l.SortOrder, l.Amount));
        }
        else
        {
            source = classFeeAmounts
                .OrderBy(a => a.SortOrder)
                .Select(a => (
                    a.FeeInstallmentId,
                    installmentNames.TryGetValue(a.FeeInstallmentId, out var name) ? name : "—",
                    a.SortOrder,
                    a.Amount));
        }

        var result = new List<FeeTypeStatementInstallmentLineDto>();
        var n = 1;
        foreach (var line in source)
        {
            var expected = line.ScheduleAmount;
            if (classFeeByInstallment.TryGetValue(line.FeeInstallmentId, out var cfa)
                && balanceByClassFeeId.TryGetValue(cfa.Id, out var balance))
            {
                expected = balance.AmountDue;
            }

            paidByInstallment.TryGetValue(line.FeeInstallmentId, out var paid);
            var remaining = Math.Max(0, expected - paid);

            result.Add(new FeeTypeStatementInstallmentLineDto(
                n++,
                line.InstallmentName,
                expected,
                paid,
                remaining));
        }

        return result;
    }

    private async Task<(string? Name, string? Phone)> ResolveParentAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var links = await _studentGuardianRepository.FindAsync(
            sg => sg.StudentId == studentId, cancellationToken);
        var primary = links.FirstOrDefault(l => l.IsPrimary) ?? links.FirstOrDefault();
        if (primary is null)
        {
            return (null, null);
        }

        var guardian = (await _guardianRepository.FindAsync(
            g => g.Id == primary.GuardianId, cancellationToken)).FirstOrDefault();
        if (guardian is null)
        {
            return (null, null);
        }

        return ($"{guardian.LastName} {guardian.FirstName}".Trim(), guardian.Phone);
    }

    private async Task<string?> ResolveCashierNameAsync(Guid? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue)
        {
            return null;
        }

        var user = (await _userRepository.FindAsync(
            u => u.Id == userId.Value, cancellationToken)).FirstOrDefault();
        if (user is null)
        {
            return null;
        }

        var full = $"{user.LastName} {user.FirstName}".Trim();
        return string.IsNullOrWhiteSpace(full) ? user.UserName : full;
    }
}
