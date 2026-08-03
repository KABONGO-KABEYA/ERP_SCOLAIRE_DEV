using System.Globalization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Deliberation.DTOs;
using SchoolManagement.Application.Deliberation.Interfaces;
using SchoolManagement.Application.Grades.Calculation;
using SchoolManagement.Application.Grades.Interfaces;
using SchoolManagement.Application.Mentions;
using SchoolManagement.Application.ResultValidation.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Deliberation;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using AcademicPeriod = SchoolManagement.Domain.Entities.Settings.AcademicPeriod;
using EnrollmentEntity = SchoolManagement.Domain.Entities.Students.Enrollment;

namespace SchoolManagement.Application.Deliberation.Services;

public sealed partial class DeliberationService : IDeliberationService
{
    private readonly IRepository<ClassPeriodResultValidation> _validationRepository;
    private readonly IRepository<PeriodResult> _periodResultRepository;
    private readonly IRepository<EnrollmentEntity> _enrollmentRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<AcademicPeriod> _periodRepository;
    private readonly IRepository<Evaluation> _evaluationRepository;
    private readonly IRepository<GradeEntry> _gradeRepository;
    private readonly IRepository<ClassPeriodDeliberationMinutes> _minutesRepository;
    private readonly IRepository<DeliberationDecision> _decisionRepository;
    private readonly IRepository<DeliberationDecisionEvent> _decisionEventRepository;
    private readonly IRepository<StudentRemedialSession> _remedialSessionRepository;
    private readonly IRepository<StudentRemedialCourse> _remedialCourseRepository;
    private readonly IRepository<CourseExemption> _exemptionRepository;
    private readonly IRepository<CourseAssignment> _courseAssignmentRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<ResultMentionDefinition> _mentionRepository;
    private readonly IRepository<ConductDefinition> _conductDefinitionRepository;
    private readonly IRepository<StudentPeriodConduct> _studentConductRepository;
    private readonly IRepository<PedagogicalBonusPoint> _bonusRepository;
    private readonly IRepository<DeliberationAuditEntry> _auditRepository;
    private readonly IRepository<AcademicMainPeriod> _mainPeriodRepository;
    private readonly IResultValidationService _resultValidationService;
    private readonly IGradeService _gradeService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DeliberationService(
        IRepository<ClassPeriodResultValidation> validationRepository,
        IRepository<PeriodResult> periodResultRepository,
        IRepository<EnrollmentEntity> enrollmentRepository,
        IRepository<Student> studentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<AcademicPeriod> periodRepository,
        IRepository<Evaluation> evaluationRepository,
        IRepository<GradeEntry> gradeRepository,
        IRepository<ClassPeriodDeliberationMinutes> minutesRepository,
        IRepository<DeliberationDecision> decisionRepository,
        IRepository<DeliberationDecisionEvent> decisionEventRepository,
        IRepository<StudentRemedialSession> remedialSessionRepository,
        IRepository<StudentRemedialCourse> remedialCourseRepository,
        IRepository<CourseExemption> exemptionRepository,
        IRepository<CourseAssignment> courseAssignmentRepository,
        IRepository<Course> courseRepository,
        IRepository<ResultMentionDefinition> mentionRepository,
        IRepository<ConductDefinition> conductDefinitionRepository,
        IRepository<StudentPeriodConduct> studentConductRepository,
        IRepository<PedagogicalBonusPoint> bonusRepository,
        IRepository<DeliberationAuditEntry> auditRepository,
        IRepository<AcademicMainPeriod> mainPeriodRepository,
        IResultValidationService resultValidationService,
        IGradeService gradeService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _validationRepository = validationRepository;
        _periodResultRepository = periodResultRepository;
        _enrollmentRepository = enrollmentRepository;
        _studentRepository = studentRepository;
        _classRoomRepository = classRoomRepository;
        _yearRepository = yearRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _periodRepository = periodRepository;
        _evaluationRepository = evaluationRepository;
        _gradeRepository = gradeRepository;
        _minutesRepository = minutesRepository;
        _decisionRepository = decisionRepository;
        _decisionEventRepository = decisionEventRepository;
        _remedialSessionRepository = remedialSessionRepository;
        _remedialCourseRepository = remedialCourseRepository;
        _exemptionRepository = exemptionRepository;
        _courseAssignmentRepository = courseAssignmentRepository;
        _courseRepository = courseRepository;
        _mentionRepository = mentionRepository;
        _conductDefinitionRepository = conductDefinitionRepository;
        _studentConductRepository = studentConductRepository;
        _bonusRepository = bonusRepository;
        _auditRepository = auditRepository;
        _mainPeriodRepository = mainPeriodRepository;
        _resultValidationService = resultValidationService;
        _gradeService = gradeService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeliberationSheetDto> GetSheetAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default)
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

