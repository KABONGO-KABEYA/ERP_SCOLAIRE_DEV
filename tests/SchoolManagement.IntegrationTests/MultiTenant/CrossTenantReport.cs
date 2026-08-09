namespace SchoolManagement.IntegrationTests.MultiTenant;

using System.Net;
using System.Text;

/// <summary>
/// Agrège les résultats des scénarios inter-écoles et produit le rapport de preuve.
/// </summary>
internal sealed class CrossTenantReport
{
    private readonly List<CrossTenantOutcome> _outcomes = [];
    private readonly Dictionary<string, HttpStatusCode> _controls = new(StringComparer.Ordinal);

    internal IReadOnlyList<CrossTenantOutcome> Outcomes => _outcomes;

    internal IReadOnlyList<CrossTenantOutcome> Failures =>
        _outcomes.Where(o => o.Verdict == VerdictFailed).ToList();

    /// <summary>Scénarios dont le contrôle propriétaire n'a pas prouvé l'existence de la donnée.</summary>
    internal IReadOnlyList<CrossTenantOutcome> Inconclusive =>
        _outcomes.Where(o => o.Verdict == VerdictInconclusive).ToList();

    private const string VerdictPassed = "REFUSÉ";
    private const string VerdictFailed = "FUITE";
    private const string VerdictInconclusive = "NON CONCLUANT";

    internal void RecordControl(string controlPath, string ownerMarker, HttpStatusCode statusCode)
    {
        _controls[$"{controlPath}|{ownerMarker}"] = statusCode;
    }

    internal void RecordCrossTenant(
        CrossTenantScenario scenario,
        string attackerMarker,
        string victimMarker,
        HttpStatusCode statusCode,
        bool leaked)
    {
        var direction = $"{attackerMarker} → {victimMarker}";
        var controlStatus = _controls.GetValueOrDefault($"{scenario.ControlPath}|{victimMarker}");
        var controlProves = controlStatus is HttpStatusCode.OK;

        // Une collection filtrée par un identifiant étranger peut légitimement répondre 200 vide :
        // seule l'absence de donnée voisine est exigée.
        var succeeded = IsSuccess(statusCode) && scenario.Expectation == CrossTenantExpectation.Denied;

        var (verdict, detail) = (leaked, succeeded, controlProves) switch
        {
            (true, _, _) => (VerdictFailed, "la réponse contient des données de l'autre école"),
            (_, true, _) => (VerdictFailed, "la requête a abouti sur une ressource d'une autre école"),
            (_, _, false) => (VerdictInconclusive,
                $"le contrôle propriétaire {scenario.ControlPath} a répondu {(int)controlStatus} — "
                + "impossible de prouver que la ressource existait"),
            _ => (VerdictPassed, (string?)null)
        };

        _outcomes.Add(new CrossTenantOutcome(
            scenario.Resource, scenario.Method, scenario.PathTemplate, direction, statusCode, verdict, detail));
    }

    internal void RecordList(
        ListLeakScenario scenario,
        string attackerMarker,
        string victimMarker,
        HttpStatusCode statusCode,
        bool leaked,
        bool seesOwnData)
    {
        var direction = $"{attackerMarker} → {victimMarker}";

        var (verdict, detail) = (leaked, IsSuccess(statusCode), seesOwnData) switch
        {
            (true, _, _) => (VerdictFailed, "la liste expose des données de l'autre école"),
            (_, false, _) => (VerdictInconclusive, "la liste n'a pas répondu 2xx pour son propriétaire"),
            (_, _, false) => (VerdictInconclusive, "la liste ne contient pas les données de son propriétaire"),
            _ => (VerdictPassed, (string?)null)
        };

        _outcomes.Add(new CrossTenantOutcome(
            scenario.Resource, "GET (liste)", scenario.PathTemplate, direction, statusCode, verdict, detail));
    }

    private static bool IsSuccess(HttpStatusCode code) => (int)code is >= 200 and < 300;

    internal string Summary()
    {
        var passed = _outcomes.Count(o => o.Verdict == VerdictPassed);
        return $"Scénarios exécutés : {_outcomes.Count} | réussis : {passed} | "
            + $"échecs : {Failures.Count} | non concluants : {Inconclusive.Count}";
    }

