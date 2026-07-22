namespace SchoolManagement.Application.Parent.Services;

using SchoolManagement.Application.Common;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Parent.DTOs;
using SchoolManagement.Application.Parent.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;

public sealed class ParentService : IParentService
{
    private readonly IRepository<StudentGuardian> _studentGuardianRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PeriodResult> _periodResultRepository;
    private readonly IRepository<Domain.Entities.Settings.AcademicPeriod> _periodRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<Domain.Entities.Settings.ClassRoom> _classRoomRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;

    public ParentService(
        IRepository<StudentGuardian> studentGuardianRepository,
        IRepository<Student> studentRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PeriodResult> periodResultRepository,
        IRepository<Domain.Entities.Settings.AcademicPeriod> periodRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<Domain.Entities.Settings.ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository)
    {
        _studentGuardianRepository = studentGuardianRepository;
        _studentRepository = studentRepository;
        _paymentRepository = paymentRepository;
        _periodResultRepository = periodResultRepository;
        _periodRepository = periodRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
    }

    public async Task<IReadOnlyList<ParentChildDto>> GetMyChildrenAsync(Guid guardianId, CancellationToken cancellationToken = default)
    {
        var links = await _studentGuardianRepository.FindAsync(sg => sg.GuardianId == guardianId, cancellationToken);
        var studentIds = links.Select(l => l.StudentId).Distinct().ToList();
        var students = await _studentRepository.FindAsync(s => studentIds.Contains(s.Id), cancellationToken);
        var enrollments = await _enrollmentRepository.FindAsync(e => studentIds.Contains(e.StudentId) && e.IsActive, cancellationToken);
        var classIds = enrollments.Select(e => e.ClassRoomId).Distinct().ToList();
        var classes = await _classRoomRepository.FindAsync(c => classIds.Contains(c.Id), cancellationToken);
        var schoolIds = students.Select(s => s.SchoolId).Distinct().ToList();
        var pedagogicalClasses = schoolIds.Count == 0
            ? []
            : await _pedagogicalClassRepository.FindAsync(p => schoolIds.Contains(p.SchoolId), cancellationToken);
        var pedagogicalMap = pedagogicalClasses.ToDictionary(p => p.Id);
        var classMap = classes.ToDictionary(c => c.Id);

        return students.Select(s =>
        {
            var enrollment = enrollments.FirstOrDefault(e => e.StudentId == s.Id);
            string? className = null;
            if (enrollment is not null && classMap.TryGetValue(enrollment.ClassRoomId, out var cr))
            {
                if (ClassRoomAvailability.IsSelectable(cr, pedagogicalMap))
                {
                    className = cr.Name;
                }
            }
            return new ParentChildDto(s.Id, s.RegistrationNumber, StudentDisplayName.Format(s), className);
        }).ToList();
    }

    public async Task<IReadOnlyList<ParentPaymentDto>> GetChildPaymentsAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureChildAccessAsync(guardianId, studentId, cancellationToken);

        var payments = await _paymentRepository.FindAsync(p => p.StudentId == studentId, cancellationToken);
        return payments
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new ParentPaymentDto(p.Id, p.ReceiptNumber, p.PaymentDate, p.TotalAmount, p.Currency, p.Status))
            .ToList();
    }

    public async Task<IReadOnlyList<ParentBulletinSummaryDto>> GetChildBulletinsAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureChildAccessAsync(guardianId, studentId, cancellationToken);

        var results = await _periodResultRepository.FindAsync(r => r.StudentId == studentId, cancellationToken);
        var periodIds = results.Select(r => r.AcademicPeriodId).Distinct().ToList();
        var periods = await _periodRepository.FindAsync(p => periodIds.Contains(p.Id), cancellationToken);
        var periodMap = periods.ToDictionary(p => p.Id);

        return results
            .OrderBy(r => periodMap.GetValueOrDefault(r.AcademicPeriodId)?.OrderIndex ?? 0)
            .Select(r => new ParentBulletinSummaryDto(
                r.AcademicPeriodId,
                periodMap.GetValueOrDefault(r.AcademicPeriodId)?.Name ?? "—",
                r.Average,
                r.Percentage,
                r.Rank,
                r.ClassSize,
                r.IsPublished))
            .ToList();
    }

    private async Task EnsureChildAccessAsync(Guid guardianId, Guid studentId, CancellationToken cancellationToken)
    {
        var links = await _studentGuardianRepository.FindAsync(
            sg => sg.GuardianId == guardianId && sg.StudentId == studentId, cancellationToken);

        if (links.Count == 0)
        {
            throw new UnauthorizedAccessException("Accès non autorisé à cet élève.");
        }
    }
}
