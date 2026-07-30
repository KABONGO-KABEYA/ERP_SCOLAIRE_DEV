namespace SchoolManagement.Application.Common.Interfaces;

using SchoolManagement.Application.Common.Models;

public interface IStudentDossierStorageService
{
    Task<StudentDossierSaveResult> SaveStudentFileAsync(
        StudentDossierFileRequest request,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);

    string ResolveAbsolutePath(string storagePath);

    string GetRootPath();

    /// <summary>
    /// Crée le dossier élève (année / Nom_Prenom_Matricule) s'il n'existe pas encore.
    /// </summary>
    string EnsureStudentFolder(
        string lastName,
        string firstName,
        string registrationNumber,
        string academicYearLabel);

    IReadOnlyList<StudentDossierFileEntry> ListStudentFiles(
        string lastName,
        string firstName,
        string registrationNumber,
        string academicYearLabel);
}

public sealed record StudentDossierFileEntry(
    string FileName,
    string StoragePath,
    long SizeBytes,
    DateTime LastModifiedUtc);
