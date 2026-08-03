using System.Globalization;
using SchoolManagement.Application.Deliberation.DTOs;
using SchoolManagement.Domain.Entities.Deliberation;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Deliberation.Services;

public sealed partial class DeliberationService
{
    public async Task<DeliberationDecisionDialogDto> GetDecisionDialogAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var (_, classRoom, period, periodContext, _) = await EnsureDeliberationContextAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);

        if (!periodContext.IsYearEnd)
        {
            throw new DomainException(
                "Les décisions de passage ne sont disponibles qu'à la fin de l'année scolaire.");
        }

        var classLabel = string.IsNullOrWhiteSpace(classRoom.Name) ? classRoom.Code : classRoom.Name;

        var periodResult = (await _periodResultRepository.FindAsync(
            p => p.ClassRoomId == classRoomId
                 && p.AcademicPeriodId == academicPeriodId
                 && p.StudentId == studentId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Résultat périodique introuvable pour cet élève.");

        var student = (await _studentRepository.FindAsync(s => s.Id == studentId, cancellationToken))
            .FirstOrDefault()
            ?? throw new DomainException("Élève introuvable.");

        var decision = (await _decisionRepository.FindAsync(
            d => d.SchoolId == schoolId
                 && d.AcademicYearId == academicYearId
                 && d.ClassRoomId == classRoomId
                 && d.AcademicPeriodId == academicPeriodId
                 && d.StudentId == studentId,
            cancellationToken)).FirstOrDefault();

        var courses = await LoadCourseOptionsAsync(academicYearId, classRoomId, cancellationToken);
        var remedialIds = Array.Empty<Guid>();
        var exemptions = Array.Empty<DeliberationExemptionItemDto>();
        string? exemptionMotive = null;
        string? exemptionObservation = null;

        if (decision is not null)
        {
            var session = (await _remedialSessionRepository.FindAsync(
                s => s.DecisionId == decision.Id, cancellationToken)).FirstOrDefault();
            if (session is not null)
            {
                var remedialCourses = await _remedialCourseRepository.FindAsync(
                    c => c.RemedialSessionId == session.Id, cancellationToken);
                remedialIds = remedialCourses.Select(c => c.CourseId).ToArray();
            }

            var exemptionEntities = await _exemptionRepository.FindAsync(
                e => e.DecisionId == decision.Id, cancellationToken);
            if (exemptionEntities.Count > 0)
            {
                var courseMap = courses.ToDictionary(c => c.CourseId);
                exemptions = exemptionEntities
                    .Select(e => new DeliberationExemptionItemDto(
                        e.CourseId,
                        courseMap.TryGetValue(e.CourseId, out var opt) ? opt.CourseName : e.CourseId.ToString(),
                        e.Motive,
                        e.Observation))
                    .ToArray();
                exemptionMotive = exemptionEntities[0].Motive;
                exemptionObservation = exemptionEntities[0].Observation;
            }
        }

        var selectedCourseIds = decision?.FinalDecision == FinalCouncilDecision.Repechage
            ? remedialIds.ToHashSet()
            : decision?.FinalDecision == FinalCouncilDecision.Dispense
                ? exemptions.Select(e => e.CourseId).ToHashSet()
                : [];

        courses = courses
            .Select(c => c with { IsSelected = selectedCourseIds.Contains(c.CourseId) })
            .ToList();

        return new DeliberationDecisionDialogDto(
            studentId,
            student.RegistrationNumber ?? "—",
            $"{student.LastName} {student.FirstName}".Trim(),
            classRoom.Id,
            classLabel,
            period.Id,
            period.Name,
            academicYearId,
            periodResult.Average,
            periodResult.Percentage,
            FormatValue(periodResult.Average),
            FormatPercentage(periodResult.Percentage),
            periodResult.Appreciation,
            periodResult.CouncilDecision,
            FormatDecisionLabel(periodResult.CouncilDecision),
            decision?.FinalDecision,
            decision is null ? "—" : FormatFinalDecisionLabel(decision.FinalDecision),
            decision?.Observation,
            decision?.DecidedAtUtc,
            decision?.DecidedByUserName,
            decision is null
                ? "—"
                : decision.DecidedAtUtc.ToLocalTime()
                    .ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
            periodContext.CanSetFinalDecision,
            periodContext.CanOfferRepechage,
            periodContext.AvailableDecisions,
            courses,
            remedialIds,
            exemptions,
            exemptionMotive,
            exemptionObservation);
    }

    public async Task<DeliberationDecisionDialogDto> SaveDecisionAsync(
        Guid schoolId,
        SaveDeliberationDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (_, _, _, periodContext) = await EnsureDeliberationWritableAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);

        if (!periodContext.CanSetFinalDecision)
        {
            throw new DomainException(
                "Les décisions de passage ne sont disponibles qu'à la fin de l'année scolaire.");
        }

        if (periodContext.AvailableDecisions.All(d => d.Value != request.FinalDecision))
        {
            throw new DomainException("Cette décision n'est pas autorisée pour cette période.");
        }

        if (request.FinalDecision == FinalCouncilDecision.Repechage && !periodContext.CanOfferRepechage)
        {
            throw new DomainException("Le repêchage n'est disponible qu'en secondaire (fin d'année).");
        }

        var periodResult = (await _periodResultRepository.FindAsync(
            p => p.ClassRoomId == request.ClassRoomId
                 && p.AcademicPeriodId == request.AcademicPeriodId
                 && p.StudentId == request.StudentId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Résultat périodique introuvable pour cet élève.");

        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.StudentId == request.StudentId
                 && e.ClassRoomId == request.ClassRoomId
                 && e.AcademicYearId == request.AcademicYearId
                 && e.IsActive,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Élève non inscrit dans cette classe.");

        _ = enrollment;

        var observation = NormalizeText(request.Observation, 2000);
        var remedialIds = (request.RemedialCourseIds ?? []).Distinct().ToList();
        var exemptionIds = (request.ExemptionCourseIds ?? []).Distinct().ToList();

        if (request.FinalDecision == FinalCouncilDecision.Repechage && remedialIds.Count == 0)
        {
            throw new DomainException("Sélectionnez au moins un cours à repêcher.");
        }

        if (request.FinalDecision == FinalCouncilDecision.Dispense)
        {
            if (exemptionIds.Count == 0)
            {
                throw new DomainException("Sélectionnez au moins un cours concerné par la dispense.");
            }

            if (string.IsNullOrWhiteSpace(request.ExemptionMotive))
            {
                throw new DomainException("Indiquez le motif de la dispense.");
            }
        }

        var assignments = await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == request.AcademicYearId
                 && a.ClassRoomId == request.ClassRoomId
                 && a.IsActive,
            cancellationToken);
        var assignmentByCourse = assignments
            .GroupBy(a => a.CourseId)
            .ToDictionary(g => g.Key, g => g.First());

        var relevantIds = request.FinalDecision switch
        {
            FinalCouncilDecision.Repechage => remedialIds,
            FinalCouncilDecision.Dispense => exemptionIds,
            _ => []
        };

        foreach (var courseId in relevantIds)
        {
            if (!assignmentByCourse.ContainsKey(courseId))
            {
                throw new DomainException("Un des cours sélectionnés n'est pas affecté à cette classe.");
            }
        }

        var (userId, userName) = ResolveActor();
        var now = DateTime.UtcNow;
        var proposed = periodResult.CouncilDecision;

        var decision = (await _decisionRepository.FindAsync(
            d => d.SchoolId == schoolId
                 && d.AcademicYearId == request.AcademicYearId
                 && d.ClassRoomId == request.ClassRoomId
                 && d.AcademicPeriodId == request.AcademicPeriodId
                 && d.StudentId == request.StudentId,
            cancellationToken)).FirstOrDefault();

        if (decision is null)
        {
            decision = new DeliberationDecision
            {
                SchoolId = schoolId,
                AcademicYearId = request.AcademicYearId,
                ClassRoomId = request.ClassRoomId,
                AcademicPeriodId = request.AcademicPeriodId,
                StudentId = request.StudentId,
                ProposedDecision = proposed,
                FinalDecision = request.FinalDecision,
                Observation = observation,
                DecidedAtUtc = now,
                DecidedByUserId = userId,
                DecidedByUserName = userName
            };
            await _decisionRepository.AddAsync(decision, cancellationToken);
        }
        else
        {
            decision.ProposedDecision = proposed;
            decision.FinalDecision = request.FinalDecision;
            decision.Observation = observation;
            decision.DecidedAtUtc = now;
            decision.DecidedByUserId = userId;
            decision.DecidedByUserName = userName;
            await _decisionRepository.UpdateAsync(decision, cancellationToken);
        }

        await _decisionEventRepository.AddAsync(new DeliberationDecisionEvent
        {
            SchoolId = schoolId,
            DecisionId = decision.Id,
            ProposedDecision = proposed,
            FinalDecision = request.FinalDecision,
            Observation = observation,
            UserId = userId,
            UserName = userName,
            OccurredAtUtc = now
        }, cancellationToken);

        await AddAuditAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            request.StudentId,
            "Decision",
            $"Décision : {FormatFinalDecisionLabel(request.FinalDecision)}",
            observation,
            userId,
            userName,
            now,
            cancellationToken);

        await SyncRemedialAsync(schoolId, decision, request, remedialIds, assignmentByCourse, cancellationToken);
        await SyncExemptionsAsync(schoolId, decision, request, exemptionIds, assignmentByCourse, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetDecisionDialogAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            request.StudentId,
            cancellationToken);
    }

    private async Task SyncRemedialAsync(
        Guid schoolId,
        DeliberationDecision decision,
        SaveDeliberationDecisionRequest request,
        IReadOnlyList<Guid> remedialIds,
        IReadOnlyDictionary<Guid, Domain.Entities.Academic.CourseAssignment> assignmentByCourse,
        CancellationToken cancellationToken)
    {
        var existingSession = (await _remedialSessionRepository.FindAsync(
            s => s.DecisionId == decision.Id, cancellationToken)).FirstOrDefault();

        if (request.FinalDecision != FinalCouncilDecision.Repechage)
        {
            if (existingSession is not null)
            {
                var oldCourses = await _remedialCourseRepository.FindAsync(
                    c => c.RemedialSessionId == existingSession.Id, cancellationToken);
                foreach (var course in oldCourses)
                {
                    await _remedialCourseRepository.DeleteAsync(course, cancellationToken);
                }

                await _remedialSessionRepository.DeleteAsync(existingSession, cancellationToken);
            }

            return;
        }

        if (existingSession is null)
        {
            existingSession = new StudentRemedialSession
            {
                SchoolId = schoolId,
                DecisionId = decision.Id,
                StudentId = decision.StudentId,
                AcademicYearId = decision.AcademicYearId,
                ClassRoomId = decision.ClassRoomId,
                AcademicPeriodId = decision.AcademicPeriodId,
                SessionKind = EvaluationSessionKind.DeuxiemeSession
            };
            await _remedialSessionRepository.AddAsync(existingSession, cancellationToken);
        }

        var currentCourses = await _remedialCourseRepository.FindAsync(
            c => c.RemedialSessionId == existingSession.Id, cancellationToken);
        var currentByCourse = currentCourses.ToDictionary(c => c.CourseId);
        var wanted = remedialIds.ToHashSet();

        foreach (var course in currentCourses.Where(c => !wanted.Contains(c.CourseId)))
        {
            await _remedialCourseRepository.DeleteAsync(course, cancellationToken);
        }

        foreach (var courseId in wanted)
        {
            if (currentByCourse.ContainsKey(courseId))
            {
                continue;
            }

            assignmentByCourse.TryGetValue(courseId, out var assignment);
            await _remedialCourseRepository.AddAsync(new StudentRemedialCourse
            {
                RemedialSessionId = existingSession.Id,
                CourseId = courseId,
                CourseAssignmentId = assignment?.Id,
                Status = RemedialCourseStatus.ACoter
            }, cancellationToken);
        }
    }

    private async Task SyncExemptionsAsync(
        Guid schoolId,
        DeliberationDecision decision,
        SaveDeliberationDecisionRequest request,
        IReadOnlyList<Guid> exemptionIds,
        IReadOnlyDictionary<Guid, Domain.Entities.Academic.CourseAssignment> assignmentByCourse,
        CancellationToken cancellationToken)
    {
        var existing = await _exemptionRepository.FindAsync(
            e => e.DecisionId == decision.Id, cancellationToken);

        if (request.FinalDecision != FinalCouncilDecision.Dispense)
        {
            foreach (var item in existing)
            {
                await _exemptionRepository.DeleteAsync(item, cancellationToken);
            }

            return;
        }

        var motive = NormalizeText(request.ExemptionMotive, 500) ?? string.Empty;
        var exemptionObservation = NormalizeText(request.ExemptionObservation, 2000);
        var wanted = exemptionIds.ToHashSet();
        var currentByCourse = existing.ToDictionary(e => e.CourseId);

        foreach (var item in existing.Where(e => !wanted.Contains(e.CourseId)))
        {
            await _exemptionRepository.DeleteAsync(item, cancellationToken);
        }

        foreach (var courseId in wanted)
        {
            assignmentByCourse.TryGetValue(courseId, out var assignment);
            if (currentByCourse.TryGetValue(courseId, out var current))
            {
                current.Motive = motive;
                current.Observation = exemptionObservation;
                current.CourseAssignmentId = assignment?.Id;
                await _exemptionRepository.UpdateAsync(current, cancellationToken);
                continue;
            }

            await _exemptionRepository.AddAsync(new CourseExemption
            {
                SchoolId = schoolId,
                DecisionId = decision.Id,
                StudentId = decision.StudentId,
                CourseId = courseId,
                CourseAssignmentId = assignment?.Id,
                Motive = motive,
                Observation = exemptionObservation
            }, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<DeliberationCourseOptionDto>> LoadCourseOptionsAsync(
        Guid academicYearId,
        Guid classRoomId,
        CancellationToken cancellationToken)
    {
        var assignments = await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == academicYearId && a.ClassRoomId == classRoomId && a.IsActive,
            cancellationToken);
        var courseIds = assignments.Select(a => a.CourseId).Distinct().ToList();
        if (courseIds.Count == 0)
        {
            return [];
        }

        var courses = await _courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken);
        var courseMap = courses.ToDictionary(c => c.Id);
        var assignmentByCourse = assignments
            .GroupBy(a => a.CourseId)
            .ToDictionary(g => g.Key, g => g.First());

        return courseIds
            .Select(id =>
            {
                courseMap.TryGetValue(id, out var course);
                assignmentByCourse.TryGetValue(id, out var assignment);
                return new DeliberationCourseOptionDto(
                    id,
                    assignment?.Id,
                    course?.Name ?? id.ToString(),
                    false);
            })
            .OrderBy(c => c.CourseName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
