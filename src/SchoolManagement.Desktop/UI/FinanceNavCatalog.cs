using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.UI;

public sealed class FinanceNavGroup
{
    public required string Title { get; init; }

    public required IReadOnlyList<FinanceNavItem> Items { get; init; }
}

public sealed class FinanceNavItem
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string IconKind { get; init; }

    public required FinanceSection Section { get; init; }

    public required string Subtitle { get; init; }
}

public static class FinanceNavCatalog
{
    public static IReadOnlyList<FinanceNavGroup> Groups { get; } =
    [
        new FinanceNavGroup
        {
            Title = "Opérations",
            Items =
            [
                new FinanceNavItem
                {
                    Key = "encaissements",
                    Title = "Encaissements",
                    IconKind = "CashPlus",
                    Section = FinanceSection.Encaissements,
                    Subtitle = "Suivi des paiements scolaires et opérations d'encaissement"
                },
                new FinanceNavItem
                {
                    Key = "categories-tarifaires",
                    Title = "Catégories tarifaires",
                    IconKind = "TagOutline",
                    Section = FinanceSection.CategoriesTarifaires,
                    Subtitle = "Attribution des catégories tarifaires aux élèves"
                }
            ]
        },
        new FinanceNavGroup
        {
            Title = "Rapports",
            Items =
            [
                new FinanceNavItem
                {
                    Key = "rapports-financiers",
                    Title = "Rapports financiers",
                    IconKind = "FileChartOutline",
                    Section = FinanceSection.Rapports,
                    Subtitle = "États et rapports de la comptabilité scolaire"
                }
            ]
        },
        new FinanceNavGroup
        {
            Title = "Répartition des recettes",
            Items =
            [
                new FinanceNavItem
                {
                    Key = "consultation-repartitions",
                    Title = "Consultation des répartitions",
                    IconKind = "ChartPie",
                    Section = FinanceSection.Consultation,
                    Subtitle = "Historique définitif des répartitions de recettes"
                }
            ]
        }
    ];

    public static FinanceNavItem? FindByKey(string key) =>
        Groups.SelectMany(group => group.Items).FirstOrDefault(item => item.Key == key);

    public static FinanceNavItem DefaultItem =>
        Groups[0].Items[0];
}

public static class FinanceNavigationBridge
{
    public static event Action<FinanceNavItem>? SectionSelected;

    public static FinanceNavItem? CurrentSelection { get; private set; }

    public static void Select(FinanceNavItem item)
    {
        CurrentSelection = item;
        SectionSelected?.Invoke(item);
    }

    public static void ApplyToViewModel(FinanceHubViewModel viewModel, FinanceNavItem item) =>
        viewModel.ApplyNavigation(item);
}
