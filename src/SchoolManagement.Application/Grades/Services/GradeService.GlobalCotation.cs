namespace SchoolManagement.Application.Grades.Services;

using SchoolManagement.Application.Common;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed partial class GradeService
{
    public async Task<GlobalCotationGridDto> GetGlobalCotationGridAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanEnterGrades();

        var year = await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository,
            schoolId,
            academicYearId,
            cancellationToken);

        var classRoom = await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
            _classRoomRepository,
            _pedagogicalClassRepository,
            _yearRepository,
            schoolId,
            classRoomId,
            cancellationToken);

        var pedagogicalMap = await SchoolConfigurationGuards.BuildPedagogicalMapAsync(
            _pedagogicalClassRepository,
            schoolId,
            cancellationToken);
        pedagogicalMap.TryGetValue(classRoom.PedagogicalClassId ?? Guid.Empty, out var ped);

        var section = (await _sectionRepository.FindAsync(
            s => s.Id == classRoom.SectionId && s.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault();

        var classDisplayName = ped is null
            ? classRoom.Name
            : $"{ped.DisplayName} {classRoom.Name}".Trim();

        var periodType = ResolvePeriodType(ped?.Program, section?.Cycle);
        var yearPeriods = (await _periodRepository.FindAsync(
            p => p.AcademicYearId == year.Id,
            cancellationToken)).ToList();
        var openPeriod = ResolveActiveCotationPeriod(yearPeriods, periodType)
            ?? throw new DomainException(
                "Aucune sous-période n'est ouverte pour cette classe. Cotation globale impossible.");

        var accessScope = ResolveCotationAccessScope();
        var allAssignments = (await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == year.Id
                 && a.ClassRoomId == classRoomId
                 && a.IsActive,
            cancellationToken)).ToList();

        var scoped = FilterAssignmentsByScope(allAssignments, teacherId, accessScope)
            .Where(a => a.ClassRoomId == classRoomId)
            .ToList();

        if (scoped.Count == 0)
        {
            throw new DomainException(
                "Aucun cours affecté à cet enseignant dans cette classe.");
        }

        var courseIds = scoped.Select(a => a.CourseId).Distinct().ToList();
        var courses = (await _courseRepository.FindAsync(
            c => courseIds.Contains(c.Id),
            cancellationToken)).ToDictionary(c => c.Id);

        var courseColumns = scoped
            .Where(a => courses.ContainsKey(a.CourseId))
            .OrderBy(a => courses[a.CourseId].Name)
            .Select(a => new GlobalCotationCourseColumnDto(
                a.CourseId,
                a.Id,
                courses[a.CourseId].Name,
                a.MaxScore <= 0 ? 20 : a.MaxScore))
            .ToList();

        var enrollments = (await _enrollmentRepository.FindAsync(
            e => e.ClassRoomId == classRoomId
                 && e.AcademicYearId == year.Id
                 && e.IsActive,
            cancellationToken)).ToList();

        var studentIds = enrollments.Select(e => e.StudentId).ToList();
        var students = studentIds.Count == 0
            ? []
            : (await _studentRepository.FindAsync(
                s => s.SchoolId == schoolId && studentIds.Contains(s.Id),
                cancellationToken)).ToDictionary(s => s.Id);

        var studentRows = enrollments
            .Select(e =>
            {
                students.TryGetValue(e.StudentId, out var student);
                var name = student is null
                    ? "—"
                    : StudentDisplayName.Format(student.LastName, student.MiddleName, student.FirstName);
                var reg = student?.RegistrationNumber ?? "—";
                return new
                {
                    e.StudentId,
                    RegistrationNumber = reg,
                    StudentName = name,
                    SortKey = name
                };
            })
            .OrderBy(x => x.SortKey, StringComparer.CurrentCultureIgnoreCase)
            .Select((x, index) => new GlobalCotationStudentRowDto(
                index + 1,
                x.StudentId,
                x.RegistrationNumber,
                x.StudentName))
            .ToList();

        var evaluationTypes = await GetEvaluationTypesAsync(schoolId, cancellationToken);
        if (openPeriod.Kind == AcademicSubPeriodKind.Travail)
        {
            evaluationTypes = evaluationTypes
                .Where(t => !string.Equals(t.Code, "EXAMEN", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(t.Name, "Examen", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return new GlobalCotationGridDto(
            classRoomId,
            classDisplayName,
            section?.Name ?? "—",
            year.Id,
            year.Label,
            openPeriod.Id,
            openPeriod.Name,
            openPeriod.Kind,
            openPeriod.Kind == AcademicSubPeriodKind.Examen ? "Examen" : "Travaux",
            openPeriod.StartDate,
            openPeriod.EndDate,
            courseColumns,
            studentRows,
            evaluationTypes);
    }

    public async Task<SaveGlobalCotationResultDto> SaveGlobalCotationAsync(
        Guid schoolId,
        SaveGlobalCotationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanEnterGrades();

        if (request.Courses is null || request.Courses.Count == 0)
        {
            throw new DomainException("Aucune note à enregistrer.");
        }

        var coursesWithGrades = request.Courses
            .Where(c => c.Grades is { Count: > 0 })
            .ToList();

        if (coursesWithGrades.Count == 0)
        {
            throw new DomainException(
                "Saisissez au moins une note avant d'enregistrer.");
        }

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

        var period = (await _periodRepository.FindAsync(
            p => p.Id == request.AcademicPeriodId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Sous-période introuvable.");

        if (period.AcademicYearId != request.AcademicYearId)
        {
            throw new DomainException("La sous-période n'appartient pas à l'année scolaire sélectionnée.");
        }

        await _resultValidation.EnsureClassPeriodNotLockedAsync(
            schoolId, request.ClassRoomId, request.AcademicPeriodId, cancellationToken);

        var allowedCourseIds = (await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == request.AcademicYearId
                 && a.ClassRoomId == request.ClassRoomId
                 && a.IsActive,
            cancellationToken))
            .Select(a => a.CourseId)
            .ToHashSet();

        foreach (var courseBlock in coursesWithGrades)
        {
            if (!allowedCourseIds.Contains(courseBlock.CourseId))
            {
                throw new DomainException(
                    "Un cours saisi n'est pas affecté à cette classe pour l'année en cours.");
            }
        }

        var evaluationType = (await _evaluationTypeRepository.FindAsync(
            t => t.Id == request.EvaluationTypeId && t.SchoolId == schoolId && t.IsActive,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Type d'évaluation introuvable.");

        var baseTitle = string.IsNullOrWhiteSpace(request.Title)
            ? evaluationType.Name
            : request.Title.Trim();

        var evaluationsCreated = 0;
        var gradesSaved = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var courseBlock in coursesWithGrades)
            {
                if (courseBlock.MaxScore <= 0)
                {
                    throw new DomainException(
                        $"Le maximum doit être supérieur à 0 pour le cours sélectionné.");
                }

                foreach (var grade in courseBlock.Grades)
                {
                    if (!grade.IsAbsent && grade.Score > courseBlock.MaxScore)
                    {
                        throw new DomainException(
                            $"Une note dépasse le maximum /{courseBlock.MaxScore}.");
                    }

                    if (!grade.IsAbsent && grade.Score < 0)
                    {
                        throw new DomainException("Une note est négative.");
                    }
                }

                var createRequest = new CreateEvaluationRequest(
                    request.AcademicYearId,
                    request.AcademicPeriodId,
                    courseBlock.CourseId,
                    request.ClassRoomId,
                    request.EvaluationTypeId,
                    null,
                    baseTitle,
                    1,
                    courseBlock.MaxScore,
                    request.EvaluationDate);

                var beforeIds = (await _evaluationRepository.FindAsync(
                    e => e.ClassRoomId == request.ClassRoomId
                         && e.CourseId == courseBlock.CourseId
                         && e.AcademicPeriodId == request.AcademicPeriodId,
                    ct)).Select(e => e.Id).ToHashSet();

                var evaluation = await CreateEvaluationAsync(schoolId, createRequest, ct);
                if (!beforeIds.Contains(evaluation.Id))
                {
                    evaluationsCreated++;
                }

                // Si Create a renvoyé une évaluation existante avec un autre MaxScore, aligner.
                if (evaluation.MaxScore != courseBlock.MaxScore && evaluation.IsOpen)
                {
                    await UpdateEvaluationAsync(
                        schoolId,
                        evaluation.Id,
                        new UpdateEvaluationRequest(
                            evaluation.Title,
                            evaluation.EvaluationDate,
                            courseBlock.MaxScore),
                        ct);
                }

                var inputs = courseBlock.Grades
                    .Select(g => new GradeEntryInput(
                        g.StudentId,
                        g.IsAbsent ? 0 : g.Score,
                        g.IsAbsent,
                        g.Comment))
                    .ToList();

                await SubmitGradesInternalAsync(
                    schoolId,
                    new SubmitGradesRequest(evaluation.Id, inputs),
                    recalculatePeriodResults: false,
                    ct);

                gradesSaved += inputs.Count;
            }
        }, cancellationToken);

        await RecalculatePeriodResultsAfterDataChangeAsync(
            schoolId,
            new CalculatePeriodResultsRequest(
                request.ClassRoomId,
                request.AcademicYearId,
                request.AcademicPeriodId),
            cancellationToken);

        return new SaveGlobalCotationResultDto(evaluationsCreated, gradesSaved);
    }

    public async Task<IReadOnlyList<GlobalCotationSessionSummaryDto>> GetGlobalCotationSessionsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanEnterGrades();

        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository, schoolId, academicYearId, cancellationToken);
        await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
            _classRoomRepository, _pedagogicalClassRepository, _yearRepository,
            schoolId, classRoomId, cancellationToken);

        var allowedCourseIds = await ResolveScopedCourseIdsAsync(
            schoolId, academicYearId, classRoomId, teacherId, cancellationToken);

        if (allowedCourseIds.Count == 0)
        {
            return [];
        }

        var evaluations = (await _evaluationRepository.FindAsync(
            e => e.ClassRoomId == classRoomId
                 && e.AcademicPeriodId == academicPeriodId
                 && allowedCourseIds.Contains(e.CourseId),
            cancellationToken)).ToList();

        if (evaluations.Count == 0)
        {
            return [];
        }

        var typeIds = evaluations.Select(e => e.EvaluationTypeId).Distinct().ToList();
        var types = (await _evaluationTypeRepository.FindAsync(
            t => typeIds.Contains(t.Id),
            cancellationToken)).ToDictionary(t => t.Id);

        var evalIds = evaluations.Select(e => e.Id).ToList();
        var grades = (await _gradeRepository.FindAsync(
            g => evalIds.Contains(g.EvaluationId),
            cancellationToken)).ToList();
        var gradeCountByEval = grades
            .GroupBy(g => g.EvaluationId)
            .ToDictionary(g => g.Key, g => g.Count());

        return evaluations
            .GroupBy(e => (
                e.EvaluationTypeId,
                TitleKey: e.Title.Trim().ToUpperInvariant()))
            .Select(g =>
            {
                var sample = g.OrderByDescending(e => e.EvaluationDate).First();
                types.TryGetValue(sample.EvaluationTypeId, out var type);
                var typeName = type?.Name ?? "—";
                var title = sample.Title.Trim();
                var courseCount = g.Select(e => e.CourseId).Distinct().Count();
                var graded = g.Sum(e => gradeCountByEval.GetValueOrDefault(e.Id));
                var canEdit = g.All(e => e.IsOpen);
                var display =
                    $"{typeName} — {title} ({sample.EvaluationDate:dd/MM/yyyy}) · {courseCount} cours";

                return new GlobalCotationSessionSummaryDto(
                    sample.EvaluationTypeId,
                    typeName,
                    title,
                    sample.EvaluationDate,
                    courseCount,
                    graded,
                    canEdit,
                    display);
            })
            .OrderByDescending(s => s.EvaluationDate)
            .ThenBy(s => s.DisplayLabel, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<GlobalCotationSessionLoadDto> LoadGlobalCotationSessionAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid teacherId,
        Guid evaluationTypeId,
        string title,
        CancellationToken cancellationToken = default)
    {
        EnsureCanEnterGrades();

        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository, schoolId, academicYearId, cancellationToken);
        await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
            _classRoomRepository, _pedagogicalClassRepository, _yearRepository,
            schoolId, classRoomId, cancellationToken);

        var allowedCourseIds = await ResolveScopedCourseIdsAsync(
            schoolId, academicYearId, classRoomId, teacherId, cancellationToken);

        var titleKey = (title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(titleKey))
        {
            throw new DomainException("Libellé d'évaluation requis.");
        }

        var evaluations = (await _evaluationRepository.FindAsync(
            e => e.ClassRoomId == classRoomId
                 && e.AcademicPeriodId == academicPeriodId
                 && e.EvaluationTypeId == evaluationTypeId
                 && allowedCourseIds.Contains(e.CourseId),
            cancellationToken))
            .Where(e => string.Equals(e.Title.Trim(), titleKey, StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.CourseId)
            .Select(g => g.OrderByDescending(e => e.EvaluationDate).First())
            .ToList();

        if (evaluations.Count == 0)
        {
            throw new DomainException("Aucune évaluation trouvée pour cette sélection.");
        }

        var evalIds = evaluations.Select(e => e.Id).ToList();
        var grades = (await _gradeRepository.FindAsync(
            g => evalIds.Contains(g.EvaluationId),
            cancellationToken)).ToList();
        var gradesByEval = grades
            .GroupBy(g => g.EvaluationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var canEdit = evaluations.All(e => e.IsOpen);
        string? readOnlyReason = canEdit
            ? null
            : "Au moins une évaluation de cette vague est fermée : consultation seule.";

        var courses = evaluations
            .Select(e =>
            {
                gradesByEval.TryGetValue(e.Id, out var evalGrades);
                evalGrades ??= [];
                return new GlobalCotationSessionCourseLoadDto(
                    e.CourseId,
                    e.Id,
                    e.MaxScore,
                    e.IsOpen,
                    evalGrades.Select(g => new GlobalCotationSessionGradeDto(
                        g.StudentId,
                        g.Score,
                        g.IsAbsent,
                        g.Comment)).ToList());
            })
            .ToList();

        var sample = evaluations.OrderByDescending(e => e.EvaluationDate).First();
        return new GlobalCotationSessionLoadDto(
            evaluationTypeId,
            sample.Title.Trim(),
            sample.EvaluationDate,
            canEdit,
            readOnlyReason,
            courses);
    }

    private async Task<HashSet<Guid>> ResolveScopedCourseIdsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid teacherId,
        CancellationToken cancellationToken)
    {
        var accessScope = ResolveCotationAccessScope();
        var allAssignments = (await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == academicYearId
                 && a.ClassRoomId == classRoomId
                 && a.IsActive,
            cancellationToken)).ToList();

        return FilterAssignmentsByScope(allAssignments, teacherId, accessScope)
            .Where(a => a.ClassRoomId == classRoomId)
            .Select(a => a.CourseId)
            .ToHashSet();
    }
}