    internal string Write(string? markerA, string? markerB)
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "security", "rapport-tests-cross-tenant.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var passed = _outcomes.Count(o => o.Verdict == VerdictPassed);
        var resources = _outcomes.Select(o => o.Resource).Distinct(StringComparer.Ordinal).OrderBy(r => r).ToList();
        var endpoints = _outcomes
            .Select(o => $"{o.Method} {o.Path}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("# Rapport de tests d'isolation multi-école");
        builder.AppendLine();
        builder.AppendLine($"Généré le {DateTime.Now:yyyy-MM-dd HH:mm} par "
            + "`CrossTenantIsolationTests.Cross_tenant_access_is_denied_on_every_business_resource`.");
        builder.AppendLine();
        builder.AppendLine("## Environnement");
        builder.AppendLine();
        builder.AppendLine("Deux écoles complètes sont créées en base SQL au début du test, puis supprimées à la fin.");
        builder.AppendLine("Chaque école possède son propre utilisateur authentifié par JWT (revendication `school_id`)");
        builder.AppendLine("et un marqueur unique présent dans tous ses libellés : sa présence dans une réponse");
        builder.AppendLine("destinée à l'autre école constituerait une fuite.");
        builder.AppendLine();
        builder.AppendLine($"- École A : `{markerA ?? "n/a"}`");
        builder.AppendLine($"- École B : `{markerB ?? "n/a"}`");
        builder.AppendLine();
        builder.AppendLine("## Résultats");
        builder.AppendLine();
        builder.AppendLine("| Indicateur | Valeur |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| Scénarios exécutés | {_outcomes.Count} |");
        builder.AppendLine($"| Scénarios réussis (accès refusé) | {passed} |");
        builder.AppendLine($"| Échecs (fuite de données) | {Failures.Count} |");
        builder.AppendLine($"| Non concluants | {Inconclusive.Count} |");
        builder.AppendLine($"| Ressources couvertes | {resources.Count} |");
        builder.AppendLine($"| Endpoints couverts | {endpoints.Count} |");
        builder.AppendLine();

        builder.AppendLine("## Codes de refus observés");
        builder.AppendLine();
        builder.AppendLine("| Code HTTP | Nombre de scénarios |");
        builder.AppendLine("|---|---|");
        foreach (var group in _outcomes
            .Where(o => o.Verdict == VerdictPassed)
            .GroupBy(o => o.StatusCode)
            .OrderBy(g => (int)g.Key))
        {
            builder.AppendLine($"| {(int)group.Key} {group.Key} | {group.Count()} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Ressources testées");
        builder.AppendLine();
        foreach (var resource in resources)
        {
            var total = _outcomes.Count(o => o.Resource == resource);
            var ok = _outcomes.Count(o => o.Resource == resource && o.Verdict == VerdictPassed);
            builder.AppendLine($"- **{resource}** — {ok}/{total} scénarios refusés");
        }

        builder.AppendLine();
        builder.AppendLine("## Endpoints testés");
        builder.AppendLine();
        foreach (var endpoint in endpoints)
        {
            builder.AppendLine($"- `{endpoint}`");
        }

        if (Failures.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Échecs");
            builder.AppendLine();
            foreach (var failure in Failures)
            {
                builder.AppendLine($"- {failure.Describe()}");
            }
        }

        if (Inconclusive.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Scénarios non concluants");
            builder.AppendLine();
            foreach (var item in Inconclusive)
            {
                builder.AppendLine($"- {item.Describe()}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Détail complet");
        builder.AppendLine();
        builder.AppendLine("| Ressource | Méthode | Endpoint | Sens | Code | Verdict |");
        builder.AppendLine("|---|---|---|---|---|---|");
        foreach (var outcome in _outcomes)
        {
            builder.AppendLine(
                $"| {outcome.Resource} | {outcome.Method} | `{outcome.Path}` | {outcome.Direction} "
                + $"| {(int)outcome.StatusCode} | {outcome.Verdict} |");
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        return path;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SchoolManagement.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
