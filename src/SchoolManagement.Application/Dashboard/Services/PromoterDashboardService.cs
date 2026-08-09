namespace SchoolManagement.Application.Dashboard.Services;

using SchoolManagement.Application.Common;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Dashboard.DTOs;
using SchoolManagement.Application.Dashboard.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Constants;
using System.Globalization;

public sealed class PromoterDashboardService : IPromoterDashboardService
{
    private static readonly string[] Palette =
    [
        "#1D4ED8", "#0B1F47", "#22C55E", "#F59E0B", "#EF4444",
        "#8B5CF6", "#06B6D4", "#EC4899", "#84CC16", "#64748B"
    ];

    private readonly IRepository<School> _schoolRepository;
    private readonly IRepository<AcademicYear> _academicYearRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentLine> _paymentLineRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;
    private readonly IRepository<RevenueAllocationEntry> _allocationEntryRepository;
    private readonly IRepository<RevenueAllocationDestination> _destinationRepository;
    private readonly IRepository<StudentAttendance> _attendanceRepository;
    private readonly IRepository<ExpensePayment> _expensePaymentRepository;
    private readonly IRepository<ClassFeeAmount> _classFeeAmountRepository;
    private readonly IRepository<RevenueAllocationKey> _allocationKeyRepository;
    private readonly IRepository<RevenueAllocationKeyDetail> _allocationKeyDetailRepository;
    private readonly IRepository<WithholdingType> _withholdingTypeRepository;
    private readonly IRepository<FeeInstallment> _feeInstallmentRepository;
    private readonly IRepository<SchoolLogo> _schoolLogoRepository;
    private readonly IRepository<Section> _sectionRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;

    public PromoterDashboardService(
        IRepository<School> schoolRepository,
        IRepository<AcademicYear> academicYearRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<Student> studentRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<RevenueAllocationEntry> allocationEntryRepository,
        IRepository<RevenueAllocationDestination> destinationRepository,
        IRepository<StudentAttendance> attendanceRepository,
        IRepository<ExpensePayment> expensePaymentRepository,
        IRepository<ClassFeeAmount> classFeeAmountRepository,
        IRepository<RevenueAllocationKey> allocationKeyRepository,
        IRepository<RevenueAllocationKeyDetail> allocationKeyDetailRepository,
        IRepository<WithholdingType> withholdingTypeRepository,
        IRepository<FeeInstallment> feeInstallmentRepository,
        IRepository<SchoolLogo> schoolLogoRepository,
        IRepository<Section> sectionRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository)
    {
        _schoolRepository = schoolRepository;
        _academicYearRepository = academicYearRepository;
        _paymentRepository = paymentRepository;
        _paymentLineRepository = paymentLineRepository;
        _studentRepository = studentRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRoomRepository = classRoomRepository;
        _feeTypeRepository = feeTypeRepository;
        _balanceRepository = balanceRepository;
        _allocationEntryRepository = allocationEntryRepository;
        _destinationRepository = destinationRepository;
        _attendanceRepository = attendanceRepository;
        _expensePaymentRepository = expensePaymentRepository;
        _classFeeAmountRepository = classFeeAmountRepository;
        _allocationKeyRepository = allocationKeyRepository;
        _allocationKeyDetailRepository = allocationKeyDetailRepository;
        _withholdingTypeRepository = withholdingTypeRepository;
        _feeInstallmentRepository = feeInstallmentRepository;
        _schoolLogoRepository = schoolLogoRepository;
        _sectionRepository = sectionRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
    }

    public async Task<PromoterDashboardOverviewDto> GetOverviewAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        RevenueGranularity granularity = RevenueGranularity.Daily,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var school = (await _schoolRepository.FindAsync(s => s.Id == schoolId, cancellationToken)).FirstOrDefault();
        var schoolName = school?.Name ?? "Établissement";

