namespace SchoolManagement.Application.Documents.Interfaces;

using SchoolManagement.Application.Documents.DTOs;

public interface IDocumentService
{
    Task<IReadOnlyList<StudentDocumentDto>> ListAsync(
        Guid schoolId,
        Guid? studentId = null,
        CancellationToken cancellationToken = default);

    Task<StudentDocumentDto> UploadAsync(
        Guid schoolId,
        UploadStudentDocumentRequest request,
        string fileName,
        string? mimeType,
        long fileSizeBytes,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string FileName, string MimeType)> DownloadAsync(
        Guid schoolId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid schoolId, Guid documentId, CancellationToken cancellationToken = default);
}
