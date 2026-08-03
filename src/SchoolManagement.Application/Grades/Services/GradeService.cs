namespace SchoolManagement.Application.Grades.Services;



using SchoolManagement.Application.Common;

using SchoolManagement.Application.Common.Interfaces;

using SchoolManagement.Application.Grades.Calculation;

using SchoolManagement.Application.Grades.DTOs;

using SchoolManagement.Application.Grades.Interfaces;

using SchoolManagement.Application.Notifications.Interfaces;

using SchoolManagement.Application.ResultValidation.Interfaces;

using SchoolManagement.Application.Schools;

using SchoolManagement.Application.Auth.Interfaces;

using SchoolManagement.Domain.Entities.Academic;

using SchoolManagement.Domain.Entities.Deliberation;

using SchoolManagement.Domain.Entities.Grades;

using SchoolManagement.Domain.Entities.Settings;

using SchoolManagement.Domain.Entities.Security;

using SchoolManagement.Domain.Entities.Students;

using SchoolManagement.Domain.Enums;

using SchoolManagement.Domain.Exceptions;

using SchoolManagement.Shared.Constants;



public sealed partial class GradeService : IGradeService

{

    private readonly IRepository<Evaluation> _evaluationRepository;

    private readonly IRepository<GradeEntry> _gradeRepository;

    private readonly IRepository<PeriodResult> _periodResultRepository;

    private readonly IRepository<Course> _courseRepository;

    private readonly IRepository<PedagogicalClassCourse> _pedagogicalClassCourseRepository;

    private readonly IRepository<ClassRoom> _classRoomRepository;

    private readonly IRepository<AcademicYear> _yearRepository;

    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;

    private readonly IRepository<Student> _studentRepository;

    private readonly IRepository<Enrollment> _enrollmentRepository;

    private readonly IRepository<CourseAssignment> _courseAssignmentRepository;

    private readonly IRepository<EvaluationTypeDefinition> _evaluationTypeRepository;

    private readonly IRepository<Teacher> _teacherRepository;

    private readonly IRepository<Section> _sectionRepository;

    private readonly IRepository<AcademicPeriod> _periodRepository;

    private readonly IRepository<AcademicMainPeriod> _mainPeriodRepository;

    private readonly IRepository<UserAccount> _userRepository;

    private readonly IRepository<ResultMentionDefinition> _mentionRepository;

    private readonly IRepository<PedagogicalBonusPoint> _bonusRepository;

    private readonly IPasswordHasher _passwordHasher;

    private readonly ICurrentUserService _currentUser;

    private readonly IUnitOfWork _unitOfWork;

    private readonly IResultCalculationService _resultCalculation;

    private readonly IResultValidationService _resultValidation;

    private readonly INotificationService _notifications;



    public GradeService(

        IRepository<Evaluation> evaluationRepository,

        IRepository<GradeEntry> gradeRepository,

        IRepository<PeriodResult> periodResultRepository,

        IRepository<Course> courseRepository,

        IRepository<PedagogicalClassCourse> pedagogicalClassCourseRepository,

        IRepository<ClassRoom> classRoomRepository,

        IRepository<AcademicYear> yearRepository,

        IRepository<PedagogicalClass> pedagogicalClassRepository,

        IRepository<Student> studentRepository,

        IRepository<Enrollment> enrollmentRepository,

        IRepository<CourseAssignment> courseAssignmentRepository,

        IRepository<EvaluationTypeDefinition> evaluationTypeRepository,

        IRepository<Teacher> teacherRepository,

        IRepository<Section> sectionRepository,

        IRepository<AcademicPeriod> periodRepository,

        IRepository<AcademicMainPeriod> mainPeriodRepository,

        IRepository<UserAccount> userRepository,

        IRepository<ResultMentionDefinition> mentionRepository,

        IRepository<PedagogicalBonusPoint> bonusRepository,

        IPasswordHasher passwordHasher,

        ICurrentUserService currentUser,

        IUnitOfWork unitOfWork,

        IResultCalculationService resultCalculation,

        IResultValidationService resultValidation,

        INotificationService notifications)

