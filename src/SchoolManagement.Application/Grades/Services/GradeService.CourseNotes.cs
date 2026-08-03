namespace SchoolManagement.Application.Grades.Services;

using SchoolManagement.Application.Common;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Exceptions;

public sealed partial class GradeService
{
    public async Task<CourseNotesGridDto> GetCourseNotesGridAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid courseId,
        Guid academicPeriodId,
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

        var period = (await _periodRepository.FindAsync(
            p => p.Id == academicPeriodId && p.AcademicYearId == year.Id,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Sous-période introuvable.");

        var course = await SchoolCourseScope.GetCourseAsync(
            _courseRepository,
            _pedagogicalClassCourseRepository,
            schoolId,
            courseId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Cours introuvable.");

        var pedagogicalMap = await SchoolConfigurationGuards.BuildPedagogicalMapAsync(
            _pedagogicalClassRepository,
            schoolId,
            cancellationToken);
        pedagogicalMap.TryGetValue(classRoom.PedagogicalClassId ?? Guid.Empty, out var ped);
        var classDisplayName = ped is null
            ? classRoom.Name
            : $"{ped.DisplayName} {classRoom.Name}".Trim();

        var periodMax = period.MaxScore > 0 ? period.MaxScore : 20;

        var evaluations = (await _evaluationRepository.FindAsync(
            e => e.ClassRoomId == classRoomId
                 && e.CourseId == courseId
                 && e.AcademicPeriodId == period.Id,
            cancellationToken))
            .OrderBy(e => e.EvaluationDate)
            .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var typeIds = evaluations.Select(e => e.EvaluationTypeId).Distinct().ToList();
        var types = typeIds.Count == 0
            ? []
            : await _evaluationTypeRepository.FindAsync(t => typeIds.Contains(t.Id), cancellationToken);
        var typeMap = types.ToDictionary(t => t.Id);

        var columns = evaluations
            .Select(e => new CourseNotesEvaluationColumnDto(
                e.Id,
                e.Title,
                typeMap.GetValueOrDefault(e.EvaluationTypeId)?.Name ?? "—",
                e.EvaluationDate,
                e.MaxScore > 0 ? e.MaxScore : periodMax,
                e.Weight))
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

        var evaluationIds = evaluations.Select(e => e.Id).ToList();
        var allGrades = evaluationIds.Count == 0
            ? []
            : (await _gradeRepository.FindAsync(
                g => evaluationIds.Contains(g.EvaluationId),
                cancellationToken)).ToList();
        var gradesByStudentEval = allGrades
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

        var rows = new List<CourseNotesStudentRowDto>(orderedStudents.Count);
        for (var i = 0; i < orderedStudents.Count; i++)
        {
            var s = orderedStudents[i];
            var cells = new List<CourseNotesCellDto>(evaluations.Count);

            foreach (var evaluation in evaluations)
            {
                if (evaluation.EnrollmentId is Guid enrollmentId)
                {
                    var enrollment = enrollments.FirstOrDefault(en => en.Id == enrollmentId);
                    if (enrollment is null || enrollment.StudentId != s.StudentId)
                    {
                        cells.Add(new CourseNotesCellDto(evaluation.Id, null, false, false));
                        continue;
                    }
                }

                if (gradesByStudentEval.TryGetValue((s.StudentId, evaluation.Id), out var grade))
                {
                    cells.Add(new CourseNotesCellDto(
                        evaluation.Id,
                        grade.IsAbsent ? null : grade.Score,
                        grade.IsAbsent,
                        true));
                }
                else
                {
                    cells.Add(new CourseNotesCellDto(evaluation.Id, null, false, false));
                }
            }

            rows.Add(new CourseNotesStudentRowDto(
                i + 1,
                s.StudentId,
                s.RegistrationNumber,
                s.StudentName,
                cells));
        }

        return new CourseNotesGridDto(
            courseId,
            course.Name,
            classRoomId,
            classDisplayName,
            year.Id,
            period.Id,
            period.Name,
            evaluations.Count,
            rows.Count,
            columns,
            rows);
    }
}
