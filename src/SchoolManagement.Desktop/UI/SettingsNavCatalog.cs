using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.UI;

public sealed class SettingsNavGroup
{
    public required string Title { get; init; }

    public required IReadOnlyList<SettingsNavItem> Items { get; init; }
}

public sealed class SettingsNavItem
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string IconKind { get; init; }

    public SettingsSection? Section { get; init; }

    public bool IsPlaceholder => Section is null;
}

public static class SettingsNavCatalog
{
    public static IReadOnlyList<SettingsNavGroup> Groups { get; } =
    [
        new SettingsNavGroup
        {
            Title = "Référentiels",
            Items =
            [
                new SettingsNavItem { Key = "etablissement", Title = "Établissement", IconKind = "Domain", Section = SettingsSection.Etablissement },
                new SettingsNavItem { Key = "structure-pedagogique", Title = "Structure pédagogique", IconKind = "GoogleClassroom", Section = SettingsSection.StructurePedagogique },
                new SettingsNavItem { Key = "annees-scolaires", Title = "Années scolaires", IconKind = "CalendarRange", Section = SettingsSection.AnneesScolaires },
                new SettingsNavItem { Key = "matieres", Title = "Configuration des cours", IconKind = "BookEducation", Section = SettingsSection.Matieres },
                new SettingsNavItem { Key = "geographie", Title = "Géographie", IconKind = "Earth", Section = SettingsSection.Geographie },
                new SettingsNavItem { Key = "utilisateurs", Title = "Utilisateurs", IconKind = "AccountCog", Section = SettingsSection.Utilisateurs }
            ]
        },
        new SettingsNavGroup
        {
            Title = "Configuration financière",
            Items =
            [
                new SettingsNavItem { Key = "frais-scolaires", Title = "Frais scolaires", IconKind = "CashMultiple", Section = SettingsSection.FraisScolaires },
                new SettingsNavItem { Key = "repartition-recettes", Title = "Répartition des recettes", IconKind = "ChartPie", Section = SettingsSection.RepartitionRecettes },
                new SettingsNavItem { Key = "retenues", Title = "Retenues", IconKind = "PercentOutline", Section = SettingsSection.Retenues },
                new SettingsNavItem { Key = "monnaies", Title = "Monnaies", IconKind = "CurrencyUsd", Section = SettingsSection.Monnaies },
                new SettingsNavItem { Key = "taux-change", Title = "Taux de change", IconKind = "SwapHorizontal", Section = SettingsSection.TauxChange },
                new SettingsNavItem { Key = "historique-taux", Title = "Historique des taux", IconKind = "History", Section = SettingsSection.HistoriqueTaux }
            ]
        },
        new SettingsNavGroup
        {
            Title = "Administration scolaire",
            Items =
            [
                new SettingsNavItem { Key = "reglement", Title = "Règlement intérieur", IconKind = "TextBoxOutline", Section = SettingsSection.Reglement },
                new SettingsNavItem { Key = "calendrier", Title = "Calendrier scolaire", IconKind = "CalendarMonth", Section = null },
                new SettingsNavItem { Key = "types-evaluations", Title = "Types d'évaluations", IconKind = "ClipboardListOutline", Section = null },
                new SettingsNavItem { Key = "coefficients", Title = "Coefficients", IconKind = "Numeric", Section = null }
            ]
        },
        new SettingsNavGroup
        {
            Title = "Administration système",
            Items =
            [
                new SettingsNavItem { Key = "sauvegarde", Title = "Sauvegarde & restauration", IconKind = "DatabaseOutline", Section = null },
                new SettingsNavItem { Key = "sync-cloud", Title = "Synchronisation cloud", IconKind = "CloudSync", Section = SettingsSection.SyncCloud },
                new SettingsNavItem { Key = "mises-a-jour", Title = "Mises à jour", IconKind = "Update", Section = SettingsSection.MisesAJour },
                new SettingsNavItem { Key = "journal", Title = "Journal d'activités", IconKind = "History", Section = null },
                new SettingsNavItem { Key = "parametres-systeme", Title = "Paramètres système", IconKind = "CogOutline", Section = null },
                new SettingsNavItem { Key = "personnalisation", Title = "Personnalisation", IconKind = "PaletteOutline", Section = null },
                new SettingsNavItem { Key = "design", Title = "Design", IconKind = "BrushOutline", Section = null }
            ]
        }
    ];

    public static SettingsNavItem? FindByKey(string key) =>
        Groups.SelectMany(group => group.Items).FirstOrDefault(item => item.Key == key);
}
