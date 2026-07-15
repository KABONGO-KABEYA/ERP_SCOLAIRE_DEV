using System.IO;
using SchoolManagement.Application.Configuration.FileStorage;

namespace SchoolManagement.Desktop.Services;

public sealed class StudentDossierPathResolver : IStudentDossierPathResolver
{
    public StudentDossierPathResolver(FileStorageConfigurationManager configurationManager)
    {
        RootPath = configurationManager.GetAbsoluteRootPath();
    }

    public string RootPath { get; }

    public string? ResolveAbsolutePath(string? storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        if (Path.IsPathRooted(storagePath) && File.Exists(storagePath))
        {
            return storagePath;
        }

        var candidate = Path.GetFullPath(Path.Combine(RootPath, storagePath.Replace('/', Path.DirectorySeparatorChar)));
        return File.Exists(candidate) ? candidate : null;
    }
}

public interface IStudentDossierPathResolver
{
    string RootPath { get; }

    string? ResolveAbsolutePath(string? storagePath);
}
