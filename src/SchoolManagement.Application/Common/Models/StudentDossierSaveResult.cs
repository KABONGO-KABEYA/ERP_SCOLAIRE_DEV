namespace SchoolManagement.Application.Common.Models;

public sealed record StudentDossierSaveResult(
    string StoragePath,
    string FileName,
    long FileSizeBytes);

/// <summary>Écriture définitive sous {année}/students/{StudentId}/ (P3).</summary>
public sealed record StudentDossierFileRequest(
    Guid StudentId,
    string AcademicYearLabel,
    string DocumentType,
    string FileName);

/// <summary>Résultat de promotion draft → dossier StudentId.</summary>
public sealed record DraftPromotionResult(
    IReadOnlyDictionary<string, string> PathMap,
    string TargetDirectoryRelative,
    bool Succeeded,
    string? ErrorMessage);
