using System.Globalization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Grades.Calculation;
using SchoolManagement.Application.Grades.Interfaces;
using SchoolManagement.Application.Mentions;
using SchoolManagement.Application.ResultValidation.DTOs;
using SchoolManagement.Application.ResultValidation.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Deliberation;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Shared.Constants;
using AcademicPeriod = SchoolManagement.Domain.Entities.Settings.AcademicPeriod;
using EnrollmentEntity = SchoolManagement.Domain.Entities.Students.Enrollment;

namespace SchoolManagement.Application.ResultValidation.Services;

public sealed class ResultValidationService : IResultValidationService
{
    private readonly IRepository<ClassPeriodResultValidation> _validationRepository;
    private readonly IRepository<ClassPeriodResultValidationEvent> _eventRepository;
    private readonly IRepository<PeriodResult> _periodResultRepository;
    private readonly IRepository<EnrollmentEntity> _enrollmentRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<AcademicPeriod> _periodRepository;
    private readonly IRepository<CourseAssignment> _courseAssignmentRepository;
    private readonly IRepository<Evaluation> _evaluationRepository;
    private readonly IRepository<GradeEntry> _gradeRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<ResultMentionDefinition> _mentionRepository;
    private readonly IResultCalculationService _resultCalculation;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ResultValidationService(
        IRepository<ClassPeriodResultValidation> validationRepository,
        IRepository<ClassPeriodResultValidationEvent> eventRepository,
        IRepository<PeriodResult> periodResultRepository,
        IRepository<EnrollmentEntity> enrollmentRepository,
        IRepository<Student> studentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<AcademicPeriod> periodRepository,
        IRepository<CourseAssignment> courseAssignmentRepository,
        IRepository<Evaluation> evaluationRepository,
        IRepository<GradeEntry> gradeRepository,
        IRepository<Course> courseRepository,
        IRepository<ResultMentionDefinition> mentionRepository,
        IResultCalculationService resultCalculation,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _validationRepository = validationRepository;
        _eventRepository = eventRepository;
        _periodResultRepository = periodResultRepository;
        _enrollmentRepository = enrollmentRepository;
        _studentRepository = studentRepository;
        _classRoomRepository = classRoomRepository;
        _yearRepository = yearRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _periodRepository = periodRepository;
        _courseAssignmentRepository = courseAssignmentRepository;
        _evaluationRepository = evaluationRepository;
        _gradeRepository = gradeRepository;
        _courseRepository = courseRepository;
        _mentionRepository = mentionRepository;
        _resultCalculation = resultCalculation;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public Task<ResultValidationSheetDto> GetSheetAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default) =>
        BuildSheetAsync(schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);

