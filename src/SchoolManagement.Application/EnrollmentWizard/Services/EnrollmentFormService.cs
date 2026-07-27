namespace SchoolManagement.Application.EnrollmentWizard.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Application.DocumentBranding.Interfaces;
using SchoolManagement.Application.EnrollmentWizard;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.EnrollmentWizard.Interfaces;
using SchoolManagement.Application.Parent.DTOs;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

public sealed class EnrollmentFormService : IEnrollmentFormService
{
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<School> _schoolRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<Section> _sectionRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<StudentGuardian> _studentGuardianRepository;
    private readonly IRepository<Guardian> _guardianRepository;
    private readonly IRepository<StudentFeeBalance> _feeBalanceRepository;
    private readonly IRepository<ClassFeeAmount> _classFeeAmountRepository;
    private readonly IRepository<StudentStatusHistory> _statusHistoryRepository;
    private readonly IRepository<StudentDocument> _studentDocumentRepository;
    private readonly IRepository<UserAccount> _userRepository;
    private readonly IDocumentPrintBrandingResolver _brandingResolver;
    private readonly IDocumentBrandingStorageService _brandingStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly IStudentDossierStorageService _studentDossierStorage;

    public EnrollmentFormService(
        IRepository<Enrollment> enrollmentRepository,
        IRepository<Student> studentRepository,
        IRepository<School> schoolRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<Section> sectionRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<StudentGuardian> studentGuardianRepository,
        IRepository<Guardian> guardianRepository,
        IRepository<StudentFeeBalance> feeBalanceRepository,
        IRepository<ClassFeeAmount> classFeeAmountRepository,
        IRepository<StudentStatusHistory> statusHistoryRepository,
        IRepository<StudentDocument> studentDocumentRepository,
        IRepository<UserAccount> userRepository,
        IDocumentPrintBrandingResolver brandingResolver,
        IDocumentBrandingStorageService brandingStorage,
        ICurrentUserService currentUser,
        IStudentDossierStorageService studentDossierStorage)
    {
        _enrollmentRepository = enrollmentRepository;
        _studentRepository = studentRepository;
        _schoolRepository = schoolRepository;
        _yearRepository = yearRepository;
        _classRoomRepository = classRoomRepository;
        _sectionRepository = sectionRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _studentGuardianRepository = studentGuardianRepository;
        _guardianRepository = guardianRepository;
        _feeBalanceRepository = feeBalanceRepository;
        _classFeeAmountRepository = classFeeAmountRepository;
        _statusHistoryRepository = statusHistoryRepository;
        _studentDocumentRepository = studentDocumentRepository;
        _userRepository = userRepository;
        _brandingResolver = brandingResolver;
        _brandingStorage = brandingStorage;
        _currentUser = currentUser;
        _studentDossierStorage = studentDossierStorage;
    }

