namespace SchoolManagement.Application.Parent.Services;

using SchoolManagement.Application.Common;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Parent.DTOs;
using SchoolManagement.Application.Parent.Interfaces;
using SchoolManagement.Application.Payments.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

public sealed class ParentService : IParentService
{
    private readonly IRepository<StudentGuardian> _studentGuardianRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentLine> _paymentLineRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<StudentFeeBalance> _balanceRepository;
    private readonly IRepository<PeriodResult> _periodResultRepository;
    private readonly IRepository<Domain.Entities.Settings.AcademicPeriod> _periodRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<Domain.Entities.Settings.ClassRoom> _classRoomRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<School> _schoolRepository;
    private readonly IRepository<ClassFeeAmount> _classFeeAmountRepository;
    private readonly IRepository<AcademicYear> _academicYearRepository;
    private readonly IRepository<Evaluation> _evaluationRepository;
    private readonly IRepository<EvaluationTypeDefinition> _evaluationTypeRepository;
    private readonly IRepository<GradeEntry> _gradeEntryRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<StudentAttendance> _attendanceRepository;
    private readonly IRepository<Announcement> _announcementRepository;
    private readonly IStudentDossierStorageService _dossierStorage;
    private readonly IFeeTypeStatementService _statementService;

    public ParentService(
        IRepository<StudentGuardian> studentGuardianRepository,
        IRepository<Student> studentRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentLine> paymentLineRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<StudentFeeBalance> balanceRepository,
        IRepository<PeriodResult> periodResultRepository,
        IRepository<Domain.Entities.Settings.AcademicPeriod> periodRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<Domain.Entities.Settings.ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<School> schoolRepository,
        IRepository<ClassFeeAmount> classFeeAmountRepository,
        IRepository<AcademicYear> academicYearRepository,
        IRepository<Evaluation> evaluationRepository,
        IRepository<EvaluationTypeDefinition> evaluationTypeRepository,
        IRepository<GradeEntry> gradeEntryRepository,
        IRepository<Course> courseRepository,
        IRepository<StudentAttendance> attendanceRepository,
        IRepository<Announcement> announcementRepository,
        IStudentDossierStorageService dossierStorage,
        IFeeTypeStatementService statementService)
    {
        _studentGuardianRepository = studentGuardianRepository;
        _studentRepository = studentRepository;
        _paymentRepository = paymentRepository;
        _paymentLineRepository = paymentLineRepository;
        _feeTypeRepository = feeTypeRepository;
        _balanceRepository = balanceRepository;
        _periodResultRepository = periodResultRepository;
        _periodRepository = periodRepository;
        _enrollmentRepository = enrollmentRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _schoolRepository = schoolRepository;
        _classFeeAmountRepository = classFeeAmountRepository;
        _academicYearRepository = academicYearRepository;
        _evaluationRepository = evaluationRepository;
        _evaluationTypeRepository = evaluationTypeRepository;
        _gradeEntryRepository = gradeEntryRepository;
        _courseRepository = courseRepository;
        _attendanceRepository = attendanceRepository;
        _announcementRepository = announcementRepository;
        _dossierStorage = dossierStorage;
        _statementService = statementService;
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
        var schools = schoolIds.Count == 0
            ? []
            : await _schoolRepository.FindAsync(s => schoolIds.Contains(s.Id), cancellationToken);
        var schoolMap = schools.ToDictionary(s => s.Id);
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
                    if (cr.PedagogicalClassId.HasValue
                        && pedagogicalMap.TryGetValue(cr.PedagogicalClassId.Value, out var ped)
                        && !string.IsNullOrWhiteSpace(ped.DisplayName))
                    {
                        className = string.IsNullOrWhiteSpace(cr.Name)
                            ? ped.DisplayName
                            : $"{ped.DisplayName} {cr.Name}".Trim();
                    }
                }
            }

            schoolMap.TryGetValue(s.SchoolId, out var school);
            var photoUrl = string.IsNullOrWhiteSpace(s.PhotoPath)
                ? null
                : $"/api/v1/parent/children/{s.Id}/photo";

            return new ParentChildDto(
                s.Id,
                s.RegistrationNumber,
                StudentDisplayName.Format(s),
                className,
                photoUrl,
                school?.Name);
        }).ToList();
    }

    public async Task<IReadOnlyList<ParentPaymentDto>> GetChildPaymentsAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureChildAccessAsync(guardianId, studentId, cancellationToken);

        var payments = await _paymentRepository.FindAsync(p => p.StudentId == studentId, cancellationToken);
        var paymentIds = payments.Select(p => p.Id).ToList();
        var lines = paymentIds.Count == 0
            ? []
            : await _paymentLineRepository.FindAsync(l => paymentIds.Contains(l.PaymentId), cancellationToken);
        var feeTypeIds = lines.Select(l => l.FeeTypeId).Distinct().ToList();
        var feeTypes = feeTypeIds.Count == 0
            ? []
            : await _feeTypeRepository.FindAsync(f => feeTypeIds.Contains(f.Id), cancellationToken);
        var feeMap = feeTypes.ToDictionary(f => f.Id, f => f.Name);
        var linesByPayment = lines.GroupBy(l => l.PaymentId).ToDictionary(g => g.Key, g => g.ToList());

        return payments
            .OrderByDescending(p => p.PaymentDate)
            .Select(p =>
            {
                linesByPayment.TryGetValue(p.Id, out var paymentLines);
                paymentLines ??= [];
                var feeLabels = paymentLines
                    .Select(l => feeMap.TryGetValue(l.FeeTypeId, out var name) ? name : null)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var primaryFeeTypeId = paymentLines.FirstOrDefault()?.FeeTypeId;

                return new ParentPaymentDto(
                    p.Id,
                    p.ReceiptNumber,
                    p.PaymentDate,
                    p.TotalAmount,
                    p.Currency,
                    p.Status,
                    feeLabels.Count == 0 ? null : string.Join(", ", feeLabels),
                    primaryFeeTypeId,
                    p.AcademicYearId);
            })
            .ToList();
    }

    public async Task<ParentPaymentSummaryDto> GetChildPaymentSummaryAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var situations = await GetChildFeeSituationsAsync(guardianId, studentId, null, cancellationToken);
        return new ParentPaymentSummaryDto(
            situations.TotalExpected,
            situations.TotalPaid,
            situations.TotalBalance,
            situations.CurrencyLabel,
            situations.FeeTypes.FirstOrDefault()?.Currency is Currency c ? (int)c : (int)Currency.CDF);
    }

    public async Task<ParentFeeSituationsResultDto> GetChildFeeSituationsAsync(
        Guid guardianId,
        Guid studentId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureChildAccessAsync(guardianId, studentId, cancellationToken);

        var student = (await _studentRepository.FindAsync(s => s.Id == studentId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var enrollments = (await _enrollmentRepository.FindAsync(
                e => e.StudentId == studentId && e.IsActive,
                cancellationToken))
            .OrderByDescending(e => e.CreatedAt)
            .ToList();

        var enrollment = academicYearId.HasValue
            ? enrollments.FirstOrDefault(e => e.AcademicYearId == academicYearId.Value)
            : enrollments.FirstOrDefault();

        if (enrollment is null)
        {
            return new ParentFeeSituationsResultDto(
                Guid.Empty,
                "—",
                Currency.CDF.ToString(),
                0,
                0,
                0,
                []);
        }

        var yearId = enrollment.AcademicYearId;
        var year = (await _academicYearRepository.FindAsync(y => y.Id == yearId, cancellationToken)).FirstOrDefault();
        var yearLabel = year?.Label ?? "—";

        var classRoom = (await _classRoomRepository.FindAsync(
            c => c.Id == enrollment.ClassRoomId, cancellationToken)).FirstOrDefault();
        var pedagogicalClassId = classRoom?.PedagogicalClassId;

        var feeTypeIds = new HashSet<Guid>();
        if (pedagogicalClassId.HasValue && enrollment.FeePricingCategoryId != Guid.Empty)
        {
            var tariffs = await _classFeeAmountRepository.FindAsync(
                a => a.SchoolId == student.SchoolId
                     && a.AcademicYearId == yearId
                     && a.PedagogicalClassId == pedagogicalClassId.Value
                     && a.FeePricingCategoryId == enrollment.FeePricingCategoryId,
                cancellationToken);
            foreach (var tariff in tariffs)
            {
                feeTypeIds.Add(tariff.FeeTypeId);
            }
        }

        var balances = await _balanceRepository.FindAsync(b => b.StudentId == studentId, cancellationToken);
        if (balances.Count > 0)
        {
            var classFeeIds = balances.Select(b => b.ClassFeeAmountId).Distinct().ToList();
            var linkedTariffs = await _classFeeAmountRepository.FindAsync(
                a => classFeeIds.Contains(a.Id) && a.AcademicYearId == yearId,
                cancellationToken);
            foreach (var tariff in linkedTariffs)
            {
                feeTypeIds.Add(tariff.FeeTypeId);
            }
        }

        if (feeTypeIds.Count == 0)
        {
            return new ParentFeeSituationsResultDto(
                yearId,
                yearLabel,
                Currency.CDF.ToString(),
                0,
                0,
                0,
                []);
        }

        var feeTypes = (await _feeTypeRepository.FindAsync(
                f => feeTypeIds.Contains(f.Id) && f.SchoolId == student.SchoolId,
                cancellationToken))
            .OrderBy(f => f.Name)
            .ToList();

        var items = new List<ParentFeeTypeSituationDto>();
        foreach (var feeType in feeTypes)
        {
            var statement = await _statementService.GetStatementForStudentAsync(
                student.SchoolId,
                studentId,
                yearId,
                feeType.Id,
                cancellationToken);

            items.Add(new ParentFeeTypeSituationDto(
                feeType.Id,
                statement.FeeTypeName,
                statement.Currency,
                statement.Currency.ToString(),
                statement.TotalExpected,
                statement.TotalPaid,
                statement.TotalRemaining,
                statement.TotalRemaining <= 0,
                statement.InstallmentSituations
                    .Select(i => new ParentFeeInstallmentSituationDto(
                        i.Number,
                        i.InstallmentName,
                        i.AmountExpected,
                        i.AmountPaid,
                        i.Remaining))
                    .ToList()));
        }

        // KPI agrégés dans la devise dominante (comme le résumé promoteur mono-devise).
        var dominantCurrency = items
            .GroupBy(i => i.Currency)
            .OrderByDescending(g => g.Sum(x => x.AmountExpected))
            .ThenByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var inCurrency = items.Where(i => i.Currency == dominantCurrency).ToList();
        var totalExpected = inCurrency.Sum(i => i.AmountExpected);
        var totalPaid = inCurrency.Sum(i => i.AmountPaid);
        var totalBalance = inCurrency.Sum(i => i.Balance);

        return new ParentFeeSituationsResultDto(
            yearId,
            yearLabel,
            dominantCurrency.ToString(),
            totalExpected,
            totalPaid,
            totalBalance,
            items);
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
                r.IsPublished,
                ResolveMention(r.Percentage, r.Appreciation),
                ResolveDecisionLabel(r.CouncilDecision),
                r.Appreciation))
            .ToList();
    }

    public async Task<ParentGradesOverviewDto> GetChildGradesAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureChildAccessAsync(guardianId, studentId, cancellationToken);

        var enrollment = (await _enrollmentRepository.FindAsync(
                e => e.StudentId == studentId && e.IsActive,
                cancellationToken))
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefault();

        var publishedResults = (await _periodResultRepository.FindAsync(
                r => r.StudentId == studentId && r.IsPublished,
                cancellationToken))
            .OrderBy(r => r.CreatedAt)
            .ToList();

        var latest = publishedResults.LastOrDefault();
        var evolution = publishedResults.Select(r => (double)r.Average).ToList();

        if (enrollment is null)
        {
            return new ParentGradesOverviewDto(
                latest?.Average ?? 0,
                latest?.Rank ?? 0,
                latest?.ClassSize ?? 0,
                evolution,
                []);
        }

        var evaluations = (await _evaluationRepository.FindAsync(
                e => e.ClassRoomId == enrollment.ClassRoomId
                     && e.AcademicYearId == enrollment.AcademicYearId
                     && e.IsPublished,
                cancellationToken))
            .ToList();

        if (evaluations.Count == 0)
        {
            return new ParentGradesOverviewDto(
                latest?.Average ?? 0,
                latest?.Rank ?? 0,
                latest?.ClassSize ?? 0,
                evolution,
                []);
        }

        var evaluationIds = evaluations.Select(e => e.Id).ToList();
        var entries = await _gradeEntryRepository.FindAsync(
            g => g.StudentId == studentId && evaluationIds.Contains(g.EvaluationId),
            cancellationToken);
        var entryByEval = entries.GroupBy(e => e.EvaluationId).ToDictionary(g => g.Key, g => g.First());

        var courseIds = evaluations.Select(e => e.CourseId).Distinct().ToList();
        var courses = await _courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken);
        var courseMap = courses.ToDictionary(c => c.Id);

        var typeIds = evaluations.Select(e => e.EvaluationTypeId).Distinct().ToList();
        var types = typeIds.Count == 0
            ? []
            : await _evaluationTypeRepository.FindAsync(t => typeIds.Contains(t.Id), cancellationToken);
        var typeMap = types.ToDictionary(t => t.Id);

        var subjects = new List<ParentGradeSubjectDto>();
        foreach (var courseGroup in evaluations.GroupBy(e => e.CourseId))
        {
            if (!courseMap.TryGetValue(courseGroup.Key, out var course))
            {
                continue;
            }

            var interrogations = new List<ParentGradeItemDto>();
            var exams = new List<ParentGradeItemDto>();
            var works = new List<ParentGradeItemDto>();
            var scores = new List<(decimal Score, decimal Max)>();

            foreach (var evaluation in courseGroup.OrderBy(e => e.EvaluationDate))
            {
                if (!entryByEval.TryGetValue(evaluation.Id, out var entry) || entry.IsAbsent)
                {
                    continue;
                }

                var type = typeMap.GetValueOrDefault(evaluation.EvaluationTypeId);
                var typeCode = type?.Code ?? "DEVOIR";
                var typeName = type?.Name ?? "—";

                var item = new ParentGradeItemDto(
                    evaluation.Title,
                    entry.Score,
                    evaluation.MaxScore,
                    evaluation.EvaluationDate.ToDateTime(TimeOnly.MinValue),
                    typeName);

                scores.Add((entry.Score, evaluation.MaxScore));
                switch (typeCode)
                {
                    case "INTERRO":
                        interrogations.Add(item);
                        break;
                    case "EXAMEN":
                    case "COMPOSITION":
                        exams.Add(item);
                        break;
                    default:
                        works.Add(item);
                        break;
                }
            }

            if (scores.Count == 0)
            {
                continue;
            }

            var avg = scores.Average(s => s.Max <= 0 ? 0 : (s.Score / s.Max) * course.MaxScore);
            subjects.Add(new ParentGradeSubjectDto(
                course.Name,
                Math.Round(avg, 2),
                course.MaxScore <= 0 ? 20 : course.MaxScore,
                interrogations,
                exams,
                works));
        }

        var general = subjects.Count == 0
            ? latest?.Average ?? 0
            : Math.Round(subjects.Average(s => s.Average), 2);

        return new ParentGradesOverviewDto(
            general,
            latest?.Rank ?? 0,
            latest?.ClassSize ?? 0,
            evolution.Count > 0 ? evolution : subjects.Select(s => (double)s.Average).ToList(),
            subjects.OrderBy(s => s.Name).ToList());
    }

    public async Task<byte[]> ExportChildBulletinPdfAsync(
        Guid guardianId,
        Guid studentId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default)
    {
        await EnsureChildAccessAsync(guardianId, studentId, cancellationToken);

        var student = (await _studentRepository.FindAsync(s => s.Id == studentId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var result = (await _periodResultRepository.FindAsync(
                r => r.StudentId == studentId && r.AcademicPeriodId == academicPeriodId,
                cancellationToken))
            .FirstOrDefault()
            ?? throw new KeyNotFoundException("Bulletin introuvable pour cette période.");

        var period = (await _periodRepository.FindAsync(p => p.Id == academicPeriodId, cancellationToken)).FirstOrDefault();
        var school = (await _schoolRepository.FindAsync(s => s.Id == student.SchoolId, cancellationToken)).FirstOrDefault();
        var classRoom = (await _classRoomRepository.FindAsync(c => c.Id == result.ClassRoomId, cancellationToken)).FirstOrDefault();

        return ParentBulletinPdfGenerator.Generate(
            schoolName: school?.Name ?? "Établissement",
            studentName: StudentDisplayName.Format(student),
            registrationNumber: student.RegistrationNumber,
            className: classRoom?.Name ?? "—",
            periodName: period?.Name ?? "—",
            average: result.Average,
            percentage: result.Percentage,
            rank: result.Rank,
            classSize: result.ClassSize,
            mention: ResolveMention(result.Percentage, result.Appreciation),
            decision: ResolveDecisionLabel(result.CouncilDecision),
            appreciation: result.Appreciation);
    }

    private static string ResolveMention(decimal percentage, string? appreciation)
    {
        if (!string.IsNullOrWhiteSpace(appreciation))
        {
            return appreciation.Trim();
        }

        return percentage switch
        {
            >= 80 => "Grande distinction",
            >= 70 => "Distinction",
            >= 60 => "Satisfaction",
            >= 50 => "Passable",
            _ => "À améliorer"
        };
    }

    private static string ResolveDecisionLabel(ClassCouncilDecision decision) => decision switch
    {
        ClassCouncilDecision.Admis => "Admis",
        ClassCouncilDecision.Ajourne => "Ajourné",
        ClassCouncilDecision.Exclu => "Exclu",
        _ => "En attente"
    };

    public async Task<IReadOnlyList<ParentAttendanceDayDto>> GetChildAttendanceAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureChildAccessAsync(guardianId, studentId, cancellationToken);

        var rows = await _attendanceRepository.FindAsync(a => a.StudentId == studentId, cancellationToken);

        return rows
            .GroupBy(a => a.AttendanceDate)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                // Agrège la journée : absent > retard > présent.
                var dayRows = g.ToList();
                var anyAbsent = dayRows.Any(r => r.Presence == StudentAttendancePresence.Absent);
                var anyLate = dayRows.Any(r => r.Presence == StudentAttendancePresence.Late);
                var status = anyAbsent ? "absent" : anyLate ? "late" : "present";
                var note = dayRows
                    .Select(r => r.Justification)
                    .FirstOrDefault(j => !string.IsNullOrWhiteSpace(j));
                return new ParentAttendanceDayDto(g.Key, status, note);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ParentCommunicationDto>> GetChildCommunicationsAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureChildAccessAsync(guardianId, studentId, cancellationToken);

        var student = (await _studentRepository.FindAsync(s => s.Id == studentId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var now = DateTime.UtcNow;
        var announcements = await _announcementRepository.FindAsync(
            a => a.SchoolId == student.SchoolId
                 && a.IsPublished
                 && (a.ExpiresAt == null || a.ExpiresAt > now)
                 && (a.TargetAudience == "All"
                     || a.TargetAudience == "Parents"
                     || a.TargetAudience == "Guardians"),
            cancellationToken);

        return announcements
            .OrderByDescending(a => a.PublishedAt)
            .Select(a => new ParentCommunicationDto(
                a.Id,
                a.Title,
                "Annonce",
                a.PublishedAt,
                a.Content,
                IsRead: false,
                Attachments: Array.Empty<ParentCommunicationAttachmentDto>()))
            .ToList();
    }

    public async Task<(Stream Stream, string FileName, string MimeType)?> OpenChildPhotoAsync(
        Guid guardianId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureChildAccessAsync(guardianId, studentId, cancellationToken);
        var student = (await _studentRepository.FindAsync(s => s.Id == studentId, cancellationToken)).FirstOrDefault();
        if (student is null || string.IsNullOrWhiteSpace(student.PhotoPath))
        {
            return null;
        }

        var stream = await _dossierStorage.OpenReadAsync(student.PhotoPath, cancellationToken);
        if (stream is null)
        {
            return null;
        }

        var fileName = Path.GetFileName(student.PhotoPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "photo.jpg";
        }

        var mime = GuessMime(fileName);
        return (stream, fileName, mime);
    }

    public async Task<byte[]> ExportChildPaymentReceiptPdfAsync(
        Guid guardianId,
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var payment = (await _paymentRepository.FindAsync(p => p.Id == paymentId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Paiement introuvable.");

        await EnsureChildAccessAsync(guardianId, payment.StudentId, cancellationToken);
        return await _statementService.ExportPdfAsync(payment.SchoolId, paymentId, feeTypeId, cancellationToken);
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

    private static string GuessMime(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };
    }
}
