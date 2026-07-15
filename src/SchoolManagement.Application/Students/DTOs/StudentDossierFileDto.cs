namespace SchoolManagement.Application.Students.DTOs;

public sealed record StudentDossierFileDto(
    string FileName,
    string StoragePath,
    long SizeBytes,
    DateTime LastModifiedUtc);
