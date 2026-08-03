using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.UI;

public enum ResultsSection
{
    ParClasse = 0,
    Individuel = 1,
    ValidationResultats = 2,
    Deliberation = 3,
    BulletinIndividuel = 4,
    BulletinsClasse = 5,
    BulletinsReimpression = 6,
    BulletinsHistoriqueImpressions = 7,
    Statistiques = 8,
    Historique = 9
}

public sealed class ResultsNavGroup
{
    public required string Title { get; init; }

    public required IReadOnlyList<ResultsNavItem> Items { get; init; }
}

public sealed class ResultsNavItem
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string IconKind { get; init; }

    public required ResultsSection Section { get; init; }

    public required string Subtitle { get; init; }

    public bool IsPlaceholder =>
        Section is not ResultsSection.ParClasse
            and not ResultsSection.Individuel
            and not ResultsSection.Deliberation;

    public bool IsBulletinSection =>
        Section is ResultsSection.BulletinIndividuel
            or ResultsSection.BulletinsClasse
            or ResultsSection.BulletinsReimpression
            or ResultsSection.BulletinsHistoriqueImpressions;
}

public static class ResultsNavCatalog
{
    public static IReadOnlyList<ResultsNavGroup> Groups { get; } =
    [
        new ResultsNavGroup
        {
            Title = "Consultation",
            Items =
            [
                new ResultsNavItem
                {
                    Key = "par-classe",
                    Title = "Résultats par classe",
                    IconKind = "GoogleClassroom",
                    Section = ResultsSection.ParClasse,
                    Subtitle = "Résultats calculés par le moteur pour une classe et une période"
                },
                new ResultsNavItem
                {
                    Key = "individuel",
                    Title = "Résultat individuel",
                    IconKind = "AccountSchoolOutline",
                    Section = ResultsSection.Individuel,
                    Subtitle = "Détail des résultats d'un élève — base du bulletin scolaire"
                }
            ]
        },
        new ResultsNavGroup
        {
            Title = "Conseil de classe",
            Items =
            [
                new ResultsNavItem
                {
                    Key = "deliberation",
                    Title = "Délibération",
                    IconKind = "Gavel",
                    Section = ResultsSection.Deliberation,
                    Subtitle = "Espace unique du Conseil de classe — validation, résultats, décisions, PV et historique"
                }
            ]
        },
        new ResultsNavGroup
        {
            Title = "Bulletins",
            Items =
            [
                new ResultsNavItem
                {
                    Key = "bulletin-individuel",
                    Title = "Bulletin individuel",
                    IconKind = "FileAccountOutline",
                    Section = ResultsSection.BulletinIndividuel,
                    Subtitle = "Bientôt disponible — données via ResultCalculationService (aucun calcul UI)"
                },
                new ResultsNavItem
                {
                    Key = "bulletins-classe",
                    Title = "Bulletins de la classe",
                    IconKind = "FileDocumentMultipleOutline",
                    Section = ResultsSection.BulletinsClasse,
                    Subtitle = "Bientôt disponible — lot de bulletins depuis le moteur de résultats"
                },
                new ResultsNavItem
                {
                    Key = "bulletins-reimpression",
                    Title = "Réimpression",
                    IconKind = "PrinterPos",
                    Section = ResultsSection.BulletinsReimpression,
                    Subtitle = "Bientôt disponible — réimpression sans recalcul"
                },
                new ResultsNavItem
                {
                    Key = "bulletins-historique",
                    Title = "Historique des impressions",
                    IconKind = "History",
                    Section = ResultsSection.BulletinsHistoriqueImpressions,
                    Subtitle = "Bientôt disponible — journal des impressions / réimpressions"
                }
            ]
        },
        new ResultsNavGroup
        {
            Title = "Documents & suivi",
            Items =
            [
                new ResultsNavItem
                {
                    Key = "statistiques",
                    Title = "Statistiques pédagogiques",
                    IconKind = "ChartBar",
                    Section = ResultsSection.Statistiques,
                    Subtitle = "Bientôt disponible"
                },
                new ResultsNavItem
                {
                    Key = "historique",
                    Title = "Historique des résultats",
                    IconKind = "ClipboardTextClockOutline",
                    Section = ResultsSection.Historique,
                    Subtitle = "Bientôt disponible"
                }
            ]
        }
    ];

    public static ResultsNavItem? FindByKey(string key) =>
        Groups.SelectMany(g => g.Items).FirstOrDefault(i => i.Key == key);

    public static ResultsNavItem DefaultItem => Groups[0].Items[0];
}

public static class ResultsNavigationBridge
{
    public static event Action<ResultsNavItem>? SectionSelected;

    public static event Action<IndividualResultNavRequest>? IndividualRequested;

    public static ResultsNavItem? CurrentSelection { get; private set; }

    public static void Select(ResultsNavItem item)
    {
        CurrentSelection = item;
        SectionSelected?.Invoke(item);
    }

    public static void RequestIndividual(IndividualResultNavRequest request) =>
        IndividualRequested?.Invoke(request);

    public static void ApplyToViewModel(ResultsHubViewModel viewModel, ResultsNavItem item) =>
        viewModel.ApplyNavigation(item);
}

public sealed record IndividualResultNavRequest(
    Guid StudentId,
    Guid AcademicYearId,
    Guid ClassRoomId,
    PedagogicalSheetPeriodMode Mode,
    Guid PeriodId);
