namespace SchoolManagement.Application.Bulletins.Services;

using SchoolManagement.Application.Bulletins.DTOs;
using SchoolManagement.Application.Bulletins.Interfaces;
using SchoolManagement.Application.Grades.Interfaces;
using SchoolManagement.Domain.Exceptions;

/// <summary>
/// Implémentation skeleton du module Bulletins.
/// <para>
/// Architecture cible :
/// <list type="number">
/// <item>Charger les notes / contextes via Grades</item>
/// <item>Obtenir moyennes, rangs, mentions via <see cref="IResultCalculationService"/> uniquement</item>
/// <item>Projeter vers <see cref="IndividualBulletinDto"/> pour l'UI / PDF — zéro calcul</item>
/// <item>Persister uniquement l'historique d'impression (métadonnées)</item>
/// </list>
/// </para>
/// <para>
/// Prérequis futur : les bulletins officiels exigent un statut
/// <c>ResultValidationStatus.Valide</c> (ou Verrouille) pour la classe / sous-période.
/// </para>
/// Les méthodes lèvent volontairement une exception métier tant que la maquette
/// et la persistance d'historique ne sont pas livrées.
/// </summary>
public sealed class BulletinQueryService : IBulletinQueryService
{
    // Conservé pour le développement suivant : injection du moteur obligatoire.
#pragma warning disable IDE0052
    private readonly IResultCalculationService _resultCalculation;
#pragma warning restore IDE0052

    public BulletinQueryService(IResultCalculationService resultCalculation)
    {
        _resultCalculation = resultCalculation;
    }

    public Task<IndividualBulletinDto> GetIndividualBulletinAsync(
        Guid schoolId,
        IndividualBulletinRequest request,
        CancellationToken cancellationToken = default) =>
        throw NotReady("Bulletin individuel");

    public Task<ClassBulletinsBatchDto> GetClassBulletinsAsync(
        Guid schoolId,
        ClassBulletinsRequest request,
        CancellationToken cancellationToken = default) =>
        throw NotReady("Bulletins de la classe");

    public Task<IReadOnlyList<BulletinPrintHistoryDto>> GetPrintHistoryAsync(
        Guid schoolId,
        Guid? academicYearId = null,
        Guid? classRoomId = null,
        Guid? studentId = null,
        CancellationToken cancellationToken = default) =>
        throw NotReady("Historique des impressions");

    public Task<BulletinPrintHistoryDto> RecordPrintAsync(
        Guid schoolId,
        RecordBulletinPrintRequest request,
        CancellationToken cancellationToken = default) =>
        throw NotReady("Réimpression / enregistrement d'impression");

    private static DomainException NotReady(string feature) =>
        new($"Module Bulletins — « {feature} » : structure prête, maquette et persistance à venir. " +
            "Aucun calcul ne sera effectué dans l'UI ; les résultats viendront de ResultCalculationService.");
}
