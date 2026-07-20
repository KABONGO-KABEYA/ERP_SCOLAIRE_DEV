namespace SchoolManagement.Application.Reports.Services;

using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Reports.DTOs;
using SchoolManagement.Application.Reports.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

public sealed class ReportService : IReportService
{
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<Section> _sectionRepository;
    private readonly IRepository<Teacher> _teacherRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentLine> _paymentLineRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<FeeInstallment> _feeInstallmentRepository;
    private readonly IRepository<PeriodResult> _periodResultRepository;
    private readonly IRepository<AcademicPeriod> _periodRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;
    private readonly IRepository<ClassFeeAmount> _classFeeAmountRepository;

    public ReportService(
        IRepository<Student> studentRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<Section> sectionRepository,
        IRepository<Teacher> teacherRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<FeeInstallment> feeInstallmentRepository,
        IRepository<PeriodResult> periodResultRepository,
        IRepository<AcademicPeriod> periodRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<ClassFeeAmount> classFeeAmountRepository)
    {
        _studentRepository = studentRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _sectionRepository = sectionRepository;
        _teacherRepository = teacherRepository;
        _paymentRepository = paymentRepository;
        _paymentLineRepository = paymentLineRepository;
        _feeTypeRepository = feeTypeRepository;
        _feeInstallmentRepository = feeInstallmentRepository;
        _periodResultRepository = periodResultRepository;
        _periodRepository = periodRepository;
        _yearRepository = yearRepository;
        _balanceRepository = balanceRepository;
        _classFeeAmountRepository = classFeeAmountRepository;
    }

    public async Task<DashboardStatsDto> GetDashboardAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var enrollments = await _enrollmentRepository.FindAsync(e => e.IsActive, cancellationToken);
        var studentIds = students.Select(s => s.Id).ToHashSet();
        var activeEnrollments = enrollments.Count(e => studentIds.Contains(e.StudentId));

        var classes = await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        var pedagogicalMap = await SchoolConfigurationGuards.BuildPedagogicalMapAsync(_pedagogicalClassRepository, schoolId, cancellationToken);
        classes = classes.Where(c => ClassRoomAvailability.IsSelectable(c, pedagogicalMap)).ToList();
        var teachers = await _teacherRepository.FindAsync(t => t.SchoolId == schoolId && t.IsActive, cancellationToken);
        var payments = await _paymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        var validated = payments.Where(p => p.Status == PaymentStatus.Complet).ToList();

