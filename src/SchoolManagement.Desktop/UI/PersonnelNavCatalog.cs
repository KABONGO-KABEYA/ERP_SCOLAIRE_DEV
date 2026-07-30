using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.UI;

public enum PersonnelSection
{
    Liste = 0,
    Nouveau = 1,
    Fonctions = 2,
    Departements = 3,
    Contrats = 4,
    Presences = 5,
    Conges = 6,
    Documents = 7,
    Historique = 8
}

public sealed class PersonnelNavGroup
{
    public required string Title { get; init; }

    public required IReadOnlyList<PersonnelNavItem> Items { get; init; }
}

public sealed class PersonnelNavItem
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string IconKind { get; init; }

    public required PersonnelSection Section { get; init; }

    public required string Subtitle { get; init; }

    public bool IsPlaceholder =>
        Section is PersonnelSection.Contrats
            or PersonnelSection.Presences
            or PersonnelSection.Conges
            or PersonnelSection.Documents
            or PersonnelSection.Historique;
}

public static class PersonnelNavCatalog
{
    public static IReadOnlyList<PersonnelNavGroup> Groups { get; } =
    [
        new PersonnelNavGroup
        {
            Title = "Gestion",
            Items =
            [
                new PersonnelNavItem
                {
                    Key = "liste",
                    Title = "Liste du personnel",
                    IconKind = "AccountGroupOutline",
                    Section = PersonnelSection.Liste,
                    Subtitle = "Gestion des ressources humaines"
                },
                new PersonnelNavItem
                {
                    Key = "nouveau",
                    Title = "Nouveau personnel",
                    IconKind = "AccountPlusOutline",
                    Section = PersonnelSection.Nouveau,
                    Subtitle = "Création et modification d'une fiche personnel"
                }
            ]
        },
        new PersonnelNavGroup
        {
            Title = "Organisation",
            Items =
            [
                new PersonnelNavItem
                {
                    Key = "fonctions",
                    Title = "Fonctions / Postes",
                    IconKind = "BadgeAccountOutline",
                    Section = PersonnelSection.Fonctions,
                    Subtitle = "Référentiel des fonctions et postes"
                },
                new PersonnelNavItem
                {
                    Key = "departements",
                    Title = "Départements",
                    IconKind = "OfficeBuildingOutline",
                    Section = PersonnelSection.Departements,
                    Subtitle = "Structure organisationnelle de l'établissement"
                }
            ]
        },
        new PersonnelNavGroup
        {
            Title = "Suivi RH",
            Items =
            [
                new PersonnelNavItem
                {
                    Key = "contrats",
                    Title = "Contrats",
                    IconKind = "FileDocumentOutline",
                    Section = PersonnelSection.Contrats,
                    Subtitle = "Gestion des contrats — disponible prochainement"
                },
                new PersonnelNavItem
                {
                    Key = "presences",
                    Title = "Présences",
                    IconKind = "CalendarCheckOutline",
                    Section = PersonnelSection.Presences,
                    Subtitle = "Suivi des présences — disponible prochainement"
                },
                new PersonnelNavItem
                {
                    Key = "conges",
                    Title = "Congés",
                    IconKind = "BeachOutline",
                    Section = PersonnelSection.Conges,
                    Subtitle = "Gestion des congés — disponible prochainement"
                },
                new PersonnelNavItem
                {
                    Key = "documents",
                    Title = "Documents",
                    IconKind = "FolderOutline",
                    Section = PersonnelSection.Documents,
                    Subtitle = "Archives documentaires — disponible prochainement"
                },
                new PersonnelNavItem
                {
                    Key = "historique",
                    Title = "Historique",
                    IconKind = "History",
                    Section = PersonnelSection.Historique,
                    Subtitle = "Journal des événements RH — disponible prochainement"
                }
            ]
        }
    ];

    public static PersonnelNavItem? FindByKey(string key) =>
        Groups.SelectMany(g => g.Items).FirstOrDefault(i => i.Key == key);

    public static PersonnelNavItem DefaultItem => Groups[0].Items[0];
}

public static class PersonnelNavigationBridge
{
    public static event Action<PersonnelNavItem>? SectionSelected;

    public static event Action<Guid?>? EditPersonnelRequested;

    public static PersonnelNavItem? CurrentSelection { get; private set; }

    public static void Select(PersonnelNavItem item)
    {
        CurrentSelection = item;
        SectionSelected?.Invoke(item);
    }

    public static void RequestEdit(Guid? personnelId) => EditPersonnelRequested?.Invoke(personnelId);

    public static void ApplyToViewModel(PersonnelHubViewModel viewModel, PersonnelNavItem item) =>
        viewModel.ApplyNavigation(item);
}
