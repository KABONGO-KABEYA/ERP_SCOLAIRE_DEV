namespace SchoolManagement.Infrastructure.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Application.Common.Storage;
using SchoolManagement.Application.Configuration.FileStorage;

public sealed class StudentDossierStorageService : IStudentDossierStorageService
{
    private readonly string _rootPath;

    public StudentDossierStorageService(FileStorageConfigurationManager configurationManager)
    {
        _rootPath = configurationManager.GetAbsoluteRootPath();
        if (!Directory.Exists(_rootPath))
        {
            throw new InvalidOperationException(
                $"Le dossier partagé configuré est introuvable : {_rootPath}");
        }
    }

    public string GetRootPath() => _rootPath;

    public async Task<StudentDossierSaveResult> SaveStudentFileAsync(
        StudentDossierFileRequest request,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var yearFolder = StudentDossierPathHelper.BuildAcademicYearFolder(request.AcademicYearLabel);
        var studentFolder = StudentDossierPathHelper.FindExistingStudentFolderName(
                _rootPath,
                request.RegistrationNumber,
                request.AcademicYearLabel)
            ?? StudentDossierPathHelper.BuildStudentFolderName(
                request.LastName,
                request.FirstName,
                request.RegistrationNumber);

        var targetDirectory = Path.Combine(_rootPath, yearFolder, studentFolder);
        Directory.CreateDirectory(targetDirectory);

        var storedFileName = StudentDossierPathHelper.BuildStoredFileName(request.DocumentType, request.FileName);
        var absolutePath = EnsureUniqueFilePath(Path.Combine(targetDirectory, storedFileName));

        await using (var fileStream = File.Create(absolutePath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var fileInfo = new FileInfo(absolutePath);
        var relativePath = Path.Combine(yearFolder, studentFolder, fileInfo.Name).Replace('\\', '/');
        return new StudentDossierSaveResult(relativePath, fileInfo.Name, fileInfo.Length);
    }

    public Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveSafePath(storagePath);
        if (absolutePath is null || !File.Exists(absolutePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(absolutePath));
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveSafePath(storagePath);
        if (absolutePath is not null && File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    public string ResolveAbsolutePath(string storagePath)
    {
        var absolutePath = ResolveSafePath(storagePath);
        return absolutePath ?? Path.Combine(_rootPath, storagePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private string? ResolveSafePath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        if (Path.IsPathRooted(storagePath))
        {
            var normalizedRoot = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(storagePath);
            return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }

        var candidate = Path.GetFullPath(Path.Combine(_rootPath, storagePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = _rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }

    private static string EnsureUniqueFilePath(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            return absolutePath;
        }

        var directory = Path.GetDirectoryName(absolutePath)!;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(absolutePath);
        var extension = Path.GetExtension(absolutePath);
        var index = 1;

        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{fileNameWithoutExtension}_{index}{extension}");
            index++;
        }
        while (File.Exists(candidate));

        return candidate;
    }

    public IReadOnlyList<StudentDossierFileEntry> ListStudentFiles(
        string lastName,
        string firstName,
        string registrationNumber,
        string academicYearLabel)
    {
        var directory = StudentDossierPathHelper.ResolveStudentDirectory(
            _rootPath,
            lastName,
            firstName,
            registrationNumber,
            academicYearLabel);

        if (directory is null)
        {
            return [];
        }

        var yearFolder = StudentDossierPathHelper.BuildAcademicYearFolder(academicYearLabel);
        var studentFolder = Path.GetFileName(directory);
        var entries = new List<StudentDossierFileEntry>();

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var info = new FileInfo(file);
            var relativePath = Path.Combine(yearFolder, studentFolder, info.Name).Replace('\\', '/');
            entries.Add(new StudentDossierFileEntry(
                info.Name,
                relativePath,
                info.Length,
                info.LastWriteTimeUtc));
        }

        return entries.OrderBy(e => e.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }
}