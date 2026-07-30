namespace SchoolManagement.Application.Grades.Services;



using SchoolManagement.Application.Common;

using SchoolManagement.Application.Common.Interfaces;

using SchoolManagement.Application.Grades.DTOs;

using SchoolManagement.Application.Grades.Interfaces;

using SchoolManagement.Application.Schools;

using SchoolManagement.Application.Auth.Interfaces;

using SchoolManagement.Domain.Entities.Academic;

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

    private readonly IRepository<UserAccount> _userRepository;

    private readonly IPasswordHasher _passwordHasher;

    private readonly ICurrentUserService _currentUser;

    private readonly IUnitOfWork _unitOfWork;



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

        IRepository<UserAccount> userRepository,

        IPasswordHasher passwordHasher,

        ICurrentUserService currentUser,

        IUnitOfWork unitOfWork)

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

        _userRepository = userRepository;

        _passwordHasher = passwordHasher;

        _currentUser = currentUser;

        _unitOfWork = unitOfWork;

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

    {

        var evaluation = (await _evaluationRepository.FindAsync(e => e.Id == request.EvaluationId, cancellationToken)).FirstOrDefault()

            ?? throw new KeyNotFoundException("Évaluation introuvable.");



        if (!evaluation.IsOpen)

        {

            throw new DomainException("Cette évaluation est fermée.");

        }



        foreach (var input in request.Grades)

        {

            if (input.Score > evaluation.MaxScore)

            {

                throw new DomainException($"La note ne peut pas dépasser {evaluation.MaxScore}.");

            }



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



        var enrollments = await _enrollmentRepository.FindAsync(

            e => e.ClassRoomId == request.ClassRoomId && e.AcademicYearId == request.AcademicYearId && e.IsActive,

            cancellationToken);



        var studentIds = enrollments.Select(e => e.StudentId).ToList();

        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);

        var studentMap = students.Where(s => studentIds.Contains(s.Id)).ToDictionary(s => s.Id);



        var evaluations = await _evaluationRepository.FindAsync(

            e => e.ClassRoomId == request.ClassRoomId && e.AcademicPeriodId == request.AcademicPeriodId, cancellationToken);



        var evaluationIds = evaluations.Select(e => e.Id).ToList();

        var allGrades = await _gradeRepository.FindAsync(g => evaluationIds.Contains(g.EvaluationId), cancellationToken);

        var courseIds = evaluations.Select(e => e.CourseId).Distinct().ToList();

        var courses = courseIds.Count == 0

            ? []

            : await _courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken);

        var courseMap = courses.ToDictionary(c => c.Id);



        var averages = new Dictionary<Guid, decimal>();



        foreach (var studentId in studentIds)

        {

            decimal weightedSum = 0;

            decimal totalWeight = 0;



            foreach (var evaluation in evaluations)

            {

                if (evaluation.EnrollmentId is Guid enrollmentId)

                {

                    var enrollment = enrollments.FirstOrDefault(e => e.Id == enrollmentId);

                    if (enrollment is null || enrollment.StudentId != studentId)

                    {

                        continue;

                    }

                }



                var grade = allGrades.FirstOrDefault(g => g.EvaluationId == evaluation.Id && g.StudentId == studentId && !g.IsAbsent);

                if (grade is null)

                {

                    continue;

                }



                var coefficient = courseMap.GetValueOrDefault(evaluation.CourseId)?.Coefficient ?? 1;

                weightedSum += grade.Score * evaluation.Weight * coefficient;

                totalWeight += evaluation.Weight * coefficient;

            }



            averages[studentId] = totalWeight > 0 ? Math.Round(weightedSum / totalWeight, 2) : 0;

        }



        var ranked = averages.OrderByDescending(a => a.Value).ToList();

        var classSize = ranked.Count;

        var results = new List<PeriodResultDto>();



        for (var i = 0; i < ranked.Count; i++)

        {

            var studentId = ranked[i].Key;

            var average = ranked[i].Value;

            var percentage = Math.Round(average / 20m * 100m, 2);

            var rank = i + 1;



            studentMap.TryGetValue(studentId, out var student);

            var name = StudentDisplayName.FormatOrDefault(student);



            var existing = await _periodResultRepository.FindAsync(

                p => p.StudentId == studentId && p.AcademicPeriodId == request.AcademicPeriodId, cancellationToken);



            if (existing.Count > 0)

            {

                var pr = existing[0];

                pr.Average = average;

                pr.Percentage = percentage;

                pr.Rank = rank;

                pr.ClassSize = classSize;

                await _periodResultRepository.UpdateAsync(pr, cancellationToken);

            }

            else

            {

                await _periodResultRepository.AddAsync(new PeriodResult

                {

                    StudentId = studentId,

                    AcademicYearId = request.AcademicYearId,

                    AcademicPeriodId = request.AcademicPeriodId,

                    ClassRoomId = request.ClassRoomId,

                    Average = average,

                    Percentage = percentage,

                    Rank = rank,

                    ClassSize = classSize

                }, cancellationToken);

            }



            results.Add(new PeriodResultDto(studentId, name, average, percentage, rank, classSize, null, ClassCouncilDecision.EnAttente));

        }



        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return results;

    }



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

