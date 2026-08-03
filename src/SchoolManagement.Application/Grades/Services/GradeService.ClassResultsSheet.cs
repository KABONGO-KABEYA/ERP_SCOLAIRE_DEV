namespace SchoolManagement.Application.Grades.Services;

using System.Globalization;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Grades.Calculation;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Grades.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using AcademicPeriod = SchoolManagement.Domain.Entities.Settings.AcademicPeriod;

public sealed partial class GradeService
{
    public async Task<ClassResultsSheetDto> GetClassResultsSheetAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        PedagogicalSheetPeriodMode mode,
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        var computation = await ComputeClassResultsAsync(
            schoolId, academicYearId, classRoomId, mode, periodId, cancellationToken);

        var studentResultById = computation.Recalculation.Students.ToDictionary(r => r.StudentId);
        var rankingByStudent = computation.Recalculation.Ranking.ToDictionary(r => r.StudentId);

        var courseDtos = computation.Courses
            .Select(c => new ClassResultsCourseColumnDto(c.CourseId, c.CourseName, c.TargetMax))
            .ToList();

        var rows = new List<ClassResultsStudentRowDto>(computation.Students.Count);
        foreach (var s in computation.Students)
        {
            studentResultById.TryGetValue(s.StudentId, out var studentResult);
            rankingByStudent.TryGetValue(s.StudentId, out var rankEntry);
            var courseResultById = studentResult?.CourseResults.ToDictionary(c => c.CourseId)
                ?? new Dictionary<Guid, CourseResultDto>();

            var cells = computation.Courses.Select(course =>
            {
                courseResultById.TryGetValue(course.CourseId, out var courseResult);
                return new ClassResultsCourseCellDto(
                    course.CourseId,
                    FormatClassResultsValue(courseResult?.NormalizedAverage));
            }).ToList();

            var decision = studentResult?.Decision ?? ClassCouncilDecision.EnAttente;
            rows.Add(new ClassResultsStudentRowDto(
                s.StudentId,
                s.RegistrationNumber,
                s.StudentName,
                rankEntry?.Rank ?? 0,
                rankEntry?.IsTied ?? false,
                cells,
                studentResult?.Average,
                studentResult?.Percentage,
                FormatClassResultsValue(studentResult?.Average),
                FormatClassResultsPercentage(studentResult?.Percentage),
                studentResult?.Mention,
                decision,
                FormatDecisionLabel(decision),
                studentResult?.IsComplete == true ? "Complet" : "Incomplet"));
        }

        rows = rows
            .OrderBy(r => r.Rank == 0 ? int.MaxValue : r.Rank)
            .ThenBy(r => r.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var stats = computation.Recalculation.Statistics;
        return new ClassResultsSheetDto(
            computation.ClassRoomId,
            computation.ClassDisplayName,
            computation.AcademicYearId,
            computation.AcademicYearLabel,
            computation.Mode,
            computation.SelectedPeriodId,
            computation.SelectedPeriodLabel,
            computation.IncludedSubPeriodIds,
            courseDtos,
            rows,
            new ClassResultsSummaryDto(
                FormatClassResultsValue(stats.ClassAverage),
                FormatClassResultsValue(stats.Maximum),
                FormatClassResultsValue(stats.Minimum),
                stats.ClassSize,
                stats.GradedStudentCount));
    }

    public async Task<IndividualResultDto> GetIndividualResultAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid studentId,
        PedagogicalSheetPeriodMode mode,
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        var computation = await ComputeClassResultsAsync(
            schoolId, academicYearId, classRoomId, mode, periodId, cancellationToken);

        var studentInfo = computation.Students.FirstOrDefault(s => s.StudentId == studentId)
            ?? throw new DomainException("Élève introuvable dans cette classe pour la période sélectionnée.");

