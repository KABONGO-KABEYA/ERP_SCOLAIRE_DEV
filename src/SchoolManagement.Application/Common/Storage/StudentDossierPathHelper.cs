using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SchoolManagement.Application.Common.Storage;

public static class StudentDossierPathHelper
{
    public const string RootFolderName = "Dossier_Elève";
    public const string TempFolderName = "temp";
    public const string StudentsFolderName = "students";
    public const string DraftMetaFileName = ".draft.json";

    /// <summary>TTL des drafts abandonnés (48 h).</summary>
    public static readonly TimeSpan DraftTimeToLive = TimeSpan.FromHours(48);

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

    public static string BuildDraftRelativeDirectory(Guid draftId) =>
        $"{TempFolderName}/{draftId:D}";

    public static string BuildStudentIdRelativeDirectory(string academicYearLabel, Guid studentId) =>
        $"{BuildAcademicYearFolder(academicYearLabel)}/{StudentsFolderName}/{studentId:D}";

    public static string BuildStudentIdRelativeFilePath(
        string academicYearLabel,
        Guid studentId,
        string fileName) =>
        $"{BuildStudentIdRelativeDirectory(academicYearLabel, studentId)}/{fileName}";

    public static bool IsTempDraftPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith($"{TempFolderName}/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseDraftIdFromPath(string? path, out Guid draftId)
    {
        draftId = Guid.Empty;
        if (!IsTempDraftPath(path))
        {
            return false;
        }

        var normalized = path!.Replace('\\', '/').TrimStart('/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && Guid.TryParse(parts[1], out draftId);
    }

    public static bool IsStudentIdStoragePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        return normalized.Contains($"/{StudentsFolderName}/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lecture legacy uniquement — NE PAS utiliser pour de nouvelles écritures (P3).
    /// </summary>
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
            var name = Path.GetFileName(yearDirectory);
            if (string.Equals(name, yearFolder, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, TempFolderName, StringComparison.OrdinalIgnoreCase))
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
            if (string.Equals(folderName, TempFolderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (folderName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return folderName;
            }
        }

        return null;
    }

    /// <summary>Résolution lecture (legacy Nom_Prenom_Matricule OU students/{StudentId}).</summary>
    public static string? ResolveStudentDirectory(
        string dossierRootPath,
        string lastName,
        string firstName,
        string registrationNumber,
        string academicYearLabel,
        Guid? studentId = null)
    {
        if (!Directory.Exists(dossierRootPath))
        {
            return null;
        }

        var yearFolder = BuildAcademicYearFolder(academicYearLabel);

        if (studentId.HasValue)
        {
            var byId = Path.Combine(
                dossierRootPath,
                yearFolder,
                StudentsFolderName,
                studentId.Value.ToString("D"));
            if (Directory.Exists(byId))
            {
                return byId;
            }
        }

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
            if (string.Equals(folderName, StudentsFolderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

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

    public static string SerializeDraftMeta(EnrollmentDraftMeta meta) =>
        JsonSerializer.Serialize(meta);

    public static EnrollmentDraftMeta? DeserializeDraftMeta(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<EnrollmentDraftMeta>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string GuessExtension(string documentType) =>
        documentType.Equals("Photo", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".pdf";
}

public sealed class EnrollmentDraftMeta
{
    public Guid DraftId { get; set; }

    public Guid SchoolId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public Guid? PromotedStudentId { get; set; }

    public DateTime? PromotedAtUtc { get; set; }

    /// <summary>
    /// StudentId cible si la promotion a échoué après COMMIT SQL — ne pas purger tant que non promu.
    /// </summary>
    public Guid? PendingPromotionStudentId { get; set; }

    public string? PendingPromotionAcademicYearLabel { get; set; }

    public string? LastPromotionError { get; set; }
}
