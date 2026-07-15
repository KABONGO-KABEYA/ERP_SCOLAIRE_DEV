using System.Text;
using System.Text.RegularExpressions;

namespace SchoolManagement.Application.Common.Storage;

public static class StudentDossierPathHelper
{
    public const string RootFolderName = "Dossier_Elève";

    public static string BuildStudentFolderName(string lastName, string firstName, string registrationNumber)
    {
        return $"{SanitizeToken(lastName)}_{SanitizeToken(firstName)}_{SanitizeToken(registrationNumber)}";
    }

    public static string BuildAcademicYearFolder(string academicYearLabel)
    {
        if (string.IsNullOrWhiteSpace(academicYearLabel))
        {
            return DateTime.UtcNow.Year.ToString();
        }

        var normalized = academicYearLabel.Trim().Replace('/', '-').Replace('\\', '-');
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsDigit(ch) || ch == '-')
            {
                builder.Append(ch);
            }
        }

        var cleaned = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(cleaned) ? DateTime.UtcNow.Year.ToString() : cleaned;
    }

    public static string BuildStoredFileName(string documentType, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = GuessExtension(documentType);
        }

        var baseName = SanitizeToken(documentType).Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "Document";
        }

        return $"{baseName}{extension.ToLowerInvariant()}";
    }

    public static string? FindExistingStudentFolderName(
        string dossierRootPath,
        string registrationNumber,
        string academicYearLabel)
    {
        if (!Directory.Exists(dossierRootPath))
        {
            return null;
        }

        var registrationToken = SanitizeToken(registrationNumber);
        if (string.IsNullOrWhiteSpace(registrationToken))
        {
            return null;
        }

        var suffix = $"_{registrationToken}";
        var yearFolder = BuildAcademicYearFolder(academicYearLabel);

        var found = FindStudentFolderInYearDirectory(Path.Combine(dossierRootPath, yearFolder), suffix);
        if (found is not null)
        {
            return found;
        }

        foreach (var yearDirectory in Directory.EnumerateDirectories(dossierRootPath))
        {
            if (string.Equals(Path.GetFileName(yearDirectory), yearFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            found = FindStudentFolderInYearDirectory(yearDirectory, suffix);
            if (found is not null)
            {
                return found;
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(dossierRootPath))
        {
            var folderName = Path.GetFileName(directory);
            if (folderName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return folderName;
            }
        }

        return null;
    }

    public static string? ResolveStudentDirectory(
        string dossierRootPath,
        string lastName,
        string firstName,
        string registrationNumber,
        string academicYearLabel)
    {
        if (!Directory.Exists(dossierRootPath))
        {
            return null;
        }

        var yearFolder = BuildAcademicYearFolder(academicYearLabel);
        var studentFolder = FindExistingStudentFolderName(dossierRootPath, registrationNumber, academicYearLabel)
            ?? BuildStudentFolderName(lastName, firstName, registrationNumber);
        var directory = Path.Combine(dossierRootPath, yearFolder, studentFolder);
        return Directory.Exists(directory) ? directory : null;
    }

    private static string? FindStudentFolderInYearDirectory(string yearDirectoryPath, string registrationSuffix)
    {
        if (!Directory.Exists(yearDirectoryPath))
        {
            return null;
        }

        foreach (var directory in Directory.EnumerateDirectories(yearDirectoryPath))
        {
            var folderName = Path.GetFileName(directory);
            if (folderName.EndsWith(registrationSuffix, StringComparison.OrdinalIgnoreCase))
            {
                return folderName;
            }
        }

        return null;
    }

    public static bool IsServerStoragePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return !Path.IsPathRooted(path) || path.StartsWith(RootFolderName, StringComparison.OrdinalIgnoreCase);
    }

    public static string SanitizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
            else if (ch is ' ' or '-' or '_')
            {
                builder.Append('_');
            }
        }

        var sanitized = Regex.Replace(builder.ToString(), "_+", "_").Trim('_');
        return sanitized;
    }

    private static string GuessExtension(string documentType) =>
        documentType.Equals("Photo", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".pdf";
}