    {

        _evaluationRepository = evaluationRepository;

        _gradeRepository = gradeRepository;

        _periodResultRepository = periodResultRepository;

        _courseRepository = courseRepository;

        _pedagogicalClassCourseRepository = pedagogicalClassCourseRepository;

        _classRoomRepository = classRoomRepository;

        _yearRepository = yearRepository;

        _pedagogicalClassRepository = pedagogicalClassRepository;

        _studentRepository = studentRepository;

        _enrollmentRepository = enrollmentRepository;

        _courseAssignmentRepository = courseAssignmentRepository;

        _evaluationTypeRepository = evaluationTypeRepository;

        _teacherRepository = teacherRepository;

        _sectionRepository = sectionRepository;

        _periodRepository = periodRepository;

        _mainPeriodRepository = mainPeriodRepository;

        _userRepository = userRepository;

        _mentionRepository = mentionRepository;

        _bonusRepository = bonusRepository;

        _passwordHasher = passwordHasher;

        _currentUser = currentUser;

        _unitOfWork = unitOfWork;

        _resultCalculation = resultCalculation;

        _resultValidation = resultValidation;

        _notifications = notifications;

    }



    public async Task<IReadOnlyList<EvaluationTypeDto>> GetEvaluationTypesAsync(

        Guid schoolId,

        CancellationToken cancellationToken = default)

    {

        var types = await _evaluationTypeRepository.FindAsync(

            t => t.SchoolId == schoolId && t.IsActive,

            cancellationToken);



        return types

            .OrderBy(t => t.Code)

            .Select(t => new EvaluationTypeDto(t.Id, t.Code, t.Name, t.IsActive))

            .ToList();

    }



    public async Task<EvaluationDto> CreateEvaluationAsync(

        Guid schoolId,

        CreateEvaluationRequest request,

        CancellationToken cancellationToken = default)

