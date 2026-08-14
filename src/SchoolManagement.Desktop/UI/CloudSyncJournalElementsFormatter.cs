using SchoolManagement.Application.CloudSync.DTOs;

namespace SchoolManagement.Desktop.UI;

/// <summary>Résumé lisible des entités synchronisées pour le journal cloud.</summary>
public static class CloudSyncJournalElementsFormatter
{
    public const int SummaryMaxLength = 58;

    private static readonly Dictionary<string, string> TableLabels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Students"] = "Élèves",
            ["Enrollments"] = "Inscriptions",
            ["Payments"] = "Paiements",
            ["PedagogicalClasses"] = "Classes",
            ["Teachers"] = "Personnel",
            ["StudentDocuments"] = "Documents",
            ["Guardians"] = "Responsables",
            ["PaymentLines"] = "Lignes paiement",
            ["StudentAttendances"] = "Présences élèves",
            ["TeacherAttendances"] = "Présences personnel",
            ["GradeEntries"] = "Notes",
            ["Evaluations"] = "Évaluations",
            ["ReportCards"] = "Bulletins",
            ["Sections"] = "Sections",
            ["Courses"] = "Cours",
            ["CashMovements"] = "Mouvements caisse",
            ["FinDepense"] = "Dépenses",
            ["FinDemandePaiement"] = "Demandes paiement",
            ["UserAccounts"] = "Comptes utilisateurs",
            ["StudentFeeBalances"] = "Soldes frais",
        };

    public static CloudSyncElementsDisplay Format(CloudSyncJournalLineDto line)
    {
        if (line.Skipped && line.UnitsSucceeded == 0 && line.RecordsSucceeded == 0)
        {
            return line.Success
                ? EmptySuccess()
                : new CloudSyncElementsDisplay(
                    "Synchronisation ignorée",
                    ["Synchronisation ignorée"],
                    IsTruncated: false);
        }

        var items = ParseTablesTouched(line.TablesTouched);
        if (items.Count == 0)
        {
            if (line.RecordsSucceeded > 0)
            {
                var fallback = $"{line.RecordsSucceeded} enregistrement(s)";
                return new CloudSyncElementsDisplay(fallback, [fallback], false);
            }

            if (!line.Skipped && (!line.Success || line.UnitsFailed > 0))
            {
                return new CloudSyncElementsDisplay(
                    "Aucun élément synchronisé",
                    ["Aucun élément synchronisé"],
                    false);
            }

            return EmptySuccess();
        }

        var detailLines = items
            .Select(i => i.Count is null ? i.Label : $"{i.Label} : {i.Count:N0}")
            .ToList();
        var fullSummary = string.Join(", ", detailLines);
        var truncated = TruncateSummary(fullSummary, out var isTruncated);
        return new CloudSyncElementsDisplay(truncated, detailLines, isTruncated);
    }

    public static string ResolveTableLabel(string tableName)
    {
        if (TableLabels.TryGetValue(tableName, out var label))
        {
            return label;
        }

        return HumanizeTableName(tableName);
    }

    private static CloudSyncElementsDisplay EmptySuccess() =>
        new("Aucune donnée à synchroniser", ["Aucune donnée à synchroniser"], false);

    private static List<SyncElementItem> ParseTablesTouched(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var trimmed = raw.Trim();
        if (trimmed.Contains('='))
        {
            return trimmed
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part =>
                {
                    var eq = part.IndexOf('=');
                    if (eq <= 0)
                    {
                        return new SyncElementItem(ResolveTableLabel(part), null);
                    }

                    var table = part[..eq].Trim();
                    return int.TryParse(part[(eq + 1)..].Trim(), out var count)
                        ? new SyncElementItem(ResolveTableLabel(table), count)
                        : new SyncElementItem(ResolveTableLabel(table), null);
                })
                .ToList();
        }

        return trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => new SyncElementItem(ResolveTableLabel(t), null))
            .ToList();
    }

    private static string TruncateSummary(string value, out bool isTruncated)
    {
        if (value.Length <= SummaryMaxLength)
        {
            isTruncated = false;
            return value;
        }

        isTruncated = true;
        return value[..SummaryMaxLength].TrimEnd(',', ' ', ';') + "...";
    }

    private static string HumanizeTableName(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return "—";
        }

        var chars = new List<char>(tableName.Length + 8);
        for (var i = 0; i < tableName.Length; i++)
        {
            var c = tableName[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(tableName[i - 1]))
            {
                chars.Add(' ');
            }

            chars.Add(i == 0 ? char.ToUpper(c) : c);
        }

        return new string(chars.ToArray());
    }

    private sealed record SyncElementItem(string Label, int? Count);
}

public sealed record CloudSyncElementsDisplay(
    string Summary,
    IReadOnlyList<string> DetailLines,
    bool IsTruncated);
