namespace SchoolManagement.Application.Grades.Interfaces;

using SchoolManagement.Application.Grades.DTOs;

public interface IGradeService
{
    Task<IReadOnlyList<EvaluationTypeDto>> GetEvaluationTypesAsync(Guid schoolId, CancellationToken cancellationToken = default);

    /// <summary>Ouvre une session de cotation après identification de l'enseignant.</summary>
    Task<CotationSessionDto> OpenCotationSessionAsync(
        Guid schoolId,
        OpenCotationSessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Périodes filtrées selon le cycle de la classe (trimestres vs semestres).</summary>
    Task<IReadOnlyList<CotationPeriodDto>> GetCotationPeriodsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rafraîchit les affectations + indicateurs d'avancement (évaluations / dernière activité)
    /// pour la session enseignant déjà ouverte.
    /// </summary>
    Task<IReadOnlyList<CotationAssignmentDto>> GetCotationAssignmentsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid teacherId,
        CancellationToken cancellationToken = default);

    Task<EvaluationDto> CreateEvaluationAsync(Guid schoolId, CreateEvaluationRequest request, CancellationToken cancellationToken = default);

    Task<EvaluationDto> UpdateEvaluationAsync(
        Guid schoolId,
        Guid evaluationId,
        UpdateEvaluationRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteEvaluationAsync(Guid schoolId, Guid evaluationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EvaluationDto>> GetEvaluationsByClassAsync(
        Guid schoolId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GradeEntryDto>> GetGradesAsync(Guid schoolId, Guid evaluationId, CancellationToken cancellationToken = default);

    Task SubmitGradesAsync(Guid schoolId, SubmitGradesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grille consolidée lecture seule (élèves × évaluations) pour un cours / sous-période.
    /// </summary>
    Task<CourseNotesGridDto> GetCourseNotesGridAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid courseId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    /// <summary>Contexte de consultation (sous-périodes + périodes principales) pour la Vue globale.</summary>
    Task<PedagogicalSheetContextDto> GetPedagogicalSheetContextAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        CancellationToken cancellationToken = default);

    /// <summary>Feuille pédagogique officielle (lecture seule) pour une sous-période ou période principale.</summary>
    Task<PedagogicalSheetDto> GetPedagogicalSheetAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        PedagogicalSheetPeriodMode mode,
        Guid periodId,
        Guid teacherId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Feuille officielle des résultats de classe (cours × élèves + classement).
    /// Calcul exclusivement via <c>IResultCalculationService</c>.
    /// </summary>
    Task<ClassResultsSheetDto> GetClassResultsSheetAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        PedagogicalSheetPeriodMode mode,
        Guid periodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Résultat individuel d'un élève (base bulletin). Calcul via <c>IResultCalculationService</c>.
    /// </summary>
    Task<IndividualResultDto> GetIndividualResultAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid studentId,
        PedagogicalSheetPeriodMode mode,
        Guid periodId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PeriodResultDto>> CalculatePeriodResultsAsync(
        Guid schoolId,
        CalculatePeriodResultsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PeriodResultDto>> GetPeriodResultsAsync(
        Guid schoolId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Après clôture d'un examen : calcule les résultats de période pour toutes les classes concernées.
    /// </summary>
    Task CalculateResultsForClosedExamAsync(
        Guid schoolId,
        Guid examSubPeriodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prépare la grille de cotation globale (classe × cours de l'enseignant).
    /// N'écrit aucune évaluation.
    /// </summary>
    Task<GlobalCotationGridDto> GetGlobalCotationGridAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid teacherId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enregistre la cotation globale : crée évaluations + notes en une seule transaction
    /// uniquement pour les cours ayant au moins une note.
    /// </summary>
    Task<SaveGlobalCotationResultDto> SaveGlobalCotationAsync(
        Guid schoolId,
        SaveGlobalCotationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Liste les vagues d'évaluations déjà enregistrées pour la classe / sous-période
    /// (regroupement type + libellé), dans le périmètre de l'enseignant.
    /// </summary>
    Task<IReadOnlyList<GlobalCotationSessionSummaryDto>> GetGlobalCotationSessionsAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid teacherId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Charge les notes d'une vague existante pour affichage / modification dans la grille globale.
    /// </summary>
    Task<GlobalCotationSessionLoadDto> LoadGlobalCotationSessionAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid teacherId,
        Guid evaluationTypeId,
        string title,
        CancellationToken cancellationToken = default);
}
