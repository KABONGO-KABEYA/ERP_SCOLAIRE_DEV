using System.Globalization;
using SchoolManagement.Application.Deliberation.DTOs;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Grades.Interfaces;
using SchoolManagement.Application.ResultValidation.DTOs;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Deliberation;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using AcademicPeriod = SchoolManagement.Domain.Entities.Settings.AcademicPeriod;

namespace SchoolManagement.Application.Deliberation.Services;

public sealed partial class DeliberationService
{
    public async Task<DeliberationSheetDto> SaveConductAsync(
        Guid schoolId,
        SaveStudentConductRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await EnsureDeliberationWritableAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);

        if (!context.PeriodContext.CanSetConduct)
        {
            throw new DomainException("La saisie de conduite n'est pas autorisée pour cette période.");
        }

        var conductDef = (await _conductDefinitionRepository.FindAsync(
            c => c.Id == request.ConductDefinitionId && c.SchoolId == schoolId && c.IsActive,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Conduite introuvable.");

        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.StudentId == request.StudentId
                 && e.ClassRoomId == request.ClassRoomId
                 && e.AcademicYearId == request.AcademicYearId
                 && e.IsActive,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Élève non inscrit dans cette classe.");

        _ = enrollment;

        var (userId, userName) = ResolveActor();
        var now = DateTime.UtcNow;
        var observation = NormalizeText(request.Observation, 2000);

        var existing = (await _studentConductRepository.FindAsync(
            c => c.SchoolId == schoolId
                 && c.ClassRoomId == request.ClassRoomId
                 && c.AcademicPeriodId == request.AcademicPeriodId
                 && c.StudentId == request.StudentId,
            cancellationToken)).FirstOrDefault();

        if (existing is null)
        {
            await _studentConductRepository.AddAsync(new StudentPeriodConduct
            {
                SchoolId = schoolId,
                AcademicYearId = request.AcademicYearId,
                ClassRoomId = request.ClassRoomId,
                AcademicPeriodId = request.AcademicPeriodId,
                StudentId = request.StudentId,
                ConductDefinitionId = conductDef.Id,
                Observation = observation,
                RecordedByUserId = userId,
                RecordedByUserName = userName,
                RecordedAtUtc = now
            }, cancellationToken);
        }
        else
        {
            existing.ConductDefinitionId = conductDef.Id;
            existing.Observation = observation;
            existing.RecordedByUserId = userId;
            existing.RecordedByUserName = userName;
            existing.RecordedAtUtc = now;
            await _studentConductRepository.UpdateAsync(existing, cancellationToken);
        }

        await AddAuditAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            request.StudentId,
            "Conduct",
            $"Conduite : {conductDef.Label}",
            observation,
            userId,
            userName,
            now,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetSheetAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);
    }

    public async Task<DeliberationSheetDto> SavePedagogicalBonusAsync(
        Guid schoolId,
        SavePedagogicalBonusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await EnsureDeliberationWritableAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);

        if (!context.PeriodContext.CanAddBonusPoints)
        {
            throw new DomainException("L'ajout de points n'est pas autorisé pour cette période.");
        }

        if (request.PointsAdded <= 0)
        {
            throw new DomainException("Les points ajoutés doivent être strictement positifs.");
        }

