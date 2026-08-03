namespace SchoolManagement.Application.Grades.Services;

using System.Globalization;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Grades.Calculation;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using AcademicPeriod = SchoolManagement.Domain.Entities.Settings.AcademicPeriod;

public sealed partial class GradeService
{
    public async Task<PedagogicalSheetContextDto> GetPedagogicalSheetContextAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
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

        var (classDisplayName, expectedType, cycleGroup) = await ResolveClassPeriodContextAsync(
            schoolId,
            classRoom,
            cancellationToken);

        var yearPeriods = (await _periodRepository.FindAsync(
            p => p.AcademicYearId == year.Id && p.MainPeriodId != null,
            cancellationToken))
            .Where(p => MatchesPeriodType(p, expectedType))
            .ToList();

        var mainIds = yearPeriods
            .Where(p => p.MainPeriodId.HasValue)
            .Select(p => p.MainPeriodId!.Value)
            .Distinct()
            .ToList();

        var mains = mainIds.Count == 0
            ? []
            : (await _mainPeriodRepository.FindAsync(
                m => m.SchoolId == schoolId
                     && m.AcademicYearId == year.Id
                     && mainIds.Contains(m.Id),
                cancellationToken))
                .Where(m => m.CycleGroup == cycleGroup || MatchesPeriodTypeName(m.PeriodType, expectedType))
                .OrderBy(m => m.OrderIndex)
                .ToList();

        if (mains.Count == 0 && mainIds.Count > 0)
        {
            mains = (await _mainPeriodRepository.FindAsync(
                m => mainIds.Contains(m.Id),
                cancellationToken))
                .OrderBy(m => m.OrderIndex)
                .ToList();
        }

        var mainOptions = mains
            .Select(m => new PedagogicalSheetPeriodOptionDto(
                m.Id,
                m.Name,
                PedagogicalSheetPeriodMode.MainPeriod,
                null,
                m.OrderIndex))
            .ToList();

        var subOptions = yearPeriods
            .OrderBy(p => p.OrderIndex)
            .ThenBy(p => p.Name)
            .Select(p => new PedagogicalSheetPeriodOptionDto(
                p.Id,
                p.Name,
                PedagogicalSheetPeriodMode.SubPeriod,
                p.Kind == AcademicSubPeriodKind.Examen ? "Examen" : "Travaux",
                p.OrderIndex))
            .ToList();

        var openSub = yearPeriods.FirstOrDefault(p => p.Status == AcademicSubPeriodStatus.Ouverte);
        Guid? defaultMain = openSub?.MainPeriodId
            ?? mainOptions.FirstOrDefault()?.Id;