        return new DashboardStatsDto(
            students.Count,
            activeEnrollments,
            classes.Count,
            teachers.Count,
            validated.Sum(p => p.TotalAmount),
            validated.Count);
    }

    public async Task<IReadOnlyList<EnrollmentByClassDto>> GetEnrollmentByClassAsync(
        Guid schoolId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var classes = await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        var pedagogicalMap = await SchoolConfigurationGuards.BuildPedagogicalMapAsync(_pedagogicalClassRepository, schoolId, cancellationToken);
        classes = classes.Where(c => ClassRoomAvailability.IsSelectable(c, pedagogicalMap)).ToList();
        if (academicYearId.HasValue)
        {
            classes = classes.Where(c => c.AcademicYearId == academicYearId.Value).ToList();
        }

        var sections = await _sectionRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var sectionMap = sections.ToDictionary(s => s.Id);

        var enrollments = await _enrollmentRepository.FindAsync(e => e.IsActive, cancellationToken);
        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);

        return classes
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Name)
            .Select(cr =>
            {
                var classEnrollments = enrollments
                    .Where(e => e.ClassRoomId == cr.Id && studentMap.ContainsKey(e.StudentId))
                    .ToList();

                var enrolledStudents = classEnrollments
                    .Select(e => studentMap[e.StudentId])
                    .ToList();

                return new EnrollmentByClassDto(
                    cr.Id,
                    cr.Code,
                    cr.Name,
                    sectionMap.GetValueOrDefault(cr.SectionId)?.Name ?? "—",
                    enrolledStudents.Count,
                    enrolledStudents.Count(s => s.Gender == Gender.Masculin),
                    enrolledStudents.Count(s => s.Gender == Gender.Feminin));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ClassAverageReportDto>> GetClassAveragesAsync(
        Guid schoolId,
        Guid? academicPeriodId = null,
        CancellationToken cancellationToken = default)
    {
        var classes = await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        var pedagogicalMap = await SchoolConfigurationGuards.BuildPedagogicalMapAsync(_pedagogicalClassRepository, schoolId, cancellationToken);
        classes = classes.Where(c => ClassRoomAvailability.IsSelectable(c, pedagogicalMap)).ToList();
        var classMap = classes.ToDictionary(c => c.Id);

        var results = await _periodResultRepository.FindAsync(_ => true, cancellationToken);
        if (academicPeriodId.HasValue)
        {
            results = results.Where(r => r.AcademicPeriodId == academicPeriodId.Value).ToList();
        }

        results = results.Where(r => classMap.ContainsKey(r.ClassRoomId)).ToList();

        var periodIds = results.Select(r => r.AcademicPeriodId).Distinct().ToList();
        var periods = await _periodRepository.FindAsync(p => periodIds.Contains(p.Id), cancellationToken);
        var periodMap = periods.ToDictionary(p => p.Id);

        return results
            .GroupBy(r => new { r.ClassRoomId, r.AcademicPeriodId })
            .Select(g =>
            {
                var cr = classMap[g.Key.ClassRoomId];
                var averages = g.Select(r => r.Average).ToList();
                return new ClassAverageReportDto(
                    cr.Id,
                    cr.Name,
                    periodMap.GetValueOrDefault(g.Key.AcademicPeriodId)?.Name ?? "—",
                    g.Count(),
                    Math.Round(averages.Average(), 2),
                    averages.Max(),
                    averages.Min(),
                    g.Count(r => r.Average >= 10),
                    g.Count(r => r.Average < 10));
            })
            .OrderBy(r => r.ClassName)
            .ThenBy(r => r.PeriodName)
            .ToList();
    }

    public async Task<FinancialSummaryDto> GetFinancialSummaryAsync(
        Guid schoolId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var payments = await _paymentRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        if (academicYearId.HasValue)
        {
            payments = payments.Where(p => p.AcademicYearId == academicYearId.Value).ToList();
        }

        var validated = payments.Where(p => p.Status == PaymentStatus.Complet).ToList();

        var balances = await _balanceRepository.FindAsync(_ => true, cancellationToken);
        if (academicYearId.HasValue)
        {
            var yearTariffIds = (await _classFeeAmountRepository.FindAsync(
                a => a.AcademicYearId == academicYearId.Value,
                cancellationToken)).Select(a => a.Id).ToHashSet();
            balances = balances.Where(b => yearTariffIds.Contains(b.ClassFeeAmountId)).ToList();
        }

        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var studentIds = students.Select(s => s.Id).ToHashSet();
        balances = balances.Where(b => studentIds.Contains(b.StudentId)).ToList();

        // Agrège par élève pour les compteurs (un élève = un statut global sur l'année).
        var byStudent = balances.GroupBy(b => b.StudentId).ToList();
        var debtorCount = byStudent.Count(g => g.Sum(b => b.AmountPaid) < g.Sum(b => b.AmountDue));
        var upToDateCount = byStudent.Count(g =>
        {
            var due = g.Sum(b => b.AmountDue);
            var paid = g.Sum(b => b.AmountPaid);
            return paid >= due && due > 0;
        });
        var partialCount = byStudent.Count(g =>
        {
            var due = g.Sum(b => b.AmountDue);
            var paid = g.Sum(b => b.AmountPaid);
            return paid > 0 && paid < due;
        });

        return new FinancialSummaryDto(
            validated.Sum(p => p.TotalAmount),
            validated.Count,
            debtorCount,
            upToDateCount,
            partialCount);
    }

    public async Task<RealizedReceiptsResultDto> GetRealizedReceiptsAsync(
        Guid schoolId,
        RealizedReceiptsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ToDate < request.FromDate)
        {
            throw new ArgumentException("La date de fin doit être postérieure ou égale à la date de début.");
        }

        var payments = await LoadValidatedPaymentsInRangeAsync(schoolId, request, cancellationToken);
        var studentIds = payments.Select(p => p.StudentId).Distinct().ToList();
        var students = studentIds.Count == 0
            ? []
            : await _studentRepository.FindAsync(s => studentIds.Contains(s.Id), cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);

        var paymentIds = payments.Select(p => p.Id).ToList();
        var lines = paymentIds.Count == 0
            ? []
            : await _paymentLineRepository.FindAsync(l => paymentIds.Contains(l.PaymentId), cancellationToken);
        if (request.FeeTypeId.HasValue)
        {
            lines = lines.Where(l => l.FeeTypeId == request.FeeTypeId.Value).ToList();
        }
        var feeTypeIds = lines.Select(l => l.FeeTypeId).Distinct().ToList();
        var feeTypes = feeTypeIds.Count == 0
            ? []
            : await _feeTypeRepository.FindAsync(f => feeTypeIds.Contains(f.Id), cancellationToken);
        var feeTypeMap = feeTypes.ToDictionary(f => f.Id);
        var linesByPayment = lines.GroupBy(l => l.PaymentId).ToDictionary(g => g.Key, g => g.ToList());

        var yearIds = payments.Select(p => p.AcademicYearId).Distinct().ToList();
        var enrollments = studentIds.Count == 0
            ? []
            : await _enrollmentRepository.FindAsync(
                e => e.IsActive && studentIds.Contains(e.StudentId),
                cancellationToken);
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
        var pedagogicalMap = ClassRoomAvailability.BuildMap(
            await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken));
        var sectionIds = classes.Select(c => c.SectionId).Distinct().ToList();
        var sections = sectionIds.Count == 0
            ? []
            : await _sectionRepository.FindAsync(s => sectionIds.Contains(s.Id) && s.SchoolId == schoolId, cancellationToken);
        var sectionMap = sections.ToDictionary(s => s.Id);

        Guid? ResolveClassId(Payment p) =>
            studentYearClass.TryGetValue((p.StudentId, p.AcademicYearId), out var classId) ? classId : null;

        string ResolveClassName(Guid? classId)
        {
            if (!classId.HasValue || !classMap.TryGetValue(classId.Value, out var cr))
            {
                return "Sans classe";
            }

            if (cr.PedagogicalClassId.HasValue
                && pedagogicalMap.TryGetValue(cr.PedagogicalClassId.Value, out var pedagogical))
            {
                return $"{pedagogical.DisplayName} {cr.Name}".Trim();
            }

            if (!string.IsNullOrWhiteSpace(cr.Code) && !string.Equals(cr.Code, cr.Name, StringComparison.OrdinalIgnoreCase))
            {
                return cr.Code;
            }

            return cr.Name;
        }

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
                var matchingSectionIds = (await _sectionRepository.FindAsync(
                        s => s.SchoolId == schoolId,
                        cancellationToken))
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
                paymentIds = payments.Select(p => p.Id).ToList();
                lines = lines.Where(l => paymentIds.Contains(l.PaymentId)).ToList();
                linesByPayment = lines.GroupBy(l => l.PaymentId).ToDictionary(g => g.Key, g => g.ToList());
            }
        }

        if (request.ClassRoomId.HasValue)
        {
            payments = payments.Where(p => ResolveClassId(p) == request.ClassRoomId.Value).ToList();
            paymentIds = payments.Select(p => p.Id).ToList();
            lines = lines.Where(l => paymentIds.Contains(l.PaymentId)).ToList();
            linesByPayment = lines.GroupBy(l => l.PaymentId).ToDictionary(g => g.Key, g => g.ToList());
        }

        var ordered = payments
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt)
            .ToList();

        var totalCount = ordered.Count;
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 2_000);
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = pageItems.Select(p =>
        {
            var name = studentMap.TryGetValue(p.StudentId, out var student)
                ? $"{student.LastName} {student.FirstName}".Trim()
                : "—";
            var classId = ResolveClassId(p);
            string? feeSummary = null;
            if (linesByPayment.TryGetValue(p.Id, out var paymentLines))
            {
                feeSummary = string.Join(", ", paymentLines
                    .Select(l => feeTypeMap.GetValueOrDefault(l.FeeTypeId)?.Name ?? "—")
                    .Distinct());
            }

            return new RealizedReceiptLineDto(
                p.Id,
                p.ReceiptNumber,
                p.StudentId,
                name,
                ResolveClassName(classId),
                p.PaymentDate,
                p.TotalAmount,
                p.Currency.ToString(),
                feeSummary,
                p.Notes);
        }).ToList();

        var installmentIds = lines
            .Where(l => l.FeeInstallmentId.HasValue)
            .Select(l => l.FeeInstallmentId!.Value)
            .Distinct()
            .ToList();
        var installments = installmentIds.Count == 0
            ? []
            : await _feeInstallmentRepository.FindAsync(i => installmentIds.Contains(i.Id), cancellationToken);
        var installmentMap = installments.ToDictionary(i => i.Id);

        var installmentColumns = installmentIds
            .Select(id =>
            {
                var installment = installmentMap.GetValueOrDefault(id);
                return new RealizedReceiptsInstallmentColumnDto(
                    id,
                    installment?.Name ?? "Tranche",
                    installment?.SortOrder ?? int.MaxValue);
            })
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.InstallmentName)
            .ToList();

        var paymentById = ordered.ToDictionary(p => p.Id);
        var pivotRows = lines
            .Where(l => l.FeeInstallmentId.HasValue && paymentById.ContainsKey(l.PaymentId))
            .GroupBy(l =>
            {
                var payment = paymentById[l.PaymentId];
                return (payment.StudentId, ClassId: ResolveClassId(payment));
            })
            .Select(g =>
            {
                var studentId = g.Key.StudentId;
                var name = studentMap.TryGetValue(studentId, out var student)
                    ? $"{student.LastName} {student.FirstName}".Trim()
                    : "—";
                var amountsByInstallment = g
                    .GroupBy(l => l.FeeInstallmentId!.Value)
                    .ToDictionary(x => x.Key, x => x.Sum(l => l.Amount));
                var amounts = installmentColumns
                    .Select(c => amountsByInstallment.GetValueOrDefault(c.FeeInstallmentId))
                    .ToList();
                return new RealizedReceiptsPivotRowDto(
                    studentId,
                    name,
                    ResolveClassName(g.Key.ClassId),
                    amounts,
                    amounts.Sum());
            })
            .OrderBy(r => r.ClassName)
            .ThenBy(r => r.StudentName)
            .ToList();

        var dailyPivotRows = lines
            .Where(l => l.FeeInstallmentId.HasValue && paymentById.ContainsKey(l.PaymentId))
            .GroupBy(l =>
            {
                var payment = paymentById[l.PaymentId];
                return (
                    Date: DateOnly.FromDateTime(payment.PaymentDate),
                    payment.StudentId,
                    ClassId: ResolveClassId(payment));
            })
            .Select(g =>
            {
                var studentId = g.Key.StudentId;
                var name = studentMap.TryGetValue(studentId, out var student)
                    ? $"{student.LastName} {student.FirstName}".Trim()
                    : "—";

                var detailsByInstallment = g
                    .GroupBy(l => l.FeeInstallmentId!.Value)
                    .ToDictionary(
                        x => x.Key,
                        x => string.Join(" | ",
                            x.OrderBy(line => paymentById[line.PaymentId].PaymentDate)
                                .ThenBy(line => paymentById[line.PaymentId].ReceiptNumber)
                                .Select(line =>
                                    $"{paymentById[line.PaymentId].ReceiptNumber} ({line.Amount:N2})")));

                var details = installmentColumns
                    .Select(c => detailsByInstallment.GetValueOrDefault(c.FeeInstallmentId) ?? string.Empty)
                    .ToList();

                return new RealizedReceiptsDailyPivotRowDto(
                    g.Key.Date,
                    studentId,
                    name,
                    ResolveClassName(g.Key.ClassId),
                    details,
                    g.Sum(x => x.Amount));
            })
            .OrderBy(r => r.Date)
            .ThenBy(r => r.ClassName)
            .ThenBy(r => r.StudentName)
            .ToList();

        var dailyBuckets = ordered
            .GroupBy(p => DateOnly.FromDateTime(p.PaymentDate))
            .OrderBy(g => g.Key)
            .Select(g => new RealizedReceiptsDailyBucketDto(g.Key, g.Sum(p => p.TotalAmount), g.Count()))
            .ToList();

        var byCurrency = ordered
            .GroupBy(p => p.Currency.ToString())
            .OrderBy(g => g.Key)
            .Select(g => new RealizedReceiptsByCurrencyDto(g.Key, g.Sum(p => p.TotalAmount), g.Count()))
            .ToList();

        var byClass = ordered
            .GroupBy(p => ResolveClassId(p))
            .Select(g =>
            {
                var classId = g.Key;
                var cr = classId.HasValue ? classMap.GetValueOrDefault(classId.Value) : null;
                return new RealizedReceiptsByClassDto(
                    classId,
                    cr?.Code ?? "—",
                    ResolveClassName(classId),
                    cr is null ? "—" : (sectionMap.GetValueOrDefault(cr.SectionId)?.Name ?? "—"),
                    g.Sum(p => p.TotalAmount),
                    g.Count());
            })
            .OrderByDescending(x => x.TotalAmount)
            .ThenBy(x => x.ClassName)
            .ToList();

        var bySection = ordered
            .GroupBy(p =>
            {
                var sectionId = ResolveSectionId(ResolveClassId(p));
                if (!sectionId.HasValue)
                {
                    return "Sans section";
                }

                return sectionMap.TryGetValue(sectionId.Value, out var section)
                    ? section.Name.Trim()
                    : "Sans section";
            })
            .Select(g =>
            {
                var section = sections.FirstOrDefault(s =>
                    string.Equals(s.Name.Trim(), g.Key, StringComparison.OrdinalIgnoreCase));
                return new RealizedReceiptsBySectionDto(
                    section?.Id,
                    section?.Code ?? "—",
                    g.Key,
                    g.Sum(p => p.TotalAmount),
                    g.Count());
            })
            .OrderByDescending(x => x.TotalAmount)
            .ThenBy(x => x.SectionName)
            .ToList();

        var byFeeType = lines
            .GroupBy(l => l.FeeTypeId)
            .Select(g =>
            {
                var fee = feeTypeMap.GetValueOrDefault(g.Key);
                return new RealizedReceiptsByFeeTypeDto(
                    g.Key,
                    fee?.Name ?? "—",
                    (fee?.Currency ?? g.First().Currency).ToString(),
                    g.Sum(l => l.Amount),
                    g.Select(l => l.PaymentId).Distinct().Count());
            })
            .OrderByDescending(x => x.TotalAmount)
            .ThenBy(x => x.FeeTypeName)
            .ToList();

        var paymentDateMap = ordered.ToDictionary(p => p.Id, p => DateOnly.FromDateTime(p.PaymentDate));

        var dailyByClass = ordered
            .GroupBy(p => (Date: DateOnly.FromDateTime(p.PaymentDate), ClassId: ResolveClassId(p)))
            .Select(g => new RealizedReceiptsDailyByClassDto(
                g.Key.Date,
                g.Key.ClassId,
                ResolveClassName(g.Key.ClassId),
                g.Sum(p => p.TotalAmount),
                g.Count()))
            .OrderBy(x => x.Date)
            .ThenBy(x => x.ClassName)
            .ToList();

        var dailyByFeeType = lines
            .Where(l => paymentDateMap.ContainsKey(l.PaymentId))
            .GroupBy(l => (Date: paymentDateMap[l.PaymentId], l.FeeTypeId))
            .Select(g =>
            {
                var fee = feeTypeMap.GetValueOrDefault(g.Key.FeeTypeId);
                return new RealizedReceiptsDailyByFeeTypeDto(
                    g.Key.Date,
                    g.Key.FeeTypeId,
                    fee?.Name ?? "—",
                    (fee?.Currency ?? g.First().Currency).ToString(),
                    g.Sum(l => l.Amount),
                    g.Select(l => l.PaymentId).Distinct().Count());
            })
            .OrderBy(x => x.Date)
            .ThenBy(x => x.FeeTypeName)
            .ToList();

        var dailyBySection = ordered
            .GroupBy(p =>
            {
                var sectionId = ResolveSectionId(ResolveClassId(p));
                var sectionName = sectionId.HasValue && sectionMap.TryGetValue(sectionId.Value, out var section)
                    ? section.Name.Trim()
                    : "Sans section";
                return (Date: DateOnly.FromDateTime(p.PaymentDate), SectionName: sectionName);
            })
            .Select(g =>
            {
                var section = sections.FirstOrDefault(s =>
                    string.Equals(s.Name.Trim(), g.Key.SectionName, StringComparison.OrdinalIgnoreCase));
                return new RealizedReceiptsDailyBySectionDto(
                    g.Key.Date,
                    section?.Id,
                    g.Key.SectionName,
                    g.Sum(p => p.TotalAmount),
                    g.Count());
            })
            .OrderBy(x => x.Date)
            .ThenBy(x => x.SectionName)
            .ToList();

        return new RealizedReceiptsResultDto(
            request.FromDate,
            request.ToDate,
            items,
            installmentColumns,
            pivotRows,
            dailyPivotRows,
            dailyBuckets,
            byCurrency,
            byClass,
            byFeeType,
            bySection,
            dailyByClass,
            dailyByFeeType,
            dailyBySection,
            lines.Sum(l => l.Amount),
            totalCount,
            totalCount);
    }

    public async Task<byte[]> ExportRealizedReceiptsPdfAsync(
        Guid schoolId,
        RealizedReceiptsRequest request,
        CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var exportRequest = request with { Page = 1, PageSize = 2_000 };
        var result = await GetRealizedReceiptsAsync(schoolId, exportRequest, cancellationToken);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Column(col =>
                {
                    col.Item().Text("Recettes réalisées").SemiBold().FontSize(16);
                    col.Item().Text($"Période du {result.FromDate:dd/MM/yyyy} au {result.ToDate:dd/MM/yyyy}")
                        .FontSize(10).FontColor(Colors.Grey.Darken2);
                });

                page.Content().Column(col =>
                {
                    if (result.InstallmentColumns.Count > 0)
                    {
                        col.Item().PaddingBottom(12).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2.2f);
                                columns.RelativeColumn(1.6f);
                                foreach (var _ in result.InstallmentColumns)
                                {
                                    columns.RelativeColumn(1.1f);
                                }

                                columns.RelativeColumn(1.1f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Nom complet").SemiBold();
                                header.Cell().Text("Classe").SemiBold();
                                foreach (var installment in result.InstallmentColumns)
                                {
                                    header.Cell().AlignRight().Text(installment.InstallmentName).SemiBold();
                                }

                                header.Cell().AlignRight().Text("Total").SemiBold();
                            });

                            foreach (var row in result.PivotRows)
                            {
                                table.Cell().Text(row.StudentName);
                                table.Cell().Text(row.ClassName);
                                for (var i = 0; i < row.InstallmentAmounts.Count; i++)
                                {
                                    var amount = row.InstallmentAmounts[i];
                                    table.Cell().AlignRight().Text(amount > 0 ? $"{amount:N2}" : "—");
                                }

                                table.Cell().AlignRight().Text($"{row.RowTotal:N2}");
                            }
                        });
                    }

                    if (result.DailyPivotRows.Count > 0)
                    {
                        col.Item().PaddingTop(16).Text("Détail journalier").SemiBold().FontSize(12);
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(2f);
                                columns.RelativeColumn(1.5f);
                                foreach (var _ in result.InstallmentColumns)
                                {
                                    columns.RelativeColumn(1.4f);
                                }

                                columns.RelativeColumn(1f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Date").SemiBold();
                                header.Cell().Text("Nom complet").SemiBold();
                                header.Cell().Text("Classe").SemiBold();
                                foreach (var installment in result.InstallmentColumns)
                                {
                                    header.Cell().Text(installment.InstallmentName).SemiBold();
                                }

                                header.Cell().AlignRight().Text("Total").SemiBold();
                            });

                            DateOnly? previousDate = null;
                            foreach (var row in result.DailyPivotRows)
                            {
                                table.Cell().Text(previousDate == row.Date ? string.Empty : $"{row.Date:dd/MM/yyyy}");
                                table.Cell().Text(row.StudentName);
                                table.Cell().Text(row.ClassName);
                                for (var i = 0; i < row.InstallmentDetails.Count; i++)
                                {
                                    var detail = row.InstallmentDetails[i];
                                    table.Cell().Text(string.IsNullOrWhiteSpace(detail) ? "—" : detail);
                                }

                                table.Cell().AlignRight().Text($"{row.RowTotal:N2}");
                                previousDate = row.Date;
                            }
                        });
                    }

                    if (result.DailyBuckets.Count > 0)
                    {
                        col.Item().PaddingTop(16).Text("Synthèse journalière").SemiBold().FontSize(12);
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Jour").SemiBold();
                                header.Cell().AlignRight().Text("Montant").SemiBold();
                                header.Cell().AlignRight().Text("Nb").SemiBold();
                            });

                            foreach (var bucket in result.DailyBuckets)
                            {
                                table.Cell().Text($"{bucket.Date:dd/MM/yyyy}");
                                table.Cell().AlignRight().Text($"{bucket.TotalAmount:N2}");
                                table.Cell().AlignRight().Text(bucket.PaymentCount.ToString());
                            }
                        });
                    }

                    if (result.ByClass.Count > 0)
                    {
                        col.Item().PaddingTop(16).Text("Par classe").SemiBold().FontSize(12);
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Classe").SemiBold();
                                header.Cell().Text("Section").SemiBold();
                                header.Cell().AlignRight().Text("Montant").SemiBold();
                                header.Cell().AlignRight().Text("Nb").SemiBold();
                            });

                            foreach (var item in result.ByClass)
                            {
                                table.Cell().Text(item.ClassName);
                                table.Cell().Text(item.SectionName);
                                table.Cell().AlignRight().Text($"{item.TotalAmount:N2}");
                                table.Cell().AlignRight().Text(item.PaymentCount.ToString());
                            }
                        });
                    }

                    if (result.ByFeeType.Count > 0)
                    {
                        col.Item().PaddingTop(16).Text("Par type de frais").SemiBold().FontSize(12);
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2.5f);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Type de frais").SemiBold();
                                header.Cell().Text("Devise").SemiBold();
                                header.Cell().AlignRight().Text("Montant").SemiBold();
                                header.Cell().AlignRight().Text("Nb").SemiBold();
                            });

                            foreach (var item in result.ByFeeType)
                            {
                                table.Cell().Text(item.FeeTypeName);
                                table.Cell().Text(item.Currency);
                                table.Cell().AlignRight().Text($"{item.TotalAmount:N2}");
                                table.Cell().AlignRight().Text(item.PaymentCount.ToString());
                            }
                        });
                    }

                    if (result.BySection.Count > 0)
                    {
                        col.Item().PaddingTop(16).Text("Par section").SemiBold().FontSize(12);
                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(2.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Code").SemiBold();
                                header.Cell().Text("Section").SemiBold();
                                header.Cell().AlignRight().Text("Montant").SemiBold();
                                header.Cell().AlignRight().Text("Nb").SemiBold();
                            });

                            foreach (var item in result.BySection)
                            {
                                table.Cell().Text(item.SectionCode);
                                table.Cell().Text(item.SectionName);
                                table.Cell().AlignRight().Text($"{item.TotalAmount:N2}");
                                table.Cell().AlignRight().Text(item.PaymentCount.ToString());
                            }
                        });
                    }
                });

                page.Footer().AlignRight().Text(
                    $"Total : {result.GrandTotal:N2} — {result.PaymentCount} paiement(s)");
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportRealizedReceiptsExcelAsync(
        Guid schoolId,
        RealizedReceiptsRequest request,
        CancellationToken cancellationToken = default)
    {
        var exportRequest = request with { Page = 1, PageSize = 2_000 };
        var result = await GetRealizedReceiptsAsync(schoolId, exportRequest, cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Recettes");
        sheet.Cell(1, 1).Value = "Recettes réalisées";
        sheet.Cell(2, 1).Value = $"Du {result.FromDate:dd/MM/yyyy} au {result.ToDate:dd/MM/yyyy}";
        sheet.Cell(3, 1).Value = "Total";
        sheet.Cell(3, 2).Value = result.GrandTotal;
        sheet.Cell(3, 3).Value = $"{result.PaymentCount} paiement(s)";

        var headers = new List<string> { "Nom complet", "Classe" };
        headers.AddRange(result.InstallmentColumns.Select(c => c.InstallmentName));
        headers.Add("Total");
        for (var i = 0; i < headers.Count; i++)
        {
            sheet.Cell(5, i + 1).Value = headers[i];
            sheet.Cell(5, i + 1).Style.Font.Bold = true;
        }

        var row = 6;
        foreach (var item in result.PivotRows)
        {
            sheet.Cell(row, 1).Value = item.StudentName;
            sheet.Cell(row, 2).Value = item.ClassName;
            for (var i = 0; i < item.InstallmentAmounts.Count; i++)
            {
                sheet.Cell(row, 3 + i).Value = item.InstallmentAmounts[i];
            }

            sheet.Cell(row, 3 + item.InstallmentAmounts.Count).Value = item.RowTotal;
            row++;
        }

        var dailySheet = workbook.Worksheets.Add("Journalier");
        var dailyHeaders = new List<string> { "Date", "Nom complet", "Classe" };
        dailyHeaders.AddRange(result.InstallmentColumns.Select(c => c.InstallmentName));
        dailyHeaders.Add("Total");
        for (var i = 0; i < dailyHeaders.Count; i++)
        {
            dailySheet.Cell(1, i + 1).Value = dailyHeaders[i];
            dailySheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var dRow = 2;
        foreach (var item in result.DailyPivotRows)
        {
            dailySheet.Cell(dRow, 1).Value = item.Date.ToDateTime(TimeOnly.MinValue);
            dailySheet.Cell(dRow, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            dailySheet.Cell(dRow, 2).Value = item.StudentName;
            dailySheet.Cell(dRow, 3).Value = item.ClassName;
            for (var i = 0; i < item.InstallmentDetails.Count; i++)
            {
                var detail = item.InstallmentDetails[i];
                dailySheet.Cell(dRow, 4 + i).Value = string.IsNullOrWhiteSpace(detail) ? "—" : detail;
            }

            dailySheet.Cell(dRow, 4 + item.InstallmentDetails.Count).Value = item.RowTotal;
            dRow++;
        }

        var dailySummarySheet = workbook.Worksheets.Add("Synthèse journalière");
        dailySummarySheet.Cell(1, 1).Value = "Date";
        dailySummarySheet.Cell(1, 2).Value = "Montant";
        dailySummarySheet.Cell(1, 3).Value = "Nb paiements";
        dailySummarySheet.Row(1).Style.Font.Bold = true;
        var dssRow = 2;
        foreach (var bucket in result.DailyBuckets)
        {
            dailySummarySheet.Cell(dssRow, 1).Value = bucket.Date.ToDateTime(TimeOnly.MinValue);
            dailySummarySheet.Cell(dssRow, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            dailySummarySheet.Cell(dssRow, 2).Value = bucket.TotalAmount;
            dailySummarySheet.Cell(dssRow, 3).Value = bucket.PaymentCount;
            dssRow++;
        }

        var classSheet = workbook.Worksheets.Add("Par classe");
        classSheet.Cell(1, 1).Value = "Code";
        classSheet.Cell(1, 2).Value = "Classe";
        classSheet.Cell(1, 3).Value = "Section";
        classSheet.Cell(1, 4).Value = "Montant";
        classSheet.Cell(1, 5).Value = "Nb paiements";
        classSheet.Row(1).Style.Font.Bold = true;
        var cRow = 2;
        foreach (var item in result.ByClass)
        {
            classSheet.Cell(cRow, 1).Value = item.ClassCode;
            classSheet.Cell(cRow, 2).Value = item.ClassName;
            classSheet.Cell(cRow, 3).Value = item.SectionName;
            classSheet.Cell(cRow, 4).Value = item.TotalAmount;
            classSheet.Cell(cRow, 5).Value = item.PaymentCount;
            cRow++;
        }

        var feeSheet = workbook.Worksheets.Add("Par type de frais");
        feeSheet.Cell(1, 1).Value = "Type de frais";
        feeSheet.Cell(1, 2).Value = "Devise";
        feeSheet.Cell(1, 3).Value = "Montant";
        feeSheet.Cell(1, 4).Value = "Nb paiements";
        feeSheet.Row(1).Style.Font.Bold = true;
        var fRow = 2;
        foreach (var item in result.ByFeeType)
        {
            feeSheet.Cell(fRow, 1).Value = item.FeeTypeName;
            feeSheet.Cell(fRow, 2).Value = item.Currency;
            feeSheet.Cell(fRow, 3).Value = item.TotalAmount;
            feeSheet.Cell(fRow, 4).Value = item.PaymentCount;
            fRow++;
        }

        var dailyClassSheet = workbook.Worksheets.Add("Journalier par classe");
        dailyClassSheet.Cell(1, 1).Value = "Date";
        dailyClassSheet.Cell(1, 2).Value = "Classe";
        dailyClassSheet.Cell(1, 3).Value = "Montant";
        dailyClassSheet.Cell(1, 4).Value = "Nb";
        dailyClassSheet.Row(1).Style.Font.Bold = true;
        var dcRow = 2;
        foreach (var item in result.DailyByClass)
        {
            dailyClassSheet.Cell(dcRow, 1).Value = item.Date.ToDateTime(TimeOnly.MinValue);
            dailyClassSheet.Cell(dcRow, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            dailyClassSheet.Cell(dcRow, 2).Value = item.ClassName;
            dailyClassSheet.Cell(dcRow, 3).Value = item.TotalAmount;
            dailyClassSheet.Cell(dcRow, 4).Value = item.PaymentCount;
            dcRow++;
        }

        var dailyFeeSheet = workbook.Worksheets.Add("Journalier par frais");
        dailyFeeSheet.Cell(1, 1).Value = "Date";
        dailyFeeSheet.Cell(1, 2).Value = "Type de frais";
        dailyFeeSheet.Cell(1, 3).Value = "Devise";
        dailyFeeSheet.Cell(1, 4).Value = "Montant";
        dailyFeeSheet.Cell(1, 5).Value = "Nb";
        dailyFeeSheet.Row(1).Style.Font.Bold = true;
        var dfRow = 2;
        foreach (var item in result.DailyByFeeType)
        {
            dailyFeeSheet.Cell(dfRow, 1).Value = item.Date.ToDateTime(TimeOnly.MinValue);
            dailyFeeSheet.Cell(dfRow, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            dailyFeeSheet.Cell(dfRow, 2).Value = item.FeeTypeName;
            dailyFeeSheet.Cell(dfRow, 3).Value = item.Currency;
            dailyFeeSheet.Cell(dfRow, 4).Value = item.TotalAmount;
            dailyFeeSheet.Cell(dfRow, 5).Value = item.PaymentCount;
            dfRow++;
        }

        var sectionSheet = workbook.Worksheets.Add("Par section");
        sectionSheet.Cell(1, 1).Value = "Code";
        sectionSheet.Cell(1, 2).Value = "Section";
        sectionSheet.Cell(1, 3).Value = "Montant";
        sectionSheet.Cell(1, 4).Value = "Nb paiements";
        sectionSheet.Row(1).Style.Font.Bold = true;
        var sRow = 2;
        foreach (var item in result.BySection)
        {
            sectionSheet.Cell(sRow, 1).Value = item.SectionCode;
            sectionSheet.Cell(sRow, 2).Value = item.SectionName;
            sectionSheet.Cell(sRow, 3).Value = item.TotalAmount;
            sectionSheet.Cell(sRow, 4).Value = item.PaymentCount;
            sRow++;
        }

        var dailySectionSheet = workbook.Worksheets.Add("Journalier par section");
        dailySectionSheet.Cell(1, 1).Value = "Date";
        dailySectionSheet.Cell(1, 2).Value = "Section";
        dailySectionSheet.Cell(1, 3).Value = "Montant";
        dailySectionSheet.Cell(1, 4).Value = "Nb";
        dailySectionSheet.Row(1).Style.Font.Bold = true;
        var dsRow = 2;
        foreach (var item in result.DailyBySection)
        {
            dailySectionSheet.Cell(dsRow, 1).Value = item.Date.ToDateTime(TimeOnly.MinValue);
            dailySectionSheet.Cell(dsRow, 1).Style.DateFormat.Format = "dd/MM/yyyy";
            dailySectionSheet.Cell(dsRow, 2).Value = item.SectionName;
            dailySectionSheet.Cell(dsRow, 3).Value = item.TotalAmount;
            dailySectionSheet.Cell(dsRow, 4).Value = item.PaymentCount;
            dsRow++;
        }

        sheet.Columns().AdjustToContents();
        dailySheet.Columns().AdjustToContents();
        dailySummarySheet.Columns().AdjustToContents();
        classSheet.Columns().AdjustToContents();
        feeSheet.Columns().AdjustToContents();
        dailyClassSheet.Columns().AdjustToContents();
        dailyFeeSheet.Columns().AdjustToContents();
        sectionSheet.Columns().AdjustToContents();
        dailySectionSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<List<Payment>> LoadValidatedPaymentsInRangeAsync(
        Guid schoolId,
        RealizedReceiptsRequest request,
        CancellationToken cancellationToken)
    {
        var payments = await _paymentRepository.FindAsync(
            p => p.SchoolId == schoolId && p.Status == PaymentStatus.Complet,
            cancellationToken);

        IEnumerable<Payment> query = payments.Where(p =>
        {
            var date = DateOnly.FromDateTime(p.PaymentDate);
            return date >= request.FromDate && date <= request.ToDate;
        });

        if (request.AcademicYearId.HasValue)
        {
            query = query.Where(p => p.AcademicYearId == request.AcademicYearId.Value);
        }

        if (request.FeeTypeId.HasValue)
        {
            var paymentIdsWithFee = (await _paymentLineRepository.FindAsync(
                    l => l.FeeTypeId == request.FeeTypeId.Value,
                    cancellationToken))
                .Select(l => l.PaymentId)
                .ToHashSet();
            query = query.Where(p => paymentIdsWithFee.Contains(p.Id));
        }

        return query.ToList();
    }
}
