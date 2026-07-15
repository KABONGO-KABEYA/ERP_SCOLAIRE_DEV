namespace SchoolManagement.Application.Configuration.FileStorage;

/// <summary>Teste l'accès réel au dossier partagé Dossier_Elève (sans création automatique).</summary>
public sealed class FileStoragePathTester
{
    public FileStoragePathTestResult TestPath(
        string racine,
        string applicationDirectory,
        bool requireWriteAccess = true)
    {
        if (string.IsNullOrWhiteSpace(racine))
        {
            return FileStoragePathTestResult.Failure("Le chemin du dossier partagé est obligatoire.");
        }

        var formatError = FileStorageConfigurationManager.ValidatePathFormat(racine);
        if (formatError is not null)
        {
            return FileStoragePathTestResult.Failure(formatError);
        }

        var absolutePath = FileStorageConfigurationManager.ResolveAbsolutePath(racine, applicationDirectory);

        try
        {
            if (!Directory.Exists(absolutePath))
            {
                return FileStoragePathTestResult.Failure(
                    $"Le dossier n'existe pas ou est inaccessible :{Environment.NewLine}{absolutePath}");
            }

            if (requireWriteAccess)
            {
                var probeFile = Path.Combine(absolutePath, $".erp_probe_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probeFile, "probe");
                File.Delete(probeFile);
            }
            else
            {
                _ = Directory.EnumerateFileSystemEntries(absolutePath).FirstOrDefault();
            }

            return FileStoragePathTestResult.Success(absolutePath);
        }
        catch (UnauthorizedAccessException)
        {
            return FileStoragePathTestResult.Failure(
                "Accès refusé. Vérifiez les droits de lecture/écriture sur le partage réseau.");
        }
        catch (IOException ex)
        {
            return FileStoragePathTestResult.Failure(
                $"Erreur réseau ou dossier indisponible : {ex.Message}");
        }
        catch (Exception ex)
        {
            return FileStoragePathTestResult.Failure(
                $"Impossible d'accéder au dossier : {ex.Message}");
        }
    }

    public FileStoragePathTestResult TestConfiguration(
        FileStorageConfiguration configuration,
        string applicationDirectory,
        bool requireWriteAccess = true) =>
        TestPath(configuration.Racine, applicationDirectory, requireWriteAccess);
}