    {

        var course = await SchoolCourseScope.GetCourseAsync(

            _courseRepository,

            _pedagogicalClassCourseRepository,

            schoolId,

            request.CourseId,

            cancellationToken)

            ?? throw new KeyNotFoundException("Cours introuvable.");



        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(

            _yearRepository,

            schoolId,

            request.AcademicYearId,

            cancellationToken);



        var classRoom = await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(

            _classRoomRepository,

            _pedagogicalClassRepository,

            _yearRepository,

            schoolId,

            request.ClassRoomId,

            cancellationToken);



        var evaluationType = (await _evaluationTypeRepository.FindAsync(

            t => t.Id == request.EvaluationTypeId && t.SchoolId == schoolId && t.IsActive,

            cancellationToken)).FirstOrDefault()

            ?? throw new KeyNotFoundException("Type d'évaluation introuvable.");



        // Moteur pédagogique : rattachement automatique à la période ouverte via la date.
        var period = (await _periodRepository.FindAsync(
            p => p.Id == request.AcademicPeriodId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Sous-période introuvable.");

        await _resultValidation.EnsureClassPeriodNotLockedAsync(
            schoolId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);

        if (period.MainPeriodId.HasValue)
        {
            if (period.Status != AcademicSubPeriodStatus.Ouverte)
            {
                throw new DomainException(
                    $"La sous-période « {period.Name} » n'est pas ouverte. Saisie impossible.");
            }

            if (period.StartDate is null || period.EndDate is null)
            {
                throw new DomainException(
                    $"La sous-période « {period.Name} » n'a pas de dates renseignées. Saisie impossible.");
            }

            if (request.EvaluationDate < period.StartDate.Value || request.EvaluationDate > period.EndDate.Value)
            {
                throw new DomainException(
                    $"La date de l'évaluation ({request.EvaluationDate:dd/MM/yyyy}) doit appartenir " +
                    $"à la période ouverte « {period.Name} » " +
                    $"({period.StartDate:dd/MM/yyyy} → {period.EndDate:dd/MM/yyyy}).");
            }

            if (period.Kind == AcademicSubPeriodKind.Examen)
            {
                var examExisting = (await _evaluationRepository.FindAsync(
                    e => e.AcademicYearId == request.AcademicYearId
                         && e.ClassRoomId == request.ClassRoomId
                         && e.CourseId == request.CourseId
                         && e.AcademicPeriodId == period.Id,
                    cancellationToken)).FirstOrDefault();
                if (examExisting is not null)
                {
                    return MapEvaluation(examExisting, course.Name, classRoom.Name, evaluationType.Name);
                }
            }
            else if (period.MaxEvaluationCount is int maxCount && maxCount > 0)
            {
                var count = (await _evaluationRepository.FindAsync(
                    e => e.AcademicYearId == request.AcademicYearId
                         && e.ClassRoomId == request.ClassRoomId
                         && e.CourseId == request.CourseId
                         && e.AcademicPeriodId == period.Id,
                    cancellationToken)).Count;
                if (count >= maxCount)
                {
                    throw new DomainException(
                        $"Nombre maximal d'évaluations atteint pour « {period.Name} » ({maxCount}).");
                }
            }
        }

        var resolvedPeriodId = period.Id;
        var normalizedTitle = request.Title.Trim();

        var existingEvaluation = (await _evaluationRepository.FindAsync(
            e => e.AcademicYearId == request.AcademicYearId
                 && e.ClassRoomId == request.ClassRoomId
                 && e.CourseId == request.CourseId
                 && e.AcademicPeriodId == resolvedPeriodId
                 && e.EvaluationTypeId == request.EvaluationTypeId,
            cancellationToken))
            .FirstOrDefault(e => string.Equals(e.Title.Trim(), normalizedTitle, StringComparison.OrdinalIgnoreCase));

        if (existingEvaluation is not null)
        {
            return MapEvaluation(existingEvaluation, course.Name, classRoom.Name, evaluationType.Name);
        }

        var courseAssignment = (await _courseAssignmentRepository.FindAsync(

            a => a.CourseId == request.CourseId

                 && a.ClassRoomId == request.ClassRoomId

                 && a.AcademicYearId == request.AcademicYearId

                 && a.IsActive,

            cancellationToken)).FirstOrDefault()

            ?? throw new DomainException("Aucune affectation de cours trouvée pour cette classe.");



        if (request.EnrollmentId is Guid enrollmentId)

        {

            var enrollment = (await _enrollmentRepository.FindAsync(

                e => e.Id == enrollmentId

                     && e.ClassRoomId == request.ClassRoomId

                     && e.AcademicYearId == request.AcademicYearId

                     && e.IsActive,

                cancellationToken)).FirstOrDefault()

                ?? throw new KeyNotFoundException("Inscription introuvable pour cette classe.");

        }



        var evaluation = new Evaluation

        {

            EnrollmentId = request.EnrollmentId,

            AcademicYearId = request.AcademicYearId,

            AcademicPeriodId = resolvedPeriodId,

            CourseAssignmentId = courseAssignment.Id,

            EvaluationTypeId = evaluationType.Id,

            CourseId = request.CourseId,

            ClassRoomId = request.ClassRoomId,

            Title = normalizedTitle,

            Weight = request.Weight,

            MaxScore = request.MaxScore > 0 ? request.MaxScore : period.MaxScore,

            EvaluationDate = request.EvaluationDate,

            IsOpen = true

        };



        await _evaluationRepository.AddAsync(evaluation, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);



        return MapEvaluation(evaluation, course.Name, classRoom.Name, evaluationType.Name);

    }



    public async Task<IReadOnlyList<EvaluationDto>> GetEvaluationsByClassAsync(

        Guid schoolId,

        Guid classRoomId,

        Guid academicPeriodId,

        CancellationToken cancellationToken = default)

    {

        var classRoom = await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(

            _classRoomRepository,

            _pedagogicalClassRepository,

            _yearRepository,

            schoolId,

            classRoomId,

            cancellationToken);



        var evaluations = await _evaluationRepository.FindAsync(

            e => e.ClassRoomId == classRoomId && e.AcademicPeriodId == academicPeriodId, cancellationToken);



        var courseIds = evaluations.Select(e => e.CourseId).Distinct().ToList();

        var courses = courseIds.Count == 0

            ? []

            : await _courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken);

        var courseMap = courses.ToDictionary(c => c.Id);



        var typeIds = evaluations.Select(e => e.EvaluationTypeId).Distinct().ToList();

        var types = typeIds.Count == 0

            ? []

            : await _evaluationTypeRepository.FindAsync(t => typeIds.Contains(t.Id), cancellationToken);

        var typeMap = types.ToDictionary(t => t.Id);

        var evaluationIds = evaluations.Select(e => e.Id).ToList();
        var gradeCounts = evaluationIds.Count == 0
            ? new Dictionary<Guid, int>()
            : (await _gradeRepository.FindAsync(g => evaluationIds.Contains(g.EvaluationId), cancellationToken))
                .GroupBy(g => g.EvaluationId)
                .ToDictionary(g => g.Key, g => g.Count());

        var studentCount = (await _enrollmentRepository.FindAsync(
            e => e.ClassRoomId == classRoomId
                 && e.AcademicYearId == classRoom.AcademicYearId
                 && e.IsActive,
            cancellationToken)).Count;

        return evaluations
            .OrderByDescending(e => e.EvaluationDate)
            .Select(e => MapEvaluation(
                e,
                courseMap.GetValueOrDefault(e.CourseId)?.Name ?? "—",
                classRoom.Name,
                typeMap.GetValueOrDefault(e.EvaluationTypeId)?.Name ?? "—",
                gradeCounts.GetValueOrDefault(e.Id),
                studentCount))
            .ToList();
    }

    public async Task<EvaluationDto> UpdateEvaluationAsync(
        Guid schoolId,
        Guid evaluationId,
        UpdateEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var evaluation = (await _evaluationRepository.FindAsync(e => e.Id == evaluationId, cancellationToken))
            .FirstOrDefault()
            ?? throw new KeyNotFoundException("Évaluation introuvable.");

        await EnsureEvaluationBelongsToSchoolAsync(schoolId, evaluation, cancellationToken);
        await EnsurePeriodAllowsMutationAsync(evaluation.AcademicPeriodId, cancellationToken);
        await _resultValidation.EnsureClassPeriodNotLockedAsync(
            schoolId, evaluation.ClassRoomId, evaluation.AcademicPeriodId, cancellationToken);

        var title = request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Le libellé de l'évaluation est obligatoire.");
        }

        var period = (await _periodRepository.FindAsync(p => p.Id == evaluation.AcademicPeriodId, cancellationToken))
            .FirstOrDefault()
            ?? throw new DomainException("Sous-période introuvable.");

        if (period.MainPeriodId.HasValue
            && period.StartDate is DateOnly start
            && period.EndDate is DateOnly end
            && (request.EvaluationDate < start || request.EvaluationDate > end))
        {
            throw new DomainException(
                $"La date de l'évaluation ({request.EvaluationDate:dd/MM/yyyy}) doit appartenir " +
                $"à la période ouverte « {period.Name} » ({start:dd/MM/yyyy} → {end:dd/MM/yyyy}).");
        }

        var duplicate = (await _evaluationRepository.FindAsync(
                e => e.Id != evaluation.Id
                     && e.ClassRoomId == evaluation.ClassRoomId
                     && e.CourseId == evaluation.CourseId
                     && e.AcademicPeriodId == evaluation.AcademicPeriodId
                     && e.EvaluationTypeId == evaluation.EvaluationTypeId,
                cancellationToken))
            .FirstOrDefault(e => string.Equals(e.Title.Trim(), title, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            throw new DomainException(
                $"Une évaluation « {title} » du même type existe déjà pour cette affectation et cette période.");
        }

        evaluation.Title = title;
        evaluation.EvaluationDate = request.EvaluationDate;
        if (request.MaxScore > 0)
        {
            evaluation.MaxScore = request.MaxScore;
        }

        await _evaluationRepository.UpdateAsync(evaluation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var course = await SchoolCourseScope.GetCourseAsync(
            _courseRepository, _pedagogicalClassCourseRepository, schoolId, evaluation.CourseId, cancellationToken);
        var classRoom = (await _classRoomRepository.FindAsync(c => c.Id == evaluation.ClassRoomId, cancellationToken))
            .FirstOrDefault();
        var type = (await _evaluationTypeRepository.FindAsync(t => t.Id == evaluation.EvaluationTypeId, cancellationToken))
            .FirstOrDefault();

        return MapEvaluation(
            evaluation,
            course?.Name ?? "—",
            classRoom?.Name ?? "—",
            type?.Name ?? "—");
    }

    public async Task DeleteEvaluationAsync(
        Guid schoolId,
        Guid evaluationId,
        CancellationToken cancellationToken = default)
    {
        var evaluation = (await _evaluationRepository.FindAsync(e => e.Id == evaluationId, cancellationToken))
            .FirstOrDefault()
            ?? throw new KeyNotFoundException("Évaluation introuvable.");

        await EnsureEvaluationBelongsToSchoolAsync(schoolId, evaluation, cancellationToken);
        await EnsurePeriodAllowsMutationAsync(evaluation.AcademicPeriodId, cancellationToken);
        await _resultValidation.EnsureClassPeriodNotLockedAsync(
            schoolId, evaluation.ClassRoomId, evaluation.AcademicPeriodId, cancellationToken);

        var grades = await _gradeRepository.FindAsync(g => g.EvaluationId == evaluationId, cancellationToken);
        if (grades.Count > 0 && !_currentUser.IsAdministrator)
        {
            throw new DomainException(
                "Suppression impossible : des notes ont déjà été saisies pour cette évaluation.");
        }

        foreach (var grade in grades)
        {
            await _gradeRepository.DeleteAsync(grade, cancellationToken);
        }

        await _evaluationRepository.DeleteAsync(evaluation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureEvaluationBelongsToSchoolAsync(
        Guid schoolId,
        Evaluation evaluation,
        CancellationToken cancellationToken)
    {
        var classRoom = (await _classRoomRepository.FindAsync(
            c => c.Id == evaluation.ClassRoomId && c.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Évaluation introuvable pour cet établissement.");
    }

    private async Task EnsurePeriodAllowsMutationAsync(Guid academicPeriodId, CancellationToken cancellationToken)
    {
        var period = (await _periodRepository.FindAsync(p => p.Id == academicPeriodId, cancellationToken))
            .FirstOrDefault()
            ?? throw new DomainException("Sous-période introuvable.");

        if (period.MainPeriodId.HasValue
            && period.Status is AcademicSubPeriodStatus.Cloturee or AcademicSubPeriodStatus.Verrouillee)
        {
            throw new DomainException(
                $"La sous-période « {period.Name} » est {period.Status}. " +
                "Création, modification et suppression d'évaluations interdites.");
        }

        if (period.MainPeriodId.HasValue && period.Status != AcademicSubPeriodStatus.Ouverte)
        {
            throw new DomainException(
                $"La sous-période « {period.Name} » n'est pas ouverte.");
        }
    }

    private static EvaluationDto MapEvaluation(

        Evaluation e,

        string courseName,

        string classRoomName,

        string evaluationTypeName,

        int gradedCount = 0,

        int studentCount = 0) =>

        new(

            e.Id,

            e.Title,

            e.EvaluationTypeId,

            evaluationTypeName,

            e.EnrollmentId,

            e.CourseAssignmentId,

            e.CourseId,

            courseName,

            e.ClassRoomId,

            classRoomName,

            e.AcademicPeriodId,

            e.Weight,

            e.MaxScore,

            e.EvaluationDate,

            e.IsOpen,

            e.IsPublished,

            gradedCount,

            studentCount);



    public async Task<IReadOnlyList<GradeEntryDto>> GetGradesAsync(

        Guid schoolId,

        Guid evaluationId,

        CancellationToken cancellationToken = default)

    {

        var evaluation = (await _evaluationRepository.FindAsync(e => e.Id == evaluationId, cancellationToken)).FirstOrDefault()

            ?? throw new KeyNotFoundException("Évaluation introuvable.");



        var grades = await _gradeRepository.FindAsync(g => g.EvaluationId == evaluationId, cancellationToken);

        var gradeMap = grades.ToDictionary(g => g.StudentId);



        var enrollments = await _enrollmentRepository.FindAsync(

            e => e.ClassRoomId == evaluation.ClassRoomId

                 && e.AcademicYearId == evaluation.AcademicYearId

                 && e.IsActive,

            cancellationToken);



        if (evaluation.EnrollmentId is Guid enrollmentId)

        {

            enrollments = enrollments.Where(e => e.Id == enrollmentId).ToList();

        }



        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);

        var studentMap = students.ToDictionary(s => s.Id);



        return enrollments

            .Select(e =>

            {

                studentMap.TryGetValue(e.StudentId, out var student);

                var name = StudentDisplayName.FormatOrDefault(student);

                if (gradeMap.TryGetValue(e.StudentId, out var grade))

                {

                    return new GradeEntryDto(grade.Id, grade.StudentId, name, grade.Score, grade.IsAbsent, grade.Comment);

                }



                return new GradeEntryDto(Guid.Empty, e.StudentId, name, 0, false, null);

            })

            .OrderBy(g => g.StudentName)

            .ToList();

    }



    public async Task SubmitGradesAsync(Guid schoolId, SubmitGradesRequest request, CancellationToken cancellationToken = default)
        => await SubmitGradesInternalAsync(schoolId, request, recalculatePeriodResults: true, cancellationToken);

    private async Task SubmitGradesInternalAsync(
        Guid schoolId,
        SubmitGradesRequest request,
        bool recalculatePeriodResults,
        CancellationToken cancellationToken)
    {

        var evaluation = (await _evaluationRepository.FindAsync(e => e.Id == request.EvaluationId, cancellationToken)).FirstOrDefault()

            ?? throw new KeyNotFoundException("Évaluation introuvable.");



        if (!evaluation.IsOpen)

        {

            throw new DomainException("Cette évaluation est fermée.");

        }

        await _resultValidation.EnsureClassPeriodNotLockedAsync(
            schoolId, evaluation.ClassRoomId, evaluation.AcademicPeriodId, cancellationToken);

        var definition = new EvaluationDefinitionInput(
            evaluation.Id,
            evaluation.CourseId,
            "—",
            evaluation.Weight <= 0 ? 1 : evaluation.Weight,
            evaluation.MaxScore,
            evaluation.EnrollmentId);

        var scoreInputs = request.Grades
            .Select(g => ScoreEntryStatusMapper.ToInput(
                evaluation.Id,
                g.StudentId,
                g.Score,
                g.IsAbsent,
                g.Comment))
            .ToList();

        var validation = _resultCalculation.ValidateScores([definition], scoreInputs);
        if (!validation.IsValid)
        {
            throw new DomainException(validation.Issues[0].Message);
        }

        foreach (var input in request.Grades)

        {

            var existing = await _gradeRepository.FindAsync(

                g => g.EvaluationId == request.EvaluationId && g.StudentId == input.StudentId, cancellationToken);



            if (existing.Count > 0)

            {

                var grade = existing[0];

                grade.Score = input.Score;

                grade.IsAbsent = input.IsAbsent;

                grade.Comment = input.Comment;

                await _gradeRepository.UpdateAsync(grade, cancellationToken);

            }

            else

            {

                await _gradeRepository.AddAsync(new GradeEntry

                {

                    EvaluationId = request.EvaluationId,

                    StudentId = input.StudentId,

                    Score = input.Score,

                    IsAbsent = input.IsAbsent,

                    Comment = input.Comment

                }, cancellationToken);

            }

        }



        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var course = await _courseRepository.GetByIdAsync(evaluation.CourseId, cancellationToken);
            var courseName = string.IsNullOrWhiteSpace(course?.Name) ? "un cours" : course!.Name;
            foreach (var studentId in request.Grades.Select(g => g.StudentId).Distinct())
            {
                await _notifications.NotifyStudentParentsAsync(
                    schoolId,
                    studentId,
                    NotificationCategory.Grades,
                    NotificationEventType.GradeRecorded,
                    "📚 Nouvelle cote publiée",
                    $"Les résultats du cours de {courseName} sont disponibles.",
                    dataJson: $"{{\"evaluationId\":\"{evaluation.Id}\",\"studentId\":\"{studentId}\"}}",
                    deepLink: "/parent/notes",
                    cancellationToken: cancellationToken);
            }
        }
        catch
        {
            // Ne jamais faire échouer la saisie de notes si la notification échoue.
        }

        if (recalculatePeriodResults)
        {
            await CalculatePeriodResultsAsync(
                schoolId,
                new CalculatePeriodResultsRequest(
                    evaluation.ClassRoomId,
                    evaluation.AcademicYearId,
                    evaluation.AcademicPeriodId),
                cancellationToken);
        }
    }



    public async Task<IReadOnlyList<PeriodResultDto>> CalculatePeriodResultsAsync(

        Guid schoolId,

        CalculatePeriodResultsRequest request,

        CancellationToken cancellationToken = default)

    {

        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(

            _yearRepository,

            schoolId,

            request.AcademicYearId,

            cancellationToken);



        await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(

            _classRoomRepository,

            _pedagogicalClassRepository,

            _yearRepository,

            schoolId,

            request.ClassRoomId,

            cancellationToken);

        await _resultValidation.EnsureClassPeriodNotLockedAsync(
            schoolId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);

        var enrollments = await _enrollmentRepository.FindAsync(

            e => e.ClassRoomId == request.ClassRoomId && e.AcademicYearId == request.AcademicYearId && e.IsActive,

            cancellationToken);



        var studentIds = enrollments.Select(e => e.StudentId).ToList();

        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);

        var studentMap = students.Where(s => studentIds.Contains(s.Id)).ToDictionary(s => s.Id);



        var evaluations = await _evaluationRepository.FindAsync(

            e => e.ClassRoomId == request.ClassRoomId && e.AcademicPeriodId == request.AcademicPeriodId, cancellationToken);



        var evaluationIds = evaluations.Select(e => e.Id).ToList();

        var allGrades = evaluationIds.Count == 0

            ? Array.Empty<GradeEntry>()

            : await _gradeRepository.FindAsync(g => evaluationIds.Contains(g.EvaluationId), cancellationToken);

        var courseIds = evaluations.Select(e => e.CourseId).Distinct().ToList();

        var courses = courseIds.Count == 0

            ? []

            : await _courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken);

        var courseMap = courses.ToDictionary(c => c.Id);

        var assignments = await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == request.AcademicYearId
                 && a.ClassRoomId == request.ClassRoomId
                 && a.IsActive,
            cancellationToken);
        var assignmentMaxByCourse = assignments
            .GroupBy(a => a.CourseId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var max = g.First().MaxScore;
                    return max > 0 ? max : 0;
                });

        var period = (await _periodRepository.FindAsync(
            p => p.Id == request.AcademicPeriodId,
            cancellationToken)).FirstOrDefault();
        var periodMax = period is { MaxScore: > 0 } ? period.MaxScore : 0;

        var previousResults = await _periodResultRepository.FindAsync(
            p => p.ClassRoomId == request.ClassRoomId && p.AcademicPeriodId == request.AcademicPeriodId,
            cancellationToken);
        var previousByStudent = previousResults.ToDictionary(p => p.StudentId);

        var rules = await CreatePeriodResultRulesAsync(schoolId, cancellationToken);

        var bonuses = await _bonusRepository.FindAsync(
            b => b.SchoolId == schoolId
                 && b.ClassRoomId == request.ClassRoomId
                 && b.AcademicPeriodId == request.AcademicPeriodId
                 && !b.IsCancelled,
            cancellationToken);
        var bonusByStudentCourse = bonuses
            .GroupBy(b => b.StudentId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<Guid, decimal>)g
                    .GroupBy(x => x.CourseId)
                    .ToDictionary(cg => cg.Key, cg => cg.Sum(x => x.PointsAdded)));

