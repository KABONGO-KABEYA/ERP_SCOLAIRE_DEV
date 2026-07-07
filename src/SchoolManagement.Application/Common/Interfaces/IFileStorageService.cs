namespace SchoolManagement.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(Guid schoolId, Guid studentId, string fileName, Stream content, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
