namespace SchoolManagement.Application.Bulletins.Interfaces;

using SchoolManagement.Application.Bulletins.DTOs;

/// <summary>
/// Façade Bulletins — Clean Architecture.
/// <para>
/// Règle stricte : aucun calcul de moyenne, rang, pourcentage, mention ou décision.
/// Toutes les données académiques sont obtenues via <c>IResultCalculationService</c>
/// (ou les services Grades qui l'encapsulent : résultat individuel / feuille de classe).
/// </para>
/// <para>
/// Responsabilités prévues :
/// <list type="bullet">
/// <item>Bulletin individuel — projection d'affichage / PDF</item>
/// <item>Bulletins de la classe — lot pour impression groupée</item>
/// <item>Réimpression — rejouer un bulletin déjà produit</item>
/// <item>Historique des impressions — journal (sans recalcul)</item>
/// </list>
/// </para>
/// </summary>
public interface IBulletinQueryService
{
    /// <summary>Construit le bulletin d'un élève à partir des résultats moteur (pas de calcul local).</summary>
    Task<IndividualBulletinDto> GetIndividualBulletinAsync(
        Guid schoolId,
        IndividualBulletinRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Construit les bulletins de toute la classe à partir des résultats moteur.</summary>
    Task<ClassBulletinsBatchDto> GetClassBulletinsAsync(
        Guid schoolId,
        ClassBulletinsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Historique des impressions / réimpressions (métadonnées).</summary>
    Task<IReadOnlyList<BulletinPrintHistoryDto>> GetPrintHistoryAsync(
        Guid schoolId,
        Guid? academicYearId = null,
        Guid? classRoomId = null,
        Guid? studentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Enregistre une impression ou réimpression (pas de recalcul des notes).</summary>
    Task<BulletinPrintHistoryDto> RecordPrintAsync(
        Guid schoolId,
        RecordBulletinPrintRequest request,
        CancellationToken cancellationToken = default);
}