        var feeTypes = (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId && f.IsActive, cancellationToken))
            .OrderBy(f => f.Name)
            .ToList();
        var availableFees = feeTypes
            .Select(f => new DashboardFeeTypeOptionDto(f.Id, f.Name, f.Currency.ToString()))
            .ToList();

        var selectedFee = ResolveSelectedFeeType(feeTypes, school?.DefaultFeeTypeId, feeTypeId);
        var selectedFeeId = selectedFee?.Id;
        var selectedFeeName = selectedFee?.Name ?? "Tous les frais";
        var currency = selectedFee?.Currency.ToString() ?? AppConstants.DefaultCurrency;

        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var paymentIds = payments.Select(p => p.Id).ToHashSet();
        var allLines = await _paymentLineRepository.FindAsync(l => paymentIds.Contains(l.PaymentId), cancellationToken);
        var feeLines = selectedFeeId is null
            ? allLines.ToList()
            : allLines.Where(l => l.FeeTypeId == selectedFeeId.Value).ToList();
        var amountByPayment = feeLines
            .GroupBy(l => l.PaymentId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        decimal SumFee(DateTime start, DateTime end) =>
            payments
                .Where(p =>
                {
                    var d = DateTime.SpecifyKind(p.PaymentDate, DateTimeKind.Utc);
                    return d >= start && d < end;
                })
                .Sum(p => amountByPayment.GetValueOrDefault(p.Id));

        var expenses = await LoadExpensesAsync(schoolId, cancellationToken);
        var years = (await _academicYearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken))
            .OrderByDescending(y => y.StartDate)
            .ToList();
        var todayDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = ResolveOperationalAcademicYear(years, todayDate);
        var currentYearId = currentYear?.Id;
        var (yearStart, yearEnd) = ResolveAcademicYearBounds(currentYear)
            ?? await ResolveSchoolYearRangeAsync(schoolId, cancellationToken);
        var (monthStart, monthEnd) = ResolveRange(DashboardPeriod.Month);
        var (prevMonthStart, prevMonthEnd) = ResolvePreviousRange(DashboardPeriod.Month);
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);
        var yesterdayStart = todayStart.AddDays(-1);

        var dayRevenue = SumFee(todayStart, todayEnd);
        var yesterdayRevenue = SumFee(yesterdayStart, todayStart);
        var monthRevenue = SumFee(monthStart, monthEnd);
        var prevMonthRevenue = SumFee(prevMonthStart, prevMonthEnd);
        // Année scolaire : privilégier AcademicYearId (source de vérité), dates en filet.
        var yearRevenue = SumFeeForAcademicYear(payments, amountByPayment, currentYearId, yearStart, yearEnd);
        var (prevYearStart, prevYearEnd) = await ResolvePreviousSchoolYearRangeAsync(
            schoolId, yearStart, cancellationToken);
        var previousYear = years
            .Where(y => y.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) <= yearStart)
            .OrderByDescending(y => y.EndDate)
            .FirstOrDefault();
        var prevYearRevenue = SumFeeForAcademicYear(
            payments,
            amountByPayment,
            previousYear?.Id,
            prevYearStart,
            prevYearEnd);

        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var studentIds = students.Select(s => s.Id).ToHashSet();

        // Même population que les encaissements Desktop : année opérationnelle + statuts actifs.
        var yearEnrollments = await LoadActiveYearEnrollmentsAsync(schoolId, currentYearId, studentIds, cancellationToken);
        var enrolledIds = yearEnrollments.Select(e => e.StudentId).Distinct().ToHashSet();
        var enrolledStudents = students.Where(s => enrolledIds.Contains(s.Id)).ToList();
        var boys = enrolledStudents.Count(s => s.Gender == Gender.Masculin);
        var girls = enrolledStudents.Count(s => s.Gender == Gender.Feminin);
        var (rangeStart, rangeEnd) = ResolveRange(period);
        var newEnrollments = yearEnrollments.Count(e =>
        {
            var d = e.EnrollmentDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            return d >= rangeStart && d < rangeEnd;
        });

        var kpis = new PromoterKpiBoardDto(
            new PromoterMoneyKpiDto(
                "Recette du jour",
                dayRevenue,
                PercentChange(dayRevenue, yesterdayRevenue),
                "vs hier"),
            new PromoterMoneyKpiDto(
                "Recette du mois",
                monthRevenue,
                PercentChange(monthRevenue, prevMonthRevenue),
                "vs mois précédent"),
            new PromoterMoneyKpiDto(
                "Recette annuelle",
                yearRevenue,
                PercentChange(yearRevenue, prevYearRevenue),
                "vs année précédente"),
            new PromoterStudentsKpiDto(enrolledStudents.Count, boys, girls, newEnrollments));

        var daily30Start = todayStart.AddDays(-29);
        var dailySeries = BuildDailySeriesFromAmounts(
            payments, amountByPayment, daily30Start, todayEnd);
        var yearPaymentsForSeries = FilterPaymentsForAcademicYear(
            payments, amountByPayment, currentYearId, yearStart, yearEnd);
        var (seriesStart, seriesEnd) = ExpandRangeToPayments(
            yearStart,
            yearEnd > todayEnd ? todayEnd : yearEnd,
            yearPaymentsForSeries);
        var monthlySeries = BuildMonthlySeriesFromAmounts(
            yearPaymentsForSeries, amountByPayment, seriesStart, seriesEnd);

        var expenseToday = SumExpenses(expenses, todayStart, todayEnd);
        var expenseMonth = SumExpenses(expenses, monthStart, monthEnd);
        var expenseYear = currentYearId is Guid yearIdForExpenses
            ? expenses.Where(e => e.AcademicYearId == yearIdForExpenses).Sum(e => e.Amount)
            : SumExpenses(expenses, yearStart, yearEnd);
        if (expenseYear == 0)
        {
            expenseYear = SumExpenses(expenses, yearStart, yearEnd);
        }
        var expenseCategories = await BuildExpenseCategoriesAsync(
            schoolId,
            expenses.Where(e =>
            {
                if (currentYearId is Guid yid && e.AcademicYearId == yid)
                {
                    return true;
                }

                var dt = e.ExpenseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                return dt >= yearStart && dt < yearEnd;
            }).ToList(),
            yearStart,
            yearEnd,
            cancellationToken);

        var expensesBoard = new PromoterExpensesBoardDto(
            expenseToday,
            expenseMonth,
            expenseYear,
            expenseCategories);

        var funds = await BuildFeeFundCashFlowAsync(
            schoolId,
            selectedFeeId,
            todayStart,
            yearStart,
            cancellationToken);
        var withholdings = await BuildWithholdingsForFeeAsync(
            schoolId,
            selectedFeeId,
            todayStart,
            monthStart,
            monthEnd,
            yearStart,
            yearEnd,
            cancellationToken);

        // Situation financière année scolaire : recettes du frais suivi − dépenses de l'école.
        var situation = new PromoterSituationDto(
            yearRevenue,
            expenseYear,
            yearRevenue - expenseYear);

        var studentMap = students.ToDictionary(s => s.Id);
        var receivableRows = await BuildFeeReceivableRowsAsync(
            schoolId,
            selectedFeeId,
            currentYearId,
            yearEnrollments,
            studentMap,
            cancellationToken);
        var overdueRows = await BuildOverdueReceivableRowsAsync(
            schoolId,
            selectedFeeId,
            currentYearId,
            yearEnrollments,
            studentMap,
            cancellationToken);
        // Cartes créances : À percevoir / Débiteurs = échéances dépassées ;
        // En ordre / Recouvrement = situation annuelle du frais.
        var receivables = AggregateReceivables(receivableRows, overdueRows);

        var summary = await GetSummaryAsync(schoolId, period, cancellationToken);
        var series = await GetRevenueSeriesAsync(schoolId, period, granularity, cancellationToken);
        var feeShares = await GetFeeTypeRepartitionAsync(schoolId, period, cancellationToken);
        var activities = await GetActivitiesAsync(schoolId, 15, cancellationToken);
        var alerts = await BuildAlertsAsync(
            schoolId,
            dayRevenue,
            yesterdayRevenue,
            expenseMonth,
            monthRevenue,
            receivables.DebtorStudents,
            enrolledStudents.Count,
            receivables.RecoveryPercent,
            cancellationToken);
        var topClasses = await GetTopClassesAsync(schoolId, period, cancellationToken);
        var quick = await GetQuickStatsAsync(schoolId, period, cancellationToken);
        var schoolLogoUrl = await ResolveSchoolLogoUrlAsync(schoolId, cancellationToken);

        return new PromoterDashboardOverviewDto(
            schoolName,
            schoolLogoUrl,
            currency,
            period.ToString(),
            DateTime.UtcNow,
            selectedFeeId,
            selectedFeeName,
            availableFees,
            kpis,
            dailySeries,
            monthlySeries,
            expensesBoard,
            funds,
            withholdings,
            situation,
            receivables,
            alerts,
            summary,
            series,
            feeShares,
            activities,
            topClasses,
            feeShares.Take(5).ToList(),
            quick);
    }

    private async Task<string?> ResolveSchoolLogoUrlAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var logos = await _schoolLogoRepository.FindAsync(
            l => l.SchoolId == schoolId && l.IsActive,
            cancellationToken);
        var primary = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();
        if (primary is null || string.IsNullOrWhiteSpace(primary.ImagePath))
        {
            return null;
        }

        return $"/{ApiRoutes.DocumentBranding}/logos/primary/file";
    }

    private static FeeType? ResolveSelectedFeeType(
        IReadOnlyList<FeeType> feeTypes,
        Guid? schoolDefaultFeeTypeId,
        Guid? requestedFeeTypeId)
    {
        if (requestedFeeTypeId is Guid requested)
        {
            var match = feeTypes.FirstOrDefault(f => f.Id == requested);
            if (match is not null)
            {
                return match;
            }
        }

        if (schoolDefaultFeeTypeId is Guid def)
        {
            var match = feeTypes.FirstOrDefault(f => f.Id == def);
            if (match is not null)
            {
                return match;
            }
        }

        return feeTypes.FirstOrDefault(f =>
                   string.Equals(f.Name, "Frais scolaire", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(f.Name, "Frais scolaires", StringComparison.OrdinalIgnoreCase))
               ?? feeTypes.FirstOrDefault(f =>
                   f.Name.Contains("scolaire", StringComparison.OrdinalIgnoreCase))
               ?? feeTypes.FirstOrDefault();
    }

    private static List<RevenuePointDto> BuildDailySeriesFromAmounts(
        List<Payment> payments,
        Dictionary<Guid, decimal> amountByPayment,
        DateTime start,
        DateTime end)
    {
        var points = new List<RevenuePointDto>();
        for (var d = start.Date; d < end; d = d.AddDays(1))
        {
            var amount = payments
                .Where(p => p.PaymentDate.Date == d)
                .Sum(p => amountByPayment.GetValueOrDefault(p.Id));
            points.Add(new RevenuePointDto(d.ToString("dd/MM"), DateTime.SpecifyKind(d, DateTimeKind.Utc), amount));
        }

        return points;
    }

    private static List<RevenuePointDto> BuildMonthlySeriesFromAmounts(
        List<Payment> payments,
        Dictionary<Guid, decimal> amountByPayment,
        DateTime start,
        DateTime end)
    {
        var points = new List<RevenuePointDto>();
        var cursor = new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        while (cursor < end)
        {
            var monthEnd = cursor.AddMonths(1);
            var amount = payments
                .Where(p => p.PaymentDate >= cursor && p.PaymentDate < monthEnd)
                .Sum(p => amountByPayment.GetValueOrDefault(p.Id));
            points.Add(new RevenuePointDto(cursor.ToString("MMM yy"), cursor, amount));
            cursor = monthEnd;
        }

        return points;
    }

    public async Task<PromoterFinancialSummaryDto> GetSummaryAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        CancellationToken cancellationToken = default)
    {
        var (rangeStart, rangeEnd) = ResolveRange(period);
        var (prevStart, prevEnd) = ResolvePreviousRange(period);
        var (monthStart, monthEnd) = ResolveRange(DashboardPeriod.Month);
        var (prevMonthStart, prevMonthEnd) = ResolvePreviousRange(DashboardPeriod.Month);
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var periodRevenue = SumPayments(payments, rangeStart, rangeEnd);
        var prevRevenue = SumPayments(payments, prevStart, prevEnd);
        var monthRevenue = SumPayments(payments, monthStart, monthEnd);
        var prevMonthRevenue = SumPayments(payments, prevMonthStart, prevMonthEnd);
        var dayRevenue = SumPayments(payments, todayStart, todayEnd);
        var yesterdayRevenue = SumPayments(payments, todayStart.AddDays(-1), todayStart);

        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var studentIds = students.Select(s => s.Id).ToHashSet();
        var enrollments = await SchoolScopedEnrollmentQueries.GetActiveForStudentsAsync(
            _enrollmentRepository, studentIds, cancellationToken);
        var schoolEnrollments = enrollments.ToList();
        var activeStudents = schoolEnrollments.Select(e => e.StudentId).Distinct().Count();

        var newEnrollments = schoolEnrollments.Count(e =>
        {
            var d = e.EnrollmentDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            return d >= rangeStart && d < rangeEnd;
        });

        var balances = await _balanceRepository.FindAsync(b => studentIds.Contains(b.StudentId), cancellationToken);
        var expected = balances.Sum(b => b.AmountDue);
        var collectedAll = balances.Sum(b => b.AmountPaid);
        var realization = expected <= 0 ? 0 : Math.Round(collectedAll / expected * 100m, 1);

        var (periodLabel, secondaryLabel, secondaryAmount, secondaryChange) = period switch
        {
            DashboardPeriod.Today => (
                "Recette du jour",
                "Recette du mois",
                monthRevenue,
                PercentChange(monthRevenue, prevMonthRevenue)),
            DashboardPeriod.Week => (
                "Recette de la semaine",
                "Recette du mois",
                monthRevenue,
                PercentChange(monthRevenue, prevMonthRevenue)),
            DashboardPeriod.Year => (
                "Recette de l'année",
                "Recette du mois",
                monthRevenue,
                PercentChange(monthRevenue, prevMonthRevenue)),
            _ => (
                "Recette du mois",
                "Recette du jour",
                dayRevenue,
                PercentChange(dayRevenue, yesterdayRevenue))
        };

        var primaryAmount = period == DashboardPeriod.Month ? monthRevenue : periodRevenue;
        var primaryChange = period == DashboardPeriod.Month
            ? PercentChange(monthRevenue, prevMonthRevenue)
            : PercentChange(periodRevenue, prevRevenue);

        if (period == DashboardPeriod.Today)
        {
            primaryAmount = dayRevenue;
            primaryChange = PercentChange(dayRevenue, yesterdayRevenue);
        }

        return new PromoterFinancialSummaryDto(
            periodLabel,
            primaryAmount,
            primaryChange,
            secondaryLabel,
            secondaryAmount,
            secondaryChange,
            newEnrollments,
            activeStudents,
            realization,
            expected,
            collectedAll);
    }

    public async Task<IReadOnlyList<RevenuePointDto>> GetRevenueSeriesAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        RevenueGranularity granularity = RevenueGranularity.Daily,
        CancellationToken cancellationToken = default)
    {
        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var (rangeStart, rangeEnd) = ResolveRange(period);
        var inRange = payments.Where(p => p.PaymentDate >= rangeStart && p.PaymentDate < rangeEnd).ToList();

        return granularity switch
        {
            RevenueGranularity.Weekly => BuildWeeklySeries(inRange, rangeStart, rangeEnd),
            RevenueGranularity.Monthly => BuildMonthlySeries(inRange, rangeStart, rangeEnd),
            _ => BuildDailySeries(inRange, rangeStart, rangeEnd)
        };
    }

    public async Task<IReadOnlyList<NamedAmountShareDto>> GetFeeTypeRepartitionAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        CancellationToken cancellationToken = default)
    {
        var (rangeStart, rangeEnd) = ResolveRange(period);
        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var paymentIds = payments
            .Where(p => p.PaymentDate >= rangeStart && p.PaymentDate < rangeEnd)
            .Select(p => p.Id)
            .ToHashSet();

        var lines = await _paymentLineRepository.FindAsync(l => paymentIds.Contains(l.PaymentId), cancellationToken);
        var feeTypes = await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken);
        var feeMap = feeTypes.ToDictionary(f => f.Id, f => f.Name);

        var groups = lines
            .GroupBy(l => l.FeeTypeId)
            .Select(g => (Name: feeMap.GetValueOrDefault(g.Key, "Autres frais"), Amount: g.Sum(x => x.Amount)))
            .Where(x => x.Amount > 0)
            .OrderByDescending(x => x.Amount)
            .ToList();

        var total = groups.Sum(x => x.Amount);
        return groups
            .Select((g, i) => new NamedAmountShareDto(
                g.Name,
                g.Amount,
                total <= 0 ? 0 : Math.Round(g.Amount / total * 100m, 1),
                Palette[i % Palette.Length]))
            .ToList();
    }

    public async Task<IReadOnlyList<FundAllocationShareDto>> GetFundDistributionAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var school = (await _schoolRepository.FindAsync(s => s.Id == schoolId, cancellationToken)).FirstOrDefault();
        var feeTypes = (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId && f.IsActive, cancellationToken)).ToList();
        var selectedFee = ResolveSelectedFeeType(feeTypes, school?.DefaultFeeTypeId, feeTypeId);
        var todayStart = DateTime.UtcNow.Date;
        var (yearStart, _) = await ResolveSchoolYearRangeAsync(schoolId, cancellationToken);
        _ = period;
        return await BuildFeeFundCashFlowAsync(
            schoolId,
            selectedFee?.Id,
            todayStart,
            yearStart,
            cancellationToken);
    }

    public async Task<IReadOnlyList<DashboardActivityDto>> GetActivitiesAsync(
        Guid schoolId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);
        var studentIds = studentMap.Keys.ToHashSet();

        var enrollments = await SchoolScopedEnrollmentQueries.GetActiveForStudentsAsync(
            _enrollmentRepository, studentIds, cancellationToken);
        var schoolEnrollments = enrollments
            .OrderByDescending(e => e.EnrollmentDate)
            .Take(take)
            .ToList();

        var paymentActivities = payments
            .OrderByDescending(p => p.PaymentDate)
            .Take(take)
            .Select(p =>
            {
                var student = studentMap.GetValueOrDefault(p.StudentId);
                var name = StudentDisplayName.FormatOrDefault(student, "Élève");
                return new DashboardActivityDto(
                    DateTime.SpecifyKind(p.PaymentDate, DateTimeKind.Utc),
                    "Payment",
                    "Paiement",
                    name,
                    p.TotalAmount,
                    p.Currency.ToString());
            });

        var enrollmentActivities = schoolEnrollments.Select(e =>
        {
            var student = studentMap.GetValueOrDefault(e.StudentId);
            var name = StudentDisplayName.FormatOrDefault(student, "Élève");
            return new DashboardActivityDto(
                e.EnrollmentDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                "Enrollment",
                "Nouvelle inscription",
                name,
                null,
                null);
        });

        return paymentActivities
            .Concat(enrollmentActivities)
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(take)
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardAlertDto>> GetAlertsAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        CancellationToken cancellationToken = default)
    {
        var overview = await GetOverviewAsync(schoolId, period, RevenueGranularity.Daily, null, cancellationToken);
        return overview.Alerts;
    }

    public async Task<IReadOnlyList<DashboardPaymentLineDto>> GetPaymentsDetailAsync(
        Guid schoolId,
        DashboardDetailScope scope,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = await ResolveDetailRangeAsync(schoolId, scope, cancellationToken);
        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var paymentIds = payments.Select(p => p.Id).ToHashSet();
        var allLines = await _paymentLineRepository.FindAsync(l => paymentIds.Contains(l.PaymentId), cancellationToken);
        var amountByPayment = feeTypeId is null
            ? payments.ToDictionary(p => p.Id, p => p.TotalAmount)
            : allLines
                .Where(l => l.FeeTypeId == feeTypeId.Value)
                .GroupBy(l => l.PaymentId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);

        return payments
            .Where(p => p.PaymentDate >= start && p.PaymentDate < end && amountByPayment.ContainsKey(p.Id))
            .OrderByDescending(p => p.PaymentDate)
            .Take(200)
            .Select(p =>
            {
                var student = studentMap.GetValueOrDefault(p.StudentId);
                var name = StudentDisplayName.FormatOrDefault(student, "Élève");
                return new DashboardPaymentLineDto(
                    p.Id,
                    DateTime.SpecifyKind(p.PaymentDate, DateTimeKind.Utc),
                    name,
                    p.ReceiptNumber ?? p.Id.ToString("N")[..8].ToUpperInvariant(),
                    amountByPayment.GetValueOrDefault(p.Id),
                    p.Currency.ToString(),
                    p.PaymentMethod ?? "—");
            })
            .ToList();
    }

    /// <summary>
    /// Détail recettes : mois = totaux journaliers (jours avec perception) ;
    /// année = totaux mensuels (mois avec perception). Frais suivi.
    /// </summary>
    public async Task<IReadOnlyList<RevenuePointDto>> GetRevenueDetailAsync(
        Guid schoolId,
        DashboardDetailScope scope,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var school = (await _schoolRepository.FindAsync(s => s.Id == schoolId, cancellationToken)).FirstOrDefault();
        var feeTypes = (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId && f.IsActive, cancellationToken)).ToList();
        var selectedFee = ResolveSelectedFeeType(feeTypes, school?.DefaultFeeTypeId, feeTypeId);

        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var paymentIds = payments.Select(p => p.Id).ToHashSet();
        var allLines = await _paymentLineRepository.FindAsync(l => paymentIds.Contains(l.PaymentId), cancellationToken);
        var feeLines = selectedFee is null
            ? allLines.ToList()
            : allLines.Where(l => l.FeeTypeId == selectedFee.Id).ToList();
        var amountByPayment = feeLines
            .GroupBy(l => l.PaymentId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var fr = CultureInfo.GetCultureInfo("fr-FR");

        if (scope == DashboardDetailScope.Year)
        {
            var years = (await _academicYearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken))
                .OrderByDescending(y => y.StartDate)
                .ToList();
            var todayDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var year = ResolveOperationalAcademicYear(years, todayDate);
            var (yearStart, yearEnd) = ResolveAcademicYearBounds(year)
                ?? await ResolveSchoolYearRangeAsync(schoolId, cancellationToken);
            var todayEnd = DateTime.UtcNow.Date.AddDays(1);
            var end = yearEnd > todayEnd ? todayEnd : yearEnd;
            var yearPayments = FilterPaymentsForAcademicYear(
                payments, amountByPayment, year?.Id, yearStart, yearEnd);
            var (seriesStart, seriesEnd) = ExpandRangeToPayments(yearStart, end, yearPayments);

            return BuildMonthlySeriesFromAmounts(yearPayments, amountByPayment, seriesStart, seriesEnd)
                .Where(p => p.Amount > 0)
                .Select(p =>
                {
                    // Libellé mois seul (ex. « Septembre ») — année scolaire déjà contextualisée.
                    var label = p.PeriodStartUtc.ToString("MMMM", fr);
                    if (label.Length > 0)
                    {
                        label = char.ToUpper(label[0], fr) + label[1..];
                    }

                    return p with { Label = label };
                })
                .ToList();
        }

        // Mois : uniquement les jours avec au moins une perception.
        var (monthStart, monthEnd) = ResolveRange(DashboardPeriod.Month);
        var lastDay = monthEnd < DateTime.UtcNow.Date.AddDays(1) ? monthEnd : DateTime.UtcNow.Date.AddDays(1);
        return BuildDailySeriesFromAmounts(payments, amountByPayment, monthStart, lastDay)
            .Where(p => p.Amount > 0)
            .Select(p => p with
            {
                Label = p.PeriodStartUtc.ToString("dd/MM/yyyy", fr)
            })
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardExpenseLineDto>> GetExpensesDetailAsync(
        Guid schoolId,
        DashboardDetailScope scope,
        Guid? destinationId = null,
        CancellationToken cancellationToken = default)
    {
        var expenses = await LoadExpensesAsync(schoolId, cancellationToken);
        var destinations = await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken);
        var destMap = destinations.ToDictionary(d => d.Id, d => d.Name);

        IEnumerable<ExpensePayment> filtered;
        if (scope == DashboardDetailScope.Year)
        {
            // Année scolaire : privilégier AcademicYearId opérationnel, avec filet date.
            var years = (await _academicYearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken))
                .OrderByDescending(y => y.StartDate)
                .ToList();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var year = ResolveOperationalAcademicYear(years, today);
            var (start, end) = await ResolveSchoolYearRangeAsync(schoolId, cancellationToken);

            filtered = expenses.Where(e =>
            {
                if (destinationId.HasValue && e.DestinationId != destinationId.Value)
                {
                    return false;
                }

                if (year is not null && e.AcademicYearId == year.Id)
                {
                    return true;
                }

                var dt = e.ExpenseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                return dt >= start && dt < end;
            });
        }
        else
        {
            var (start, end) = await ResolveDetailRangeAsync(schoolId, scope, cancellationToken);
            filtered = expenses.Where(e =>
            {
                var dt = e.ExpenseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                return dt >= start && dt < end && (!destinationId.HasValue || e.DestinationId == destinationId.Value);
            });
        }

        return filtered
            .OrderByDescending(e => e.ExpenseDate)
            .ThenBy(e => e.Label)
            .Take(2000)
            .Select(e => new DashboardExpenseLineDto(
                e.Id,
                e.ExpenseDate,
                e.Label,
                FormatExpenseCategory(e.Category)
                    ?? destMap.GetValueOrDefault(e.DestinationId, "Autres"),
                e.Amount,
                e.Currency.ToString(),
                e.Reference))
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardDebtorLineDto>> GetDebtorsDetailAsync(
        Guid schoolId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var breakdown = await GetFeeReceivablesBreakdownAsync(schoolId, feeTypeId, cancellationToken);
        return breakdown.Debtors;
    }

    public async Task<FeeReceivablesBreakdownDto> GetFeeReceivablesBreakdownAsync(
        Guid schoolId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var school = (await _schoolRepository.FindAsync(s => s.Id == schoolId, cancellationToken)).FirstOrDefault();
        var feeTypes = (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId && f.IsActive, cancellationToken)).ToList();
        var selectedFee = ResolveSelectedFeeType(feeTypes, school?.DefaultFeeTypeId, feeTypeId)
            ?? throw new InvalidOperationException("Aucun type de frais actif n'est configuré.");

        var years = (await _academicYearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken))
            .OrderByDescending(y => y.StartDate)
            .ToList();
        var todayDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentYear = ResolveOperationalAcademicYear(years, todayDate)
            ?? throw new InvalidOperationException("Aucune année scolaire n'est configurée.");

        var students = (await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken))
            .ToDictionary(s => s.Id);
        var enrollments = await LoadActiveYearEnrollmentsAsync(
            schoolId,
            currentYear.Id,
            students.Keys.ToHashSet(),
            cancellationToken);

        var classRooms = (await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken))
            .ToDictionary(c => c.Id);
        var yearTariffs = (await _classFeeAmountRepository.FindAsync(
                a => a.SchoolId == schoolId
                     && a.AcademicYearId == currentYear.Id
                     && a.FeeTypeId == selectedFee.Id,
                cancellationToken))
            .ToList();

        var installmentIds = yearTariffs.Select(t => t.FeeInstallmentId).Distinct().ToList();
        var installments = installmentIds.Count == 0
            ? new Dictionary<Guid, FeeInstallment>()
            : (await _feeInstallmentRepository.FindAsync(i => installmentIds.Contains(i.Id), cancellationToken))
                .ToDictionary(i => i.Id);

        var allTariffIds = yearTariffs.Select(a => a.Id).ToHashSet();
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var balances = allTariffIds.Count == 0 || studentIds.Count == 0
            ? []
            : (await _balanceRepository.FindAsync(
                    b => studentIds.Contains(b.StudentId) && allTariffIds.Contains(b.ClassFeeAmountId),
                    cancellationToken))
                .ToList();
        var paidByTariffStudent = balances
            .GroupBy(b => (b.StudentId, b.ClassFeeAmountId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPaid));
        var dueByTariffStudent = balances
            .GroupBy(b => (b.StudentId, b.ClassFeeAmountId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountDue));

        var tariffsByInstallment = yearTariffs.GroupBy(t => t.FeeInstallmentId).ToList();
        var byInstallment = new List<FeeInstallmentReceivableDto>();
        foreach (var group in tariffsByInstallment)
        {
            var installmentId = group.Key;
            installments.TryGetValue(installmentId, out var installment);
            var tariffsForInstallment = group.ToDictionary(
                t => (t.PedagogicalClassId, t.FeePricingCategoryId),
                t => t);

            decimal expected = 0;
            decimal paid = 0;
            foreach (var enrollment in enrollments)
            {
                if (!classRooms.TryGetValue(enrollment.ClassRoomId, out var room)
                    || room.PedagogicalClassId is not Guid pedId)
                {
                    continue;
                }

                if (!tariffsForInstallment.TryGetValue((pedId, enrollment.FeePricingCategoryId), out var tariff))
                {
                    continue;
                }

                if (tariff.Amount > 0)
                {
                    expected += tariff.Amount;
                }
                else
                {
                    dueByTariffStudent.TryGetValue((enrollment.StudentId, tariff.Id), out var dueAmount);
                    expected += dueAmount;
                }

                paidByTariffStudent.TryGetValue((enrollment.StudentId, tariff.Id), out var paidAmount);
                paid += paidAmount;
            }

            // Si le tarif est 0 mais des soldes existent hors matching (rare), ignore.
            byInstallment.Add(new FeeInstallmentReceivableDto(
                installmentId,
                installment?.Name ?? "Tranche",
                installment?.SortOrder ?? 999,
                expected,
                paid,
                Math.Max(0m, expected - paid)));
        }

        byInstallment = byInstallment
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.InstallmentName)
            .ToList();

        var totalExpected = byInstallment.Sum(x => x.AmountExpected);
        var totalPaid = byInstallment.Sum(x => x.AmountPaid);
        var totalRemaining = Math.Max(0m, totalExpected - totalPaid);

        // Si aucune tranche tarifaire, retomber sur la logique élève (attendu agrégé).
        var receivableRows = await BuildFeeReceivableRowsAsync(
            schoolId,
            selectedFee.Id,
            currentYear.Id,
            enrollments,
            students,
            cancellationToken);
        if (byInstallment.Count == 0 && receivableRows.Count > 0)
        {
            totalExpected = receivableRows.Sum(r => r.AmountDue);
            totalPaid = receivableRows.Sum(r => r.AmountPaid);
            totalRemaining = receivableRows.Sum(r => r.Remaining);
        }
        else if (byInstallment.Count > 0)
        {
            // Aligner le total sur la somme des tranches (référence) ; les débiteurs restent calculés élève.
            totalExpected = byInstallment.Sum(x => x.AmountExpected);
            totalPaid = byInstallment.Sum(x => x.AmountPaid);
            totalRemaining = Math.Max(0m, totalExpected - totalPaid);
        }

        var byDestination = await BuildDestinationReceivablesAsync(
            schoolId,
            selectedFee.Id,
            currentYear.Id,
            totalExpected,
            totalPaid,
            cancellationToken);

        // Débiteurs = uniquement les tranches échues non soldées (montant = somme des retards).
        var overdueRows = await BuildOverdueReceivableRowsAsync(
            schoolId,
            selectedFee.Id,
            currentYear.Id,
            enrollments,
            students,
            cancellationToken);
        var debtors = overdueRows
            .Where(r => r.Remaining > 0)
            .OrderByDescending(r => r.Remaining)
            .Take(300)
            .Select(r => new DashboardDebtorLineDto(
                r.StudentId,
                r.StudentName,
                r.ClassName,
                r.AmountDue,
                r.AmountPaid,
                r.Remaining))
            .ToList();

        var overdueExpected = overdueRows.Sum(r => r.AmountDue);
        var overduePaid = overdueRows.Sum(r => r.AmountPaid);
        var overdueRemaining = overdueRows.Sum(r => r.Remaining);

        return new FeeReceivablesBreakdownDto(
            selectedFee.Id,
            selectedFee.Name,
            currentYear.Id,
            currentYear.Label,
            selectedFee.Currency.ToString(),
            // Totaux de synthèse = créances échues (alignés sur la liste débiteurs).
            overdueRows.Count > 0 ? overdueExpected : totalExpected,
            overdueRows.Count > 0 ? overduePaid : totalPaid,
            overdueRows.Count > 0 ? overdueRemaining : totalRemaining,
            byInstallment,
            byDestination,
            debtors);
    }

    private async Task<IReadOnlyList<FeeDestinationReceivableDto>> BuildDestinationReceivablesAsync(
        Guid schoolId,
        Guid feeTypeId,
        Guid academicYearId,
        decimal totalExpected,
        decimal totalPaid,
        CancellationToken cancellationToken)
    {
        var destinations = (await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);

        var openKey = (await _allocationKeyRepository.FindAsync(
                k => k.SchoolId == schoolId
                     && k.AcademicYearId == academicYearId
                     && k.FeeTypeId == feeTypeId
                     && k.EndDate == null,
                cancellationToken))
            .OrderByDescending(k => k.StartDate)
            .ThenByDescending(k => k.CreatedAt)
            .FirstOrDefault();

        var shares = new List<(Guid DestinationId, decimal Percentage)>();
        if (openKey is not null)
        {
            var details = (await _allocationKeyDetailRepository.FindAsync(
                    d => d.AllocationKeyId == openKey.Id,
                    cancellationToken))
                .OrderBy(d => d.SortOrder)
                .ToList();
            shares.AddRange(details.Select(d => (d.DestinationId, d.Value)));
        }

        if (shares.Count == 0)
        {
            var principal = destinations.Values.FirstOrDefault(d =>
                string.Equals(d.Code, "PRN", StringComparison.OrdinalIgnoreCase) && d.IsActive)
                ?? destinations.Values.FirstOrDefault(d => d.IsActive);
            if (principal is null)
            {
                return [];
            }

            shares.Add((principal.Id, 100m));
        }

        // Attendu par compte via pourcentages (dernier compte = reste pour éviter l'écart d'arrondi).
        var expectedByDest = new Dictionary<Guid, decimal>();
        decimal allocatedExpected = 0;
        for (var i = 0; i < shares.Count; i++)
        {
            var (destId, pct) = shares[i];
            decimal amount;
            if (i == shares.Count - 1)
            {
                amount = Math.Round(totalExpected - allocatedExpected, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                amount = Math.Round(totalExpected * pct / 100m, 2, MidpointRounding.AwayFromZero);
                allocatedExpected += amount;
            }

            expectedByDest[destId] = amount;
        }

        // Encaissé réel déjà réparti (hors retenues) sur ce frais / année.
        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var yearPaymentIds = payments
            .Where(p => p.AcademicYearId == academicYearId)
            .Select(p => p.Id)
            .ToHashSet();
        var collectedByDest = (await _allocationEntryRepository.FindAsync(
                e => e.SchoolId == schoolId
                     && e.FeeTypeId == feeTypeId
                     && e.WithholdingTypeId == null
                     && e.AcademicYearId == academicYearId,
                cancellationToken))
            .Where(e => yearPaymentIds.Contains(e.PaymentId))
            .GroupBy(e => e.DestinationId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        // Si aucun encaissement écrit, projeter le payé global via les mêmes %.
        var hasCollectedEntries = collectedByDest.Count > 0 && collectedByDest.Values.Sum() > 0;
        if (!hasCollectedEntries && totalPaid > 0)
        {
            decimal allocatedPaid = 0;
            for (var i = 0; i < shares.Count; i++)
            {
                var (destId, pct) = shares[i];
                decimal amount;
                if (i == shares.Count - 1)
                {
                    amount = Math.Round(totalPaid - allocatedPaid, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    amount = Math.Round(totalPaid * pct / 100m, 2, MidpointRounding.AwayFromZero);
                    allocatedPaid += amount;
                }

                collectedByDest[destId] = amount;
            }
        }

        return shares
            .Select(s =>
            {
                destinations.TryGetValue(s.DestinationId, out var dest);
                var expected = expectedByDest.GetValueOrDefault(s.DestinationId);
                var collected = collectedByDest.GetValueOrDefault(s.DestinationId);
                return new FeeDestinationReceivableDto(
                    s.DestinationId,
                    dest?.Code ?? "—",
                    dest?.Name ?? "Compte",
                    s.Percentage,
                    expected,
                    collected,
                    Math.Max(0m, expected - collected));
            })
            .OrderByDescending(x => x.AmountExpected)
            .ThenBy(x => x.DestinationName)
            .ToList();
    }

    public async Task<IReadOnlyList<DashboardFundMovementDto>> GetFundMovementsAsync(
        Guid schoolId,
        Guid destinationId,
        CancellationToken cancellationToken = default)
    {
        var destinations = await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken);
        var destName = destinations.FirstOrDefault(d => d.Id == destinationId)?.Name ?? "Compte";
        var entries = await _allocationEntryRepository.FindAsync(
            e => e.SchoolId == schoolId && e.DestinationId == destinationId,
            cancellationToken);

        return entries
            .OrderByDescending(e => e.AllocatedAt)
            .Take(200)
            .Select(e => new DashboardFundMovementDto(
                e.Id,
                DateTime.SpecifyKind(e.AllocatedAt, DateTimeKind.Utc),
                destName,
                e.Amount,
                AppConstants.DefaultCurrency,
                null))
            .ToList();
    }

    public async Task<EnrolledStudentsBySectionDto> GetEnrolledStudentsBySectionAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var currentYear = (await _academicYearRepository.FindAsync(
                y => y.SchoolId == schoolId,
                cancellationToken))
            .OrderByDescending(y => y.StartDate)
            .ToList();
        var todayDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var operationalYear = ResolveOperationalAcademicYear(currentYear, todayDate);

        var students = (await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken))
            .ToDictionary(s => s.Id);
        var enrollments = await LoadActiveYearEnrollmentsAsync(
            schoolId,
            operationalYear?.Id,
            students.Keys.ToHashSet(),
            cancellationToken);

        var classRooms = (await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken))
            .ToDictionary(c => c.Id);
        var sectionIds = classRooms.Values.Select(c => c.SectionId).Distinct().ToList();
        var sections = (await _sectionRepository.FindAsync(s => sectionIds.Contains(s.Id), cancellationToken))
            .ToDictionary(s => s.Id);
        var pedIds = classRooms.Values
            .Where(c => c.PedagogicalClassId.HasValue)
            .Select(c => c.PedagogicalClassId!.Value)
            .Distinct()
            .ToList();
        var pedagogical = pedIds.Count == 0
            ? new Dictionary<Guid, PedagogicalClass>()
            : (await _pedagogicalClassRepository.FindAsync(p => pedIds.Contains(p.Id), cancellationToken))
                .ToDictionary(p => p.Id);

        string ResolveClassName(ClassRoom room)
        {
            if (room.PedagogicalClassId is Guid pedId && pedagogical.TryGetValue(pedId, out var ped))
            {
                return string.IsNullOrWhiteSpace(room.Name)
                    ? ped.DisplayName
                    : $"{ped.DisplayName} {room.Name}".Trim();
            }

            return string.IsNullOrWhiteSpace(room.Name) ? "—" : room.Name;
        }

        var rows = new List<(Guid SectionId, string SectionName, Guid ClassRoomId, string ClassName, Gender Gender)>();
        foreach (var enrollment in enrollments)
        {
            if (!students.TryGetValue(enrollment.StudentId, out var student))
            {
                continue;
            }

            if (!classRooms.TryGetValue(enrollment.ClassRoomId, out var room))
            {
                continue;
            }

            sections.TryGetValue(room.SectionId, out var section);
            rows.Add((
                room.SectionId,
                string.IsNullOrWhiteSpace(section?.Name) ? "Sans section" : section!.Name,
                room.Id,
                ResolveClassName(room),
                student.Gender));
        }

        var sectionGroups = rows
            .GroupBy(r => new { r.SectionId, r.SectionName })
            .Select(sectionGroup =>
            {
                var classRows = sectionGroup
                    .GroupBy(r => new { r.ClassRoomId, r.ClassName })
                    .Select(classGroup =>
                    {
                        var boys = classGroup.Count(x => x.Gender == Gender.Masculin);
                        var girls = classGroup.Count(x => x.Gender == Gender.Feminin);
                        return new EnrolledClassRowDto(
                            classGroup.Key.ClassRoomId,
                            classGroup.Key.ClassName,
                            boys + girls,
                            boys,
                            girls);
                    })
                    .OrderBy(c => c.ClassName)
                    .ToList();

                var sectionBoys = classRows.Sum(c => c.Boys);
                var sectionGirls = classRows.Sum(c => c.Girls);
                return new EnrolledSectionGroupDto(
                    sectionGroup.Key.SectionId,
                    sectionGroup.Key.SectionName,
                    sectionBoys + sectionGirls,
                    sectionBoys,
                    sectionGirls,
                    classRows);
            })
            .OrderBy(s => s.SectionName)
            .ToList();

        var totalBoys = sectionGroups.Sum(s => s.Boys);
        var totalGirls = sectionGroups.Sum(s => s.Girls);
        return new EnrolledStudentsBySectionDto(
            totalBoys + totalGirls,
            totalBoys,
            totalGirls,
            sectionGroups);
    }

    private async Task<IReadOnlyList<DashboardAlertDto>> BuildAlertsAsync(
        Guid schoolId,
        decimal dayRevenue,
        decimal yesterdayRevenue,
        decimal expenseMonth,
        decimal monthRevenue,
        int debtors,
        int enrolled,
        decimal recovery,
        CancellationToken cancellationToken)
    {
        var alerts = new List<DashboardAlertDto>();

        if (debtors > Math.Max(10, enrolled / 5))
        {
            alerts.Add(new DashboardAlertDto(
                "danger",
                "MANY_DEBTORS",
                "Élèves débiteurs",
                $"{debtors} élève(s) ont encore un solde à payer.",
                "debtors"));
        }
        else if (debtors > 0)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "LATE_PAYMENTS",
                "Retards de paiement",
                $"{debtors} élève(s) en retard de paiement.",
                "debtors"));
        }

        if (recovery < 50)
        {
            alerts.Add(new DashboardAlertDto(
                "danger",
                "LOW_RECOVERY",
                "Faible recouvrement",
                $"Taux de recouvrement à {recovery:0.#} %.",
                "receivables"));
        }

        if (yesterdayRevenue > 0 && dayRevenue < yesterdayRevenue * 0.4m)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "LOW_DAILY_REVENUE",
                "Faible recette journalière",
                "Les encaissements du jour sont nettement inférieurs à hier.",
                "payments_today"));
        }

        if (monthRevenue > 0 && expenseMonth > monthRevenue * 0.85m)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "HIGH_EXPENSES",
                "Dépenses élevées",
                "Les dépenses du mois approchent ou dépassent les recettes.",
                "expenses_month"));
        }

        var avgDaily = yesterdayRevenue;
        if (avgDaily > 0)
        {
            var expenses = await LoadExpensesAsync(schoolId, cancellationToken);
            var todayStart = DateTime.UtcNow.Date;
            var expenseToday = SumExpenses(expenses, todayStart, todayStart.AddDays(1));
            if (expenseToday > avgDaily * 2)
            {
                alerts.Add(new DashboardAlertDto(
                    "danger",
                    "UNUSUAL_EXPENSE",
                    "Dépenses inhabituelles",
                    "Les dépenses du jour sont anormalement élevées.",
                    "expenses_today"));
            }
        }

        if (alerts.Count == 0)
        {
            alerts.Add(new DashboardAlertDto(
                "success",
                "ALL_GOOD",
                "Situation saine",
                "Aucune alerte critique pour le moment.",
                null));
        }

        return alerts;
    }

    private sealed record FeeReceivableRow(
        Guid StudentId,
        string StudentName,
        string ClassName,
        decimal AmountDue,
        decimal AmountPaid,
        decimal Remaining);

    private async Task<List<Enrollment>> LoadActiveYearEnrollmentsAsync(
        Guid schoolId,
        Guid? academicYearId,
        HashSet<Guid> schoolStudentIds,
        CancellationToken cancellationToken)
    {
        var enrollments = await SchoolScopedEnrollmentQueries.GetActiveForStudentsAsync(
            _enrollmentRepository, schoolStudentIds, cancellationToken);
        IEnumerable<Enrollment> query = enrollments;
        if (academicYearId is Guid yearId)
        {
            query = query.Where(e => e.AcademicYearId == yearId);
        }

        return query
            .Where(e => e.Status is EnrollmentStatus.Inscrit
                or EnrollmentStatus.Reinscrit
                or EnrollmentStatus.PreInscription)
            .GroupBy(e => e.StudentId)
            .Select(g => g.OrderByDescending(x => x.EnrollmentDate).First())
            .ToList();
    }

    private static PromoterReceivablesDto AggregateReceivables(
        IReadOnlyList<FeeReceivableRow> annualRows,
        IReadOnlyList<FeeReceivableRow> overdueRows)
    {
        if (annualRows.Count == 0 && overdueRows.Count == 0)
        {
            return new PromoterReceivablesDto(0, 0, 0, 0);
        }

        // À percevoir / Débiteurs = retards d'échéance uniquement.
        var remaining = overdueRows.Sum(r => r.Remaining);
        var debtors = overdueRows.Count(r => r.Remaining > 0);
        // En ordre / Recouvrement = situation annuelle du frais suivi.
        var fullyPaid = annualRows.Count(r => r.AmountDue > 0 && r.Remaining <= 0);
        var expected = annualRows.Sum(r => r.AmountDue);
        var collected = annualRows.Sum(r => Math.Min(r.AmountPaid, r.AmountDue));
        var recovery = expected <= 0 ? 0 : Math.Round(collected / expected * 100m, 1);
        return new PromoterReceivablesDto(remaining, debtors, fullyPaid, recovery);
    }

    /// <summary>
    /// Créances alignées sur les encaissements Desktop :
    /// pour chaque inscrit année courante, Attendu = tarif (classe × catégorie × frais),
    /// Payé = soldes de ces mêmes lignes tarifaires, Reste = max(0, Attendu − Payé).
    /// Inclut les élèves sans solde provisionné.
    /// </summary>
    private async Task<List<FeeReceivableRow>> BuildFeeReceivableRowsAsync(
        Guid schoolId,
        Guid? selectedFeeId,
        Guid? currentYearId,
        IReadOnlyList<Enrollment> enrollments,
        IReadOnlyDictionary<Guid, Student> students,
        CancellationToken cancellationToken)
    {
        if (enrollments.Count == 0 || selectedFeeId is null || currentYearId is null)
        {
            return [];
        }

        var classRooms = (await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken))
            .ToDictionary(c => c.Id);
        var yearTariffs = (await _classFeeAmountRepository.FindAsync(
                a => a.SchoolId == schoolId
                     && a.AcademicYearId == currentYearId.Value
                     && a.FeeTypeId == selectedFeeId.Value,
                cancellationToken))
            .ToList();

        var tariffTotals = yearTariffs
            .GroupBy(a => (a.PedagogicalClassId, a.FeePricingCategoryId))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

        var tariffsByClassCategory = yearTariffs
            .GroupBy(a => (a.PedagogicalClassId, a.FeePricingCategoryId))
            .ToDictionary(g => g.Key, g => g.Select(a => a.Id).ToHashSet());

        var allTariffIds = yearTariffs.Select(a => a.Id).ToHashSet();
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var balances = allTariffIds.Count == 0
            ? []
            : (await _balanceRepository.FindAsync(
                    b => studentIds.Contains(b.StudentId) && allTariffIds.Contains(b.ClassFeeAmountId),
                    cancellationToken))
                .ToList();
        var balancesByStudent = balances
            .GroupBy(b => b.StudentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<FeeReceivableRow>(enrollments.Count);
        foreach (var enrollment in enrollments)
        {
            if (!students.TryGetValue(enrollment.StudentId, out var student))
            {
                continue;
            }

            classRooms.TryGetValue(enrollment.ClassRoomId, out var room);
            var pedClassId = room?.PedagogicalClassId;
            var className = string.IsNullOrWhiteSpace(room?.Name) ? "—" : room!.Name;
            var studentName = StudentDisplayName.Format(student);

            decimal amountExpected = 0;
            HashSet<Guid>? matchingTariffIds = null;
            if (pedClassId is Guid pedId
                && tariffTotals.TryGetValue((pedId, enrollment.FeePricingCategoryId), out var tariffAmount))
            {
                amountExpected = tariffAmount;
                tariffsByClassCategory.TryGetValue((pedId, enrollment.FeePricingCategoryId), out matchingTariffIds);
            }

            balancesByStudent.TryGetValue(student.Id, out var studentBalances);
            studentBalances ??= [];

            // Payé uniquement sur les lignes tarifaires de la classe × catégorie de l'élève.
            var relevantBalances = matchingTariffIds is null
                ? studentBalances
                : studentBalances.Where(b => matchingTariffIds.Contains(b.ClassFeeAmountId)).ToList();

            var amountPaid = relevantBalances.Sum(b => b.AmountPaid);
            if (amountExpected <= 0)
            {
                // Fallback : attendu figé sur soldes si aucun tarif configuré pour cette classe/catégorie.
                amountExpected = relevantBalances.Sum(b => b.AmountDue);
            }

            var remaining = Math.Max(0m, amountExpected - amountPaid);
            rows.Add(new FeeReceivableRow(
                student.Id,
                studentName,
                className,
                amountExpected,
                amountPaid,
                remaining));
        }

        return rows;
    }

    /// <summary>
    /// Débiteurs promoteur : uniquement les tranches du frais suivi dont DueDate &lt; aujourd'hui
    /// et montant restant &gt; 0. Le montant à payer = somme des restes de ces tranches.
    /// </summary>
    private async Task<List<FeeReceivableRow>> BuildOverdueReceivableRowsAsync(
        Guid schoolId,
        Guid? selectedFeeId,
        Guid? currentYearId,
        IReadOnlyList<Enrollment> enrollments,
        IReadOnlyDictionary<Guid, Student> students,
        CancellationToken cancellationToken)
    {
        if (enrollments.Count == 0 || selectedFeeId is null || currentYearId is null)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var classRooms = (await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken))
            .ToDictionary(c => c.Id);
        var yearTariffs = (await _classFeeAmountRepository.FindAsync(
                a => a.SchoolId == schoolId
                     && a.AcademicYearId == currentYearId.Value
                     && a.FeeTypeId == selectedFeeId.Value
                     && a.DueDate != null
                     && a.DueDate < today,
                cancellationToken))
            .ToList();

        if (yearTariffs.Count == 0)
        {
            return [];
        }

        var tariffsByClassCategory = yearTariffs
            .GroupBy(a => (a.PedagogicalClassId, a.FeePricingCategoryId))
            .ToDictionary(g => g.Key, g => g.ToList());

        var allTariffIds = yearTariffs.Select(a => a.Id).ToHashSet();
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var balances = (await _balanceRepository.FindAsync(
                b => studentIds.Contains(b.StudentId) && allTariffIds.Contains(b.ClassFeeAmountId),
                cancellationToken))
            .ToList();
        var paidByTariffStudent = balances
            .GroupBy(b => (b.StudentId, b.ClassFeeAmountId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPaid));
        var dueByTariffStudent = balances
            .GroupBy(b => (b.StudentId, b.ClassFeeAmountId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountDue));

        var rows = new List<FeeReceivableRow>();
        foreach (var enrollment in enrollments)
        {
            if (!students.TryGetValue(enrollment.StudentId, out var student))
            {
                continue;
            }

            if (!classRooms.TryGetValue(enrollment.ClassRoomId, out var room)
                || room.PedagogicalClassId is not Guid pedId)
            {
                continue;
            }

            if (!tariffsByClassCategory.TryGetValue((pedId, enrollment.FeePricingCategoryId), out var overdueTariffs))
            {
                continue;
            }

            decimal amountDue = 0;
            decimal amountPaid = 0;
            foreach (var tariff in overdueTariffs)
            {
                var expected = tariff.Amount > 0
                    ? tariff.Amount
                    : dueByTariffStudent.GetValueOrDefault((enrollment.StudentId, tariff.Id));
                if (expected <= 0)
                {
                    continue;
                }

                var paid = paidByTariffStudent.GetValueOrDefault((enrollment.StudentId, tariff.Id));
                var remainingOnInstallment = Math.Max(0m, expected - paid);
                if (remainingOnInstallment <= 0)
                {
                    continue;
                }

                amountDue += expected;
                amountPaid += Math.Min(paid, expected);
            }

            var remaining = Math.Max(0m, amountDue - amountPaid);
            if (remaining <= 0)
            {
                continue;
            }

            rows.Add(new FeeReceivableRow(
                student.Id,
                StudentDisplayName.Format(student),
                string.IsNullOrWhiteSpace(room.Name) ? "—" : room.Name,
                amountDue,
                amountPaid,
                remaining));
        }

        return rows;
    }

    private async Task<IReadOnlyList<FundAllocationShareDto>> BuildFeeFundCashFlowAsync(
        Guid schoolId,
        Guid? selectedFeeId,
        DateTime todayStart,
        DateTime yearStart,
        CancellationToken cancellationToken)
    {
        if (selectedFeeId is null)
        {
            return [];
        }

        var todayEnd = todayStart.AddDays(1);
        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var paymentDateById = payments.ToDictionary(p => p.Id, p => p.PaymentDate.Date);

        var entries = (await _allocationEntryRepository.FindAsync(
                e => e.SchoolId == schoolId
                     && e.FeeTypeId == selectedFeeId.Value
                     && e.WithholdingTypeId == null,
                cancellationToken))
            .Where(e => paymentDateById.ContainsKey(e.PaymentId))
            .ToList();

        var destinations = (await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);

        // Comptes configurés sur la clé ouverte du frais (année courante) + comptes déjà alimentés.
        var destinationIds = new HashSet<Guid>();
        var currentYear = (await _academicYearRepository.FindAsync(
                y => y.SchoolId == schoolId && y.IsCurrent,
                cancellationToken))
            .FirstOrDefault();
        if (currentYear is not null)
        {
            var openKey = (await _allocationKeyRepository.FindAsync(
                    k => k.SchoolId == schoolId
                         && k.AcademicYearId == currentYear.Id
                         && k.FeeTypeId == selectedFeeId.Value
                         && k.EndDate == null,
                    cancellationToken))
                .OrderByDescending(k => k.StartDate)
                .ThenByDescending(k => k.CreatedAt)
                .FirstOrDefault();
            if (openKey is not null)
            {
                var details = await _allocationKeyDetailRepository.FindAsync(
                    d => d.AllocationKeyId == openKey.Id,
                    cancellationToken);
                foreach (var d in details)
                {
                    destinationIds.Add(d.DestinationId);
                }
            }
        }

        foreach (var id in entries.Select(e => e.DestinationId))
        {
            destinationIds.Add(id);
        }

        if (destinationIds.Count == 0)
        {
            return [];
        }

        var expenses = (await LoadExpensesAsync(schoolId, cancellationToken))
            .Where(e => destinationIds.Contains(e.DestinationId))
            .ToList();

        decimal SumEnc(IEnumerable<RevenueAllocationEntry> source, DateTime? fromInclusive, DateTime? toExclusive)
        {
            return source
                .Where(e =>
                {
                    if (!paymentDateById.TryGetValue(e.PaymentId, out var date))
                    {
                        return false;
                    }

                    if (fromInclusive.HasValue && date < fromInclusive.Value)
                    {
                        return false;
                    }

                    if (toExclusive.HasValue && date >= toExclusive.Value)
                    {
                        return false;
                    }

                    return true;
                })
                .Sum(e => e.Amount);
        }

        decimal SumDep(IEnumerable<ExpensePayment> source, DateTime? fromInclusive, DateTime? toExclusive)
        {
            return source
                .Where(e =>
                {
                    var date = e.ExpenseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    if (fromInclusive.HasValue && date < fromInclusive.Value)
                    {
                        return false;
                    }

                    if (toExclusive.HasValue && date >= toExclusive.Value)
                    {
                        return false;
                    }

                    return true;
                })
                .Sum(e => e.Amount);
        }

        var rows = destinationIds
            .Select((id, i) =>
            {
                destinations.TryGetValue(id, out var dest);
                var destEntries = entries.Where(e => e.DestinationId == id);
                var destExpenses = expenses.Where(e => e.DestinationId == id);
                // J-1 : solde depuis le début d'année scolaire jusqu'à hier.
                var j1Enc = SumEnc(destEntries, yearStart, todayStart);
                var j1Dep = SumDep(destExpenses, yearStart, todayStart);
                var periodJ1 = j1Enc - j1Dep;
                var encJ = SumEnc(destEntries, todayStart, todayEnd);
                var depJ = SumDep(destExpenses, todayStart, todayEnd);
                var solde = periodJ1 + encJ - depJ;
                return new FundAllocationShareDto(
                    id,
                    dest?.Code ?? "—",
                    dest?.Name ?? "Compte",
                    periodJ1,
                    encJ,
                    depJ,
                    solde,
                    0,
                    Palette[i % Palette.Length]);
            })
            .Where(r =>
                r.PeriodJ1 != 0
                || r.EncaissementJ != 0
                || r.DepenseJ != 0
                || destinations.GetValueOrDefault(r.DestinationId)?.IsActive == true)
            .OrderByDescending(r => r.EncaissementJ)
            .ThenByDescending(r => r.Solde)
            .ThenBy(r => r.Name)
            .ToList();

        var totalEncJ = rows.Sum(r => r.EncaissementJ);
        return rows
            .Select((r, i) => r with
            {
                Percentage = totalEncJ <= 0 ? 0 : Math.Round(r.EncaissementJ / totalEncJ * 100m, 1),
                ColorHex = Palette[i % Palette.Length]
            })
            .ToList();
    }

    private async Task<IReadOnlyList<PromoterWithholdingShareDto>> BuildWithholdingsForFeeAsync(
        Guid schoolId,
        Guid? selectedFeeId,
        DateTime todayStart,
        DateTime monthStart,
        DateTime monthEnd,
        DateTime yearStart,
        DateTime yearEnd,
        CancellationToken cancellationToken)
    {
        if (selectedFeeId is null)
        {
            return [];
        }

        var todayEnd = todayStart.AddDays(1);
        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var paymentDateById = payments.ToDictionary(p => p.Id, p => p.PaymentDate.Date);

        var entries = (await _allocationEntryRepository.FindAsync(
                e => e.SchoolId == schoolId
                     && e.FeeTypeId == selectedFeeId.Value
                     && e.WithholdingTypeId != null,
                cancellationToken))
            .Where(e => paymentDateById.ContainsKey(e.PaymentId) && e.WithholdingTypeId.HasValue)
            .ToList();

        if (entries.Count == 0)
        {
            return [];
        }

        var types = (await _withholdingTypeRepository.FindAsync(t => t.SchoolId == schoolId, cancellationToken))
            .ToDictionary(t => t.Id);

        bool InRange(Guid paymentId, DateTime start, DateTime end) =>
            paymentDateById.TryGetValue(paymentId, out var d) && d >= start && d < end;

        return entries
            .GroupBy(e => e.WithholdingTypeId!.Value)
            .Select(g =>
            {
                types.TryGetValue(g.Key, out var type);
                return new PromoterWithholdingShareDto(
                    g.Key,
                    type?.Name ?? "Retenue",
                    g.Where(e => InRange(e.PaymentId, todayStart, todayEnd)).Sum(e => e.Amount),
                    g.Where(e => InRange(e.PaymentId, monthStart, monthEnd)).Sum(e => e.Amount),
                    g.Where(e => InRange(e.PaymentId, yearStart, yearEnd)).Sum(e => e.Amount));
            })
            .Where(x => x.AmountToday > 0 || x.AmountMonth > 0 || x.AmountYear > 0)
            .OrderByDescending(x => x.AmountYear)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<IReadOnlyList<NamedAmountShareDto>> BuildExpenseCategoriesAsync(
        Guid schoolId,
        List<ExpensePayment> expenses,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        _ = schoolId;
        _ = start;
        _ = end;
        _ = cancellationToken;

        // La liste reçue est déjà filtrée (année opérationnelle / plage).
        var groups = expenses
            .GroupBy(e => FormatExpenseCategory(e.Category) ?? "Autre")
            .Select(g => (
                Name: g.Key,
                Amount: g.Sum(x => x.Amount)))
            .Where(x => x.Amount > 0)
            .OrderByDescending(x => x.Amount)
            .ToList();

        var total = groups.Sum(x => x.Amount);
        return groups
            .Select((g, i) => new NamedAmountShareDto(
                g.Name,
                g.Amount,
                total <= 0 ? 0 : Math.Round(g.Amount / total * 100m, 1),
                Palette[i % Palette.Length]))
            .ToList();
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

    private async Task<IReadOnlyList<ClassRevenueRankDto>> GetTopClassesAsync(
        Guid schoolId,
        DashboardPeriod period,
        CancellationToken cancellationToken)
    {
        var (rangeStart, rangeEnd) = ResolveRange(period);
        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var inRange = payments.Where(p => p.PaymentDate >= rangeStart && p.PaymentDate < rangeEnd).ToList();
        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var studentIds = students.Select(s => s.Id).ToHashSet();
        var enrollments = await SchoolScopedEnrollmentQueries.GetActiveForStudentsAsync(
            _enrollmentRepository, studentIds, cancellationToken);
        var classes = await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        var classMap = classes.ToDictionary(c => c.Id);

        var studentClass = enrollments
            .GroupBy(e => e.StudentId)
            .ToDictionary(g => g.Key, g => g.First().ClassRoomId);

        return inRange
            .Where(p => studentClass.ContainsKey(p.StudentId))
            .GroupBy(p => studentClass[p.StudentId])
            .Select(g =>
            {
                var cr = classMap.GetValueOrDefault(g.Key);
                return (Name: cr?.Name ?? "Classe", Amount: g.Sum(x => x.TotalAmount));
            })
            .OrderByDescending(x => x.Amount)
            .Take(5)
            .Select((x, i) => new ClassRevenueRankDto(i + 1, x.Name, x.Amount))
            .ToList();
    }

    private async Task<PromoterQuickStatsDto> GetQuickStatsAsync(
        Guid schoolId,
        DashboardPeriod period,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        var attendance = await _attendanceRepository.FindAsync(
            a => a.SchoolId == schoolId && a.AttendanceDate == today,
            cancellationToken);
        var present = attendance.Count(a => a.IsPresent);
        var absent = attendance.Count(a => !a.IsPresent);

        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var paymentsToday = payments.Count(p => p.PaymentDate >= todayStart && p.PaymentDate < todayEnd);

        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var studentIds = students.Select(s => s.Id).ToHashSet();
        var balances = await _balanceRepository.FindAsync(b => studentIds.Contains(b.StudentId), cancellationToken);
        var remaining = balances.Sum(b => Math.Max(0, b.AmountDue - b.AmountPaid));

        var (rangeStart, rangeEnd) = ResolveRange(period);
        var allocations = await _allocationEntryRepository.FindAsync(
            e => e.SchoolId == schoolId && e.AllocatedAt >= rangeStart && e.AllocatedAt < rangeEnd,
            cancellationToken);

        return new PromoterQuickStatsDto(
            present,
            absent,
            paymentsToday,
            paymentsToday,
            remaining,
            allocations.Sum(a => a.Amount));
    }

    private async Task<(DateTime Start, DateTime End)> ResolveSchoolYearRangeAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        var years = (await _academicYearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken))
            .OrderByDescending(y => y.StartDate)
            .ToList();
        if (years.Count == 0)
        {
            return ResolveRange(DashboardPeriod.Year);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var year = ResolveOperationalAcademicYear(years, today) ?? years.First();

        var start = year.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = year.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
        return (start, end);
    }

    /// <summary>
    /// Année scolaire opérationnelle : IsCurrent déjà démarrée, sinon année en cours de dates,
    /// sinon dernière année terminée (évite KPI inscrits à 0 sur une année future).
    /// </summary>
    private static AcademicYear? ResolveOperationalAcademicYear(
        IReadOnlyList<AcademicYear> years,
        DateOnly today)
    {
        if (years.Count == 0)
        {
            return null;
        }

        var currentStarted = years.FirstOrDefault(y => y.IsCurrent && today >= y.StartDate);
        if (currentStarted is not null)
        {
            return currentStarted;
        }

        return years.FirstOrDefault(y => today >= y.StartDate && today <= y.EndDate)
               ?? years.Where(y => y.EndDate < today).OrderByDescending(y => y.EndDate).FirstOrDefault()
               // Ne jamais basculer sur une année IsCurrent pas encore démarrée (KPI à 0).
               ?? years.FirstOrDefault(y => y.IsCurrent && today >= y.StartDate)
               ?? years.FirstOrDefault();
    }

    private static (DateTime Start, DateTime End)? ResolveAcademicYearBounds(AcademicYear? year)
    {
        if (year is null)
        {
            return null;
        }

        var start = year.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = year.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
        return (start, end);
    }

    /// <summary>
    /// Recette année scolaire : AcademicYearId d'abord, plage de dates en secours.
    /// </summary>
    private static decimal SumFeeForAcademicYear(
        IReadOnlyList<Payment> payments,
        IReadOnlyDictionary<Guid, decimal> amountByPayment,
        Guid? academicYearId,
        DateTime yearStart,
        DateTime yearEnd)
    {
        if (academicYearId is Guid yearId)
        {
            var byYearId = payments
                .Where(p => p.AcademicYearId == yearId)
                .Sum(p => amountByPayment.GetValueOrDefault(p.Id));
            if (byYearId > 0)
            {
                return byYearId;
            }
        }

        return payments
            .Where(p =>
            {
                var d = DateTime.SpecifyKind(p.PaymentDate, DateTimeKind.Utc);
                return d >= yearStart && d < yearEnd;
            })
            .Sum(p => amountByPayment.GetValueOrDefault(p.Id));
    }

    private static List<Payment> FilterPaymentsForAcademicYear(
        IReadOnlyList<Payment> payments,
        IReadOnlyDictionary<Guid, decimal> amountByPayment,
        Guid? academicYearId,
        DateTime yearStart,
        DateTime yearEnd)
    {
        if (academicYearId is Guid yearId)
        {
            var byYearId = payments.Where(p => p.AcademicYearId == yearId).ToList();
            if (byYearId.Any(p => amountByPayment.GetValueOrDefault(p.Id) > 0))
            {
                return byYearId;
            }
        }

        return payments
            .Where(p =>
            {
                var d = DateTime.SpecifyKind(p.PaymentDate, DateTimeKind.Utc);
                return d >= yearStart && d < yearEnd;
            })
            .ToList();
    }

    private static (DateTime Start, DateTime End) ExpandRangeToPayments(
        DateTime start,
        DateTime end,
        IReadOnlyList<Payment> payments)
    {
        if (payments.Count == 0)
        {
            return (start, end);
        }

        var minPay = payments.Min(p => p.PaymentDate).Date;
        var maxPayExclusive = payments.Max(p => p.PaymentDate).Date.AddDays(1);
        var seriesStart = minPay < start.Date
            ? DateTime.SpecifyKind(minPay, DateTimeKind.Utc)
            : start;
        var seriesEnd = maxPayExclusive > end.Date
            ? DateTime.SpecifyKind(maxPayExclusive, DateTimeKind.Utc)
            : end;
        return (seriesStart, seriesEnd);
    }

    private async Task<(DateTime Start, DateTime End)> ResolvePreviousSchoolYearRangeAsync(
        Guid schoolId,
        DateTime currentYearStart,
        CancellationToken cancellationToken)
    {
        var years = (await _academicYearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken))
            .Where(y => y.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) <= currentYearStart)
            .OrderByDescending(y => y.EndDate)
            .ToList();
        var previous = years.FirstOrDefault();
        if (previous is null)
        {
            return (currentYearStart.AddYears(-1), currentYearStart);
        }

        var start = previous.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = previous.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);
        return (start, end);
    }

    private async Task<(DateTime Start, DateTime End)> ResolveDetailRangeAsync(
        Guid schoolId,
        DashboardDetailScope scope,
        CancellationToken cancellationToken) =>
        scope switch
        {
            DashboardDetailScope.Today => ResolveRange(DashboardPeriod.Today),
            DashboardDetailScope.Month => ResolveRange(DashboardPeriod.Month),
            _ => await ResolveSchoolYearRangeAsync(schoolId, cancellationToken)
        };

    private async Task<List<Payment>> LoadValidatedPaymentsAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var payments = await _paymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        return payments.Where(p => p.Status == PaymentStatus.Complet).ToList();
    }

    private async Task<List<ExpensePayment>> LoadExpensesAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var items = await _expensePaymentRepository.FindAsync(e => e.SchoolId == schoolId, cancellationToken);
        return items.ToList();
    }

    private static decimal SumPayments(IEnumerable<Payment> payments, DateTime start, DateTime end) =>
        payments.Where(p => p.PaymentDate >= start && p.PaymentDate < end).Sum(p => p.TotalAmount);

    private static decimal SumExpenses(IEnumerable<ExpensePayment> expenses, DateTime start, DateTime end) =>
        expenses
            .Where(e =>
            {
                var dt = e.ExpenseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                return dt >= start && dt < end;
            })
            .Sum(e => e.Amount);

    private static decimal PercentChange(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return current == 0 ? 0 : 100;
        }

        return Math.Round((current - previous) / previous * 100m, 1);
    }

    private static (DateTime Start, DateTime End) ResolveRange(DashboardPeriod period)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        return period switch
        {
            DashboardPeriod.Today => (today, today.AddDays(1)),
            DashboardPeriod.Week => (
                today.AddDays(-((7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7)),
                today.AddDays(1)),
            DashboardPeriod.Year => (new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), today.AddDays(1)),
            _ => (new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc), today.AddDays(1))
        };
    }

    private static (DateTime Start, DateTime End) ResolvePreviousRange(DashboardPeriod period)
    {
        var (start, end) = ResolveRange(period);
        var length = end - start;
        return period switch
        {
            DashboardPeriod.Month => (start.AddMonths(-1), start),
            DashboardPeriod.Year => (start.AddYears(-1), start),
            _ => (start - length, start)
        };
    }

    private static List<RevenuePointDto> BuildDailySeries(List<Payment> payments, DateTime start, DateTime end)
    {
        var points = new List<RevenuePointDto>();
        for (var d = start.Date; d < end; d = d.AddDays(1))
        {
            var amount = payments.Where(p => p.PaymentDate.Date == d).Sum(p => p.TotalAmount);
            points.Add(new RevenuePointDto(d.ToString("dd/MM"), DateTime.SpecifyKind(d, DateTimeKind.Utc), amount));
        }

        return points;
    }

    private static List<RevenuePointDto> BuildWeeklySeries(List<Payment> payments, DateTime start, DateTime end)
    {
        var points = new List<RevenuePointDto>();
        var cursor = start.Date;
        var week = 1;
        while (cursor < end)
        {
            var weekEnd = cursor.AddDays(7);
            if (weekEnd > end) weekEnd = end;
            var amount = payments.Where(p => p.PaymentDate >= cursor && p.PaymentDate < weekEnd).Sum(p => p.TotalAmount);
            points.Add(new RevenuePointDto($"S{week}", DateTime.SpecifyKind(cursor, DateTimeKind.Utc), amount));
            cursor = weekEnd;
            week++;
        }

        return points;
    }

    private static List<RevenuePointDto> BuildMonthlySeries(List<Payment> payments, DateTime start, DateTime end)
    {
        var points = new List<RevenuePointDto>();
        var cursor = new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        while (cursor < end)
        {
            var monthEnd = cursor.AddMonths(1);
            var amount = payments.Where(p => p.PaymentDate >= cursor && p.PaymentDate < monthEnd).Sum(p => p.TotalAmount);
            points.Add(new RevenuePointDto(cursor.ToString("MMM yy"), cursor, amount));
            cursor = monthEnd;
        }

        return points;
    }
}
