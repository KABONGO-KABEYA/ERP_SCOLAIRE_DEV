namespace SchoolManagement.Application.Documents.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Documents.DTOs;
using SchoolManagement.Application.Documents.Interfaces;
using SchoolManagement.Domain.Entities.Students;

public sealed class DocumentService : IDocumentService
{
    private readonly IRepository<StudentDocument> _documentRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;

    public DocumentService(
        IRepository<StudentDocument> documentRepository,
        IRepository<Student> studentRepository,
        IFileStorageService fileStorage,
        IUnitOfWork unitOfWork)
    {
        _documentRepository = documentRepository;
        _studentRepository = studentRepository;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<StudentDocumentDto>> ListAsync(
        Guid schoolId,
        Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);
        var studentIds = studentMap.Keys.ToList();

        var documents = await _documentRepository.FindAsync(
            d => studentIds.Contains(d.StudentId) && (!studentId.HasValue || d.StudentId == studentId.Value),
            cancellationToken);

        return documents
            .OrderByDescending(d => d.CreatedAt)
            .Select(d =>
            {
                studentMap.TryGetValue(d.StudentId, out var student);
                var name = student is null ? "—" : $"{student.LastName} {student.FirstName}";
                return new StudentDocumentDto(d.Id, d.StudentId, name, d.DocumentType, d.FileName, d.FileSizeBytes, d.MimeType, d.CreatedAt);
            })
            .ToList();
    }

    public async Task<StudentDocumentDto> UploadAsync(
        Guid schoolId,
        UploadStudentDocumentRequest request,
        string fileName,
        string? mimeType,
        long fileSizeBytes,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var student = (await _studentRepository.FindAsync(
            s => s.Id == request.StudentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var storagePath = await _fileStorage.SaveAsync(schoolId, request.StudentId, fileName, content, cancellationToken);

        var document = new StudentDocument
        {
            StudentId = request.StudentId,
            DocumentType = request.DocumentType,
            FileName = fileName,
            StoragePath = storagePath,
            MimeType = mimeType,
            FileSizeBytes = fileSizeBytes
        };

        await _documentRepository.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StudentDocumentDto(
            document.Id,
            document.StudentId,
            $"{student.LastName} {student.FirstName}",
            document.DocumentType,
            document.FileName,
            document.FileSizeBytes,
            document.MimeType,
            document.CreatedAt);
    }

    public async Task<(Stream Stream, string FileName, string MimeType)> DownloadAsync(
        Guid schoolId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = (await _documentRepository.FindAsync(d => d.Id == documentId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Document introuvable.");

        var student = (await _studentRepository.FindAsync(
            s => s.Id == document.StudentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new UnauthorizedAccessException();

        var stream = await _fileStorage.OpenReadAsync(document.StoragePath, cancellationToken)
            ?? throw new FileNotFoundException("Fichier introuvable sur le serveur.");

        return (stream, document.FileName, document.MimeType ?? "application/octet-stream");
    }

    public async Task DeleteAsync(Guid schoolId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = (await _documentRepository.FindAsync(d => d.Id == documentId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Document introuvable.");

        var student = (await _studentRepository.FindAsync(
            s => s.Id == document.StudentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new UnauthorizedAccessException();

        await _fileStorage.DeleteAsync(document.StoragePath, cancellationToken);
        await _documentRepository.DeleteAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
