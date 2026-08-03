using SchoolManagement.Application.Deliberation.DTOs;

namespace SchoolManagement.Application.Deliberation.Interfaces;

/// <summary>
/// Conseil de classe : consultation PeriodResult, bonus, conduite, décisions fin d'année, validation de classe.
/// Aucun recalcul dans l'UI — le service déclenche le moteur métier quand nécessaire.
/// </summary>
public interface IDeliberationService
{
    Task<DeliberationSheetDto> GetSheetAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    Task<DeliberationMinutesDto> GetMinutesAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default);

    Task<DeliberationMinutesDto> SaveMinutesAsync(
        Guid schoolId,
        SaveDeliberationMinutesRequest request,
        CancellationToken cancellationToken = default);

    Task<DeliberationDecisionDialogDto> GetDecisionDialogAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<DeliberationDecisionDialogDto> SaveDecisionAsync(
        Guid schoolId,
        SaveDeliberationDecisionRequest request,
        CancellationToken cancellationToken = default);

    Task<DeliberationSheetDto> SaveConductAsync(
        Guid schoolId,
        SaveStudentConductRequest request,
        CancellationToken cancellationToken = default);

    Task<DeliberationSheetDto> SavePedagogicalBonusAsync(
        Guid schoolId,
        SavePedagogicalBonusRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PedagogicalBonusDto>> GetPedagogicalBonusesAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid? studentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Contexte d'ajout de points : note actuelle, bonus déjà accordés, points encore ajoutables.
    /// </summary>
    Task<PedagogicalBonusDialogDto> GetPedagogicalBonusDialogAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<ValidateDeliberationClassResultDto> ValidateClassAsync(
        Guid schoolId,
        ValidateDeliberationClassRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Annule une validation de classe déjà effectuée, uniquement si la sous-période n'est pas clôturée.
    /// </summary>
    Task<ValidateDeliberationClassResultDto> CancelClassValidationAsync(
        Guid schoolId,
        ValidateDeliberationClassRequest request,
        CancellationToken cancellationToken = default);
}
