namespace SchoolManagement.Application.Common.Interfaces;

public interface IDocumentBrandingStorageService
{
    string GetDocumentsRootPath();

    Task<string> SaveLogoAsync(Guid schoolId, string originalFileName, Stream content, CancellationToken cancellationToken = default);

    Task<string> SaveHeaderAsync(Guid schoolId, string originalFileName, Stream content, CancellationToken cancellationToken = default);

    Task<string> SaveSignatureAsync(Guid schoolId, string originalFileName, Stream content, CancellationToken cancellationToken = default);

    Task<string> SaveStampAsync(Guid schoolId, string originalFileName, Stream content, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    string ResolveAbsolutePath(string relativePath);

    bool FileExists(string relativePath);
}
