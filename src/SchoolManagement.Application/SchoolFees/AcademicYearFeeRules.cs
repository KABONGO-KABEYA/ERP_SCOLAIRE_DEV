namespace SchoolManagement.Application.SchoolFees;

using SchoolManagement.Application.Schools.DTOs;

public static class AcademicYearFeeRules
{
    public static bool CanEditFees(AcademicYearDto year, DateOnly? referenceDate = null)
    {
        referenceDate ??= DateOnly.FromDateTime(DateTime.Today);

        if (year.IsCurrent)
        {
            return true;
        }

        return year.StartDate > referenceDate;
    }

    public static string GetReadOnlyReason(AcademicYearDto year) =>
        $"L'année {year.Label} est passée : consultation uniquement.";
}
