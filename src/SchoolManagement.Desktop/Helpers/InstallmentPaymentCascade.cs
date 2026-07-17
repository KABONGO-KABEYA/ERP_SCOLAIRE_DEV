using System.Globalization;
using SchoolManagement.Desktop.Models;

namespace SchoolManagement.Desktop.Helpers;

/// <summary>
/// Cascade de répartition des versements par SortOrder.
/// Ne pas modifier la logique métier sans revue explicite.
/// </summary>
public static class InstallmentPaymentCascade
{
    public static void Redistribute(IEnumerable<InstallmentCollectRow> rows, decimal total)
    {
        var remainingToAssign = Math.Max(0, total);
        foreach (var row in rows.OrderBy(r => r.SortOrder).ThenBy(r => r.Name))
        {
            var assign = Math.Min(row.Remaining, remainingToAssign);
            row.SetTodayPayment(assign, suppressNotify: false);
            remainingToAssign -= assign;
        }
    }

    public static void RefreshEditability(IEnumerable<InstallmentCollectRow> rows)
    {
        var previousCovered = true;
        foreach (var row in rows.OrderBy(r => r.SortOrder).ThenBy(r => r.Name))
        {
            var canEdit = previousCovered && row.Remaining > 0;
            row.CanEditTodayPayment = canEdit;
            row.CanEditPhysicalNumber = canEdit || row.TodayPayment > 0;

            if (row.Remaining - row.TodayPayment > 0)
            {
                previousCovered = false;
            }
        }
    }

    public static void ValidateCascadeOrThrow(IEnumerable<InstallmentCollectRow> rows)
    {
        var previousCovered = true;
        foreach (var row in rows.OrderBy(r => r.SortOrder).ThenBy(r => r.Name))
        {
            if (row.TodayPayment > 0 && !previousCovered)
            {
                throw new InvalidOperationException(
                    $"Impossible de verser sur « {row.Name} » tant que la tranche précédente n'est pas soldée.");
            }

            if (row.Remaining - row.TodayPayment > 0)
            {
                previousCovered = false;
            }
        }
    }

    /// <summary>
    /// Applique une saisie manuelle sur une tranche : clamp + clear des suivantes si non couverte.
    /// </summary>
    public static void ApplyTodayPaymentEdit(
        IList<InstallmentCollectRow> rows,
        InstallmentCollectRow row,
        bool commitClamp)
    {
        if (!row.CanEditTodayPayment && row.TodayPayment <= 0)
        {
            return;
        }

        if (!TryParseDecimal(row.TodayPaymentText, out var value))
        {
            value = 0;
        }

        value = Math.Clamp(value, 0, row.Remaining);

        // Cascade: clear subsequent installments if this one no longer covers remaining.
        var ordered = rows.OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToList();
        var index = ordered.IndexOf(row);
        if (index >= 0 && row.Remaining - value > 0)
        {
            for (var i = index + 1; i < ordered.Count; i++)
            {
                ordered[i].SetTodayPayment(0, suppressNotify: false);
            }
        }

        if (commitClamp || value != row.TodayPayment)
        {
            row.SetTodayPayment(value, suppressNotify: false);
        }

        RefreshEditability(rows);
    }

    public static bool TryParseDecimal(string? text, out decimal value)
    {
        if (decimal.TryParse(text?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
    }
}
