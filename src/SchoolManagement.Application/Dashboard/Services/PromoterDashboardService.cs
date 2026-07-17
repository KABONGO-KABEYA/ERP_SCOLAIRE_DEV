namespace SchoolManagement.Application.Dashboard.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Dashboard.DTOs;
using SchoolManagement.Application.Dashboard.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Constants;

public sealed class PromoterDashboardService : IPromoterDashboardService
{
    private static readonly string[] Palette =
    [
        "#1D4ED8", "#0B1F47", "#22C55E", "#F59E0B", "#EF4444",
        "#8B5CF6", "#06B6D4", "#EC4899", "#84CC16", "#64748B"
    ];

    private readonly IRepository<School> _schoolRepository;
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

    public PromoterDashboardService(
        IRepository<School> schoolRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<Student> studentRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<RevenueAllocationEntry> allocationEntryRepository,
        IRepository<RevenueAllocationDestination> destinationRepository,
        IRepository<StudentAttendance> attendanceRepository)
    {
        _schoolRepository = schoolRepository;
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
    }

    public async Task<PromoterDashboardOverviewDto> GetOverviewAsync(
        Guid schoolId,
        DashboardPeriod period = DashboardPeriod.Month,
        RevenueGranularity granularity = RevenueGranularity.Daily,
        CancellationToken cancellationToken = default)
    {
        var school = (await _schoolRepository.FindAsync(s => s.Id == schoolId, cancellationToken)).FirstOrDefault();
        var schoolName = school?.Name ?? "Établissement";

        var summary = await GetSummaryAsync(schoolId, period, cancellationToken);
        var series = await GetRevenueSeriesAsync(schoolId, period, granularity, cancellationToken);
        var feeShares = await GetFeeTypeRepartitionAsync(schoolId, period, cancellationToken);
        var funds = await GetFundDistributionAsync(schoolId, period, cancellationToken);
        var activities = await GetActivitiesAsync(schoolId, 15, cancellationToken);
        var alerts = await GetAlertsAsync(schoolId, period, cancellationToken);
        var topClasses = await GetTopClassesAsync(schoolId, period, cancellationToken);
        var quick = await GetQuickStatsAsync(schoolId, period, cancellationToken);

        return new PromoterDashboardOverviewDto(
            schoolName,
            AppConstants.DefaultCurrency,
            period.ToString(),
            DateTime.UtcNow,
            summary,
            series,
            feeShares,
            funds,
            activities,
            alerts,
            topClasses,
            feeShares.Take(5).ToList(),
            quick);
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
        var periodRevenue = SumInRange(payments, rangeStart, rangeEnd);
        var prevRevenue = SumInRange(payments, prevStart, prevEnd);
        var monthRevenue = SumInRange(payments, monthStart, monthEnd);
        var prevMonthRevenue = SumInRange(payments, prevMonthStart, prevMonthEnd);
        var dayRevenue = SumInRange(payments, todayStart, todayEnd);
        var yesterdayRevenue = SumInRange(payments, todayStart.AddDays(-1), todayStart);

        var enrollments = await _enrollmentRepository.FindAsync(e => e.IsActive, cancellationToken);
        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var studentIds = students.Select(s => s.Id).ToHashSet();
        var schoolEnrollments = enrollments.Where(e => studentIds.Contains(e.StudentId)).ToList();
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
        CancellationToken cancellationToken = default)
    {
        var (rangeStart, rangeEnd) = ResolveRange(period);
        var entries = await _allocationEntryRepository.FindAsync(
            e => e.SchoolId == schoolId && e.AllocatedAt >= rangeStart && e.AllocatedAt < rangeEnd,
            cancellationToken);
        var destinations = await _destinationRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken);
        var destMap = destinations.ToDictionary(d => d.Id, d => d.Name);

        var groups = entries
            .GroupBy(e => e.DestinationId)
            .Select(g => (
                Id: g.Key,
                Name: destMap.GetValueOrDefault(g.Key, "Destination"),
                Amount: g.Sum(x => x.Amount)))
            .Where(x => x.Amount > 0)
            .OrderByDescending(x => x.Amount)
            .ToList();