        var studentResult = computation.Recalculation.Students.FirstOrDefault(s => s.StudentId == studentId);
        var rankEntry = computation.Recalculation.Ranking.FirstOrDefault(r => r.StudentId == studentId);
        var courseResultById = studentResult?.CourseResults.ToDictionary(c => c.CourseId)
            ?? new Dictionary<Guid, CourseResultDto>();

        var courseRows = computation.Courses.Select(course =>
        {
            courseResultById.TryGetValue(course.CourseId, out var courseResult);
            var maximum = courseResult?.Maximum
                ?? (course.TargetMax > 0 ? course.TargetMax : null);
            return new IndividualResultCourseRowDto(
                course.CourseId,
                course.CourseName,
                FormatClassResultsValue(maximum),
                FormatClassResultsValue(courseResult?.Result),
                FormatClassResultsValue(courseResult?.NormalizedAverage),
                courseResult?.Mention,
                FormatCourseObservation(courseResult),
                courseResult?.Result,
                maximum);
        }).ToList();

        var decision = studentResult?.Decision ?? ClassCouncilDecision.EnAttente;
        var rank = rankEntry?.Rank ?? 0;
        var isTied = rankEntry?.IsTied ?? false;

        return new IndividualResultDto(
            studentInfo.StudentId,
            studentInfo.RegistrationNumber,
            studentInfo.StudentName,
            studentInfo.PhotoPath,
            computation.ClassRoomId,
            computation.ClassDisplayName,
            computation.AcademicYearId,
            computation.AcademicYearLabel,
            computation.Mode,
            computation.SelectedPeriodId,
            computation.SelectedPeriodLabel,
            rank,
            isTied,
            computation.Recalculation.Statistics.ClassSize,
            studentResult?.Average,
            studentResult?.Percentage,
            FormatClassResultsValue(studentResult?.Average),
            FormatClassResultsPercentage(studentResult?.Percentage),
            studentResult?.Mention,
            decision,
            FormatDecisionLabel(decision),
            FormatRankDisplay(rank, isTied),
            courseRows);
    }

    private async Task<ClassResultsComputation> ComputeClassResultsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        PedagogicalSheetPeriodMode mode,
        Guid periodId,
        CancellationToken cancellationToken)
    {
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
                    $"Aucune sous-période rattachée à « {main.Name} ». Impossible de charger le trimestre/semestre.");
            }

            selectedLabel = main.Name;
        }

        var includedIds = includedSubs.Select(s => s.Id).ToHashSet();

        var allAssignments = (await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == year.Id
                 && a.ClassRoomId == classRoomId
                 && a.IsActive,
            cancellationToken)).ToList();

        var courseIds = allAssignments.Select(a => a.CourseId).Distinct().ToList();
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

        var assignmentMaxByCourse = allAssignments
            .GroupBy(a => a.CourseId)
            .ToDictionary(
                g => g.Key,
                g => g.First().MaxScore > 0 ? g.First().MaxScore : 0);

        var defaultPeriodMax = includedSubs
            .Select(s => s.MaxScore)
            .FirstOrDefault(m => m > 0);

        var courseColumns = allAssignments
            .Where(a => courses.ContainsKey(a.CourseId))
            .GroupBy(a => a.CourseId)
            .Select(g =>
            {
                var course = courses[g.Key];
                var courseEvals = evaluations
                    .Where(e => e.CourseId == g.Key)
                    .OrderBy(e => e.EvaluationDate)
                    .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
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

                return new ClassResultsCourseComputation(
                    course.Id,
                    course.Name,
                    targetMax,
                    course.Coefficient is > 0 ? course.Coefficient : 1m,
                    courseEvals);
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
                return new ClassResultsStudentComputation(
                    e.StudentId,
                    student?.RegistrationNumber ?? "—",
                    name,
                    student?.PhotoPath);
            })
            .OrderBy(x => x.StudentName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var rules = await CreatePeriodResultRulesAsync(schoolId, cancellationToken);
        var courseContexts = courseColumns.Select(c =>
            new CourseContextInput(
                c.CourseId,
                c.CourseName,
                c.Coefficient,
                c.TargetMax,
                c.Evaluations.Select(e => new EvaluationDefinitionInput(
                    e.Id,
                    c.CourseId,
                    c.CourseName,
                    e.Weight <= 0 ? 1 : e.Weight,
                    e.MaxScore > 0
                        ? e.MaxScore
                        : periodMaxById.GetValueOrDefault(e.AcademicPeriodId, defaultPeriodMax))).ToList()))
            .ToList();

        var studentInputs = orderedStudents.Select(s =>
        {
            var scores = new List<ScoreEntryInput>();
            foreach (var course in courseColumns)
            {
                foreach (var ev in course.Evaluations)
                {
                    if (!gradeLookup.TryGetValue((s.StudentId, ev.Id), out var grade))
                    {
                        scores.Add(new ScoreEntryInput(
                            ev.Id, s.StudentId, null, ScoreEntryStatus.NotGraded));
                        continue;
                    }

                    scores.Add(ScoreEntryStatusMapper.ToInput(
                        ev.Id, s.StudentId, grade.Score, grade.IsAbsent, grade.Comment));
                }
            }

            return new StudentScoresInput(s.StudentId, s.StudentName, scores);
        }).ToList();

        var recalculation = _resultCalculation.RecalculateClass(studentInputs, courseContexts, rules);

        return new ClassResultsComputation(
            classRoomId,
            classDisplayName,
            year.Id,
            year.Label,
            mode,
            periodId,
            selectedLabel,
            includedSubs.Select(s => s.Id).ToList(),
            courseColumns,
            orderedStudents,
            recalculation);
    }

    private static string FormatClassResultsValue(decimal? value) =>
        value is null ? "—" : value.Value.ToString("0.##", CultureInfo.CurrentCulture);

    private static string FormatClassResultsPercentage(decimal? value) =>
        value is null ? "—" : $"{value.Value.ToString("0.##", CultureInfo.CurrentCulture)} %";

    private static string FormatDecisionLabel(ClassCouncilDecision decision) =>
        decision switch
        {
            ClassCouncilDecision.Admis => "Admis",
            ClassCouncilDecision.Ajourne => "Ajourné",
            ClassCouncilDecision.Exclu => "Exclu",
            _ => "En attente"
        };

    private static string FormatRankDisplay(int rank, bool isTied) =>
        rank <= 0
            ? "—"
            : isTied
                ? $"{rank} ="
                : rank.ToString(CultureInfo.InvariantCulture);

    private static string FormatCourseObservation(CourseResultDto? courseResult)
    {
        if (courseResult is null)
        {
            return "—";
        }

        if (courseResult.ValidationErrors.Count > 0)
        {
            return string.Join(" ; ", courseResult.ValidationErrors);
        }

        if (courseResult.AbsentCount > 0)
        {
            return $"{courseResult.AbsentCount} absence(s)";
        }

        if (courseResult.NotGradedCount > 0)
        {
            return "Notes manquantes";
        }

        return courseResult.IsComplete ? string.Empty : "Incomplet";
    }

    private sealed record ClassResultsCourseComputation(
        Guid CourseId,
        string CourseName,
        int TargetMax,
        decimal Coefficient,
        IReadOnlyList<Evaluation> Evaluations);

    private sealed record ClassResultsStudentComputation(
        Guid StudentId,
        string RegistrationNumber,
        string StudentName,
        string? PhotoPath);

    private sealed record ClassResultsComputation(
        Guid ClassRoomId,
        string ClassDisplayName,
        Guid AcademicYearId,
        string AcademicYearLabel,
        PedagogicalSheetPeriodMode Mode,
        Guid SelectedPeriodId,
        string SelectedPeriodLabel,
        IReadOnlyList<Guid> IncludedSubPeriodIds,
        IReadOnlyList<ClassResultsCourseComputation> Courses,
        IReadOnlyList<ClassResultsStudentComputation> Students,
        ClassRecalculationResult Recalculation);
}