        var courseContexts = evaluations
            .GroupBy(e => e.CourseId)
            .Select(g =>
            {
                var courseId = g.Key;
                courseMap.TryGetValue(courseId, out var courseEntity);
                var targetMax = periodMax;
                if (assignmentMaxByCourse.TryGetValue(courseId, out var assignmentMax) && assignmentMax > 0)
                {
                    targetMax = assignmentMax;
                }
                else if (courseEntity is { MaxScore: > 0 })
                {
                    targetMax = courseEntity.MaxScore;
                }

                if (targetMax <= 0)
                {
                    targetMax = g.Max(e => e.MaxScore > 0 ? e.MaxScore : 0);
                }

                return new CourseContextInput(
                    courseId,
                    courseEntity?.Name ?? "—",
                    courseEntity?.Coefficient is > 0 ? courseEntity.Coefficient : 1,
                    targetMax,
                    g.Select(e => new EvaluationDefinitionInput(
                        e.Id,
                        e.CourseId,
                        courseEntity?.Name ?? "—",
                        e.Weight <= 0 ? 1 : e.Weight,
                        e.MaxScore > 0 ? e.MaxScore : (periodMax > 0 ? periodMax : 0),
                        e.EnrollmentId)).ToList());
            })
            .ToList();

        var enrollmentById = enrollments.ToDictionary(e => e.Id);
        var studentInputs = studentIds.Select(studentId =>
        {
            studentMap.TryGetValue(studentId, out var student);
            var name = StudentDisplayName.FormatOrDefault(student);

            var scores = new List<ScoreEntryInput>();
            foreach (var evaluation in evaluations)
            {
                if (evaluation.EnrollmentId is Guid enrollmentId)
                {
                    if (!enrollmentById.TryGetValue(enrollmentId, out var enrollment)
                        || enrollment.StudentId != studentId)
                    {
                        continue;
                    }
                }

                var grade = allGrades.FirstOrDefault(g => g.EvaluationId == evaluation.Id && g.StudentId == studentId);
                if (grade is null)
                {
                    scores.Add(new ScoreEntryInput(evaluation.Id, studentId, null, ScoreEntryStatus.NotGraded));
                    continue;
                }

                scores.Add(ScoreEntryStatusMapper.ToInput(
                    evaluation.Id,
                    studentId,
                    grade.Score,
                    grade.IsAbsent,
                    grade.Comment));
            }

            return new StudentScoresInput(
                studentId,
                name,
                scores,
                bonusByStudentCourse.GetValueOrDefault(studentId));
        }).ToList();