        const decimal maxPerOperation = 20m;
        if (request.PointsAdded > maxPerOperation)
        {
            throw new DomainException(
                $"Ajout de points trop élevé (maximum {maxPerOperation.ToString("0.##", CultureInfo.CurrentCulture)} points par opération).");
        }

        var motive = NormalizeText(request.Motive, 500)
            ?? throw new DomainException("Indiquez le motif de l'ajout de points.");

        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.StudentId == request.StudentId
                 && e.ClassRoomId == request.ClassRoomId
                 && e.AcademicYearId == request.AcademicYearId
                 && e.IsActive,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Élève non inscrit dans cette classe.");

        _ = enrollment;

        var assignment = (await _courseAssignmentRepository.FindAsync(
            a => a.AcademicYearId == request.AcademicYearId
                 && a.ClassRoomId == request.ClassRoomId
                 && a.CourseId == request.CourseId
                 && a.IsActive,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Ce cours n'est pas affecté à la classe.");

        var dialog = await GetPedagogicalBonusDialogAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            request.StudentId,
            cancellationToken);
        var courseCtx = dialog.Courses.FirstOrDefault(c => c.CourseId == request.CourseId)
            ?? throw new DomainException("Cours introuvable pour cet élève.");

        if (request.PointsAdded > courseCtx.RemainingAddable)
        {
            throw new DomainException(
                $"Impossible d'ajouter {request.PointsAdded.ToString("0.##", CultureInfo.CurrentCulture)} pt(s). " +
                $"Il reste {courseCtx.RemainingAddableDisplay} ajoutable(s) sur ce cours " +
                $"(note actuelle {courseCtx.CurrentScoreDisplay} / max {courseCtx.MaximumDisplay}).");
        }

        var courseAssignmentId = request.CourseAssignmentId ?? assignment.Id;
        var (userId, userName) = ResolveActor();
        var now = DateTime.UtcNow;

        await _bonusRepository.AddAsync(new PedagogicalBonusPoint
        {
            SchoolId = schoolId,
            AcademicYearId = request.AcademicYearId,
            ClassRoomId = request.ClassRoomId,
            AcademicPeriodId = request.AcademicPeriodId,
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            CourseAssignmentId = courseAssignmentId,
            PointsAdded = request.PointsAdded,
            Motive = motive,
            RecordedByUserId = userId,
            RecordedByUserName = userName,
            RecordedAtUtc = now
        }, cancellationToken);

        await AddAuditAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            request.StudentId,
            "BonusPoints",
            $"+{request.PointsAdded.ToString("0.##", CultureInfo.CurrentCulture)} pts (cours {request.CourseId})",
            motive,
            userId,
            userName,
            now,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Recalcul officiel — l'UI ne calcule jamais.
        await _gradeService.RecalculatePeriodResultsAfterDataChangeAsync(
            schoolId,
            new CalculatePeriodResultsRequest(
                request.ClassRoomId,
                request.AcademicYearId,
                request.AcademicPeriodId),
            cancellationToken);

        return await GetSheetAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PedagogicalBonusDto>> GetPedagogicalBonusesAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureDeliberationContextAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);

        var bonuses = (await _bonusRepository.FindAsync(
            b => b.SchoolId == schoolId
                 && b.ClassRoomId == classRoomId
                 && b.AcademicPeriodId == academicPeriodId
                 && !b.IsCancelled
                 && (studentId == null || b.StudentId == studentId),
            cancellationToken))
            .OrderByDescending(b => b.RecordedAtUtc)
            .ToList();

        if (bonuses.Count == 0)
        {
            return [];
        }

        var studentIds = bonuses.Select(b => b.StudentId).Distinct().ToHashSet();
        var courseIds = bonuses.Select(b => b.CourseId).Distinct().ToHashSet();
        var students = await _studentRepository.FindAsync(s => studentIds.Contains(s.Id), cancellationToken);
        var courses = await _courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);
        var courseMap = courses.ToDictionary(c => c.Id);

