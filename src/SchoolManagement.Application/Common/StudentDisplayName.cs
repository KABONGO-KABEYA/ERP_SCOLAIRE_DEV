namespace SchoolManagement.Application.Common;

using SchoolManagement.Domain.Entities.Students;

/// <summary>
/// Affichage standard des noms d'élèves : Nom Postnom Prénom (RDC).
/// </summary>
public static class StudentDisplayName
{
    public static string Format(string lastName, string? middleName, string firstName)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(lastName)) parts.Add(lastName.Trim());
        if (!string.IsNullOrWhiteSpace(middleName)) parts.Add(middleName.Trim());
        if (!string.IsNullOrWhiteSpace(firstName)) parts.Add(firstName.Trim());
        return parts.Count == 0 ? string.Empty : string.Join(" ", parts);
    }

    public static string Format(Student student) =>
        Format(student.LastName, student.MiddleName, student.FirstName);

    public static string FormatOrDefault(Student? student, string fallback = "—") =>
        student is null ? fallback : Format(student);
}
