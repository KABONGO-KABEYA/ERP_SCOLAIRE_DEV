namespace SchoolManagement.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Application.Common.Storage;
using SchoolManagement.Application.Configuration.FileStorage;
using SchoolManagement.Domain.Exceptions;

public sealed class StudentDossierStorageService : IStudentDossierStorageService
{
    private readonly string _rootPath;
    private readonly ILogger<StudentDossierStorageService> _logger;

    public StudentDossierStorageService(
        FileStorageConfigurationManager configurationManager,
        ILogger<StudentDossierStorageService> logger)
    {
        _rootPath = configurationManager.GetAbsoluteRootPath();
        _logger = logger;
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
        var targetDirectory = Path.Combine(
            _rootPath,
            yearFolder,
            StudentDossierPathHelper.StudentsFolderName,
            request.StudentId.ToString("D"));
        Directory.CreateDirectory(targetDirectory);

        var storedFileName = StudentDossierPathHelper.BuildStoredFileName(request.DocumentType, request.FileName);
        var absolutePath = EnsureUniqueFilePath(Path.Combine(targetDirectory, storedFileName));

        await using (var fileStream = File.Create(absolutePath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var fileInfo = new FileInfo(absolutePath);
        var relativePath = StudentDossierPathHelper.BuildStudentIdRelativeFilePath(
            request.AcademicYearLabel,
            request.StudentId,
            fileInfo.Name);
        return new StudentDossierSaveResult(relativePath, fileInfo.Name, fileInfo.Length);
    }

    public async Task<StudentDossierSaveResult> SaveDraftFileAsync(
        Guid draftId,
        Guid schoolId,
        Guid? createdByUserId,
        string documentType,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        EnsureDraft(draftId, schoolId, createdByUserId, createIfMissing: true);

        var targetDirectory = GetDraftAbsoluteDirectory(draftId);
        Directory.CreateDirectory(targetDirectory);

        var storedFileName = StudentDossierPathHelper.BuildStoredFileName(documentType, fileName);
        var absolutePath = EnsureUniqueFilePath(Path.Combine(targetDirectory, storedFileName));

        await using (var fileStream = File.Create(absolutePath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        var fileInfo = new FileInfo(absolutePath);
        var relativePath = $"{StudentDossierPathHelper.BuildDraftRelativeDirectory(draftId)}/{fileInfo.Name}";
        return new StudentDossierSaveResult(relativePath, fileInfo.Name, fileInfo.Length);
    }

    public void EnsureDraft(
        Guid draftId,
        Guid schoolId,
        Guid? createdByUserId,
        bool createIfMissing = true)
    {
        ValidateDraftId(draftId);
        var draftDir = GetDraftAbsoluteDirectory(draftId);
        var metaPath = Path.Combine(draftDir, StudentDossierPathHelper.DraftMetaFileName);

        if (File.Exists(metaPath))
        {
            AssertDraftAccess(draftId, schoolId, createdByUserId, requireSameCreator: false);
            return;
        }

        if (!createIfMissing)
        {
            throw new KeyNotFoundException("Draft d'inscription introuvable ou expiré.");
        }

        Directory.CreateDirectory(draftDir);
        var now = DateTime.UtcNow;
        var meta = new EnrollmentDraftMeta
        {
            DraftId = draftId,
            SchoolId = schoolId,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(StudentDossierPathHelper.DraftTimeToLive)
        };
        File.WriteAllText(metaPath, StudentDossierPathHelper.SerializeDraftMeta(meta));
    }

    public void AssertDraftAccess(Guid draftId, Guid schoolId, Guid? userId, bool requireSameCreator = false)
    {
        ValidateDraftId(draftId);
        var meta = ReadDraftMeta(draftId)
            ?? throw new KeyNotFoundException("Draft d'inscription introuvable ou expiré.");

        if (meta.SchoolId != schoolId)
        {
            throw new UnauthorizedAccessException("Ce draft n'appartient pas à cet établissement.");
        }

        if (meta.ExpiresAtUtc < DateTime.UtcNow && meta.PromotedStudentId is null)
        {
            throw new DomainException("Ce draft d'inscription a expiré.");
        }

        if (requireSameCreator
            && meta.CreatedByUserId.HasValue
            && userId.HasValue
            && meta.CreatedByUserId.Value != userId.Value)
        {
            throw new UnauthorizedAccessException("Ce draft appartient à un autre utilisateur.");
        }
    }

    public async Task<DraftPromotionResult> PromoteDraftToStudentAsync(
        Guid draftId,
        Guid schoolId,
        Guid studentId,
        string academicYearLabel,
        CancellationToken cancellationToken = default)
    {
        AssertDraftAccess(draftId, schoolId, userId: null, requireSameCreator: false);

        var draftDir = GetDraftAbsoluteDirectory(draftId);
        if (!Directory.Exists(draftDir))
        {
            return new DraftPromotionResult(
                new Dictionary<string, string>(),
                StudentDossierPathHelper.BuildStudentIdRelativeDirectory(academicYearLabel, studentId),
                Succeeded: true,
                ErrorMessage: null);
        }

        var targetRelative = StudentDossierPathHelper.BuildStudentIdRelativeDirectory(academicYearLabel, studentId);
        var targetDir = Path.Combine(
            _rootPath,
            StudentDossierPathHelper.BuildAcademicYearFolder(academicYearLabel),
            StudentDossierPathHelper.StudentsFolderName,
            studentId.ToString("D"));

        try
        {
            Directory.CreateDirectory(targetDir);
            var pathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var draftRelativePrefix = StudentDossierPathHelper.BuildDraftRelativeDirectory(draftId);

            foreach (var sourceFile in Directory.EnumerateFiles(draftDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(sourceFile);
                if (string.Equals(name, StudentDossierPathHelper.DraftMetaFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var destAbsolute = Path.Combine(targetDir, name);
                // Copie + vérif + delete source (SMB-safe, non atomique).
                await CopyFileAsync(sourceFile, destAbsolute, cancellationToken);
                if (!File.Exists(destAbsolute) || new FileInfo(destAbsolute).Length == 0 && new FileInfo(sourceFile).Length > 0)
                {
                    throw new IOException($"Échec de copie vers {destAbsolute}");
                }

                File.Delete(sourceFile);
                var oldRelative = $"{draftRelativePrefix}/{name}";
                var newRelative = $"{targetRelative}/{name}";
                pathMap[oldRelative] = newRelative;
            }

            var meta = ReadDraftMeta(draftId);
            if (meta is not null)
            {
                meta.PromotedStudentId = studentId;
                meta.PromotedAtUtc = DateTime.UtcNow;
                meta.PendingPromotionStudentId = null;
                meta.PendingPromotionAcademicYearLabel = null;
                meta.LastPromotionError = null;
                File.WriteAllText(
                    Path.Combine(draftDir, StudentDossierPathHelper.DraftMetaFileName),
                    StudentDossierPathHelper.SerializeDraftMeta(meta));
            }

            TryDeleteEmptyDraftDirectory(draftDir);
            return new DraftPromotionResult(pathMap, targetRelative, Succeeded: true, ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Échec promotion draft {DraftId} → student {StudentId}. Le draft est conservé pour retry.",
                draftId,
                studentId);
            MarkPendingPromotion(draftId, studentId, academicYearLabel, ex.Message);
            return new DraftPromotionResult(
                new Dictionary<string, string>(),
                targetRelative,
                Succeeded: false,
                ErrorMessage: ex.Message);
        }
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

    public string EnsureStudentIdFolder(Guid studentId, string academicYearLabel)
    {
        var yearFolder = StudentDossierPathHelper.BuildAcademicYearFolder(academicYearLabel);
        var targetDirectory = Path.Combine(
            _rootPath,
            yearFolder,
            StudentDossierPathHelper.StudentsFolderName,
            studentId.ToString("D"));
        Directory.CreateDirectory(targetDirectory);
        return targetDirectory;
    }

    public int PurgeExpiredDrafts(DateTime utcNow)
    {
        var tempRoot = Path.Combine(_rootPath, StudentDossierPathHelper.TempFolderName);
        if (!Directory.Exists(tempRoot))
        {
            return 0;
        }

        var purged = 0;
        foreach (var draftDir in Directory.EnumerateDirectories(tempRoot))
        {
            try
            {
                var name = Path.GetFileName(draftDir);
                if (!Guid.TryParse(name, out var draftId))
                {
                    continue;
                }

                var meta = ReadDraftMeta(draftId);
                if (meta is null)
                {
                    // Dossier sans méta : purge si plus vieux que TTL (LastWriteTime).
                    var info = new DirectoryInfo(draftDir);
                    if (info.LastWriteTimeUtc.Add(StudentDossierPathHelper.DraftTimeToLive) < utcNow)
                    {
                        Directory.Delete(draftDir, recursive: true);
                        purged++;
                    }

                    continue;
                }

                // Ne jamais purger un draft en attente de promotion post-COMMIT (évite perte fichier).
                if (meta.PendingPromotionStudentId.HasValue && !meta.PromotedStudentId.HasValue)
                {
                    continue;
                }

                if (meta.PromotedStudentId.HasValue)
                {
                    // Draft déjà promu : nettoyer le reste.
                    Directory.Delete(draftDir, recursive: true);
                    purged++;
                    continue;
                }

                if (meta.ExpiresAtUtc < utcNow)
                {
                    Directory.Delete(draftDir, recursive: true);
                    purged++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Purge draft échouée pour {Dir}", draftDir);
            }
        }

        return purged;
    }

    public async Task<int> RetryPendingPromotionsAsync(CancellationToken cancellationToken = default)
    {
        var tempRoot = Path.Combine(_rootPath, StudentDossierPathHelper.TempFolderName);
        if (!Directory.Exists(tempRoot))
        {
            return 0;
        }

        var retried = 0;
        foreach (var draftDir in Directory.EnumerateDirectories(tempRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(draftDir);
            if (!Guid.TryParse(name, out var draftId))
            {
                continue;
            }

            var meta = ReadDraftMeta(draftId);
            if (meta?.PendingPromotionStudentId is not { } studentId
                || meta.PromotedStudentId.HasValue
                || string.IsNullOrWhiteSpace(meta.PendingPromotionAcademicYearLabel))
            {
                continue;
            }

            var result = await PromoteDraftToStudentAsync(
                draftId,
                meta.SchoolId,
                studentId,
                meta.PendingPromotionAcademicYearLabel,
                cancellationToken);
            if (result.Succeeded)
            {
                retried++;
            }
        }

        return retried;
    }

    public IReadOnlyList<StudentDossierFileEntry> ListStudentFiles(
        Guid studentId,
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
            academicYearLabel,
            studentId);

        if (directory is null)
        {
            return [];
        }

        var yearFolder = StudentDossierPathHelper.BuildAcademicYearFolder(academicYearLabel);
        var relativeDir = directory.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(_rootPath, directory).Replace('\\', '/')
            : Path.Combine(yearFolder, Path.GetFileName(directory)).Replace('\\', '/');

        var entries = new List<StudentDossierFileEntry>();
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var info = new FileInfo(file);
            if (string.Equals(info.Name, StudentDossierPathHelper.DraftMetaFileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = $"{relativeDir}/{info.Name}";
            entries.Add(new StudentDossierFileEntry(
                info.Name,
                relativePath,
                info.Length,
                info.LastWriteTimeUtc));
        }

        return entries.OrderBy(e => e.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private EnrollmentDraftMeta? ReadDraftMeta(Guid draftId)
    {
        var metaPath = Path.Combine(GetDraftAbsoluteDirectory(draftId), StudentDossierPathHelper.DraftMetaFileName);
        if (!File.Exists(metaPath))
        {
            return null;
        }

        return StudentDossierPathHelper.DeserializeDraftMeta(File.ReadAllText(metaPath));
    }

    private void MarkPendingPromotion(
        Guid draftId,
        Guid studentId,
        string academicYearLabel,
        string errorMessage)
    {
        try
        {
            var meta = ReadDraftMeta(draftId);
            if (meta is null)
            {
                return;
            }

            meta.PendingPromotionStudentId = studentId;
            meta.PendingPromotionAcademicYearLabel = academicYearLabel;
            meta.LastPromotionError = errorMessage;
            // Évite la purge TTL tant que le retry n'a pas abouti.
            meta.ExpiresAtUtc = DateTime.UtcNow.AddDays(30);
            var metaPath = Path.Combine(
                GetDraftAbsoluteDirectory(draftId),
                StudentDossierPathHelper.DraftMetaFileName);
            File.WriteAllText(metaPath, StudentDossierPathHelper.SerializeDraftMeta(meta));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible de marquer pending promotion pour draft {DraftId}", draftId);
        }
    }

    private string GetDraftAbsoluteDirectory(Guid draftId) =>
        Path.Combine(_rootPath, StudentDossierPathHelper.TempFolderName, draftId.ToString("D"));

    private static void ValidateDraftId(Guid draftId)
    {
        if (draftId == Guid.Empty)
        {
            throw new DomainException("draftId invalide.");
        }
    }

    private static void TryDeleteEmptyDraftDirectory(string draftDir)
    {
        try
        {
            if (!Directory.Exists(draftDir))
            {
                return;
            }

            var remaining = Directory.EnumerateFileSystemEntries(draftDir)
                .Where(p => !string.Equals(
                    Path.GetFileName(p),
                    StudentDossierPathHelper.DraftMetaFileName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (remaining.Count == 0)
            {
                Directory.Delete(draftDir, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var src = File.OpenRead(source);
        await using var dst = File.Create(destination);
        await src.CopyToAsync(dst, cancellationToken);
        await dst.FlushAsync(cancellationToken);
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
}