        // La délibération consulte les PeriodResult déjà calculés (pas besoin d'une validation préalable).
        var periodResults = (await _periodResultRepository.FindAsync(
            p => p.ClassRoomId == classRoomId && p.AcademicPeriodId == academicPeriodId,
            cancellationToken)).ToList();
        if (periodResults.Count == 0)
        {
            throw new DomainException(
                "Aucun résultat calculé pour cette classe / période. Lancez d'abord le calcul des résultats.");
        }

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

        var enrollments = await _enrollmentRepository.FindAsync(
            e => e.ClassRoomId == classRoomId && e.AcademicYearId == academicYearId && e.IsActive,
            cancellationToken);
        var studentIds = enrollments.Select(e => e.StudentId).ToHashSet();
        var students = studentIds.Count == 0
            ? []
            : await _studentRepository.FindAsync(s => studentIds.Contains(s.Id), cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);

        var decisions = await _decisionRepository.FindAsync(
            d => d.SchoolId == schoolId
                 && d.AcademicYearId == academicYearId
                 && d.ClassRoomId == classRoomId
                 && d.AcademicPeriodId == academicPeriodId,
            cancellationToken);
        var decisionByStudent = decisions.ToDictionary(d => d.StudentId);

        var conducts = await _studentConductRepository.FindAsync(
            c => c.SchoolId == schoolId
                 && c.ClassRoomId == classRoomId
                 && c.AcademicPeriodId == academicPeriodId,
            cancellationToken);
        var conductDefs = (await _conductDefinitionRepository.FindAsync(
            c => c.SchoolId == schoolId && c.IsActive, cancellationToken))
            .OrderBy(c => c.SortOrder)
            .ToList();
        var conductDefMap = conductDefs.ToDictionary(c => c.Id);
        var conductByStudent = conducts.ToDictionary(c => c.StudentId);

