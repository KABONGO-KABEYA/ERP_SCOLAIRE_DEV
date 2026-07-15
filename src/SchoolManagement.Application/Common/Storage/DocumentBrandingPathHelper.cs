namespace SchoolManagement.Application.Common.Storage;

public static class DocumentBrandingPathHelper
{
    public const long MaxLogoFileBytes = 5 * 1024 * 1024;
    public const long MaxHeaderFileBytes = 10 * 1024 * 1024;
    public const long MaxSignatureFileBytes = 3 * 1024 * 1024;
    public const long MaxStampFileBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp"
    };

    public static string GetDocumentsRoot(string fileStorageRoot)
    {
        var directory = fileStorageRoot.Trim().TrimEnd('\\', '/');
        var folderName = Path.GetFileName(directory);
        if (folderName.Equals(StudentDossierPathHelper.RootFolderName, StringComparison.OrdinalIgnoreCase)
            || folderName.Equals("Dossier_Eleve", StringComparison.OrdinalIgnoreCase))
        {
            directory = Path.GetDirectoryName(directory) ?? directory;
        }

        return Path.Combine(directory, "Documents");
    }

    public static string BuildRelativePath(string categoryFolder, Guid schoolId, string fileName) =>
        Path.Combine(categoryFolder, schoolId.ToString("N"), fileName).Replace('\\', '/');

    public static void ValidateImageFile(string fileName, long fileSizeBytes, long maxBytes)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Format non autorisé. Utilisez PNG, JPG, JPEG ou BMP.");
        }

        if (fileSizeBytes <= 0)
        {
            throw new InvalidOperationException("Le fichier est vide.");
        }

        if (fileSizeBytes > maxBytes)
        {
            throw new InvalidOperationException(
                $"Le fichier dépasse la taille maximale autorisée ({maxBytes / (1024 * 1024)} Mo).");
        }
    }
}