        return new PedagogicalSheetContextDto(
            classRoomId,
            classDisplayName,
            year.Id,
            year.Label,
            subOptions,
            mainOptions,
            openSub?.Id ?? subOptions.FirstOrDefault()?.Id,
            defaultMain);
    }

    public async Task<PedagogicalSheetDto> GetPedagogicalSheetAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        PedagogicalSheetPeriodMode mode,
        Guid periodId,
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

        var (classDisplayName, expectedType, _) = await ResolveClassPeriodContextAsync(
            schoolId,
            classRoom,
            cancellationToken);

        var yearPeriods = (await _periodRepository.FindAsync(
            p => p.AcademicYearId == year.Id && p.MainPeriodId != null,
            cancellationToken))
            .Where(p => MatchesPeriodType(p, expectedType))
            .ToList();

        IReadOnlyList<AcademicPeriod> includedSubs;
        string selectedLabel;

        if (mode == PedagogicalSheetPeriodMode.SubPeriod)
        {
            var sub = yearPeriods.FirstOrDefault(p => p.Id == periodId)
                ?? throw new DomainException("Sous-période introuvable pour cette classe.");
            includedSubs = [sub];
            selectedLabel = sub.Name;
        }
        else
        {
            var main = (await _mainPeriodRepository.FindAsync(
                m => m.Id == periodId && m.AcademicYearId == year.Id,
                cancellationToken)).FirstOrDefault()
                ?? throw new DomainException("Période principale introuvable.");
            includedSubs = yearPeriods
                .Where(p => p.MainPeriodId == main.Id)
                .OrderBy(p => p.OrderIndex)
                .ToList();
            if (includedSubs.Count == 0)
            {
                throw new DomainException(
                    $"Aucune sous-période rattachée à « {main.Name} ».");
            }

            selectedLabel = main.Name;
        }

        var includedIds = includedSubs.Select(s => s.Id).ToHashSet();
        var periodNameById = includedSubs.ToDictionary(s => s.Id, s => s.Name);

        var accessScope = ResolveCotationAccessScope();
        var allAssignments = (await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == year.Id
                 && a.ClassRoomId == classRoomId
                 && a.IsActive,
            cancellationToken)).ToList();

        var scoped = FilterAssignmentsByScope(allAssignments, teacherId, accessScope)
            .Where(a => a.ClassRoomId == classRoomId)
            .ToList();

        var courseIds = scoped.Select(a => a.CourseId).Distinct().ToList();
        var courses = courseIds.Count == 0
            ? new Dictionary<Guid, Course>()
            : (await _courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken))
                .ToDictionary(c => c.Id);

        var evaluations = (await _evaluationRepository.FindAsync(
            e => e.ClassRoomId == classRoomId
                 && includedIds.Contains(e.AcademicPeriodId)
                 && courseIds.Contains(e.CourseId),
            cancellationToken)).ToList();

        var periodMaxById = includedSubs.ToDictionary(
            s => s.Id,
            s => s.MaxScore > 0 ? s.MaxScore : 0);

        var assignmentMaxByCourse = scoped
            .GroupBy(a => a.CourseId)
            .ToDictionary(
                g => g.Key,
                g => g.First().MaxScore > 0 ? g.First().MaxScore : 0);

        var defaultPeriodMax = includedSubs
            .Select(s => s.MaxScore)
            .FirstOrDefault(m => m > 0);

        var courseGroups = scoped
            .Where(a => courses.ContainsKey(a.CourseId))
            .GroupBy(a => a.CourseId)
            .Select(g =>
            {
                var course = courses[g.Key];
                var courseEvals = evaluations
                    .Where(e => e.CourseId == g.Key)
                    .OrderBy(e => e.EvaluationDate)
                    .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                    .Select(e => new PedagogicalSheetEvaluationColumnDto(
                        e.Id,
                        e.Title,
                        e.EvaluationDate,
                        e.MaxScore > 0
                            ? e.MaxScore
                            : periodMaxById.GetValueOrDefault(e.AcademicPeriodId, defaultPeriodMax),
                        e.AcademicPeriodId,
                        periodNameById.GetValueOrDefault(e.AcademicPeriodId, "—")))
                    .ToList();

                var targetMax = 0;
                if (assignmentMaxByCourse.TryGetValue(g.Key, out var assignmentMax) && assignmentMax > 0)
                {
                    targetMax = assignmentMax;
                }
                else if (course.MaxScore > 0)
                {
                    targetMax = course.MaxScore;
                }
                else if (defaultPeriodMax > 0)
                {
                    targetMax = defaultPeriodMax;
                }

                return new PedagogicalSheetCourseGroupDto(course.Id, course.Name, targetMax, courseEvals);
            })
            .OrderBy(c => c.CourseName, StringComparer.CurrentCultureIgnoreCase)
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

        var evalIds = evaluations.Select(e => e.Id).ToList();
        var allGrades = evalIds.Count == 0
            ? []
            : (await _gradeRepository.FindAsync(
                g => evalIds.Contains(g.EvaluationId),
                cancellationToken)).ToList();
        var gradeLookup = allGrades
            .GroupBy(g => (g.StudentId, g.EvaluationId))
            .ToDictionary(g => g.Key, g => g.First());

        var orderedStudents = enrollments
            .Select(e =>
            {
                students.TryGetValue(e.StudentId, out var student);
                var name = student is null
                    ? "—"
                    : StudentDisplayName.Format(student.LastName, student.MiddleName, student.FirstName);
                return new
                {
                    e.StudentId,
                    RegistrationNumber = student?.RegistrationNumber ?? "—",
                    StudentName = name,
                    SortKey = name
                };
            })
            .OrderBy(x => x.SortKey, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var sheetRules = CreatePedagogicalSheetRules();
        var courseContexts = courseGroups.Select(group =>
        {
            courses.TryGetValue(group.CourseId, out var courseEntity);
            return new CourseContextInput(
                group.CourseId,
                group.CourseName,
                courseEntity?.Coefficient is > 0 ? courseEntity.Coefficient : 1,
                group.TargetMaxScore > 0 ? group.TargetMaxScore : 0,
                group.Evaluations.Select(e => new EvaluationDefinitionInput(
                    e.EvaluationId,
                    group.CourseId,
                    group.CourseName,
                    1,
                    e.MaxScore)).ToList());
        }).ToList();

        var studentInputs = orderedStudents.Select(s =>
        {
            var scores = new List<ScoreEntryInput>();
            foreach (var group in courseGroups)
            {
                foreach (var ev in group.Evaluations)
                {
                    if (!gradeLookup.TryGetValue((s.StudentId, ev.EvaluationId), out var grade))
                    {
                        scores.Add(new ScoreEntryInput(
                            ev.EvaluationId, s.StudentId, null, ScoreEntryStatus.NotGraded));
                        continue;
                    }

                    scores.Add(ScoreEntryStatusMapper.ToInput(
                        ev.EvaluationId, s.StudentId, grade.Score, grade.IsAbsent, grade.Comment));
                }
            }

            return new StudentScoresInput(s.StudentId, s.StudentName, scores);
        }).ToList();

        var recalculation = _resultCalculation.RecalculateClass(studentInputs, courseContexts, sheetRules);
        var studentResultById = recalculation.Students.ToDictionary(r => r.StudentId);

        var rows = new List<PedagogicalSheetStudentRowDto>(orderedStudents.Count);
        for (var i = 0; i < orderedStudents.Count; i++)
        {
            var s = orderedStudents[i];
            studentResultById.TryGetValue(s.StudentId, out var studentResult);
            var courseResultById = studentResult?.CourseResults.ToDictionary(c => c.CourseId)
                ?? new Dictionary<Guid, CourseResultDto>();

            var courseCells = new List<PedagogicalSheetCourseCellsDto>(courseGroups.Count);
            foreach (var group in courseGroups)
            {
                var cells = group.Evaluations
                    .Select(ev =>
                    {
                        if (!gradeLookup.TryGetValue((s.StudentId, ev.EvaluationId), out var grade))
                        {
                            return new PedagogicalSheetCellDto(ev.EvaluationId, "—");
                        }

                        return new PedagogicalSheetCellDto(
                            ev.EvaluationId,
                            FormatPedagogicalCell(grade.Score, grade.IsAbsent, grade.Comment));
                    })
                    .ToList();

                courseResultById.TryGetValue(group.CourseId, out var courseResult);
                courseCells.Add(new PedagogicalSheetCourseCellsDto(
                    group.CourseId,
                    cells,
                    FormatEngineValue(courseResult?.Result, group.Evaluations.Count),
                    FormatEngineValue(courseResult?.NormalizedAverage, group.Evaluations.Count)));
            }

            rows.Add(new PedagogicalSheetStudentRowDto(
                i + 1,
                s.StudentId,
                s.RegistrationNumber,
                s.StudentName,
                courseCells));
        }

        var stats = recalculation.Statistics;
        var summary = new PedagogicalSheetSummaryDto(
            FormatStat(stats.ClassAverage),
            FormatStat(stats.Maximum),
            FormatStat(stats.Minimum),
            stats.GradedStudentCount.ToString(CultureInfo.InvariantCulture),
            stats.AbsentCount.ToString(CultureInfo.InvariantCulture));

        return new PedagogicalSheetDto(
            classRoomId,
            classDisplayName,
            year.Id,
            mode,
            periodId,
            selectedLabel,
            includedSubs.Select(s => s.Id).ToList(),
            courseGroups,
            rows,
            summary);
    }

    private static string FormatEngineValue(decimal? value, int evaluationCount)
    {
        if (evaluationCount == 0 || value is null)
        {
            return "—";
        }

        return value.Value.ToString("0.##", CultureInfo.CurrentCulture);
    }

    private static string FormatStat(decimal? value) =>
        value is null ? "—" : value.Value.ToString("0.##", CultureInfo.CurrentCulture);

    private async Task<(string ClassDisplayName, AcademicPeriodType ExpectedType, PedagogicalCycleGroup CycleGroup)>
        ResolveClassPeriodContextAsync(
            Guid schoolId,
            ClassRoom classRoom,
            CancellationToken cancellationToken)
    {
        PedagogicalClass? ped = null;
        if (classRoom.PedagogicalClassId.HasValue)
        {
            ped = (await _pedagogicalClassRepository.FindAsync(
                p => p.Id == classRoom.PedagogicalClassId.Value && p.SchoolId == schoolId,
                cancellationToken)).FirstOrDefault();
        }

        var section = (await _sectionRepository.FindAsync(
            s => s.Id == classRoom.SectionId && s.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault();

        var classDisplayName = ped is null
            ? classRoom.Name
            : $"{ped.DisplayName} {classRoom.Name}".Trim();

        var expectedType = ResolvePeriodType(ped?.Program, section?.Cycle);
        var cycleGroup = ResolveCycleGroup(ped?.Program, section?.Cycle);
        return (classDisplayName, expectedType, cycleGroup);
    }

    private static bool MatchesPeriodTypeName(AcademicPeriodType periodType, AcademicPeriodType expected) =>
        periodType == expected;

    /// <summary>
    /// Affichage cellule — pas de calcul. ABS / DISP / EXC reconnus ; sinon note ou —.
    /// </summary>
    private static string FormatPedagogicalCell(decimal score, bool isAbsent, string? comment)
    {
        if (isAbsent)
        {
            return "ABS";
        }

        if (!string.IsNullOrWhiteSpace(comment))
        {
            var code = comment.Trim();
            if (code.Equals("DISP", StringComparison.OrdinalIgnoreCase)
                || code.Equals("EXC", StringComparison.OrdinalIgnoreCase)
                || code.Equals("ABS", StringComparison.OrdinalIgnoreCase))
            {
                return code.ToUpperInvariant();
            }
        }

        return score.ToString("0.##", CultureInfo.GetCultureInfo("fr-FR"));
    }
}
