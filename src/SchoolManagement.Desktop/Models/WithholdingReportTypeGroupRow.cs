namespace SchoolManagement.Desktop.Models;

using SchoolManagement.Application.RevenueAllocation.DTOs;

/// <summary>Groupe de retenues avec rupture par type.</summary>
public sealed class WithholdingReportTypeGroupRow
{
    public required Guid WithholdingTypeId { get; init; }

    public required string WithholdingTypeCode { get; init; }

    public required string WithholdingTypeName { get; init; }

    public string TypeLabel => string.IsNullOrWhiteSpace(WithholdingTypeCode)
        ? WithholdingTypeName
        : $"{WithholdingTypeCode} — {WithholdingTypeName}";

    public required decimal TypeTotal { get; init; }

    public required IReadOnlyList<WithholdingReportStudentLineDto> Students { get; init; }
}