    public async Task<EnrollmentFormDocumentDto> GetFormAsync(
        Guid schoolId,
        Guid enrollmentId,
        CancellationToken cancellationToken = default)
    {
        var enrollment = (await _enrollmentRepository.FindAsync(
            e => e.Id == enrollmentId,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Inscription introuvable.");

        var student = (await _studentRepository.FindAsync(
            s => s.Id == enrollment.StudentId && s.SchoolId == schoolId && !s.IsArchived,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        var school = (await _schoolRepository.FindAsync(s => s.Id == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Établissement introuvable.");

        var year = (await _yearRepository.FindAsync(y => y.Id == enrollment.AcademicYearId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Année scolaire introuvable.");

        var classRoom = (await _classRoomRepository.FindAsync(c => c.Id == enrollment.ClassRoomId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Classe introuvable.");

        var section = (await _sectionRepository.FindAsync(s => s.Id == classRoom.SectionId, cancellationToken)).FirstOrDefault();
        PedagogicalClass? pedagogicalClass = null;
        if (classRoom.PedagogicalClassId.HasValue)
        {
            pedagogicalClass = (await _pedagogicalClassRepository.FindAsync(
                p => p.Id == classRoom.PedagogicalClassId.Value,
                cancellationToken)).FirstOrDefault();
        }

        var guardians = await BuildGuardiansAsync(student.Id, cancellationToken);
        var father = FindGuardian(guardians, "Père", "Pere", "Father");
        var mother = FindGuardian(guardians, "Mère", "Mere", "Mother");
        var legalGuardian = guardians.FirstOrDefault(g => g.IsPrimary)
            ?? guardians.FirstOrDefault(g => !IsParent(g));

        var yearTariffIds = (await _classFeeAmountRepository.FindAsync(
            a => a.AcademicYearId == enrollment.AcademicYearId,
            cancellationToken)).Select(a => a.Id).ToHashSet();
        var feeBalances = (await _feeBalanceRepository.FindAsync(
            f => f.StudentId == student.Id && yearTariffIds.Contains(f.ClassFeeAmountId),
            cancellationToken)).ToList();
        var registrationFee = feeBalances.Sum(f => f.AmountDue);
        var currency = feeBalances.FirstOrDefault()?.Currency;

        var documents = (await _studentDocumentRepository.FindAsync(d => d.StudentId == student.Id, cancellationToken))
            .Select(d => d.DocumentType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d)
            .ToList();

        var statusHistory = (await _statusHistoryRepository.FindAsync(
            h => h.StudentId == student.Id && h.AcademicYearId == enrollment.AcademicYearId,
            cancellationToken))
            .OrderByDescending(h => h.EffectiveDate)
            .FirstOrDefault();

        var registrationKind = statusHistory?.Reason;
        var registrationKindLabel = MapRegistrationKindLabel(registrationKind, enrollment.Status);
        var registrationStatut = MapRegistrationStatut(registrationKind, enrollment.Status);
        var className = pedagogicalClass is not null
            ? pedagogicalClass.DisplayName
            : classRoom.Name;

        var branding = await _brandingResolver.ResolveAsync(schoolId, DocumentBrandingType.FicheInscription, cancellationToken);
        var age = EnrollmentBusinessRules.CalculateAge(student.DateOfBirth, enrollment.EnrollmentDate);
        var address = student.Address;
        var parentAccessAccounts = await LoadParentAccessAccountsAsync(schoolId, student.Id, cancellationToken);

        return new EnrollmentFormDocumentDto(
            school.Name,
            year.Label,
            DateTime.Now,
            branding,
            student.RegistrationNumber,
            student.LastName,
            student.FirstName,
            student.MiddleName,
            GetGenderLabel(student.Gender),
            student.DateOfBirth,
            age,
            student.PlaceOfBirth,
            student.Nationality,
            EnrollmentFormFieldParser.ExtractLabeledValue(address, "Province"),
            EnrollmentFormFieldParser.ExtractLabeledValue(address, "Territoire"),
            EnrollmentFormFieldParser.ExtractLabeledValue(address, "Ville"),
            ExtractStreet(address),
            ExtractHouseNumber(address),
            student.Phone,
            student.Email,
            student.PhotoPath,
            className,
            section?.Name,
            MapEducationRegime(pedagogicalClass, section),
            registrationStatut,
            registrationKindLabel,
            enrollment.EnrollmentDate,
            EnrollmentFormFieldParser.ExtractNoteValue(enrollment.Notes, "Provenance:"),
            className,
            EnrollmentFormFieldParser.ExtractNoteValue(enrollment.Notes, "Code élève:"),
            student.BloodGroup,
            EnrollmentFormFieldParser.ExtractMedicalValue(student.MedicalNotes, "Allergies"),
            EnrollmentFormFieldParser.ExtractMedicalValue(student.MedicalNotes, "Maladies chroniques"),
            EnrollmentFormFieldParser.ExtractMedicalValue(student.MedicalNotes, "Handicap"),
            EnrollmentFormFieldParser.ExtractMedicalValue(student.MedicalNotes, "Médecin"),
            EnrollmentFormFieldParser.ExtractMedicalValue(student.MedicalNotes, "Centre médical"),
            ExtractObservations(student.MedicalNotes),
            documents,
            registrationFee > 0 ? registrationFee : null,
            0m,
            currency?.ToString(),
            _currentUser.UserName ?? "Système",
            Environment.MachineName,
            "2026.1.0",
            father,
            mother,
            legalGuardian,
            guardians,
            parentAccessAccounts);
    }

    private async Task<IReadOnlyList<ParentAppAccessCredentialDto>> LoadParentAccessAccountsAsync(
        Guid schoolId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var links = (await _studentGuardianRepository.FindAsync(l => l.StudentId == studentId, cancellationToken)).ToList();
        if (links.Count == 0)
        {
            return [];
        }

        var guardianIds = links.Select(l => l.GuardianId).Distinct().ToList();
        var guardians = (await _guardianRepository.FindAsync(
            g => guardianIds.Contains(g.Id),
            cancellationToken)).ToDictionary(g => g.Id);

        var users = (await _userRepository.FindAsync(
            u => u.SchoolId == schoolId && u.GuardianId.HasValue && guardianIds.Contains(u.GuardianId.Value),
            cancellationToken)).ToList();

        var results = new List<ParentAppAccessCredentialDto>();
        foreach (var user in users.Where(u => u.GuardianId.HasValue))
        {
            guardians.TryGetValue(user.GuardianId!.Value, out var guardian);
            var fullName = guardian is null
                ? $"{user.FirstName} {user.LastName}".Trim()
                : $"{guardian.FirstName} {guardian.LastName}".Trim();

            results.Add(new ParentAppAccessCredentialDto(
                user.GuardianId.Value,
                fullName,
                user.UserName,
                TemporaryPassword: null,
                WasCreated: false,
                user.MustChangePassword));
        }

        return results;
    }

    private async Task<IReadOnlyList<EnrollmentFormGuardianDto>> BuildGuardiansAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var links = await _studentGuardianRepository.FindAsync(l => l.StudentId == studentId, cancellationToken);
        var guardians = new List<EnrollmentFormGuardianDto>();
        foreach (var link in links.OrderByDescending(l => l.IsPrimary).ThenBy(l => l.Relationship))
        {
            var guardian = (await _guardianRepository.FindAsync(g => g.Id == link.GuardianId, cancellationToken)).FirstOrDefault();
            if (guardian is null)
            {
                continue;
            }

            guardians.Add(new EnrollmentFormGuardianDto(
                guardian.LastName,
                guardian.FirstName,
                link.Relationship,
                guardian.Phone,
                guardian.Email,
                guardian.Address,
                guardian.Profession,
                link.IsPrimary,
                link.CanPickup));
        }

        return guardians;
    }

    private static EnrollmentFormGuardianDto? FindGuardian(
        IReadOnlyList<EnrollmentFormGuardianDto> guardians,
        params string[] keywords) =>
        guardians.FirstOrDefault(g => keywords.Any(k =>
            g.Relationship.Contains(k, StringComparison.OrdinalIgnoreCase)));

    private static bool IsParent(EnrollmentFormGuardianDto guardian) =>
        guardian.Relationship.Contains("Père", StringComparison.OrdinalIgnoreCase)
        || guardian.Relationship.Contains("Pere", StringComparison.OrdinalIgnoreCase)
        || guardian.Relationship.Contains("Mère", StringComparison.OrdinalIgnoreCase)
        || guardian.Relationship.Contains("Mere", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractStreet(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var first = address.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return first is null || first.Contains(':', StringComparison.Ordinal) ? null : first;
    }

    private static string? ExtractHouseNumber(string? address) =>
        EnrollmentFormFieldParser.ExtractLabeledValue(address, "N° maison")
        ?? EnrollmentFormFieldParser.ExtractLabeledValue(address, "Maison");

    private static string? ExtractObservations(string? medicalNotes)
    {
        if (string.IsNullOrWhiteSpace(medicalNotes))
        {
            return null;
        }

        var observations = EnrollmentFormFieldParser.ExtractMedicalValue(medicalNotes, "Observations");
        return observations ?? medicalNotes;
    }

    private static string GetGenderLabel(Gender gender) => gender switch
    {
        Gender.Feminin => "Féminin",
        _ => "Masculin"
    };

    private static string MapEducationRegime(PedagogicalClass? pedagogicalClass, Section? section)
    {
        if (pedagogicalClass?.Program == SchoolProgram.Maternelle)
        {
            return "Maternelle";
        }

        if (pedagogicalClass?.Program == SchoolProgram.Primaire || section?.Cycle == EducationCycle.Primaire)
        {
            return "Primaire";
        }

        return "Secondaire";
    }

    private static string MapRegistrationStatut(string? reason, EnrollmentStatus status) =>
        reason switch
        {
            nameof(RegistrationKind.Reinscription) => "Ancien élève",
            nameof(RegistrationKind.Transfert) => "Transfert",
            nameof(RegistrationKind.RetourApresAbandon) => "Ancien élève",
            nameof(RegistrationKind.NouvelleInscription) => "Nouveau",
            _ => status switch
            {
                EnrollmentStatus.Reinscrit => "Ancien élève",
                EnrollmentStatus.Transfere => "Transfert",
                _ => "Nouveau"
            }
        };

    public async Task<StoredEnrollmentFileDto> SaveToStudentDossierAsync(
        Guid schoolId,
        Guid enrollmentId,
        IReadOnlyList<ParentAppAccessCredentialDto>? parentAccessAccounts = null,
        CancellationToken cancellationToken = default)
    {
        var form = await GetFormAsync(schoolId, enrollmentId, cancellationToken);
        if (parentAccessAccounts is { Count: > 0 })
        {
            form = form with { ParentAccessAccounts = parentAccessAccounts };
        }

        var pdfBytes = EnrollmentFormPdfGenerator.BuildPdfBytes(form, TryLoadImage);
        await using var stream = new MemoryStream(pdfBytes);
        var saved = await _studentDossierStorage.SaveStudentFileAsync(
            new StudentDossierFileRequest(
                form.LastName,
                form.FirstName,
                form.RegistrationNumber,
                form.AcademicYearLabel,
                "Fiche_inscription",
                "Fiche_inscription.pdf"),
            stream,
            cancellationToken);

        return new StoredEnrollmentFileDto(
            saved.StoragePath,
            saved.FileName,
            saved.FileSizeBytes);
    }

    private byte[]? TryLoadImage(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        try
        {
            if (_brandingStorage.FileExists(relativePath))
            {
                var brandingPath = _brandingStorage.ResolveAbsolutePath(relativePath);
                return File.Exists(brandingPath) ? File.ReadAllBytes(brandingPath) : null;
            }

            var dossierPath = _studentDossierStorage.ResolveAbsolutePath(relativePath);
            return File.Exists(dossierPath) ? File.ReadAllBytes(dossierPath) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string MapRegistrationKindLabel(string? reason, EnrollmentStatus status) =>
        reason switch
        {
            nameof(RegistrationKind.Reinscription) => "Réinscription",
            nameof(RegistrationKind.Transfert) => "Transfert",
            nameof(RegistrationKind.RetourApresAbandon) => "Retour après abandon",
            nameof(RegistrationKind.NouvelleInscription) => "Nouvelle inscription",
            _ => status switch
            {
                EnrollmentStatus.Reinscrit => "Réinscription",
                EnrollmentStatus.Transfere => "Transfert",
                _ => "Inscription"
            }
        };
}
