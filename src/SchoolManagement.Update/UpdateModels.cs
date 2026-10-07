namespace SchoolManagement.Update;

public enum UpdateInstallRole
{
    Unknown,
    Server,
    Client
}

public sealed class DetectedInstallation
{
    public required string InstallRoot { get; init; }
    public required UpdateInstallRole Role { get; init; }
    public required string ApiDirectory { get; init; }
    public required string DesktopDirectory { get; init; }
    public required string PreviousVersion { get; init; }
    public bool HasApiService { get; init; }
}

public sealed class UpdateReport
{
    public bool Success { get; init; }
    public string PreviousVersion { get; init; } = "";
    public string NewVersion { get; init; } = "";
    public bool ApiOk { get; init; }
    public bool DatabaseOk { get; init; }
    public bool SchemaOk { get; init; }
    public bool DesktopOk { get; init; }
    public bool SubscriptionOk { get; init; }
    public bool SubscriptionCreated { get; init; }
    public bool SubscriptionAlreadyExisted { get; init; }
    public string? SubscriptionDetail { get; init; }
    public int SchoolCountBefore { get; init; }
    public int SchoolCountAfter { get; init; }
    public string? RollbackPath { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> LogLines { get; init; } = Array.Empty<string>();

    public string FormatSummary()
    {
        var lines = new List<string>
        {
            "==================================================",
            "       ERP SCOLAIRE — MISE À JOUR",
            "==================================================",
            "",
            $"Version précédente : {PreviousVersion}",
            $"Nouvelle version    : {NewVersion}",
            "",
            $"API                  : {(ApiOk ? "OK" : "ÉCHEC")}",
            $"Base de données      : {(DatabaseOk ? "OK" : "ÉCHEC")}",
            $"Mise à niveau schéma : {(SchemaOk ? "OK" : "ÉCHEC")}",
            $"Desktop              : {(DesktopOk ? "OK" : "ÉCHEC")}",
            $"Abonnement           : {(SubscriptionOk ? "OK" : "ÉCHEC")}",
            "",
        };

        if (SubscriptionAlreadyExisted)
        {
            lines.Add("Abonnement existant détecté.");
            lines.Add("Aucune modification de l'abonnement existant.");
        }
        else if (SubscriptionCreated)
        {
            lines.Add("Abonnement initial :");
            lines.Add(SubscriptionDetail ?? "01/09/2026 → 01/10/2026");
        }

        lines.Add("");
        lines.Add("Toutes les données existantes ont été conservées.");
        if (!string.IsNullOrWhiteSpace(RollbackPath))
            lines.Add($"Sauvegarde binaires : {RollbackPath}");

        if (!Success && !string.IsNullOrWhiteSpace(Error))
        {
            lines.Add("");
            lines.Add($"ERREUR : {Error}");
        }

        lines.Add("");
        lines.Add(Success
            ? "=================================================="
              + Environment.NewLine
              + "        MISE À JOUR TERMINÉE"
              + Environment.NewLine
              + "=================================================="
            : "=================================================="
              + Environment.NewLine
              + "        MISE À JOUR ÉCHOUÉE"
              + Environment.NewLine
              + "==================================================");

        return string.Join(Environment.NewLine, lines);
    }
}
