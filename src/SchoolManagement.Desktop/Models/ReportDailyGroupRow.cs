namespace SchoolManagement.Desktop.Models;

/// <summary>Groupe journalier générique (rupture par date) pour les onglets recettes.</summary>
public sealed class ReportDailyGroupRow<T>
{
    public required DateOnly Date { get; init; }

    public string DateLabel => Date.ToString("dd/MM/yyyy");

    public required IReadOnlyList<T> Rows { get; init; }

    public required decimal DayTotal { get; init; }
}