        var bonuses = await _bonusRepository.FindAsync(
            b => b.SchoolId == schoolId
                 && b.ClassRoomId == classRoomId
                 && b.AcademicPeriodId == academicPeriodId
                 && !b.IsCancelled,
            cancellationToken);
        var bonusTotalByStudent = bonuses
            .GroupBy(b => b.StudentId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.PointsAdded));

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
                decisionByStudent.TryGetValue(p.StudentId, out var final);
                conductByStudent.TryGetValue(p.StudentId, out var conduct);
                Guid? conductId = conduct?.ConductDefinitionId;
                string? conductLabel = null;
                if (conduct is not null && conductDefMap.TryGetValue(conduct.ConductDefinitionId, out var def))
                {
                    conductLabel = def.Label;
                }

                var mention = MentionLabelResolver.ResolveOrFallback(
                    p.Appreciation, p.Percentage, mentionDefs);
                if (string.IsNullOrWhiteSpace(p.Appreciation) && !string.IsNullOrWhiteSpace(mention))
                {
                    p.Appreciation = mention;
                    appreciationDirty = true;
                }

                return new DeliberationStudentRowDto(
                    p.StudentId,
                    matricule,
                    name,
                    p.Rank,
                    p.Average,
                    p.Percentage,
                    FormatValue(p.Average),
                    FormatPercentage(p.Percentage),
                    mention,
                    conductId,
                    conductLabel,
                    p.CouncilDecision,
                    FormatDecisionLabel(p.CouncilDecision),
                    final?.FinalDecision,
                    final is null ? "—" : FormatFinalDecisionLabel(final.FinalDecision),
                    final?.Observation ?? conduct?.Observation,
                    bonusTotalByStudent.GetValueOrDefault(p.StudentId),
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

        var admitted = rows.Count(r => r.ProposedDecision == ClassCouncilDecision.Admis);
        var deferred = rows.Count(r => r.ProposedDecision == ClassCouncilDecision.Ajourne);
        var excluded = rows.Count(r => r.ProposedDecision == ClassCouncilDecision.Exclu);
        var pending = periodContext.CanSetFinalDecision
            ? rows.Count(r => r.FinalDecision is null)
            : rows.Count(r => r.ProposedDecision == ClassCouncilDecision.EnAttente);
        var missingConduct = rows.Count(r => string.IsNullOrWhiteSpace(r.ConductLabel));
        var studentCount = Math.Max(enrollments.Count, rows.Count);
        var classAverage = rows.Count == 0 ? (decimal?)null : rows.Average(r => r.Average);
        var decided = admitted + deferred + excluded;
        var successRate = decided == 0 ? (decimal?)null : Math.Round(100m * admitted / decided, 2);

        var classLabel = string.IsNullOrWhiteSpace(classRoom.Name)
            ? classRoom.Code
            : classRoom.Name;

        var specialCases = await BuildSpecialCasesAsync(
            classRoomId, academicPeriodId, rows, cancellationToken);

        var courseOptions = await LoadCourseOptionsAsync(academicYearId, classRoomId, cancellationToken);

        return new DeliberationSheetDto(
            year.Id,
            year.Label,
            classRoom.Id,
            classLabel,
            period.Id,
            period.Name,
            status,
            FormatStatusLabel(status),
            validation?.ValidatedAtUtc,
            validation?.ValidatedByUserName,
            periodContext,
            new DeliberationSummaryDto(
                studentCount,
                admitted,
                deferred,
                excluded,
                pending,
                missingConduct,
                classAverage,
                FormatValue(classAverage),
                successRate,
                successRate is null
                    ? "—"
                    : $"{successRate.Value.ToString("0.##", CultureInfo.CurrentCulture)} %"),
            rows,
            conductDefs.Select(c => new ConductOptionDto(c.Id, c.Label, c.SortOrder)).ToList(),
            courseOptions,
            specialCases);
    }

    /// <summary>
    /// Construit le panneau conseil à partir des PeriodResult déjà enregistrés.
    /// Les absences sont lues depuis les cotes persistées (pas de recalcul).
    /// </summary>
    private async Task<DeliberationSpecialCasesDto> BuildSpecialCasesAsync(
        Guid classRoomId,
        Guid academicPeriodId,
        IReadOnlyList<DeliberationStudentRowDto> rows,
        CancellationToken cancellationToken)
    {
        var rowByStudent = rows.ToDictionary(r => r.StudentId);

        var evaluations = await _evaluationRepository.FindAsync(
            e => e.ClassRoomId == classRoomId && e.AcademicPeriodId == academicPeriodId,
            cancellationToken);
        var evaluationIds = evaluations.Select(e => e.Id).ToList();

        var justifiedCounts = new Dictionary<Guid, int>();
        var unjustifiedCounts = new Dictionary<Guid, int>();

        if (evaluationIds.Count > 0)
        {
            var absentGrades = await _gradeRepository.FindAsync(
                g => evaluationIds.Contains(g.EvaluationId) && g.IsAbsent,
                cancellationToken);

            foreach (var grade in absentGrades)
            {
                if (!rowByStudent.ContainsKey(grade.StudentId))
                {
                    continue;
                }

                var status = ScoreEntryStatusMapper.FromGradeEntry(
                    grade.IsAbsent, grade.Comment, grade.IsAbsent ? null : grade.Score);

                if (status == ScoreEntryStatus.AbsentJustified)
                {
                    justifiedCounts[grade.StudentId] =
                        justifiedCounts.GetValueOrDefault(grade.StudentId) + 1;
                }
                else if (status == ScoreEntryStatus.AbsentUnjustified)
                {
                    unjustifiedCounts[grade.StudentId] =
                        unjustifiedCounts.GetValueOrDefault(grade.StudentId) + 1;
                }
            }
        }

        static DeliberationSpecialCaseItemDto Item(
            DeliberationStudentRowDto row,
            string code,
            string label,
            string detail) =>
            new(row.StudentId, row.RegistrationNumber, row.FullName, code, label, detail);

        var deferredItems = rows
            .Where(r => r.ProposedDecision == ClassCouncilDecision.Ajourne)
            .Select(r => Item(r, "DEFERRED", "Ajourné",
                $"Décision : Ajourné · {r.PercentageDisplay}"))
            .ToList();

        var excludedItems = rows
            .Where(r => r.ProposedDecision == ClassCouncilDecision.Exclu)
            .Select(r => Item(r, "EXCLUDED", "Exclu",
                $"Décision : Exclu · {r.PercentageDisplay}"))
            .ToList();

        var particularItems = rows
            .Where(r => r.FinalDecision is null
                        && (r.ProposedDecision == ClassCouncilDecision.EnAttente
                            || string.Equals(r.ProposedDecisionLabel, "En attente de décision", StringComparison.Ordinal)))
            .Select(r => Item(r, "PARTICULAR", "Décision particulière",
                $"Décision en attente · {r.PercentageDisplay}"))
            .ToList();

        var justifiedItems = justifiedCounts
            .OrderBy(kv => rowByStudent[kv.Key].FullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(kv =>
            {
                var row = rowByStudent[kv.Key];
                var n = kv.Value;
                return Item(row, "ABS_J", "Absence justifiée",
                    n == 1 ? "1 absence justifiée" : $"{n} absences justifiées");
            })
            .ToList();

        var unjustifiedItems = unjustifiedCounts
            .OrderBy(kv => rowByStudent[kv.Key].FullName, StringComparer.CurrentCultureIgnoreCase)
            .Select(kv =>
            {
                var row = rowByStudent[kv.Key];
                var n = kv.Value;
                return Item(row, "ABS_I", "Absence injustifiée",
                    n == 1 ? "1 absence injustifiée" : $"{n} absences injustifiées");
            })
            .ToList();

        return new DeliberationSpecialCasesDto(
            deferredItems,
            excludedItems,
            justifiedItems,
            unjustifiedItems,
            particularItems);
    }

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

    /// <summary>
    /// Colonne « Décision » : libellé % tant qu'aucune décision du Conseil n'est enregistrée ;
    /// sinon la décision officielle (ex. « En attente de décision » → « Redouble »).
    /// N'altère pas le moteur de calcul ni PeriodResult.CouncilDecision.
    /// </summary>
    private static string FormatDecisionColumnLabel(decimal percentage, FinalCouncilDecision? final) =>
        final is FinalCouncilDecision decided
            ? FormatFinalDecisionLabel(decided)
            : FormatAutomaticDecisionLabel(percentage);

    private static string FormatAutomaticDecisionLabel(decimal percentage) =>
        percentage switch
        {
            >= 91m => "Élite",
            >= 80m => "Grande distinction",
            >= 70m => "Distinction",
            >= 55m => "Satisfaction",
            _ => "En attente de décision"
        };

    private static string FormatFinalDecisionLabel(FinalCouncilDecision decision) =>
        decision switch
        {
            FinalCouncilDecision.Satisfaction => "Satisfaction",
            FinalCouncilDecision.Distinction => "Distinction",
            FinalCouncilDecision.GrandeDistinction => "Grande distinction",
            FinalCouncilDecision.Elite => "Élite",
            FinalCouncilDecision.PasseDeClasse => "Passe de classe",
            FinalCouncilDecision.Redouble => "Redouble",
            FinalCouncilDecision.PasseAilleurs => "Passe ailleurs",
            FinalCouncilDecision.Repechage => "Repêchage",
            FinalCouncilDecision.Exclu => "Exclu",
            FinalCouncilDecision.Dispense => "Dispensé",
            _ => "—"
        };

    private static string FormatStatusLabel(ResultValidationStatus status) =>
        status switch
        {
            ResultValidationStatus.Valide => "Validé",
            ResultValidationStatus.Verrouille => "Verrouillé",
            _ => "Non validé"
        };
}