    public async Task<ResultValidationReadinessDto> GetReadinessReportAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default)
    {
        await EnsureContextAsync(schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);
        return await BuildReadinessAsync(schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);
    }

    public async Task<ResultValidationSheetDto> ValidateAsync(
        Guid schoolId,
        ResultValidationActionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanValidate();
        var readiness = await GetReadinessReportAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);
        if (!readiness.IsReady)
        {
            var detail = string.Join(" | ", readiness.Issues.Select(i => i.Message));
            throw new DomainException(
                "Validation impossible : des contrôles ont échoué. " + detail);
        }

        var entity = await GetOrCreateValidationAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);

        if (entity.Status == ResultValidationStatus.Verrouille)
        {
            throw new DomainException("Les résultats sont verrouillés ; validation impossible.");
        }

        if (entity.Status == ResultValidationStatus.Valide)
        {
            throw new DomainException("Les résultats sont déjà validés.");
        }

        var (userId, userName) = ResolveActor();
        entity.Status = ResultValidationStatus.Valide;
        entity.ValidatedAtUtc = DateTime.UtcNow;
        entity.ValidatedByUserId = userId;
        entity.ValidatedByUserName = userName;
        entity.Observations = NormalizeObservations(request.Observations);
        await _validationRepository.UpdateAsync(entity, cancellationToken);
        await AddEventAsync(
            entity,
            ResultValidationOperation.Validation,
            userId,
            userName,
            entity.Observations,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildSheetAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);
    }

    public async Task<ResultValidationSheetDto> CancelValidationAsync(
        Guid schoolId,
        ResultValidationActionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanValidate();
        var (_, _, period) = await EnsureContextAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);

        if (IsPeriodClosed(period))
        {
            throw new DomainException(
                "La période est clôturée ; l'annulation de la validation est interdite.");
        }

        var entity = await FindValidationAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken)
            ?? throw new DomainException("Aucune validation à annuler.");

        if (entity.Status == ResultValidationStatus.Verrouille)
        {
            throw new DomainException(
                "Les résultats sont verrouillés ; annulation de validation interdite.");
        }

        if (entity.Status != ResultValidationStatus.Valide)
        {
            throw new DomainException("Les résultats ne sont pas validés.");
        }

        var (userId, userName) = ResolveActor();
        entity.Status = ResultValidationStatus.NonValide;
        entity.ValidatedAtUtc = null;
        entity.ValidatedByUserId = null;
        entity.ValidatedByUserName = null;
        entity.Observations = NormalizeObservations(request.Observations);
        await _validationRepository.UpdateAsync(entity, cancellationToken);
        await AddEventAsync(
            entity,
            ResultValidationOperation.Annulation,
            userId,
            userName,
            entity.Observations,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildSheetAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);
    }

    public async Task<ResultValidationSheetDto> LockAsync(
        Guid schoolId,
        ResultValidationActionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanLock();
        await EnsureContextAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);

        var entity = await GetOrCreateValidationAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);

        if (entity.Status != ResultValidationStatus.Valide)
        {
            throw new DomainException("Verrouillage autorisé uniquement après validation.");
        }

        var (userId, userName) = ResolveActor();
        entity.Status = ResultValidationStatus.Verrouille;
        entity.LockedAtUtc = DateTime.UtcNow;
        entity.LockedByUserId = userId;
        entity.LockedByUserName = userName;
        entity.Observations = NormalizeObservations(request.Observations);
        await _validationRepository.UpdateAsync(entity, cancellationToken);
        await AddEventAsync(
            entity,
            ResultValidationOperation.Verrouillage,
            userId,
            userName,
            entity.Observations,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildSheetAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);
    }

    public async Task<ResultValidationSheetDto> UnlockAsync(
        Guid schoolId,
        ResultValidationActionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanUnlock();
        await EnsureContextAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);

        var entity = await FindValidationAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken)
            ?? throw new DomainException("Aucun verrouillage à lever.");

        if (entity.Status != ResultValidationStatus.Verrouille)
        {
            throw new DomainException("Les résultats ne sont pas verrouillés.");
        }

        var (userId, userName) = ResolveActor();
        entity.Status = ResultValidationStatus.Valide;
        entity.LockedAtUtc = null;
        entity.LockedByUserId = null;
        entity.LockedByUserName = null;
        entity.Observations = NormalizeObservations(request.Observations);
        await _validationRepository.UpdateAsync(entity, cancellationToken);
        await AddEventAsync(
            entity,
            ResultValidationOperation.Deverrouillage,
            userId,
            userName,
            entity.Observations,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await BuildSheetAsync(
            schoolId, request.AcademicYearId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);
    }

    public async Task RecordCalculationAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOrCreateValidationAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);
        var (userId, userName) = ResolveActor();
        await AddEventAsync(
            entity,
            ResultValidationOperation.CalculEffectue,
            userId,
            userName,
            "Résultats calculés et persistés.",
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureClassPeriodNotLockedAsync(
        Guid schoolId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default)
    {
        var locked = (await _validationRepository.FindAsync(
            v => v.SchoolId == schoolId
                 && v.ClassRoomId == classRoomId
                 && v.AcademicPeriodId == academicPeriodId
                 && v.Status == ResultValidationStatus.Verrouille,
            cancellationToken)).FirstOrDefault();

        if (locked is not null)
        {
            throw new DomainException(
                "Les résultats de cette classe / sous-période sont verrouillés. " +
                "Aucune modification des notes, cotations ou décisions n'est autorisée.");
        }
    }

    private async Task<ResultValidationSheetDto> BuildSheetAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var (year, classRoom, period) = await EnsureContextAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);

        var enrollments = await _enrollmentRepository.FindAsync(
            e => e.ClassRoomId == classRoomId && e.AcademicYearId == academicYearId && e.IsActive,
            cancellationToken);
        var studentIds = enrollments.Select(e => e.StudentId).ToHashSet();
        var students = studentIds.Count == 0
            ? []
            : await _studentRepository.FindAsync(s => studentIds.Contains(s.Id), cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);

        var periodResults = await _periodResultRepository.FindAsync(
            p => p.ClassRoomId == classRoomId && p.AcademicPeriodId == academicPeriodId,
            cancellationToken);

        var validation = await FindValidationAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);
        var status = validation?.Status ?? ResultValidationStatus.NonValide;

        var mentionDefs = (await _mentionRepository.FindAsync(
            m => m.SchoolId == schoolId && m.IsActive, cancellationToken))
            .OrderByDescending(m => m.MinPercentageInclusive)
            .ToList();

        var appreciationDirty = false;
        var rows = periodResults
            .OrderBy(p => p.Rank == 0 ? int.MaxValue : p.Rank)
            .ThenBy(p =>
            {
                studentMap.TryGetValue(p.StudentId, out var st);
                return st is null ? "" : $"{st.LastName} {st.FirstName}";
            }, StringComparer.CurrentCultureIgnoreCase)
            .Select(p =>
            {
                studentMap.TryGetValue(p.StudentId, out var st);
                var name = st is null ? "—" : $"{st.LastName} {st.FirstName}".Trim();
                var matricule = st?.RegistrationNumber ?? "—";
                var mention = MentionLabelResolver.ResolveOrFallback(
                    p.Appreciation, p.Percentage, mentionDefs);
                if (string.IsNullOrWhiteSpace(p.Appreciation) && !string.IsNullOrWhiteSpace(mention))
                {
                    p.Appreciation = mention;
                    appreciationDirty = true;
                }

                return new ResultValidationStudentRowDto(
                    p.StudentId,
                    matricule,
                    name,
                    p.Rank,
                    p.Average,
                    p.Percentage,
                    FormatValue(p.Average),
                    FormatPercentage(p.Percentage),
                    mention,
                    p.CouncilDecision,
                    FormatDecisionLabel(p.CouncilDecision),
                    status,
                    FormatStatusLabel(status));
            })
            .ToList();

        if (appreciationDirty)
        {
            foreach (var periodResult in periodResults.Where(p => !string.IsNullOrWhiteSpace(p.Appreciation)))
            {
                await _periodResultRepository.UpdateAsync(periodResult, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var admitted = rows.Count(r => r.Decision == ClassCouncilDecision.Admis);
        var deferred = rows.Count(r => r.Decision == ClassCouncilDecision.Ajourne);
        var excluded = rows.Count(r => r.Decision == ClassCouncilDecision.Exclu);
        var pending = rows.Count(r => r.Decision == ClassCouncilDecision.EnAttente);
        var studentCount = Math.Max(enrollments.Count, rows.Count);
        var classAverage = rows.Count == 0 ? (decimal?)null : rows.Average(r => r.Average);
        var decided = admitted + deferred + excluded;
        var successRate = decided == 0 ? (decimal?)null : Math.Round(100m * admitted / decided, 2);

        var calculatedAt = periodResults.Count == 0
            ? (DateTime?)null
            : periodResults.Min(p => p.CreatedAt);
        var lastUpdated = periodResults.Count == 0
            ? (DateTime?)null
            : periodResults.Max(p => p.UpdatedAt ?? p.CreatedAt);

        var events = Array.Empty<ResultValidationEventDto>();
        if (validation is not null)
        {
            var storedEvents = await _eventRepository.FindAsync(
                e => e.ValidationId == validation.Id,
                cancellationToken);
            events = storedEvents
                .OrderByDescending(e => e.OccurredAtUtc)
                .Select(e => new ResultValidationEventDto(
                    e.Id,
                    e.Operation,
                    FormatOperationLabel(e.Operation),
                    e.OccurredAtUtc,
                    string.IsNullOrWhiteSpace(e.UserName) ? "—" : e.UserName,
                    e.Observations))
                .ToArray();
        }

        var readiness = await BuildReadinessAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);

        var canValidate = CanValidatePermission()
            && status == ResultValidationStatus.NonValide
            && readiness.IsReady;
        var canCancel = CanValidatePermission()
            && status == ResultValidationStatus.Valide
            && !IsPeriodClosed(period);
        var canLock = CanLockPermission() && status == ResultValidationStatus.Valide;
        var canUnlock = CanUnlockPermission() && status == ResultValidationStatus.Verrouille;

        var classLabel = string.IsNullOrWhiteSpace(classRoom.Name)
            ? classRoom.Code
            : classRoom.Name;

        return new ResultValidationSheetDto(
            year.Id,
            year.Label,
            classRoom.Id,
            classLabel,
            period.Id,
            period.Name,
            status,
            FormatStatusLabel(status),
            new ResultValidationSummaryDto(
                studentCount,
                admitted,
                deferred,
                excluded,
                pending,
                classAverage,
                FormatValue(classAverage),
                successRate,
                successRate is null ? "—" : $"{successRate.Value.ToString("0.##", CultureInfo.CurrentCulture)} %",
                calculatedAt,
                lastUpdated),
            rows,
            events,
            readiness,
            canValidate,
            canCancel,
            canLock,
            canUnlock);
    }

    private async Task<ResultValidationReadinessDto> BuildReadinessAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var issues = new List<ResultValidationIssueDto>();

        var enrollments = await _enrollmentRepository.FindAsync(
            e => e.ClassRoomId == classRoomId && e.AcademicYearId == academicYearId && e.IsActive,
            cancellationToken);

        if (enrollments.Count == 0)
        {
            issues.Add(new ResultValidationIssueDto(
                "NO_ENROLLMENT",
                "Error",
                "Aucun élève inscrit dans cette classe pour l'année sélectionnée."));
            return new ResultValidationReadinessDto(false, false, issues);
        }

        var periodResults = await _periodResultRepository.FindAsync(
            p => p.ClassRoomId == classRoomId && p.AcademicPeriodId == academicPeriodId,
            cancellationToken);
        var resultByStudent = periodResults.ToDictionary(p => p.StudentId);
        var hasCalculated = periodResults.Count > 0;

        if (!hasCalculated)
        {
            issues.Add(new ResultValidationIssueDto(
                "NO_PERIOD_RESULTS",
                "Error",
                "Aucun résultat calculé pour cette classe / sous-période. Calculez les résultats avant validation."));
        }

        var studentIds = enrollments.Select(e => e.StudentId).ToHashSet();
        var students = await _studentRepository.FindAsync(s => studentIds.Contains(s.Id), cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);

        foreach (var enrollment in enrollments)
        {
            studentMap.TryGetValue(enrollment.StudentId, out var student);
            var name = student is null
                ? enrollment.StudentId.ToString()
                : $"{student.LastName} {student.FirstName}".Trim();

            if (!resultByStudent.TryGetValue(enrollment.StudentId, out var result))
            {
                issues.Add(new ResultValidationIssueDto(
                    "MISSING_RESULT",
                    "Error",
                    $"Résultat manquant pour {name}.",
                    enrollment.StudentId));
                continue;
            }

            if (result.Rank <= 0 && result.Average == 0 && result.Percentage == 0)
            {
                issues.Add(new ResultValidationIssueDto(
                    "MISSING_AVERAGE",
                    "Warning",
                    $"Moyenne / rang non renseignés pour {name}.",
                    enrollment.StudentId));
            }
        }

        var assignments = await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == academicYearId && a.ClassRoomId == classRoomId && a.IsActive,
            cancellationToken);
        var courseIds = assignments.Select(a => a.CourseId).Distinct().ToList();
        var courses = courseIds.Count == 0
            ? []
            : await _courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken);
        var courseMap = courses.ToDictionary(c => c.Id);

        var evaluations = await _evaluationRepository.FindAsync(
            e => e.ClassRoomId == classRoomId && e.AcademicPeriodId == academicPeriodId,
            cancellationToken);
        var evaluationsByCourse = evaluations.GroupBy(e => e.CourseId).ToDictionary(g => g.Key, g => g.ToList());

        // Cours affectés sans évaluation sur cette sous-période : information, non bloquant.
        // La validation porte sur les PeriodResult déjà calculés (cours effectivement cotés).
        var coursesWithoutEvaluation = courseIds
            .Where(id => !evaluationsByCourse.ContainsKey(id))
            .Select(id => courseMap.TryGetValue(id, out var c) ? c.Name : id.ToString())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        if (coursesWithoutEvaluation.Count > 0)
        {
            issues.Add(new ResultValidationIssueDto(
                "COURSES_WITHOUT_EVALUATION",
                "Warning",
                $"{coursesWithoutEvaluation.Count} cours affecté(s) sans évaluation sur cette sous-période " +
                $"(non bloquant) : {string.Join(", ", coursesWithoutEvaluation.Take(8))}" +
                (coursesWithoutEvaluation.Count > 8 ? "…" : ".")));
        }

        // Bloquant uniquement si une évaluation existe sans aucune note.
        foreach (var (courseId, courseEvals) in evaluationsByCourse)
        {
            courseMap.TryGetValue(courseId, out var course);
            var courseName = course?.Name ?? courseId.ToString();
            var evalIds = courseEvals.Select(e => e.Id).ToList();
            var grades = await _gradeRepository.FindAsync(
                g => evalIds.Contains(g.EvaluationId),
                cancellationToken);
            if (grades.Count == 0)
            {
                issues.Add(new ResultValidationIssueDto(
                    "MISSING_GRADES",
                    "Error",
                    $"Aucune note saisie pour le cours « {courseName} » alors qu'une évaluation existe.",
                    CourseId: courseId));
            }
        }

        // Contrôle moteur (dry-run, non persisté) — incohérences / incomplets.
        if (evaluations.Count > 0 && enrollments.Count > 0)
        {
            var evaluationIds = evaluations.Select(e => e.Id).ToList();
            var allGrades = await _gradeRepository.FindAsync(
                g => evaluationIds.Contains(g.EvaluationId),
                cancellationToken);
            var gradesByEvalStudent = allGrades
                .GroupBy(g => (g.EvaluationId, g.StudentId))
                .ToDictionary(g => g.Key, g => g.First());

            var period = (await _periodRepository.FindAsync(
                p => p.Id == academicPeriodId, cancellationToken)).FirstOrDefault();
            var periodMax = period is { MaxScore: > 0 } ? period.MaxScore : 0;
            var assignmentMaxByCourse = assignments
                .GroupBy(a => a.CourseId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var max = g.First().MaxScore;
                        return max > 0 ? max : 0;
                    });

            var courseContexts = evaluations
                .GroupBy(e => e.CourseId)
                .Select(g =>
                {
                    var cId = g.Key;
                    courseMap.TryGetValue(cId, out var courseEntity);
                    var targetMax = periodMax;
                    if (targetMax <= 0 && assignmentMaxByCourse.TryGetValue(cId, out var aMax) && aMax > 0)
                    {
                        targetMax = aMax;
                    }

                    if (targetMax <= 0)
                    {
                        targetMax = g.Max(e => e.MaxScore);
                    }

                    var defs = g.Select(e => new EvaluationDefinitionInput(
                        e.Id,
                        e.CourseId,
                        courseEntity?.Name ?? "Cours",
                        e.Weight,
                        e.MaxScore)).ToList();

                    return new CourseContextInput(
                        cId,
                        courseEntity?.Name ?? "Cours",
                        1,
                        targetMax,
                        defs);
                })
                .ToList();

            var studentInputs = enrollments.Select(en =>
            {
                studentMap.TryGetValue(en.StudentId, out var st);
                var name = st is null ? "—" : $"{st.LastName} {st.FirstName}".Trim();
                var scores = new List<ScoreEntryInput>();
                foreach (var evaluation in evaluations)
                {
                    if (gradesByEvalStudent.TryGetValue((evaluation.Id, en.StudentId), out var grade))
                    {
                        scores.Add(ScoreEntryStatusMapper.ToInput(
                            evaluation.Id,
                            en.StudentId,
                            grade.Score,
                            grade.IsAbsent,
                            grade.Comment));
                    }
                    else
                    {
                        scores.Add(new ScoreEntryInput(
                            evaluation.Id, en.StudentId, null, ScoreEntryStatus.NotGraded));
                    }
                }

                return new StudentScoresInput(en.StudentId, name, scores);
            }).ToList();

            var rules = CreatePeriodResultRules();
            var allEvalDefs = courseContexts.SelectMany(c => c.Evaluations).ToList();
            foreach (var student in studentInputs)
            {
                var validation = _resultCalculation.ValidateScores(allEvalDefs, student.Scores, rules);
                foreach (var issue in validation.Issues)
                {
                    issues.Add(new ResultValidationIssueDto(
                        issue.Code,
                        "Error",
                        $"{student.StudentName} : {issue.Message}",
                        student.StudentId));
                }
            }

            var recalc = _resultCalculation.RecalculateClass(studentInputs, courseContexts, rules);
            // Incomplet dry-run = au moins un cours sans moyenne (ex. note manquante).
            // Non bloquant si PeriodResult déjà persisté : la grille de validation s'appuie dessus.
            if (recalc.Statistics.IncompleteStudentCount > 0)
            {
                issues.Add(new ResultValidationIssueDto(
                    "INCOMPLETE_STUDENTS",
                    "Warning",
                    $"Le moteur signale {recalc.Statistics.IncompleteStudentCount} élève(s) avec résultats incomplets (non bloquant si résultats déjà calculés)."));
            }

            foreach (var studentResult in recalc.Students.Where(s => !s.IsComplete))
            {
                issues.Add(new ResultValidationIssueDto(
                    "INCOMPLETE_STUDENT",
                    "Warning",
                    $"Résultat incomplet pour {studentResult.StudentName}.",
                    studentResult.StudentId));
            }

            foreach (var studentResult in recalc.Students)
            {
                foreach (var courseResult in studentResult.CourseResults.Where(c => c.ValidationErrors.Count > 0))
                {
                    foreach (var err in courseResult.ValidationErrors)
                    {
                        issues.Add(new ResultValidationIssueDto(
                            "COURSE_INCOHERENCE",
                            "Error",
                            $"{studentResult.StudentName} / {courseResult.CourseName} : {err}",
                            studentResult.StudentId,
                            courseResult.CourseId));
                    }
                }
            }
        }

        var blocking = issues
            .Where(i => !string.Equals(i.Severity, "Warning", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(i.Severity, "Info", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new ResultValidationReadinessDto(
            blocking.Count == 0 && hasCalculated,
            hasCalculated,
            issues);
    }

    private async Task<(AcademicYear Year, ClassRoom ClassRoom, AcademicPeriod Period)> EnsureContextAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var year = await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository, schoolId, academicYearId, cancellationToken);
        var classRoom = await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
            _classRoomRepository, _pedagogicalClassRepository, _yearRepository,
            schoolId, classRoomId, cancellationToken);

        var period = (await _periodRepository.FindAsync(
            p => p.Id == academicPeriodId, cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Sous-période introuvable.");

        if (period.AcademicYearId != academicYearId)
        {
            throw new DomainException("La sous-période n'appartient pas à l'année scolaire sélectionnée.");
        }

        return (year, classRoom, period);
    }

    private async Task<ClassPeriodResultValidation?> FindValidationAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        return (await _validationRepository.FindAsync(
            v => v.SchoolId == schoolId
                 && v.AcademicYearId == academicYearId
                 && v.ClassRoomId == classRoomId
                 && v.AcademicPeriodId == academicPeriodId,
            cancellationToken)).FirstOrDefault();
    }

    private async Task<ClassPeriodResultValidation> GetOrCreateValidationAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var existing = await FindValidationAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new ClassPeriodResultValidation
        {
            SchoolId = schoolId,
            AcademicYearId = academicYearId,
            ClassRoomId = classRoomId,
            AcademicPeriodId = academicPeriodId,
            Status = ResultValidationStatus.NonValide
        };
        await _validationRepository.AddAsync(created, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return created;
    }

    private async Task AddEventAsync(
        ClassPeriodResultValidation validation,
        ResultValidationOperation operation,
        Guid? userId,
        string userName,
        string? observations,
        CancellationToken cancellationToken)
    {
        await _eventRepository.AddAsync(new ClassPeriodResultValidationEvent
        {
            SchoolId = validation.SchoolId,
            ValidationId = validation.Id,
            Operation = operation,
            UserId = userId,
            UserName = userName,
            OccurredAtUtc = DateTime.UtcNow,
            Observations = NormalizeObservations(observations)
        }, cancellationToken);
    }

    private (Guid? UserId, string UserName) ResolveActor()
    {
        var userId = _currentUser.UserId;
        var userName = string.IsNullOrWhiteSpace(_currentUser.UserName)
            ? "Système"
            : _currentUser.UserName!;
        return (userId, userName);
    }

    private void EnsureCanValidate()
    {
        if (!CanValidatePermission())
        {
            throw new UnauthorizedAccessException(
                "Permission insuffisante pour valider les résultats.");
        }
    }

    private void EnsureCanLock()
    {
        if (!CanLockPermission())
        {
            throw new UnauthorizedAccessException(
                "Permission insuffisante pour verrouiller les résultats.");
        }
    }

    private void EnsureCanUnlock()
    {
        if (!CanUnlockPermission())
        {
            throw new UnauthorizedAccessException(
                "Permission insuffisante pour déverrouiller les résultats.");
        }
    }

    private bool CanValidatePermission() =>
        _currentUser.IsAdministrator
        || _currentUser.HasPermission(Permissions.ResultsValidationValidate)
        || _currentUser.HasPermission(Permissions.AdminFull)
        || HasElevatedRole("DIRECTION", "PREFET", "PROMOTEUR", "ADMIN");

    private bool CanLockPermission() =>
        _currentUser.IsAdministrator
        || _currentUser.HasPermission(Permissions.ResultsValidationLock)
        || _currentUser.HasPermission(Permissions.AdminFull)
        || HasElevatedRole("ADMIN", "PROMOTEUR");

    private bool CanUnlockPermission() =>
        _currentUser.IsAdministrator
        || _currentUser.HasPermission(Permissions.ResultsValidationUnlock)
        || _currentUser.HasPermission(Permissions.AdminFull)
        || HasElevatedRole("ADMIN", "PROMOTEUR");

    private bool HasElevatedRole(params string[] codes) =>
        _currentUser.Roles.Any(r => codes.Contains(r, StringComparer.OrdinalIgnoreCase));

    private static ResultCalculationRules CreatePeriodResultRules()
    {
        var defaults = ResultCalculationRules.CreateDefault();
        return new ResultCalculationRules
        {
            RoundingMode = defaults.RoundingMode,
            CourseAggregationMode = CourseAggregationMode.WeightedNormalized,
            UnjustifiedAbsenceMode = defaults.UnjustifiedAbsenceMode,
            JustifiedAbsenceMode = defaults.JustifiedAbsenceMode,
            ExcusedMode = defaults.ExcusedMode,
            DispensedMode = defaults.DispensedMode,
            Mentions =
            [
                new MentionThreshold(55m, "Satisfaction", 69m),
                new MentionThreshold(70m, "Distinction", 79m),
                new MentionThreshold(80m, "Grande distinction", 90m),
                new MentionThreshold(91m, "Élite", 100m)
            ],
            Decision = defaults.Decision
        };
    }

    private static string? NormalizeObservations(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatValue(decimal? value) =>
        value is null ? "—" : value.Value.ToString("0.##", CultureInfo.CurrentCulture);

    private static string FormatPercentage(decimal value) =>
        $"{value.ToString("0.##", CultureInfo.CurrentCulture)} %";

    private static string FormatDecisionLabel(ClassCouncilDecision decision) =>
        decision switch
        {
            ClassCouncilDecision.Admis => "Admis",
            ClassCouncilDecision.Ajourne => "Ajourné",
            ClassCouncilDecision.Exclu => "Exclu",
            _ => "En attente"
        };

    private static string FormatStatusLabel(ResultValidationStatus status) =>
        status switch
        {
            ResultValidationStatus.Valide => "Validé",
            ResultValidationStatus.Verrouille => "Verrouillé",
            _ => "Non validé"
        };

    private static string FormatOperationLabel(ResultValidationOperation operation) =>
        operation switch
        {
            ResultValidationOperation.CalculEffectue => "Calcul effectué",
            ResultValidationOperation.Validation => "Validation",
            ResultValidationOperation.Annulation => "Annulation",
            ResultValidationOperation.Verrouillage => "Verrouillage",
            ResultValidationOperation.Deverrouillage => "Déverrouillage",
            _ => operation.ToString()
        };

    private static bool IsPeriodClosed(AcademicPeriod period) =>
        period.IsClosed
        || period.Status is AcademicSubPeriodStatus.Cloturee or AcademicSubPeriodStatus.Verrouillee;
}
