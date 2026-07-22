using System.Data;

namespace SchoolManagement.Desktop.Models;

/// <summary>Groupe journalier du pivot recettes (rupture par date).</summary>
public sealed class DailyPivotGroupRow
{
    public required DateOnly Date { get; init; }

    public string DateLabel => Date.ToString("dd/MM/yyyy");

    public required DataView Rows { get; init; }
}
