namespace SchoolManagement.Application.Common.Models;

public sealed record StudentDossierSaveResult(
    string StoragePath,
    string FileName,
    long FileSizeBytes);

public sealed record StudentDossierFileRequest(
    string LastName,
    string FirstName,
    string RegistrationNumber,
    string AcademicYearLabel,
    string DocumentType,
    string FileName);
