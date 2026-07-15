namespace SchoolManagement.Application.EnrollmentWizard.Services;

using SchoolManagement.Application.EnrollmentWizard;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Exceptions;

public sealed partial class EnrollmentWizardService
{
    public async Task<StudentDossierEditDto> GetStudentDossierForEditAsync(
        Guid schoolId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var prerequisites = await GetPrerequisitesAsync(schoolId, cancellationToken);
        if (!prerequisites.IsReady || prerequisites.CurrentAcademicYearId is null)
        {
            throw new DomainException("Les prérequis système ne sont pas satisfaits.");
        }

        var student = (await _studentRepository.FindAsync(
            s => s.Id == studentId && s.SchoolId == schoolId && !s.IsArchived,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.StudentId == studentId
                 && e.AcademicYearId == prerequisites.CurrentAcademicYearId.Value
                 && e.IsActive,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Aucune inscription active trouvée pour l'année scolaire courante.");

        var classRoom = (await _classRoomRepository.FindAsync(
            c => c.Id == enrollment.ClassRoomId && c.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Classe introuvable.");

        var statusHistory = (await _statusHistoryRepository.FindAsync(
            h => h.StudentId == studentId && h.AcademicYearId == enrollment.AcademicYearId,
            cancellationToken))
            .OrderByDescending(h => h.EffectiveDate)
            .FirstOrDefault();

        var guardianLinks = (await _studentGuardianRepository.FindAsync(
            sg => sg.StudentId == studentId,
            cancellationToken)).ToList();
        var guardianIds = guardianLinks.Select(l => l.GuardianId).Distinct().ToList();
        var guardians = guardianIds.Count == 0
            ? []
            : (await _guardianRepository.FindAsync(g => guardianIds.Contains(g.Id), cancellationToken)).ToList();

        var documents = (await _studentDocumentRepository.FindAsync(
            d => d.StudentId == studentId,
            cancellationToken)).ToList();

        var dossier = await StudentDossierEditMapper.BuildRequestAsync(
            student,
            enrollment,
            classRoom,
            statusHistory,
            guardianLinks,
            guardians,
            documents,
            _addressService,
            cancellationToken);

        var (canChangeClass, blockedReason) = await CanStudentChangeClassAsync(
            studentId,
            enrollment.ClassRoomId,
            enrollment.AcademicYearId,
            cancellationToken);

        return new StudentDossierEditDto(
            student.Id,
            enrollment.Id,
            student.RegistrationNumber,
            canChangeClass,
            blockedReason,
            dossier);
    }

    public Task<EnrollmentValidationResultDto> ValidateStudentDossierUpdateAsync(
        Guid schoolId,
        Guid enrollmentId,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default) =>
        ValidateStudentDossierUpdateInternalAsync(schoolId, enrollmentId, request, cancellationToken);

    public async Task<UpdateStudentDossierResultDto> UpdateStudentDossierAsync(
        Guid schoolId,
        Guid enrollmentId,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateStudentDossierUpdateInternalAsync(
            schoolId,
            enrollmentId,
            request,
            cancellationToken);
        if (!validation.IsValid)
        {
            throw new DomainException(validation.Issues.First().Message);
        }

        var prerequisites = await GetPrerequisitesAsync(schoolId, cancellationToken);
        if (!prerequisites.IsReady || prerequisites.CurrentAcademicYearId is null)
        {
            throw new DomainException("Les prérequis d'inscription ne sont pas satisfaits.");
        }

        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.Id == enrollmentId && e.IsActive,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Inscription introuvable.");

        if (enrollment.AcademicYearId != prerequisites.CurrentAcademicYearId.Value)
        {
            throw new DomainException("Seule l'inscription de l'année scolaire courante peut être modifiée.");
        }

        var student = (await _studentRepository.FindAsync(
            s => s.Id == enrollment.StudentId && s.SchoolId == schoolId && !s.IsArchived,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        if (request.ExistingStudentId != student.Id)
        {
            throw new DomainException("L'identifiant élève ne correspond pas à l'inscription.");
        }

        var classChanged = enrollment.ClassRoomId != request.Scolarite.ClassRoomId;
        if (classChanged)
        {
            var (canChangeClass, blockedReason) = await CanStudentChangeClassAsync(
                student.Id,
                enrollment.ClassRoomId,
                enrollment.AcademicYearId,
                cancellationToken);
            if (!canChangeClass)
            {
                throw new DomainException(blockedReason ?? "La classe ne peut plus être modifiée.");
            }

            var classRoom = await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
                _classRoomRepository,
                _pedagogicalClassRepository,
                _yearRepository,
                schoolId,
                request.Scolarite.ClassRoomId,
                cancellationToken);

            await EnsureClassCapacityAsync(
                classRoom,
                request.Scolarite.ClassRoomId,
                enrollment.AcademicYearId,
                cancellationToken);
        }

        await ApplyStudentFieldsAsync(student, request, cancellationToken);
        await _studentRepository.UpdateAsync(student, cancellationToken);

        await ReplaceGuardiansAsync(
            schoolId,
            student.Id,
            request.Guardians,
            request.ResidenceAddress,
            cancellationToken);

        enrollment.ClassRoomId = request.Scolarite.ClassRoomId;
        enrollment.EnrollmentDate = request.Scolarite.EnrollmentDate;
        enrollment.Notes = BuildEnrollmentNotes(request.Scolarite);
        await _enrollmentRepository.UpdateAsync(enrollment, cancellationToken);

        await PersistNewDocumentsAsync(
            student.Id,
            student,
            prerequisites.CurrentAcademicYearLabel ?? enrollment.AcademicYearId.ToString(),
            request.Documents,
            cancellationToken);

        await _auditRepository.AddAsync(new AuditEntry
        {
            Action = "EnrollmentWizard.UpdateDossier",
            EntityName = nameof(Student),
            EntityId = student.Id,
            NewValues = classChanged
                ? "Dossier élève mis à jour avec changement de classe."
                : "Dossier élève mis à jour.",
            Timestamp = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var ficheMessage = string.Empty;
        try
        {
            await _enrollmentFormService.SaveToStudentDossierAsync(schoolId, enrollment.Id, cancellationToken);
            ficheMessage = " Fiche d'inscription (PDF) régénérée dans le dossier élève.";
        }
        catch
        {
            ficheMessage = " Fiche d'inscription (PDF) non régénérée (dossier partagé indisponible).";
        }

        return new UpdateStudentDossierResultDto(
            student.Id,
            enrollment.Id,
            student.RegistrationNumber,
            $"{student.LastName} {student.FirstName}",
            "Dossier élève mis à jour." + ficheMessage);
    }

    private async Task<EnrollmentValidationResultDto> ValidateStudentDossierUpdateInternalAsync(
        Guid schoolId,
        Guid enrollmentId,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var issues = new List<EnrollmentValidationIssueDto>();

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            issues.Add(new("last_name", "Le nom de l'élève est obligatoire.", "identity"));
        }

        if (string.IsNullOrWhiteSpace(request.MiddleName))
        {
            issues.Add(new("middle_name", "Le postnom de l'élève est obligatoire.", "identity"));
        }

        if (request.DateOfBirth == default)
        {
            issues.Add(new("date_of_birth", "La date de naissance est invalide.", "identity"));
        }

        if (request.Scolarite.ClassRoomId == Guid.Empty)
        {
            issues.Add(new("class_room", "La classe est obligatoire.", "scolarite"));
        }

        if (request.Guardians.All(g => !g.IsPrimary))
        {
            issues.Add(new("primary_guardian", "Le responsable principal doit être renseigné.", "guardians"));
        }

        var primary = request.Guardians.FirstOrDefault(g => g.IsPrimary);
        if (primary is not null && string.IsNullOrWhiteSpace(primary.Phone))
        {
            issues.Add(new("primary_phone", "Le téléphone du responsable principal est obligatoire.", "guardians"));
        }

        if (!request.ConfirmAccuracy)
        {
            issues.Add(new("confirmation", "Vous devez confirmer l'exactitude des informations.", "validation"));
        }

        ValidateGuardianGenders(request.Guardians, issues);

        if (issues.Count > 0)
        {
            return new EnrollmentValidationResultDto(false, issues);
        }

        try
        {
            var prerequisites = await GetPrerequisitesAsync(schoolId, cancellationToken);
            if (!prerequisites.IsReady || prerequisites.CurrentAcademicYearId is null)
            {
                issues.Add(new("prerequisites", "Les prérequis système ne sont pas satisfaits.", "prerequisites"));
                return new EnrollmentValidationResultDto(false, issues);
            }

            var enrollment = (await _enrollmentRepository.FindAsync(
                e => e.Id == enrollmentId && e.IsActive,
                cancellationToken)).FirstOrDefault()
                ?? throw new KeyNotFoundException("Inscription introuvable.");

            if (enrollment.AcademicYearId != prerequisites.CurrentAcademicYearId.Value)
            {
                issues.Add(new(
                    "enrollment_year",
                    "Seule l'inscription de l'année scolaire courante peut être modifiée.",
                    "scolarite"));
                return new EnrollmentValidationResultDto(false, issues);
            }

            if (!request.ExistingStudentId.HasValue || request.ExistingStudentId.Value != enrollment.StudentId)
            {
                issues.Add(new("student_mismatch", "L'élève ne correspond pas à l'inscription.", "identity"));
                return new EnrollmentValidationResultDto(false, issues);
            }

            await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
                _classRoomRepository,
                _pedagogicalClassRepository,
                _yearRepository,
                schoolId,
                request.Scolarite.ClassRoomId,
                cancellationToken);

            var classRoom = (await _classRoomRepository.FindAsync(
                c => c.Id == request.Scolarite.ClassRoomId && c.SchoolId == schoolId,
                cancellationToken)).First();

            if (enrollment.ClassRoomId != request.Scolarite.ClassRoomId)
            {
                var (canChangeClass, blockedReason) = await CanStudentChangeClassAsync(
                    enrollment.StudentId,
                    enrollment.ClassRoomId,
                    enrollment.AcademicYearId,
                    cancellationToken);
                if (!canChangeClass)
                {
                    issues.Add(new(
                        "class_locked",
                        blockedReason ?? "La classe ne peut plus être modifiée.",
                        "scolarite"));
                    return new EnrollmentValidationResultDto(false, issues);
                }

                await EnsureClassCapacityAsync(
                    classRoom,
                    request.Scolarite.ClassRoomId,
                    enrollment.AcademicYearId,
                    cancellationToken);
            }

            var pedagogicalMap = await SchoolConfigurationGuards.BuildPedagogicalMapAsync(
                _pedagogicalClassRepository, schoolId, cancellationToken);

            if (classRoom.PedagogicalClassId.HasValue
                && pedagogicalMap.TryGetValue(classRoom.PedagogicalClassId.Value, out var pedagogicalClass))
            {
                EnrollmentBusinessRules.EnsureAgeCompatible(
                    request.DateOfBirth,
                    pedagogicalClass,
                    request.Scolarite.EnrollmentDate);
            }
        }
        catch (Exception ex) when (ex is DomainException or KeyNotFoundException)
        {
            issues.Add(new("business_rule", ex.Message, null));
        }

        return new EnrollmentValidationResultDto(issues.Count == 0, issues);
    }

    private async Task<(bool CanChange, string? Reason)> CanStudentChangeClassAsync(
        Guid studentId,
        Guid classRoomId,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        var gradeEntries = await _gradeEntryRepository.FindAsync(g => g.StudentId == studentId, cancellationToken);
        if (gradeEntries.Count > 0)
        {
            var evaluationIds = gradeEntries.Select(g => g.EvaluationId).ToHashSet();
            var evaluations = await _evaluationRepository.FindAsync(
                e => evaluationIds.Contains(e.Id)
                     && e.ClassRoomId == classRoomId
                     && e.AcademicYearId == academicYearId,
                cancellationToken);
            if (evaluations.Count > 0)
            {
                return (false, "Des notes ont déjà été saisies pour cette classe.");
            }
        }

        var attendances = await _attendanceRepository.FindAsync(
            a => a.StudentId == studentId && a.ClassRoomId == classRoomId,
            cancellationToken);
        if (attendances.Count > 0)
        {
            return (false, "Des présences ont déjà été enregistrées pour cette classe.");
        }

        var periodResults = await _periodResultRepository.FindAsync(
            p => p.StudentId == studentId
                 && p.ClassRoomId == classRoomId
                 && p.AcademicYearId == academicYearId,
            cancellationToken);
        if (periodResults.Count > 0)
        {
            return (false, "Des résultats de période existent déjà pour cette classe.");
        }

        return (true, null);
    }

    private async Task PersistNewDocumentsAsync(
        Guid studentId,
        Student student,
        string academicYearLabel,
        IReadOnlyList<EnrollmentDocumentStatusDto> documents,
        CancellationToken cancellationToken)
    {
        var existingPaths = (await _studentDocumentRepository.FindAsync(
            d => d.StudentId == studentId,
            cancellationToken))
            .Select(d => d.StoragePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in documents.Where(d =>
                     d.Status.Equals("Complet", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(d.FileName)
                     && !string.IsNullOrWhiteSpace(d.StoragePath)))
        {
            var storagePath = await EnsureDossierStoragePathAsync(
                student,
                academicYearLabel,
                doc.DocumentType,
                doc.FileName!,
                doc.StoragePath,
                cancellationToken);

            if (existingPaths.Contains(storagePath))
            {
                continue;
            }

            await _studentDocumentRepository.AddAsync(new StudentDocument
            {
                StudentId = studentId,
                DocumentType = doc.DocumentType,
                FileName = doc.FileName!,
                StoragePath = storagePath,
                MimeType = GuessMimeType(doc.FileName),
                FileSizeBytes = doc.FileSizeBytes
            }, cancellationToken);

            existingPaths.Add(storagePath);
        }
    }
}