        return bonuses.Select(b =>
        {
            studentMap.TryGetValue(b.StudentId, out var st);
            courseMap.TryGetValue(b.CourseId, out var course);
            var name = st is null ? "—" : $"{st.LastName} {st.FirstName}".Trim();
            return new PedagogicalBonusDto(
                b.Id,
                b.StudentId,
                name,
                b.CourseId,
                course?.Name ?? "—",
                b.PointsAdded,
                b.Motive,
                b.RecordedByUserName,
                b.RecordedAtUtc,
                b.RecordedAtUtc.ToLocalTime()
                    .ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture));
        }).ToList();
    }

    public async Task<PedagogicalBonusDialogDto> GetPedagogicalBonusDialogAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var (_, _, _, periodContext, _) = await EnsureDeliberationContextAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);

        _ = periodContext;

        var student = (await _studentRepository.FindAsync(s => s.Id == studentId, cancellationToken))
            .FirstOrDefault()
            ?? throw new DomainException("Élève introuvable.");

        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.StudentId == studentId
                 && e.ClassRoomId == classRoomId
                 && e.AcademicYearId == academicYearId
                 && e.IsActive,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Élève non inscrit dans cette classe.");
        _ = enrollment;

        var individual = await _gradeService.GetIndividualResultAsync(
            schoolId,
            academicYearId,
            classRoomId,
            studentId,
            PedagogicalSheetPeriodMode.SubPeriod,
            academicPeriodId,
            cancellationToken);

        var bonuses = await _bonusRepository.FindAsync(
            b => b.SchoolId == schoolId
                 && b.ClassRoomId == classRoomId
                 && b.AcademicPeriodId == academicPeriodId
                 && b.StudentId == studentId
                 && !b.IsCancelled,
            cancellationToken);

        var bonusByCourse = bonuses
            .GroupBy(b => b.CourseId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.PointsAdded));

        var studentBonusTotal = bonuses.Sum(b => b.PointsAdded);
        const decimal maxPerOperation = 20m;
        var courseOptions = await LoadCourseOptionsAsync(academicYearId, classRoomId, cancellationToken);
        var courseResultById = individual.Courses.ToDictionary(c => c.CourseId);

        var courses = courseOptions.Select(opt =>
        {
            courseResultById.TryGetValue(opt.CourseId, out var row);
            var baseScore = row?.Result;
            var maximum = row?.Maximum;
            var existingBonus = bonusByCourse.GetValueOrDefault(opt.CourseId);
            decimal currentScore;
            decimal remaining;

            if (maximum is > 0)
            {
                var raw = (baseScore ?? 0m) + existingBonus;
                currentScore = raw > maximum.Value ? maximum.Value : raw;
                remaining = maximum.Value - currentScore;
                if (remaining < 0)
                {
                    remaining = 0;
                }
            }
            else
            {
                // Pas de plafond de cours connu : on laisse le plafond par opération.
                currentScore = (baseScore ?? 0m) + existingBonus;
                remaining = maxPerOperation;
            }

            // RemainingAddable = reste jusqu'au max du cours (pas limité à 20).
            // MaxPointsPerOperation reste le plafond d'une seule saisie.

            return new PedagogicalBonusCourseContextDto(
                opt.CourseId,
                opt.CourseAssignmentId,
                opt.CourseName,
                baseScore,
                currentScore,
                maximum,
                FormatBonusValue(baseScore),
                FormatBonusValue(currentScore),
                FormatBonusValue(maximum),
                existingBonus,
                FormatBonusValue(existingBonus),
                remaining,
                FormatBonusValue(remaining));
        })
        .OrderBy(c => c.CourseName, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

        var studentName = $"{student.LastName} {student.FirstName}".Trim();
        return new PedagogicalBonusDialogDto(
            studentId,
            string.IsNullOrWhiteSpace(studentName) ? "—" : studentName,
            studentBonusTotal,
            FormatBonusValue(studentBonusTotal),
            maxPerOperation,
            courses);
    }

    private static string FormatBonusValue(decimal? value) =>
        value is null ? "—" : value.Value.ToString("0.##", CultureInfo.CurrentCulture);

    public async Task<ValidateDeliberationClassResultDto> ValidateClassAsync(
        Guid schoolId,
        ValidateDeliberationClassRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await EnsureDeliberationWritableAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);

        if (!context.PeriodContext.CanValidateClass)
        {
            throw new DomainException("La validation de classe n'est pas autorisée (période verrouillée).");
        }

        var sheet = await GetSheetAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);

        if (sheet.Summary.MissingConductCount > 0)
        {
            throw new DomainException(
                $"Conduite manquante pour {sheet.Summary.MissingConductCount} élève(s). " +
                "Saisissez la conduite de chaque élève avant de valider.");
        }

        if (context.PeriodContext.CanSetFinalDecision
            && sheet.Students.Any(s => s.FinalDecision is null))
        {
            var missing = sheet.Students.Count(s => s.FinalDecision is null);
            throw new DomainException(
                $"Décision finale manquante pour {missing} élève(s). " +
                "Enregistrez toutes les décisions de passage avant de valider.");
        }

        if (context.PeriodContext.CanOfferRepechage)
        {
            var repechageStudents = sheet.Students
                .Where(s => s.FinalDecision == FinalCouncilDecision.Repechage)
                .ToList();
            foreach (var student in repechageStudents)
            {
                var decision = (await _decisionRepository.FindAsync(
                    d => d.SchoolId == schoolId
                         && d.ClassRoomId == request.ClassRoomId
                         && d.AcademicPeriodId == request.AcademicPeriodId
                         && d.StudentId == student.StudentId,
                    cancellationToken)).FirstOrDefault();
                if (decision is null)
                {
                    throw new DomainException($"Décision de repêchage introuvable pour {student.FullName}.");
                }

                var session = (await _remedialSessionRepository.FindAsync(
                    s => s.DecisionId == decision.Id, cancellationToken)).FirstOrDefault()
                    ?? throw new DomainException(
                        $"Sélectionnez les cours à repêcher pour {student.FullName}.");

                var courses = await _remedialCourseRepository.FindAsync(
                    c => c.RemedialSessionId == session.Id, cancellationToken);
                if (courses.Count == 0)
                {
                    throw new DomainException(
                        $"Sélectionnez au moins un cours à repêcher pour {student.FullName}.");
                }
            }
        }

        var observation = NormalizeText(request.Observation, 2000);
        var validationSheet = await _resultValidationService.ValidateAsync(
            schoolId,
            new ResultValidationActionRequest(
                request.AcademicYearId,
                request.ClassRoomId,
                request.AcademicPeriodId,
                observation),
            cancellationToken);

        Guid? remedialPeriodId = null;
        string? remedialPeriodName = null;
        var remedialStudentCount = 0;

        if (context.PeriodContext.CanOfferRepechage)
        {
            var (periodId, periodName, count) = await EnsureRemedialPeriodAsync(
                schoolId,
                request.AcademicYearId,
                request.ClassRoomId,
                request.AcademicPeriodId,
                cancellationToken);
            remedialPeriodId = periodId;
            remedialPeriodName = periodName;
            remedialStudentCount = count;
        }

        var (userId, userName) = ResolveActor();
        await AddAuditAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            null,
            "ClassValidation",
            "Validation officielle des résultats de la classe",
            observation,
            userId,
            userName,
            DateTime.UtcNow,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var message = remedialStudentCount > 0
            ? $"Classe validée. Période de repêchage créée ({remedialStudentCount} élève(s)) — reste fermée jusqu'à ouverture par l'administrateur."
            : "Résultats de la classe validés officiellement.";

        return new ValidateDeliberationClassResultDto(
            true,
            message,
            validationSheet.Status,
            validationSheet.StatusLabel,
            remedialPeriodId,
            remedialPeriodName,
            remedialStudentCount);
    }

    public async Task<ValidateDeliberationClassResultDto> CancelClassValidationAsync(
        Guid schoolId,
        ValidateDeliberationClassRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (_, _, period, periodContext, status) = await EnsureDeliberationContextAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            cancellationToken);

        if (DeliberationPeriodModeResolver.IsPeriodClosed(period))
        {
            throw new DomainException(
                "La période est clôturée ; l'annulation de la validation est interdite.");
        }

        if (status == ResultValidationStatus.Verrouille)
        {
            throw new DomainException(
                "Les résultats sont verrouillés ; annulation de validation interdite.");
        }

        if (status != ResultValidationStatus.Valide || !periodContext.CanCancelValidation)
        {
            throw new DomainException("Aucune validation à annuler pour cette classe / période.");
        }

        var observation = NormalizeText(request.Observation, 2000);
        var validationSheet = await _resultValidationService.CancelValidationAsync(
            schoolId,
            new ResultValidationActionRequest(
                request.AcademicYearId,
                request.ClassRoomId,
                request.AcademicPeriodId,
                observation),
            cancellationToken);

        var (userId, userName) = ResolveActor();
        await AddAuditAsync(
            schoolId,
            request.AcademicYearId,
            request.ClassRoomId,
            request.AcademicPeriodId,
            null,
            "ClassValidationCancel",
            "Annulation de la validation officielle de la classe",
            observation,
            userId,
            userName,
            DateTime.UtcNow,
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ValidateDeliberationClassResultDto(
            true,
            "Validation annulée. Le conseil peut à nouveau être modifié.",
            validationSheet.Status,
            validationSheet.StatusLabel,
            null,
            null,
            0);
    }

    private async Task<(Guid? PeriodId, string? PeriodName, int StudentCount)> EnsureRemedialPeriodAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken)
    {
        var decisions = await _decisionRepository.FindAsync(
            d => d.SchoolId == schoolId
                 && d.AcademicYearId == academicYearId
                 && d.ClassRoomId == classRoomId
                 && d.AcademicPeriodId == academicPeriodId
                 && d.FinalDecision == FinalCouncilDecision.Repechage,
            cancellationToken);

        if (decisions.Count == 0)
        {
            return (null, null, 0);
        }

        var existingRemedial = (await _periodRepository.FindAsync(
            p => p.AcademicYearId == academicYearId && p.IsRemedial,
            cancellationToken)).FirstOrDefault();

        AcademicPeriod remedial;
        if (existingRemedial is null)
        {
            var maxOrder = (await _periodRepository.FindAsync(
                p => p.AcademicYearId == academicYearId, cancellationToken))
                .DefaultIfEmpty()
                .Max(p => p?.OrderIndex ?? 0);

            remedial = new AcademicPeriod
            {
                SchoolId = schoolId,
                AcademicYearId = academicYearId,
                MainPeriodId = null,
                Name = "Repêchage",
                PeriodType = AcademicPeriodType.Semestre,
                OrderIndex = maxOrder + 1,
                Kind = AcademicSubPeriodKind.Examen,
                Status = AcademicSubPeriodStatus.AVenir,
                IsClosed = true,
                IsRemedial = true,
                MaxScore = 20,
                MaxEvaluationCount = 1
            };
            await _periodRepository.AddAsync(remedial, cancellationToken);

            var (userId, userName) = ResolveActor();
            await AddAuditAsync(
                schoolId,
                academicYearId,
                classRoomId,
                academicPeriodId,
                null,
                "RemedialPeriod",
                "Création automatique de la période Repêchage (fermée)",
                null,
                userId,
                userName,
                DateTime.UtcNow,
                cancellationToken);
        }
        else
        {
            remedial = existingRemedial;
        }

        return (remedial.Id, remedial.Name, decisions.Count);
    }

    private async Task EnsureCatalogSeededAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var mentions = await _mentionRepository.FindAsync(m => m.SchoolId == schoolId, cancellationToken);
        if (mentions.Count == 0)
        {
            var defaults = new (string Label, decimal Min, decimal Max, int Order)[]
            {
                ("Satisfaction", 55m, 69m, 1),
                ("Distinction", 70m, 79m, 2),
                ("Grande distinction", 80m, 90m, 3),
                ("Élite", 91m, 100m, 4)
            };

            foreach (var (label, min, max, order) in defaults)
            {
                await _mentionRepository.AddAsync(new ResultMentionDefinition
                {
                    SchoolId = schoolId,
                    Label = label,
                    MinPercentageInclusive = min,
                    MaxPercentageInclusive = max,
                    SortOrder = order,
                    IsActive = true
                }, cancellationToken);
            }
        }

        var conducts = await _conductDefinitionRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        if (conducts.Count == 0)
        {
            var labels = new[]
            {
                "Excellent", "Très bon", "Bon", "Assez bon", "Passable", "Médiocre"
            };
            for (var i = 0; i < labels.Length; i++)
            {
                await _conductDefinitionRepository.AddAsync(new ConductDefinition
                {
                    SchoolId = schoolId,
                    Label = labels[i],
                    SortOrder = i + 1,
                    IsActive = true
                }, cancellationToken);
            }
        }

        if (mentions.Count == 0 || conducts.Count == 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task AddAuditAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid? studentId,
        string actionCode,
        string summary,
        string? observation,
        Guid? userId,
        string userName,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        await _auditRepository.AddAsync(new DeliberationAuditEntry
        {
            SchoolId = schoolId,
            AcademicYearId = academicYearId,
            ClassRoomId = classRoomId,
            AcademicPeriodId = academicPeriodId,
            StudentId = studentId,
            ActionCode = actionCode,
            Summary = summary,
            Observation = observation,
            UserId = userId,
            UserName = userName,
            OccurredAtUtc = occurredAtUtc
        }, cancellationToken);
    }

    private async Task<(AcademicYear Year, ClassRoom ClassRoom, AcademicPeriod Period, DeliberationPeriodContextDto PeriodContext)>
        EnsureDeliberationWritableAsync(
            Guid schoolId,
            Guid academicYearId,
            Guid classRoomId,
            Guid academicPeriodId,
            CancellationToken cancellationToken)
    {
        var (year, classRoom, period, periodContext, status) = await EnsureDeliberationContextAsync(
            schoolId, academicYearId, classRoomId, academicPeriodId, cancellationToken);

        if (status == ResultValidationStatus.Verrouille || periodContext.IsReadOnly)
        {
            throw new DomainException("Les résultats sont verrouillés ; modification interdite.");
        }

        if (status == ResultValidationStatus.Valide)
        {
            throw new DomainException(
                "Les résultats sont déjà validés. Annulez la validation pour modifier le conseil.");
        }

        return (year, classRoom, period, periodContext);
    }

    private async Task<(AcademicYear Year, ClassRoom ClassRoom, AcademicPeriod Period, DeliberationPeriodContextDto PeriodContext, ResultValidationStatus Status)>
        EnsureDeliberationContextAsync(
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

        var validation = (await _validationRepository.FindAsync(
            v => v.SchoolId == schoolId
                 && v.AcademicYearId == academicYearId
                 && v.ClassRoomId == classRoomId
                 && v.AcademicPeriodId == academicPeriodId,
            cancellationToken)).FirstOrDefault();
        var status = validation?.Status ?? ResultValidationStatus.NonValide;

        await EnsureCatalogSeededAsync(schoolId, cancellationToken);

        var pedagogicalClass = classRoom.PedagogicalClassId is Guid pcId
            ? (await _pedagogicalClassRepository.FindAsync(p => p.Id == pcId, cancellationToken)).FirstOrDefault()
            : null;
        var mainPeriods = (await _mainPeriodRepository.FindAsync(
            m => m.SchoolId == schoolId && m.AcademicYearId == academicYearId, cancellationToken)).ToList();
        var yearPeriods = (await _periodRepository.FindAsync(
            p => p.AcademicYearId == academicYearId, cancellationToken)).ToList();
        var periodContext = DeliberationPeriodModeResolver.Resolve(
            period, classRoom, pedagogicalClass, mainPeriods, yearPeriods, status);

        return (year, classRoom, period, periodContext, status);
    }
}
