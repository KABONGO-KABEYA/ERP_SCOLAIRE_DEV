using System.IO;
using SchoolManagement.Application.Common.Storage;
using SchoolManagement.Application.Configuration.FileStorage;

namespace SchoolManagement.Desktop.Services;

public sealed class DocumentBrandingPathResolver : IDocumentBrandingPathResolver
{
    public DocumentBrandingPathResolver(FileStorageConfigurationManager configurationManager)
    {
        DocumentsRoot = DocumentBrandingPathHelper.GetDocumentsRoot(configurationManager.GetAbsoluteRootPath());
    }

    public string DocumentsRoot { get; }

    public string? ResolveAbsolutePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        if (Path.IsPathRooted(relativePath) && File.Exists(relativePath))
        {
            return relativePath;
        }

        var candidate = Path.GetFullPath(Path.Combine(DocumentsRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        return File.Exists(candidate) ? candidate : null;
    }
}

public interface IDocumentBrandingPathResolver
{
    string DocumentsRoot { get; }

    string? ResolveAbsolutePath(string? relativePath);
}
