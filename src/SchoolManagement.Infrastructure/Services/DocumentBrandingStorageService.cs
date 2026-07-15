namespace SchoolManagement.Infrastructure.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Storage;
using SchoolManagement.Application.Configuration.FileStorage;

public sealed class DocumentBrandingStorageService : IDocumentBrandingStorageService
{
    private readonly string _documentsRoot;

    public DocumentBrandingStorageService(FileStorageConfigurationManager configurationManager)
    {
        var fileRoot = configurationManager.GetAbsoluteRootPath();
        if (!Directory.Exists(fileRoot))
        {
            throw new InvalidOperationException(
                $"Le dossier partagé configuré est introuvable : {fileRoot}");
        }

        _documentsRoot = DocumentBrandingPathHelper.GetDocumentsRoot(fileRoot);
    }

    public string GetDocumentsRootPath() => _documentsRoot;

    public Task<string> SaveLogoAsync(Guid schoolId, string originalFileName, Stream content, CancellationToken cancellationToken = default) =>
        SaveAsync("Logos", schoolId, originalFileName, content, DocumentBrandingPathHelper.MaxLogoFileBytes, cancellationToken);

    public Task<string> SaveHeaderAsync(Guid schoolId, string originalFileName, Stream content, CancellationToken cancellationToken = default) =>
        SaveAsync("Entetes", schoolId, originalFileName, content, DocumentBrandingPathHelper.MaxHeaderFileBytes, cancellationToken);

    public Task<string> SaveSignatureAsync(Guid schoolId, string originalFileName, Stream content, CancellationToken cancellationToken = default) =>
        SaveAsync("Signatures", schoolId, originalFileName, content, DocumentBrandingPathHelper.MaxSignatureFileBytes, cancellationToken);

    public Task<string> SaveStampAsync(Guid schoolId, string originalFileName, Stream content, CancellationToken cancellationToken = default) =>
        SaveAsync("Cachets", schoolId, originalFileName, content, DocumentBrandingPathHelper.MaxStampFileBytes, cancellationToken);

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveSafePath(relativePath);
        if (absolutePath is not null && File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    public string ResolveAbsolutePath(string relativePath)
    {
        var safe = ResolveSafePath(relativePath);
        return safe ?? Path.Combine(_documentsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public bool FileExists(string relativePath)
    {
        var absolutePath = ResolveSafePath(relativePath);
        return absolutePath is not null && File.Exists(absolutePath);
    }

    private async Task<string> SaveAsync(
        string categoryFolder,
        Guid schoolId,
        string originalFileName,
        Stream content,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        if (!content.CanSeek)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return await SaveAsync(categoryFolder, schoolId, originalFileName, buffer, maxBytes, cancellationToken);
        }

        DocumentBrandingPathHelper.ValidateImageFile(originalFileName, content.Length, maxBytes);

        var targetDirectory = Path.Combine(_documentsRoot, categoryFolder, schoolId.ToString("N"));
        Directory.CreateDirectory(targetDirectory);

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(targetDirectory, storedFileName);

        await using (var fileStream = File.Create(absolutePath))
        {
            content.Position = 0;
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        return DocumentBrandingPathHelper.BuildRelativePath(categoryFolder, schoolId, storedFileName);
    }

    private string? ResolveSafePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        if (Path.IsPathRooted(relativePath))
        {
            var normalizedRoot = _documentsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(relativePath);
            return fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }

        var candidate = Path.GetFullPath(Path.Combine(_documentsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = _documentsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }
}
