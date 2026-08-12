using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Navigation;

public abstract record DesktopViewTarget;

public sealed record DirectDesktopViewTarget(Type ViewModelType) : DesktopViewTarget;

public sealed record SettingsDesktopViewTarget(SettingsNavItem Item) : DesktopViewTarget;

public sealed record FinanceDesktopViewTarget(FinanceNavItem Item) : DesktopViewTarget;

public sealed record PersonnelDesktopViewTarget(PersonnelNavItem Item) : DesktopViewTarget;

public sealed record ResultsDesktopViewTarget(ResultsNavItem Item) : DesktopViewTarget;

public interface IDesktopViewRegistry
{
    bool TryResolve(string desktopViewKey, out DesktopViewTarget target);

    Type? ResolveHubViewModelType(string moduleCode);
}

/// <summary>
/// Mapping technique DesktopViewKey → ViewModel / section hub.
/// La structure du menu vient exclusivement de l'API catalogue.
/// </summary>
public sealed class DesktopViewRegistry : IDesktopViewRegistry
{
    private readonly Dictionary<string, DesktopViewTarget> _map;

    public DesktopViewRegistry()
    {
        _map = new Dictionary<string, DesktopViewTarget>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dashboard.Main"] = new DirectDesktopViewTarget(typeof(DashboardViewModel)),
            ["Students.Main"] = new DirectDesktopViewTarget(typeof(StudentsViewModel)),
            ["Students.Enrollment"] = new DirectDesktopViewTarget(typeof(EnrollmentWizardViewModel)),
            ["StudentCards.Main"] = new DirectDesktopViewTarget(typeof(StudentCardsViewModel)),
            ["Academic.Main"] = new DirectDesktopViewTarget(typeof(AcademicViewModel)),
            ["PedagogicalPeriods.Main"] = new DirectDesktopViewTarget(typeof(PedagogicalPeriodsViewModel)),
            ["Grades.Main"] = new DirectDesktopViewTarget(typeof(GradesViewModel)),
            ["Documents.Main"] = new DirectDesktopViewTarget(typeof(DocumentsViewModel)),
            ["Statistics.Main"] = new DirectDesktopViewTarget(typeof(StatisticsViewModel)),

            ["Settings.Etablissement"] = Settings("etablissement"),
            ["Settings.StructurePedagogique"] = Settings("structure-pedagogique"),
            ["Settings.AnneesScolaires"] = Settings("annees-scolaires"),
            ["Settings.Matieres"] = Settings("matieres"),
            ["Settings.Geographie"] = Settings("geographie"),
            // Compat : ancienne clé → écran sécurité Utilisateurs
            ["Settings.Utilisateurs"] = new DirectDesktopViewTarget(typeof(SecurityUsersViewModel)),
            ["Settings.FraisScolaires"] = Settings("frais-scolaires"),
            ["Settings.RepartitionRecettes"] = Settings("repartition-recettes"),
            ["Settings.Retenues"] = Settings("retenues"),
            ["Settings.Monnaies"] = Settings("monnaies"),
            ["Settings.TauxChange"] = Settings("taux-change"),
            ["Settings.HistoriqueTaux"] = Settings("historique-taux"),
            ["Settings.Reglement"] = Settings("reglement"),
            ["Settings.Mentions"] = Settings("mentions"),
            ["Settings.SyncCloud"] = Settings("sync-cloud"),
            ["Settings.MisesAJour"] = Settings("mises-a-jour"),
            ["Settings.ParentActivation"] = Settings("activation-mobile-parent"),
            ["Settings.QrEtablissement"] = Settings("qr-etablissement"),
            ["Security.Users"] = new DirectDesktopViewTarget(typeof(SecurityUsersViewModel)),
            ["Security.Roles"] = new DirectDesktopViewTarget(typeof(SecurityRolesViewModel)),
            ["Security.Exceptions"] = new DirectDesktopViewTarget(typeof(SecurityExceptionsViewModel)),
            ["Security.Audit"] = new DirectDesktopViewTarget(typeof(SecurityAuditViewModel)),
            ["Platform.Catalog"] = new DirectDesktopViewTarget(typeof(PlatformCatalogViewModel)),

            ["Personnel.Liste"] = Personnel("liste"),
            ["Personnel.Nouveau"] = Personnel("nouveau"),
            ["Personnel.Fonctions"] = Personnel("fonctions"),
            ["Personnel.Departements"] = Personnel("departements"),

            ["Results.ParClasse"] = Results("par-classe"),
            ["Results.Individuel"] = Results("individuel"),
            ["Results.Deliberation"] = Results("deliberation"),
            ["Results.ValidationResultats"] = new DirectDesktopViewTarget(typeof(ResultValidationViewModel)),

            ["Finance.Encaissements"] = Finance("encaissements"),
            ["Finance.CategoriesTarifaires"] = Finance("categories-tarifaires"),
            ["Finance.Rapports"] = Finance("rapports-financiers"),
            ["Finance.SituationPaiements"] = Finance("situation-paiements"),
            ["Finance.Depenses"] = Finance("depenses"),
        };
    }

    public bool TryResolve(string desktopViewKey, out DesktopViewTarget target)
    {
        if (string.IsNullOrWhiteSpace(desktopViewKey))
        {
            target = null!;
            return false;
        }

        return _map.TryGetValue(desktopViewKey, out target!);
    }

    public Type? ResolveHubViewModelType(string moduleCode) =>
        moduleCode.ToUpperInvariant() switch
        {
            "SETTINGS" or "SECURITY" => typeof(SettingsViewModel),
            "FINANCE" => typeof(FinanceHubViewModel),
            "PERSONNEL" => typeof(PersonnelHubViewModel),
            "RESULTS" => typeof(ResultsHubViewModel),
            "DOCUMENTS" => typeof(DocumentsHubViewModel),
            _ => null
        };

    private static SettingsDesktopViewTarget Settings(string key)
    {
        var item = SettingsNavCatalog.FindByKey(key)
            ?? throw new InvalidOperationException($"SettingsNavCatalog manquant: {key}");
        return new SettingsDesktopViewTarget(item);
    }

    private static FinanceDesktopViewTarget Finance(string key)
    {
        var item = FinanceNavCatalog.FindByKey(key)
            ?? throw new InvalidOperationException($"FinanceNavCatalog manquant: {key}");
        return new FinanceDesktopViewTarget(item);
    }

    private static PersonnelDesktopViewTarget Personnel(string key)
    {
        var item = PersonnelNavCatalog.FindByKey(key)
            ?? throw new InvalidOperationException($"PersonnelNavCatalog manquant: {key}");
        return new PersonnelDesktopViewTarget(item);
    }

    private static ResultsDesktopViewTarget Results(string key)
    {
        var item = ResultsNavCatalog.FindByKey(key)
            ?? throw new InvalidOperationException($"ResultsNavCatalog manquant: {key}");
        return new ResultsDesktopViewTarget(item);
    }
}
