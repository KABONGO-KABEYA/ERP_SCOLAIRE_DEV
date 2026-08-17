namespace SchoolManagement.Application.PedagogicalPeriods.Interfaces;

using SchoolManagement.Application.PedagogicalPeriods.DTOs;
using SchoolManagement.Domain.Enums;

public interface IPedagogicalPeriodService
{
    Task<PedagogicalPeriodStructureDto> GetStructureAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<PedagogicalPeriodStructureDto> CreateDefaultStructureAsync(
        Guid schoolId,
        CreatePedagogicalStructureRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Seed de la structure pédagogique par défaut d'une année nouvelle
    /// (création d'année / configuration initiale). Ce n'est pas une opération
    /// de gestion métier : n'exige pas <c>pedagogical-periods.manage</c>.
    /// </summary>
    Task<PedagogicalPeriodStructureDto> SeedDefaultStructureForNewYearAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<PedagogicalSubPeriodDto> OpenSubPeriodAsync(
        Guid schoolId,
        Guid subPeriodId,
        OpenSubPeriodRequest? request = null,
        CancellationToken cancellationToken = default);

    Task<PedagogicalSubPeriodDto> CloseSubPeriodAsync(
        Guid schoolId,
        Guid subPeriodId,
        CancellationToken cancellationToken = default);

    Task<PedagogicalSubPeriodDto> LockSubPeriodAsync(
        Guid schoolId,
        Guid subPeriodId,
        CancellationToken cancellationToken = default);

    Task<PedagogicalSubPeriodDto> UnlockSubPeriodAsync(
        Guid schoolId,
        Guid subPeriodId,
        CancellationToken cancellationToken = default);

    Task<PedagogicalSubPeriodDto> UpdateSubPeriodSettingsAsync(
        Guid schoolId,
        Guid subPeriodId,
        UpdateSubPeriodSettingsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Propose des dates séquentielles sans chevauchement pour les sous-périodes « À venir ».
    /// </summary>
    Task<PedagogicalPeriodStructureDto> ProposeSequentialDatesAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<ActiveSubPeriodDto?> GetActiveSubPeriodAsync(
        Guid schoolId,
        Guid academicYearId,
        PedagogicalCycleGroup cycleGroup,
        CancellationToken cancellationToken = default);
}