        var previousSnapshots = previousByStudent
            .Select(p => new PreviousCourseResultSnapshot(
                p.Key,
                Guid.Empty,
                p.Value.Average,
                p.Value.Percentage))
            .ToList();

        var recalculation = _resultCalculation.RecalculateClass(
            studentInputs,
            courseContexts,
            rules,
            previousSnapshots);

        var rankingByStudent = recalculation.Ranking.ToDictionary(r => r.StudentId);
        var classSize = recalculation.Statistics.ClassSize;
        var results = new List<PeriodResultDto>();

        foreach (var studentResult in recalculation.Students)
        {
            rankingByStudent.TryGetValue(studentResult.StudentId, out var rankEntry);
            var average = studentResult.Average ?? 0;
            var percentage = studentResult.Percentage ?? 0;
            var rank = rankEntry?.Rank ?? 0;

            if (previousByStudent.TryGetValue(studentResult.StudentId, out var existing))
            {
                existing.Average = average;
                existing.Percentage = percentage;
                existing.Rank = rank;
                existing.ClassSize = classSize;
                existing.Appreciation = studentResult.Mention;
                existing.CouncilDecision = studentResult.Decision;
                await _periodResultRepository.UpdateAsync(existing, cancellationToken);
            }
            else
            {
                await _periodResultRepository.AddAsync(new PeriodResult
                {
                    SchoolId = schoolId,
                    StudentId = studentResult.StudentId,
                    AcademicYearId = request.AcademicYearId,
                    AcademicPeriodId = request.AcademicPeriodId,
                    ClassRoomId = request.ClassRoomId,
                    Average = average,
                    Percentage = percentage,
                    Rank = rank,
                    ClassSize = classSize,
                    Appreciation = studentResult.Mention,
                    CouncilDecision = studentResult.Decision
                }, cancellationToken);
            }

            results.Add(new PeriodResultDto(
                studentResult.StudentId,
                studentResult.StudentName,
                average,
                percentage,
                rank,
                classSize,
                studentResult.Mention,
                studentResult.Decision));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _resultValidation.RecordCalculationAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);
        return results.OrderBy(r => r.Rank).ThenBy(r => r.StudentName).ToList();
    }

    /// <summary>
    /// Règles utilisées pour les PeriodResult (moyenne normalisée + mentions paramétrées).
    /// </summary>
    private async Task<ResultCalculationRules> CreatePeriodResultRulesAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        var defaults = ResultCalculationRules.CreateDefault();
        var mentions = (await _mentionRepository.FindAsync(
            m => m.SchoolId == schoolId && m.IsActive, cancellationToken))
            .OrderByDescending(m => m.MinPercentageInclusive)
            .Select(m => new MentionThreshold(
                m.MinPercentageInclusive,
                m.Label,
                m.MaxPercentageInclusive))
            .ToList();

        // Seed minimal si aucune mention configurée (premier calcul avant délibération).
        if (mentions.Count == 0)
        {
            mentions =
            [
                new MentionThreshold(55m, "Satisfaction", 69m),
                new MentionThreshold(70m, "Distinction", 79m),
                new MentionThreshold(80m, "Grande distinction", 90m),
                new MentionThreshold(91m, "Élite", 100m)
            ];
        }

        return new ResultCalculationRules
        {
            RoundingMode = defaults.RoundingMode,
            CourseAggregationMode = CourseAggregationMode.WeightedNormalized,
            UnjustifiedAbsenceMode = defaults.UnjustifiedAbsenceMode,
            JustifiedAbsenceMode = defaults.JustifiedAbsenceMode,
            ExcusedMode = defaults.ExcusedMode,
            DispensedMode = defaults.DispensedMode,
            Mentions = mentions,
            Decision = defaults.Decision
        };
    }

    private static ResultCalculationRules CreatePedagogicalSheetRules() =>
        ResultCalculationRules.CreateDefault();




    public async Task<IReadOnlyList<PeriodResultDto>> GetPeriodResultsAsync(

        Guid schoolId,

        Guid classRoomId,

        Guid academicPeriodId,

        CancellationToken cancellationToken = default)

    {

        await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(

            _classRoomRepository,

            _pedagogicalClassRepository,

            _yearRepository,

            schoolId,

            classRoomId,

            cancellationToken);



        var periodResults = await _periodResultRepository.FindAsync(

            p => p.ClassRoomId == classRoomId && p.AcademicPeriodId == academicPeriodId,

            cancellationToken);



        if (periodResults.Count == 0)

        {

            return [];

        }



        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);

        var studentMap = students.ToDictionary(s => s.Id);



        return periodResults

            .OrderBy(p => p.Rank)

            .Select(p =>

            {

                studentMap.TryGetValue(p.StudentId, out var student);

                var name = student is null ? "—" : $"{student.LastName} {student.FirstName}";

                return new PeriodResultDto(

                    p.StudentId,

                    name,

                    p.Average,

                    p.Percentage,

                    p.Rank,

                    p.ClassSize,

                    p.Appreciation,

                    p.CouncilDecision);

            })

            .ToList();

    }

    public async Task CalculateResultsForClosedExamAsync(
        Guid schoolId,
        Guid examSubPeriodId,
        CancellationToken cancellationToken = default)
    {
        var period = (await _periodRepository.FindAsync(
            p => p.Id == examSubPeriodId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Sous-période d'examen introuvable.");

        if (period.Kind != AcademicSubPeriodKind.Examen)
        {
            return;
        }

        var evaluations = await _evaluationRepository.FindAsync(
            e => e.AcademicPeriodId == examSubPeriodId,
            cancellationToken);

        var classRoomIds = evaluations.Select(e => e.ClassRoomId).Distinct().ToList();
        foreach (var classRoomId in classRoomIds)
        {
            await CalculatePeriodResultsAsync(
                schoolId,
                new CalculatePeriodResultsRequest(classRoomId, period.AcademicYearId, examSubPeriodId),
                cancellationToken);
        }
    }

}

