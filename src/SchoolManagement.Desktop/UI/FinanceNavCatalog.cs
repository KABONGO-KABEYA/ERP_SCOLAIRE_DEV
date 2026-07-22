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
                    Subtitle = "Recettes réalisées, répartitions et états de la comptabilité scolaire"
                },
                new FinanceNavItem
                {
                    Key = "situation-paiements",
                    Title = "Situation des paiements",
                    IconKind = "ClipboardListOutline",
                    Section = FinanceSection.SituationPaiements,
                    Subtitle = "Liste des élèves selon leur situation de paiement (en ordre / non en ordre)"
                }
            ]
        },
        new FinanceNavGroup
        {
            Title = "Comptabilité",
            Items =
            [
                new FinanceNavItem
                {
                    Key = "demandes-paiement",
                    Title = "Demandes de paiement",
                    IconKind = "FileDocumentEditOutline",
                    Section = FinanceSection.DemandesPaiement,
                    Subtitle = "Demandes de décaissement et workflow d'approbation"
                },
                new FinanceNavItem
                {
                    Key = "depenses",
                    Title = "Dépenses",
                    IconKind = "CashMinus",
                    Section = FinanceSection.Depenses,
                    Subtitle = "Dépenses effectuées imputées sur les comptes bénéficiaires"
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
