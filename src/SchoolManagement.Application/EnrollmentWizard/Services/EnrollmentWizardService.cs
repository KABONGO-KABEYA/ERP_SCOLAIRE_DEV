namespace SchoolManagement.Application.EnrollmentWizard.Services;

using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.EnrollmentWizard.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class EnrollmentWizardService : IEnrollmentWizardService
{
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<Section> _sectionRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<Guardian> _guardianRepository;
    private readonly IRepository<StudentGuardian> _studentGuardianRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<StudentStatusHistory> _statusHistoryRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<StudentFeeBalance> _feeBalanceRepository;
    private readonly IRepository<StudentDocument> _studentDocumentRepository;
    private readonly IRepository<AuditEntry> _auditRepository;
    private readonly IPedagogicalStructureService _pedagogicalStructureService;
    private readonly IUnitOfWork _unitOfWork;

    public EnrollmentWizardService(
        IRepository<AcademicYear> yearRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<Section> sectionRepository,
        IRepository<Student> studentRepository,
        IRepository<Guardian> guardianRepository,
        IRepository<StudentGuardian> studentGuardianRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<StudentStatusHistory> statusHistoryRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<StudentFeeBalance> feeBalanceRepository,
        IRepository<StudentDocument> studentDocumentRepository,
        IRepository<AuditEntry> auditRepository,
        IPedagogicalStructureService pedagogicalStructureService,
        IUnitOfWork unitOfWork)
    {
        _yearRepository = yearRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _sectionRepository = sectionRepository;
        _studentRepository = studentRepository;
        _guardianRepository = guardianRepository;
        _studentGuardianRepository = studentGuardianRepository;
        _enrollmentRepository = enrollmentRepository;
        _statusHistoryRepository = statusHistoryRepository;
        _feeTypeRepository = feeTypeRepository;
        _feeBalanceRepository = feeBalanceRepository;
        _studentDocumentRepository = studentDocumentRepository;
        _auditRepository = auditRepository;
        _pedagogicalStructureService = pedagogicalStructureService;
        _unitOfWork = unitOfWork;
    }

    public async Task<EnrollmentPrerequisitesDto> GetPrerequisitesAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<EnrollmentPrerequisiteIssueDto>();

        var currentYear = (await _yearRepository.FindAsync(
            y => y.SchoolId == schoolId && y.IsCurrent && !y.IsClosed,
            cancellationToken)).FirstOrDefault();

        if (currentYear is null)
        {
            issues.Add(new EnrollmentPrerequisiteIssueDto(
                "academic_year",
                "Impossible de procéder à une inscription tant qu'aucune année scolaire courante ouverte n'est configurée.",
                "academic-years",
                "Configurer maintenant"));
        }

        var summary = await _pedagogicalStructureService.GetSummaryAsync(schoolId, skipEnsure: true, cancellationToken);
        if (summary.EnabledClasses == 0)
        {
            issues.Add(new EnrollmentPrerequisiteIssueDto(
                "pedagogical_structure",
                "Impossible de procéder à une inscription tant que la structure pédagogique n'est pas configurée.",
                "pedagogical-structure",
                "Configurer maintenant"));
        }

        if (summary.TotalLocals == 0)
        {
            issues.Add(new EnrollmentPrerequisiteIssueDto(
                "class_locals",
                "Impossible de procéder à une inscription tant qu'aucun local n'est défini pour les classes.",
                "pedagogical-structure",
                "Configurer maintenant"));
        }

        return new EnrollmentPrerequisitesDto(
            issues.Count == 0,
            issues,
            currentYear?.Id,
            currentYear?.Label,
            summary,
            0);
    }

    public async Task<GeneratedRegistrationNumberDto> GenerateRegistrationNumberAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var year = DateTime.UtcNow.Year;
        var next = students.Count + 1;
        string candidate;
        do
        {
            candidate = $"ELV-{year}-{next:D5}";
            next++;
        }
        while (students.Any(s => s.RegistrationNumber.Equals(candidate, StringComparison.OrdinalIgnoreCase)));

        return new GeneratedRegistrationNumberDto(candidate);
    }

    public async Task<IReadOnlyList<EnrollmentStudentSearchResultDto>> SearchStudentsAsync(
        Guid schoolId,
        string search,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return [];
        }

        var term = search.Trim().ToLowerInvariant();
        var students = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);
        var guardians = await _guardianRepository.FindAsync(g => g.SchoolId == schoolId, cancellationToken);
        var links = await _studentGuardianRepository.FindAsync(_ => true, cancellationToken);
        var enrollments = await _enrollmentRepository.FindAsync(e => e.IsActive, cancellationToken);
        var years = await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);
        var classRooms = await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        var pedagogicalMap = ClassRoomAvailability.BuildMap(
            await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken));

        var guardianIdsByStudent = links
            .GroupBy(l => l.StudentId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.GuardianId).ToList());

        var currentYear = years.FirstOrDefault(y => y.IsCurrent && !y.IsClosed);

        var results = new List<EnrollmentStudentSearchResultDto>();
        foreach (var student in students)
        {
            var guardianPhones = guardianIdsByStudent.GetValueOrDefault(student.Id)?
                .Select(id => guardians.FirstOrDefault(g => g.Id == id)?.Phone)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.ToLowerInvariant())
                .ToList() ?? [];

            var matches = student.FirstName.ToLowerInvariant().Contains(term)
                          || student.LastName.ToLowerInvariant().Contains(term)
                          || (student.MiddleName?.ToLowerInvariant().Contains(term) ?? false)
                          || student.RegistrationNumber.ToLowerInvariant().Contains(term)
                          || (student.Phone?.ToLowerInvariant().Contains(term) ?? false)
                          || guardianPhones.Any(p => p.Contains(term));

            if (!matches)
            {
                continue;
            }

            var studentEnrollments = enrollments.Where(e => e.StudentId == student.Id).ToList();
            var currentEnrollment = currentYear is not null
                ? studentEnrollments.FirstOrDefault(e => e.AcademicYearId == currentYear.Id)
                : null;

            string? previousClass = null;
            string? previousYear = null;
            string status;

            if (currentEnrollment is not null)
            {
                status = "Inscrit année en cours";
                var room = classRooms.FirstOrDefault(c => c.Id == currentEnrollment.ClassRoomId);
                if (room is not null)
                {
                    pedagogicalMap.TryGetValue(room.PedagogicalClassId ?? Guid.Empty, out var pc);
                    previousClass = pc is not null ? $"{pc.DisplayName} {room.Name}" : room.Name;
                    previousYear = currentYear?.Label;
                }
            }
            else
            {
                var last = studentEnrollments
                    .OrderByDescending(e => e.EnrollmentDate)
                    .FirstOrDefault();

                if (last is not null)
                {
                    var year = years.FirstOrDefault(y => y.Id == last.AcademicYearId);
                    var room = classRooms.FirstOrDefault(c => c.Id == last.ClassRoomId);
                    pedagogicalMap.TryGetValue(room?.PedagogicalClassId ?? Guid.Empty, out var pc);
                    previousClass = room is not null
                        ? pc is not null ? $"{pc.DisplayName} {room.Name}" : room.Name
                        : null;
                    previousYear = year?.Label;
                    status = last.Status.ToString();
                }
                else
                {
                    status = "Dossier existant";
                }
            }

            results.Add(new EnrollmentStudentSearchResultDto(
                student.Id,
                student.RegistrationNumber,
                student.FirstName,
                student.LastName,
                student.MiddleName,
                student.Gender,
                student.DateOfBirth,
                student.PhotoPath,
                student.Phone,
                previousClass,
                previousYear,
                status));
        }

        return results
            .OrderBy(r => r.LastName)
            .ThenBy(r => r.FirstName)
            .Take(25)
            .ToList();
    }

    public async Task<EnrollmentStructureOptionsDto> GetStructureOptionsAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var currentYear = (await _yearRepository.FindAsync(
            y => y.SchoolId == schoolId && y.IsCurrent && !y.IsClosed,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Aucune année scolaire courante ouverte.");

        var sections = await _sectionRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var sectionDtos = sections
            .OrderBy(s => s.Name)
            .Select(s => new SectionDto(s.Id, s.Code, s.Name, s.Cycle))
            .ToList();

        var classes = await _classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId && c.AcademicYearId == currentYear.Id,
            cancellationToken);

        var pedagogicalMap = ClassRoomAvailability.BuildMap(
            await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken));

        classes = classes.Where(c => ClassRoomAvailability.IsSelectable(c, pedagogicalMap)).ToList();

        var enrollments = await _enrollmentRepository.FindAsync(
            e => e.AcademicYearId == currentYear.Id && e.IsActive,
            cancellationToken);

        var countByClass = enrollments
            .GroupBy(e => e.ClassRoomId)
            .ToDictionary(g => g.Key, g => g.Count());

        var classOptions = classes
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Name)
            .Select(c =>
            {
                pedagogicalMap.TryGetValue(c.PedagogicalClassId ?? Guid.Empty, out var pedagogical);
                var section = sections.FirstOrDefault(s => s.Id == c.SectionId);
                var count = countByClass.GetValueOrDefault(c.Id);
                var fullName = pedagogical is not null
                    ? $"{pedagogical.DisplayName} {c.Name}"
                    : c.Name;

                return new EnrollmentClassOptionDto(
                    c.Id,
                    c.Code,
                    fullName,
                    c.Name,
                    pedagogical?.DisplayName,
                    pedagogical?.HumanitiesSection ?? section?.Name,
                    pedagogical?.StudyOption,
                    c.SectionId,
                    section?.Name ?? "—",
                    c.PedagogicalClassId,
                    c.MaxCapacity,
                    count,
                    pedagogical?.MinAge,
                    pedagogical?.MaxAge,
                    true);
            })
            .ToList();

        return new EnrollmentStructureOptionsDto(
            currentYear.Id,
            currentYear.Label,
            sectionDtos,
            classOptions);
    }

    public async Task<ClassCapacityDto> GetClassCapacityAsync(
        Guid schoolId,
        Guid classRoomId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        var classRoom = (await _classRoomRepository.FindAsync(
            c => c.Id == classRoomId && c.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Classe introuvable.");

        var count = (await _enrollmentRepository.FindAsync(
            e => e.ClassRoomId == classRoomId && e.AcademicYearId == academicYearId && e.IsActive,
            cancellationToken)).Count;

        var max = classRoom.MaxCapacity;
        var remaining = max.HasValue ? Math.Max(0, max.Value - count) : int.MaxValue;

        return new ClassCapacityDto(
            classRoomId,
            max,
            count,
            remaining == int.MaxValue ? 0 : remaining,
            max.HasValue && count >= max.Value);
    }

    public async Task<EnrollmentFeeSummaryDto> CalculateFeesAsync(
        Guid schoolId,
        IReadOnlyList<Guid>? selectedFeeTypeIds = null,
        IReadOnlyDictionary<Guid, decimal>? discounts = null,
        CancellationToken cancellationToken = default)
    {
        var feeTypes = await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken);
        if (selectedFeeTypeIds is { Count: > 0 })
        {
            var set = selectedFeeTypeIds.ToHashSet();
            feeTypes = feeTypes.Where(f => set.Contains(f.Id)).ToList();
        }

        discounts ??= new Dictionary<Guid, decimal>();

        var lines = feeTypes
            .OrderBy(f => f.Name)
            .Select(f =>
            {
                var discount = discounts.GetValueOrDefault(f.Id);
                var net = Math.Max(0, f.DefaultAmount - discount);
                return new EnrollmentFeeLineDto(
                    f.Id,
                    f.Code,
                    f.Name,
                    f.DefaultAmount,
                    discount,
                    0,
                    net,
                    f.IsMandatory);
            })
            .ToList();

        var currency = feeTypes.FirstOrDefault()?.Currency ?? Currency.CDF;
        return new EnrollmentFeeSummaryDto(lines, lines.Sum(l => l.NetAmount), currency);
    }

    public Task<EnrollmentValidationResultDto> ValidateAsync(
        Guid schoolId,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default) =>
        ValidateInternalAsync(schoolId, request, cancellationToken);

    public async Task<CompleteEnrollmentResultDto> CompleteAsync(
        Guid schoolId,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateInternalAsync(schoolId, request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new DomainException(validation.Issues.First().Message);
        }

        var prerequisites = await GetPrerequisitesAsync(schoolId, cancellationToken);
        if (!prerequisites.IsReady || prerequisites.CurrentAcademicYearId is null)
        {
            throw new DomainException("Les prérequis d'inscription ne sont pas satisfaits.");
        }

        var academicYearId = prerequisites.CurrentAcademicYearId.Value;
        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository, schoolId, academicYearId, cancellationToken);

        var classRoom = await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
            _classRoomRepository,
            _pedagogicalClassRepository,
            schoolId,
            request.Scolarite.ClassRoomId,
            cancellationToken);

        await EnsureClassCapacityAsync(classRoom, request.Scolarite.ClassRoomId, academicYearId, cancellationToken);

        var pedagogicalMap = await SchoolConfigurationGuards.BuildPedagogicalMapAsync(
            _pedagogicalClassRepository, schoolId, cancellationToken);

        if (classRoom.PedagogicalClassId.HasValue
            && pedagogicalMap.TryGetValue(classRoom.PedagogicalClassId.Value, out var pedagogicalClass))
        {
            EnrollmentBusinessRules.EnsureAgeCompatible(request.DateOfBirth, pedagogicalClass, request.Scolarite.EnrollmentDate);
        }

        Student student;
        if (request.ExistingStudentId.HasValue)
        {
            student = (await _studentRepository.FindAsync(
                s => s.Id == request.ExistingStudentId.Value && s.SchoolId == schoolId && !s.IsArchived,
                cancellationToken)).FirstOrDefault()
                ?? throw new KeyNotFoundException("Élève introuvable.");

            ApplyStudentFields(student, request);
            await _studentRepository.UpdateAsync(student, cancellationToken);
        }
        else
        {
            var registration = await GenerateRegistrationNumberAsync(schoolId, cancellationToken);
            student = CreateStudentEntity(schoolId, registration.RegistrationNumber, request);
            await _studentRepository.AddAsync(student, cancellationToken);
        }

        await ReplaceGuardiansAsync(schoolId, student.Id, request.Guardians, cancellationToken);

        var enrollmentStatus = MapRegistrationKind(request.Scolarite.RegistrationKind);
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            AcademicYearId = academicYearId,
            ClassRoomId = request.Scolarite.ClassRoomId,
            EnrollmentDate = request.Scolarite.EnrollmentDate,
            Status = enrollmentStatus,
            IsActive = true,
            Notes = BuildEnrollmentNotes(request.Scolarite)
        };

        await _enrollmentRepository.AddAsync(enrollment, cancellationToken);

        await _statusHistoryRepository.AddAsync(new StudentStatusHistory
        {
            StudentId = student.Id,
            AcademicYearId = academicYearId,
            PreviousStatus = EnrollmentStatus.PreInscription,
            NewStatus = enrollmentStatus,
            EffectiveDate = request.Scolarite.EnrollmentDate,
            Reason = request.Scolarite.RegistrationKind.ToString()
        }, cancellationToken);

        var feeSummary = request.FeeSummary;
        var totalDue = 0m;

        if (feeSummary is { Lines.Count: > 0 })
        {
            totalDue = feeSummary.TotalDue;
            foreach (var line in feeSummary.Lines)
            {
                await _feeBalanceRepository.AddAsync(new StudentFeeBalance
                {
                    StudentId = student.Id,
                    AcademicYearId = academicYearId,
                    FeeTypeId = line.FeeTypeId,
                    AmountDue = line.NetAmount,
                    AmountPaid = 0,
                    Currency = feeSummary.Currency
                }, cancellationToken);
            }
        }

        await PersistDocumentsAsync(student.Id, request.Documents, cancellationToken);

        var auditActions = new List<string>
        {
            "Dossier élève créé/mis à jour",
            $"Matricule définitif : {student.RegistrationNumber}",
            "Dossier scolaire et inscription enregistrés",
            "Affectation classe et local confirmée",
            feeSummary is null
                ? "Frais scolaires : à traiter séparément (module Paiements)"
                : "Dossier financier initialisé (frais d'inscription)",
            "Dossier de présence prêt",
            "Dossier d'examens prêt",
            "Dossier de bulletins prêt",
            "Dossier disciplinaire prêt"
        };

        await _auditRepository.AddAsync(new AuditEntry
        {
            Action = "EnrollmentWizard.Complete",
            EntityName = nameof(Student),
            EntityId = student.Id,
            NewValues = string.Join("; ", auditActions),
            Timestamp = DateTime.UtcNow
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var className = classRoom.PedagogicalClassId.HasValue
            && pedagogicalMap.TryGetValue(classRoom.PedagogicalClassId.Value, out var pc)
            ? $"{pc.DisplayName} {classRoom.Name}"
            : classRoom.Name;

        return new CompleteEnrollmentResultDto(
            student.Id,
            enrollment.Id,
            student.RegistrationNumber,
            $"{student.LastName} {student.FirstName}",
            className,
            totalDue,
            feeSummary is null
                ? "Dossier élève enregistré. Les frais scolaires seront traités séparément dans le module Paiements."
                : "Inscription validée. Dossiers scolaire, financier, présence, examens, bulletins et disciplinaire initialisés.");
    }

    private async Task PersistDocumentsAsync(
        Guid studentId,
        IReadOnlyList<EnrollmentDocumentStatusDto> documents,
        CancellationToken cancellationToken)
    {
        foreach (var doc in documents.Where(d =>
                     d.Status.Equals("Complet", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(d.FileName)))
        {
            await _studentDocumentRepository.AddAsync(new StudentDocument
            {
                StudentId = studentId,
                DocumentType = doc.DocumentType,
                FileName = doc.FileName!,
                StoragePath = doc.StoragePath ?? doc.FileName!,
                MimeType = GuessMimeType(doc.FileName),
                FileSizeBytes = 0
            }, cancellationToken);

            if (doc.DocumentType.Equals("Photo", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(doc.StoragePath))
            {
                var student = (await _studentRepository.FindAsync(s => s.Id == studentId, cancellationToken)).FirstOrDefault();
                if (student is not null)
                {
                    student.PhotoPath = doc.StoragePath;
                    await _studentRepository.UpdateAsync(student, cancellationToken);
                }
            }
        }
    }

    private static string? GuessMimeType(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }

    private async Task<EnrollmentValidationResultDto> ValidateInternalAsync(
        Guid schoolId,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var issues = new List<EnrollmentValidationIssueDto>();

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            issues.Add(new("last_name", "Le nom de l'élève est obligatoire.", "identity"));
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            issues.Add(new("first_name", "Le prénom de l'élève est obligatoire.", "identity"));
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

        var mandatoryDocs = new[] { "Acte de naissance", "Photo" };
        foreach (var docType in mandatoryDocs)
        {
            var doc = request.Documents.FirstOrDefault(d =>
                d.DocumentType.Equals(docType, StringComparison.OrdinalIgnoreCase));
            if (doc is null || !doc.Status.Equals("Complet", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new(
                    $"document_{docType}",
                    $"Le document « {docType} » est obligatoire et doit être complet.",
                    "documents"));
            }
        }

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

            await SchoolConfigurationGuards.EnsureSelectableClassRoomAsync(
                _classRoomRepository,
                _pedagogicalClassRepository,
                schoolId,
                request.Scolarite.ClassRoomId,
                cancellationToken);

            var classRoom = (await _classRoomRepository.FindAsync(
                c => c.Id == request.Scolarite.ClassRoomId && c.SchoolId == schoolId,
                cancellationToken)).First();

            await EnsureClassCapacityAsync(classRoom, request.Scolarite.ClassRoomId, prerequisites.CurrentAcademicYearId.Value, cancellationToken);

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

            if (request.ExistingStudentId is null)
            {
                var duplicate = await _studentRepository.FindAsync(
                    s => s.SchoolId == schoolId
                         && !s.IsArchived
                         && s.FirstName == request.FirstName
                         && s.LastName == request.LastName
                         && s.DateOfBirth == request.DateOfBirth,
                    cancellationToken);

                if (duplicate.Count > 0)
                {
                    issues.Add(new(
                        "duplicate",
                        "Un élève avec le même nom, prénom et date de naissance existe déjà.",
                        "search"));
                }
            }
            else
            {
                var active = await _enrollmentRepository.FindAsync(
                    e => e.StudentId == request.ExistingStudentId.Value
                         && e.AcademicYearId == prerequisites.CurrentAcademicYearId.Value
                         && e.IsActive,
                    cancellationToken);

                if (active.Count > 0)
                {
                    issues.Add(new(
                        "already_enrolled",
                        "Cet élève est déjà inscrit pour l'année scolaire courante.",
                        "scolarite"));
                }
            }
        }
        catch (Exception ex) when (ex is DomainException or KeyNotFoundException)
        {
            issues.Add(new("business_rule", ex.Message, null));
        }

        return new EnrollmentValidationResultDto(issues.Count == 0, issues);
    }

    private async Task EnsureClassCapacityAsync(
        ClassRoom classRoom,
        Guid classRoomId,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        if (!classRoom.MaxCapacity.HasValue)
        {
            return;
        }

        var capacity = await GetClassCapacityAsync(classRoom.SchoolId, classRoomId, academicYearId, cancellationToken);
        if (capacity.IsFull)
        {
            throw new DomainException($"La classe est complète ({capacity.CurrentCount}/{capacity.MaxCapacity} places).");
        }
    }

    private static Student CreateStudentEntity(Guid schoolId, string registrationNumber, CompleteEnrollmentRequest request)
    {
        var student = new Student
        {
            SchoolId = schoolId,
            RegistrationNumber = registrationNumber,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            MiddleName = request.MiddleName?.Trim(),
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            PlaceOfBirth = request.PlaceOfBirth?.Trim(),
            Nationality = request.Nationality?.Trim() ?? "Congolaise",
            Address = BuildAddress(request),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            PhotoPath = request.PhotoPath,
            BloodGroup = request.Medical.BloodGroup?.Trim(),
            MedicalNotes = BuildMedicalNotes(request)
        };
        return student;
    }

    private static void ApplyStudentFields(Student student, CompleteEnrollmentRequest request)
    {
        student.FirstName = request.FirstName.Trim();
        student.LastName = request.LastName.Trim();
        student.MiddleName = request.MiddleName?.Trim();
        student.Gender = request.Gender;
        student.DateOfBirth = request.DateOfBirth;
        student.PlaceOfBirth = request.PlaceOfBirth?.Trim();
        student.Nationality = request.Nationality?.Trim() ?? student.Nationality;
        student.Address = BuildAddress(request);
        student.Phone = request.Phone?.Trim();
        student.Email = request.Email?.Trim();
        student.PhotoPath = request.PhotoPath ?? student.PhotoPath;
        student.BloodGroup = request.Medical.BloodGroup?.Trim();
        student.MedicalNotes = BuildMedicalNotes(request);
    }

    private static string? BuildAddress(CompleteEnrollmentRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            parts.Add(request.Address.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            parts.Add($"Ville: {request.City.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Territory))
        {
            parts.Add($"Territoire: {request.Territory.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Province))
        {
            parts.Add($"Province: {request.Province.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Country))
        {
            parts.Add($"Pays: {request.Country.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            parts.Add($"Langue: {request.Language.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(request.Religion))
        {
            parts.Add($"Religion: {request.Religion.Trim()}");
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static string BuildMedicalNotes(CompleteEnrollmentRequest request)
    {
        var m = request.Medical;
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(m.Allergies))
        {
            lines.Add($"Allergies: {m.Allergies}");
        }

        if (!string.IsNullOrWhiteSpace(m.ChronicDiseases))
        {
            lines.Add($"Maladies chroniques: {m.ChronicDiseases}");
        }

        if (!string.IsNullOrWhiteSpace(m.Treatment))
        {
            lines.Add($"Traitement: {m.Treatment}");
        }

        if (!string.IsNullOrWhiteSpace(m.DoctorName))
        {
            lines.Add($"Médecin: {m.DoctorName}");
        }

        if (!string.IsNullOrWhiteSpace(m.MedicalCenter))
        {
            lines.Add($"Centre médical: {m.MedicalCenter}");
        }

        if (!string.IsNullOrWhiteSpace(m.Disability))
        {
            lines.Add($"Handicap: {m.Disability}");
        }

        if (!string.IsNullOrWhiteSpace(m.Observations))
        {
            lines.Add($"Observations: {m.Observations}");
        }

        if (m.MedicalEmergency)
        {
            lines.Add("URGENCE MÉDICALE");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string? BuildEnrollmentNotes(EnrollmentScolariteDto scolarite)
    {
        var parts = new List<string>();
        if (scolarite.OrderNumber.HasValue)
        {
            parts.Add($"N° ordre: {scolarite.OrderNumber}");
        }

        if (!string.IsNullOrWhiteSpace(scolarite.PreviousSchool))
        {
            parts.Add($"Provenance: {scolarite.PreviousSchool}");
        }

        if (!string.IsNullOrWhiteSpace(scolarite.PreviousStudentCode))
        {
            parts.Add($"Code élève: {scolarite.PreviousStudentCode}");
        }

        if (!string.IsNullOrWhiteSpace(scolarite.PermanentNumber))
        {
            parts.Add($"N° permanent: {scolarite.PermanentNumber}");
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private async Task ReplaceGuardiansAsync(
        Guid schoolId,
        Guid studentId,
        IReadOnlyList<GuardianInputDto> guardians,
        CancellationToken cancellationToken)
    {
        var existingLinks = await _studentGuardianRepository.FindAsync(
            sg => sg.StudentId == studentId,
            cancellationToken);

        foreach (var link in existingLinks)
        {
            await _studentGuardianRepository.DeleteAsync(link, cancellationToken);
        }

        foreach (var input in guardians.Where(g =>
                     !string.IsNullOrWhiteSpace(g.FirstName) || !string.IsNullOrWhiteSpace(g.LastName)))
        {
            var guardian = new Guardian
            {
                SchoolId = schoolId,
                FirstName = input.FirstName.Trim(),
                LastName = input.LastName.Trim(),
                Phone = input.Phone?.Trim(),
                Email = input.Email?.Trim(),
                Address = input.Address?.Trim(),
                Profession = string.IsNullOrWhiteSpace(input.Employer)
                    ? input.Profession?.Trim()
                    : $"{input.Profession?.Trim()} — {input.Employer.Trim()}"
            };

            await _guardianRepository.AddAsync(guardian, cancellationToken);

            await _studentGuardianRepository.AddAsync(new StudentGuardian
            {
                StudentId = studentId,
                GuardianId = guardian.Id,
                Relationship = string.IsNullOrWhiteSpace(input.Relationship) ? "Responsable" : input.Relationship.Trim(),
                IsPrimary = input.IsPrimary,
                CanPickup = input.CanPickup
            }, cancellationToken);
        }
    }

    private static EnrollmentStatus MapRegistrationKind(RegistrationKind kind) => kind switch
    {
        RegistrationKind.Reinscription => EnrollmentStatus.Reinscrit,
        RegistrationKind.Transfert => EnrollmentStatus.Transfere,
        RegistrationKind.RetourApresAbandon => EnrollmentStatus.Inscrit,
        _ => EnrollmentStatus.Inscrit
    };
}

internal static class EnrollmentBusinessRules
{
    public static int CalculateAge(DateOnly dateOfBirth, DateOnly referenceDate)
    {
        var age = referenceDate.Year - dateOfBirth.Year;
        if (dateOfBirth > referenceDate.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    public static void EnsureAgeCompatible(DateOnly dateOfBirth, PedagogicalClass pedagogicalClass, DateOnly referenceDate)
    {
        if (!pedagogicalClass.MinAge.HasValue && !pedagogicalClass.MaxAge.HasValue)
        {
            return;
        }

        var age = CalculateAge(dateOfBirth, referenceDate);
        if (pedagogicalClass.MinAge.HasValue && age < pedagogicalClass.MinAge.Value)
        {
            throw new DomainException(
                $"L'âge de l'élève ({age} ans) est inférieur à l'âge minimum ({pedagogicalClass.MinAge} ans) pour cette classe.");
        }

        if (pedagogicalClass.MaxAge.HasValue && age > pedagogicalClass.MaxAge.Value)
        {
            throw new DomainException(
                $"L'âge de l'élève ({age} ans) dépasse l'âge maximum ({pedagogicalClass.MaxAge} ans) pour cette classe.");
        }
    }
}