        var total = groups.Sum(x => x.Amount);
        return groups
            .Select(g => new FundAllocationShareDto(
                g.Id,
                g.Name,
                g.Amount,
                total <= 0 ? 0 : Math.Round(g.Amount / total * 100m, 1)))
            .ToList();
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

        var enrollments = await _enrollmentRepository.FindAsync(e => e.IsActive, cancellationToken);
        var schoolEnrollments = enrollments
            .Where(e => studentIds.Contains(e.StudentId))
            .OrderByDescending(e => e.EnrollmentDate)
            .Take(take)
            .ToList();

        var paymentActivities = payments
            .OrderByDescending(p => p.PaymentDate)
            .Take(take)
            .Select(p =>
            {
                var student = studentMap.GetValueOrDefault(p.StudentId);
                var name = student is null ? "Élève" : $"{student.LastName} {student.FirstName}".Trim();
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
            var name = student is null ? "Élève" : $"{student.LastName} {student.FirstName}".Trim();
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
        var alerts = new List<DashboardAlertDto>();
        var summary = await GetSummaryAsync(schoolId, period, cancellationToken);

        if (summary.PeriodRevenueChangePercent < -10)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "REVENUE_DOWN",
                "Recettes en baisse sur la période sélectionnée."));
        }

        if (summary.RealizationRate >= 95)
        {
            alerts.Add(new DashboardAlertDto(
                "success",
                "TARGET_NEAR",
                $"Objectif de perception atteint à {summary.RealizationRate:0.#} %."));
        }
        else if (summary.RealizationRate < 50)
        {
            alerts.Add(new DashboardAlertDto(
                "danger",
                "TARGET_LOW",
                $"Taux de réalisation faible ({summary.RealizationRate:0.#} %)."));
        }

        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var studentIds = students.Select(s => s.Id).ToHashSet();
        var balances = await _balanceRepository.FindAsync(b => studentIds.Contains(b.StudentId), cancellationToken);
        var debtors = balances
            .GroupBy(b => b.StudentId)
            .Count(g => g.Sum(x => x.AmountDue - x.AmountPaid) > 0);

        if (debtors > 0)
        {
            alerts.Add(new DashboardAlertDto(
                "warning",
                "LATE_PAYMENTS",
                $"{debtors} élève(s) en retard de paiement."));
        }

        if (alerts.Count == 0)
        {
            alerts.Add(new DashboardAlertDto(
                "info",
                "ALL_GOOD",
                "Aucune alerte critique pour le moment."));
        }

        return alerts;
    }

    private async Task<IReadOnlyList<ClassRevenueRankDto>> GetTopClassesAsync(
        Guid schoolId,
        DashboardPeriod period,
        CancellationToken cancellationToken)
    {
        var (rangeStart, rangeEnd) = ResolveRange(period);
        var payments = await LoadValidatedPaymentsAsync(schoolId, cancellationToken);
        var inRange = payments.Where(p => p.PaymentDate >= rangeStart && p.PaymentDate < rangeEnd).ToList();
        var enrollments = await _enrollmentRepository.FindAsync(e => e.IsActive, cancellationToken);
        var classes = await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        var classMap = classes.ToDictionary(c => c.Id);

        var studentClass = enrollments
            .GroupBy(e => e.StudentId)
            .ToDictionary(g => g.Key, g => g.First().ClassRoomId);

        var ranks = inRange
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

        return ranks;
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

    private async Task<List<Payment>> LoadValidatedPaymentsAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var payments = await _paymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        return payments
            .Where(p => p.Status == PaymentStatus.Complet)
            .ToList();
    }

    private static decimal SumInRange(IEnumerable<Payment> payments, DateTime start, DateTime end) =>
        payments.Where(p => p.PaymentDate >= start && p.PaymentDate < end).Sum(p => p.TotalAmount);

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
