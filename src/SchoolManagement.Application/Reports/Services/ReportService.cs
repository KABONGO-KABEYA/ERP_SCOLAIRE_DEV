namespace SchoolManagement.Application.Reports.Services;

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
    private readonly IRepository<PeriodResult> _periodResultRepository;
    private readonly IRepository<AcademicPeriod> _periodRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;

    public ReportService(
        IRepository<Student> studentRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<Section> sectionRepository,
        IRepository<Teacher> teacherRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PeriodResult> periodResultRepository,
        IRepository<AcademicPeriod> periodRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<StudentFeeBalance> balanceRepository)
    {
        _studentRepository = studentRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _sectionRepository = sectionRepository;
        _teacherRepository = teacherRepository;
        _paymentRepository = paymentRepository;
        _periodResultRepository = periodResultRepository;
        _periodRepository = periodRepository;
        _yearRepository = yearRepository;
        _balanceRepository = balanceRepository;
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
            balances = balances.Where(b => b.AcademicYearId == academicYearId.Value).ToList();
        }

        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var studentIds = students.Select(s => s.Id).ToHashSet();
        balances = balances.Where(b => studentIds.Contains(b.StudentId)).ToList();

        var debtorCount = balances.Count(b => b.AmountPaid < b.AmountDue);
        var upToDateCount = balances.Count(b => b.AmountPaid >= b.AmountDue && b.AmountDue > 0);
        var partialCount = balances.Count(b => b.AmountPaid > 0 && b.AmountPaid < b.AmountDue);

        return new FinancialSummaryDto(
            validated.Sum(p => p.TotalAmount),
            validated.Count,
            debtorCount,
            upToDateCount,
            partialCount);
    }
}
