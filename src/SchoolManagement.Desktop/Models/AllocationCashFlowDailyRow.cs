namespace SchoolManagement.Desktop.Models;

using SchoolManagement.Application.RevenueAllocation.DTOs;

/// <summary>Groupe journalier de répartition (rupture par date).</summary>
public sealed class AllocationCashFlowDailyGroupRow
{
    public required DateOnly Date { get; init; }

    public string DateLabel => Date.ToString("dd/MM/yyyy");

    public required IReadOnlyList<AllocationCashFlowRowDto> Rows { get; init; }
}
