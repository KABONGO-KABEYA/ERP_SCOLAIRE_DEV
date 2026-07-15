namespace SchoolManagement.Application.SchoolFees;

using SchoolManagement.Application.SchoolFees.DTOs;

public static class ClassFeeScheduleSignatureHelper
{
    public static string Compute(IEnumerable<ClassFeeScheduleLineDto> lines) =>
        string.Join(
            ';',
            lines
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.FeeInstallmentId)
                .Select(l =>
                    $"{l.FeeInstallmentId:N}|{l.SortOrder}|{l.Amount}|{(l.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty)}"));

    public static bool HasConfiguredValues(IEnumerable<ClassFeeScheduleLineDto> lines) =>
        lines.Any(l => l.Amount > 0 || l.DueDate is not null);
}
