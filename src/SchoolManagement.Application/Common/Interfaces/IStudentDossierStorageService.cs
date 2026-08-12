namespace SchoolManagement.Application.Common.Interfaces;

using SchoolManagement.Application.Common.Models;

public interface IStudentDossierStorageService
{
    /// <summary>Écriture définitive : {année}/students/{StudentId}/ — jamais FindExisting.</summary>
    Task<StudentDossierSaveResult> SaveStudentFileAsync(
        StudentDossierFileRequest request,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Upload wizard : temp/{draftId}/ uniquement.</summary>
    Task<StudentDossierSaveResult> SaveDraftFileAsync(
        Guid draftId,
        Guid schoolId,
        Guid? createdByUserId,
        string documentType,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Crée/valide le draft (ownership école + utilisateur).</summary>
    void EnsureDraft(
        Guid draftId,
        Guid schoolId,
        Guid? createdByUserId,
        bool createIfMissing = true);

    /// <summary>Vérifie qu'un draft appartient à l'école (et optionnellement au créateur).</summary>
    void AssertDraftAccess(Guid draftId, Guid schoolId, Guid? userId, bool requireSameCreator = false);

    /// <summary>
    /// Après COMMIT SQL : copie sécurisée temp/{draftId}/ → {année}/students/{StudentId}/.
    /// En cas d'échec partiel, conserve le draft et retourne Succeeded=false.
    /// </summary>
    Task<DraftPromotionResult> PromoteDraftToStudentAsync(
        Guid draftId,
        Guid schoolId,
        Guid studentId,
        string academicYearLabel,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);

    string ResolveAbsolutePath(string storagePath);

    string GetRootPath();

    /// <summary>Crée {année}/students/{StudentId}/.</summary>
    string EnsureStudentIdFolder(Guid studentId, string academicYearLabel);

    /// <summary>Purge les drafts expirés sous temp/ (jamais students/ ni dossiers legacy).</summary>
    int PurgeExpiredDrafts(DateTime utcNow);

    /// <summary>
    /// Réessaie les promotions marquées PendingPromotion après un échec filesystem post-COMMIT.
    /// </summary>
    Task<int> RetryPendingPromotionsAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<StudentDossierFileEntry> ListStudentFiles(
        Guid studentId,
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
