namespace SchoolManagement.Application.Documents.DTOs;

public sealed record StudentDocumentDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string DocumentType,
    string FileName,
    long FileSizeBytes,
    string? MimeType,
    DateTime CreatedAt);

public sealed record UploadStudentDocumentRequest(
    Guid StudentId,
    string DocumentType);
