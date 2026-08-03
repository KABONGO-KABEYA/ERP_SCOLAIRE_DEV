namespace SchoolManagement.Application.EnrollmentWizard.Services;

using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Common.Models;
using SchoolManagement.Application.Common.Storage;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.EnrollmentWizard.Interfaces;
using SchoolManagement.Application.Parent.DTOs;
using SchoolManagement.Application.Parent.Interfaces;
using SchoolManagement.Application.Notifications.Interfaces;
using SchoolManagement.Application.SchoolFees.Interfaces;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Application.Geography.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed partial class EnrollmentWizardService : IEnrollmentWizardService
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
    private readonly ISchoolFeeService _schoolFeeService;
    private readonly IStudentFeeBalanceProvisioner _feeBalanceProvisioner;
    private readonly IRepository<StudentFeeBalance> _feeBalanceRepository;
    private readonly IRepository<StudentDocument> _studentDocumentRepository;
    private readonly IRepository<GradeEntry> _gradeEntryRepository;
    private readonly IRepository<Evaluation> _evaluationRepository;
    private readonly IRepository<StudentAttendance> _attendanceRepository;
    private readonly IRepository<PeriodResult> _periodResultRepository;
    private readonly IRepository<AuditEntry> _auditRepository;
    private readonly IPedagogicalStructureService _pedagogicalStructureService;
    private readonly IStudentDossierStorageService _studentDossierStorage;
    private readonly IAddressService _addressService;
    private readonly IEnrollmentFormService _enrollmentFormService;
    private readonly IParentAccessProvisioningService _parentAccessProvisioning;
    private readonly INotificationService _notifications;
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
        ISchoolFeeService schoolFeeService,
        IStudentFeeBalanceProvisioner feeBalanceProvisioner,
        IRepository<StudentFeeBalance> feeBalanceRepository,
        IRepository<StudentDocument> studentDocumentRepository,
        IRepository<GradeEntry> gradeEntryRepository,
        IRepository<Evaluation> evaluationRepository,
        IRepository<StudentAttendance> attendanceRepository,
        IRepository<PeriodResult> periodResultRepository,
        IRepository<AuditEntry> auditRepository,
        IPedagogicalStructureService pedagogicalStructureService,
        IStudentDossierStorageService studentDossierStorage,
        IAddressService addressService,
        IEnrollmentFormService enrollmentFormService,
        IParentAccessProvisioningService parentAccessProvisioning,
        INotificationService notifications,
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
        _schoolFeeService = schoolFeeService;
        _feeBalanceProvisioner = feeBalanceProvisioner;
        _feeBalanceRepository = feeBalanceRepository;
        _studentDocumentRepository = studentDocumentRepository;
        _gradeEntryRepository = gradeEntryRepository;
        _evaluationRepository = evaluationRepository;
        _attendanceRepository = attendanceRepository;
        _periodResultRepository = periodResultRepository;
        _auditRepository = auditRepository;
        _pedagogicalStructureService = pedagogicalStructureService;
        _studentDossierStorage = studentDossierStorage;
        _addressService = addressService;
        _enrollmentFormService = enrollmentFormService;
        _parentAccessProvisioning = parentAccessProvisioning;
        _notifications = notifications;
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

        var summary = await _pedagogicalStructureService.GetSummaryAsync(
            schoolId,
            skipEnsure: true,
            academicYearId: currentYear?.Id,
            cancellationToken);

        if (summary.EnabledClasses == 0)
        {
            issues.Add(new EnrollmentPrerequisiteIssueDto(
                "pedagogical_structure",
                "Impossible de procéder à une inscription tant que la structure pédagogique n'est pas configurée.",
                "pedagogical-structure",
                "Configurer maintenant"));
        }

        if (currentYear is not null)
        {
            var locals = await _classRoomRepository.FindAsync(
                c => c.SchoolId == schoolId
                    && c.AcademicYearId == currentYear.Id
                    && c.PedagogicalClassId.HasValue,
                cancellationToken);
            var pedagogicalMap = ClassRoomAvailability.BuildMap(
                await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken));
            var selectableLocals = locals.Count(c => ClassRoomAvailability.IsSelectable(c, pedagogicalMap));

            if (selectableLocals == 0)
            {
                issues.Add(new EnrollmentPrerequisiteIssueDto(
                    "class_locals",
                    "Impossible de procéder à une inscription tant qu'aucun local actif n'est défini pour l'année scolaire courante.",
                    "pedagogical-structure",
                    "Configurer maintenant"));
            }
        }
        else if (summary.TotalLocals == 0)
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
        bool forReinscription = false,
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
                ? studentEnrollments.FirstOrDefault(e => e.AcademicYearId == currentYear.Id && e.IsActive)
                : null;

            if (forReinscription && currentEnrollment is not null)
            {
                continue;
            }

            var referenceEnrollment = studentEnrollments
                .Where(e => currentYear is null || e.AcademicYearId != currentYear.Id)
                .OrderByDescending(e => e.EnrollmentDate)
                .FirstOrDefault()
                ?? studentEnrollments.OrderByDescending(e => e.EnrollmentDate).FirstOrDefault();

            int? lastClassLevel = null;
            if (referenceEnrollment is not null)
            {
                var referenceRoom = classRooms.FirstOrDefault(c => c.Id == referenceEnrollment.ClassRoomId);
                lastClassLevel = referenceRoom?.Level;
            }

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
                status,
                lastClassLevel));
        }

        return results
            .OrderBy(r => r.LastName)
            .ThenBy(r => r.FirstName)
            .Take(25)
            .ToList();
    }

    public async Task<IReadOnlyList<EnrollmentGuardianSearchResultDto>> SearchGuardiansAsync(
        Guid schoolId,
        string search,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return [];
        }

        var term = search.Trim().ToLowerInvariant();
        var guardians = await _guardianRepository.FindAsync(g => g.SchoolId == schoolId, cancellationToken);

        return guardians
            .Where(g => !g.IsDeleted)
            .Where(g =>
                g.FirstName.ToLowerInvariant().Contains(term)
                || g.LastName.ToLowerInvariant().Contains(term)
                || (g.Phone?.ToLowerInvariant().Contains(term) ?? false)
                || (g.Email?.ToLowerInvariant().Contains(term) ?? false)
                || (g.Profession?.ToLowerInvariant().Contains(term) ?? false))
            .OrderBy(g => g.LastName)
            .ThenBy(g => g.FirstName)
            .Take(25)
            .Select(g => new EnrollmentGuardianSearchResultDto(
                g.Id,
                g.FirstName,
                g.LastName,
                g.Phone,
                g.Email,
                g.Address,
                g.Profession,
                g.Gender))
            .ToList();
    }

    public async Task<StoredEnrollmentFileDto> StoreEnrollmentFileAsync(
        Guid schoolId,
        string lastName,
        string firstName,
        string registrationNumber,
        string academicYearLabel,
        string documentType,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("Le nom de l'élève est requis pour enregistrer un fichier.");
        }

        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            throw new DomainException("Le matricule est requis pour enregistrer un fichier dans le dossier élève.");
        }

        var saved = await _studentDossierStorage.SaveStudentFileAsync(
            new StudentDossierFileRequest(
                lastName,
                string.IsNullOrWhiteSpace(firstName) ? lastName : firstName,
                registrationNumber,
                academicYearLabel,
                documentType,
                fileName),
            content,
            cancellationToken);

        return new StoredEnrollmentFileDto(saved.StoragePath, saved.FileName, saved.FileSizeBytes);
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
            .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(s => s.Code).First())
            .OrderBy(s => s.Name)
            .Select(s => new SectionDto(s.Id, s.Code, s.Name, s.Cycle))
            .ToList();

        var canonicalSectionIdByName = sectionDtos.ToDictionary(
            s => s.Name.Trim(),
            s => s.Id,
            StringComparer.OrdinalIgnoreCase);

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

                Guid canonicalSectionId;
                string sectionName;
                if (section is not null && sectionDtos.Any(s => s.Id == c.SectionId))
                {
                    canonicalSectionId = c.SectionId;
                    sectionName = section.Name;
                }
                else if (pedagogical is not null)
                {
                    var programCode = PedagogicalSectionMapping.GetSectionCode(pedagogical.Program);
                    var programSection = sectionDtos.FirstOrDefault(s =>
                        s.Code.Equals(programCode, StringComparison.OrdinalIgnoreCase));
                    canonicalSectionId = programSection?.Id ?? c.SectionId;
                    sectionName = programSection?.Name
                        ?? pedagogical.HumanitiesSection
                        ?? section?.Name
                        ?? "—";
                }
                else if (section is not null
                    && canonicalSectionIdByName.TryGetValue(section.Name.Trim(), out var canonicalId))
                {
                    canonicalSectionId = canonicalId;
                    sectionName = section.Name;
                }
                else
                {
                    canonicalSectionId = c.SectionId;
                    sectionName = section?.Name ?? "—";
                }

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
                    pedagogical?.HumanitiesSection ?? sectionName,
                    pedagogical?.StudyOption,
                    canonicalSectionId,
                    sectionName,
                    c.PedagogicalClassId,
                    c.Level,
                    c.MaxCapacity,
                    count,
                    pedagogical?.MinAge,
                    pedagogical?.MaxAge,
                    true);
            })
            .ToList();

        var enrollmentSectionIds = classOptions.Select(c => c.SectionId).ToHashSet();
        var enrollmentSections = sectionDtos
            .Where(s => enrollmentSectionIds.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToList();

        return new EnrollmentStructureOptionsDto(
            currentYear.Id,
            currentYear.Label,
            enrollmentSections,
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
        Guid? pedagogicalClassId = null,
        Guid? academicYearId = null,
        IReadOnlyList<Guid>? selectedFeeTypeIds = null,
        IReadOnlyDictionary<Guid, decimal>? discounts = null,
        CancellationToken cancellationToken = default)
    {
        var feeTypes = await _feeTypeRepository.FindAsync(
            f => f.SchoolId == schoolId && f.IsActive,
            cancellationToken);
        if (selectedFeeTypeIds is { Count: > 0 })
        {
            var set = selectedFeeTypeIds.ToHashSet();
            feeTypes = feeTypes.Where(f => set.Contains(f.Id)).ToList();
        }

        AcademicYear? year = null;
        if (academicYearId.HasValue)
        {
            year = (await _yearRepository.FindAsync(
                y => y.SchoolId == schoolId && y.Id == academicYearId.Value,
                cancellationToken)).FirstOrDefault();
        }
        else
        {
            year = (await _yearRepository.FindAsync(
                y => y.SchoolId == schoolId && y.IsCurrent && !y.IsClosed,
                cancellationToken)).FirstOrDefault();
        }

        discounts ??= new Dictionary<Guid, decimal>();

        var lines = new List<EnrollmentFeeLineDto>();
        foreach (var feeType in feeTypes.OrderBy(f => f.Name))
        {
            var discount = discounts.GetValueOrDefault(feeType.Id);
            decimal gross = 0;
            if (year is not null && pedagogicalClassId.HasValue)
            {
                gross = await _schoolFeeService.ResolveAnnualAmountAsync(
                    schoolId,
                    year.Id,
                    pedagogicalClassId.Value,
                    feeType.Id,
                    cancellationToken);
            }

            var net = Math.Max(0, gross - discount);
            lines.Add(new EnrollmentFeeLineDto(
                feeType.Id,
                feeType.Code,
                feeType.Name,
                gross,
                discount,
                0,
                net,
                feeType.IsMandatory));
        }

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
            _yearRepository,
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

            await ApplyStudentFieldsAsync(student, request, cancellationToken);
            await _studentRepository.UpdateAsync(student, cancellationToken);
        }
        else
        {
            var registration = await GenerateRegistrationNumberAsync(schoolId, cancellationToken);
            student = await CreateStudentEntityAsync(schoolId, registration.RegistrationNumber, request, cancellationToken);
            await _studentRepository.AddAsync(student, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var linkedGuardians = await ReplaceGuardiansAsync(
            schoolId,
            student.Id,
            request.Guardians,
            request.ResidenceAddress,
            cancellationToken);

        // Ne jamais bloquer l'inscription si la création des comptes parents échoue.
        IReadOnlyList<ParentAppAccessCredentialDto> parentAccessAccounts = [];
        string parentAccessWarning = string.Empty;
        try
        {
            parentAccessAccounts = await _parentAccessProvisioning.EnsureAccessForGuardiansAsync(
                schoolId,
                linkedGuardians,
                cancellationToken);
        }
        catch (Exception ex)
        {
            parentAccessWarning =
                $" Accès parent non créé automatiquement ({ex.Message}). L'inscription élève est tout de même enregistrée.";
        }

        var enrollmentStatus = MapRegistrationKind(request.Scolarite.RegistrationKind);
        var generalCategory = await _schoolFeeService.EnsureGeneralPricingCategoryAsync(schoolId, cancellationToken);
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            AcademicYearId = academicYearId,
            ClassRoomId = request.Scolarite.ClassRoomId,
            FeePricingCategoryId = generalCategory.Id,
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

        var pedagogicalClassId = classRoom.PedagogicalClassId ?? request.Scolarite.PedagogicalClassId;
        var feeSummary = await ResolveEnrollmentFeeSummaryAsync(
            schoolId,
            academicYearId,
            pedagogicalClassId,
            request.FeeSummary,
            cancellationToken);

        if (!pedagogicalClassId.HasValue)
        {
            throw new DomainException("La classe pédagogique est obligatoire pour initialiser les frais scolaires.");
        }

        var currency = feeSummary?.Currency
            ?? (await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken))
                .FirstOrDefault()?.Currency
            ?? Currency.CDF;

        var totalDue = await _feeBalanceProvisioner.ProvisionForStudentAsync(
            schoolId,
            student.Id,
            academicYearId,
            pedagogicalClassId.Value,
            generalCategory.Id,
            currency,
            cancellationToken);

        var balanceLineCount = (await _feeBalanceRepository.FindAsync(
            b => b.StudentId == student.Id,
            cancellationToken)).Count;

        await PersistDocumentsAsync(
            student.Id,
            student,
            prerequisites.CurrentAcademicYearLabel ?? academicYearId.ToString(),
            request.Documents,
            cancellationToken);

        var auditActions = new List<string>
        {
            "Dossier élève créé/mis à jour",
            $"Matricule définitif : {student.RegistrationNumber}",
            "Dossier scolaire et inscription enregistrés",
            "Affectation classe et local confirmée",
            balanceLineCount > 0
                ? $"Dossier financier initialisé ({balanceLineCount} solde(s), dû {totalDue:N2} {currency})"
                : "Frais scolaires : aucun tarif applicable pour la classe (soldes non créés)",
            parentAccessAccounts.Count > 0
                ? $"Accès application parent : {parentAccessAccounts.Count} compte(s)"
                : "Aucun accès application parent (pas de tuteur renseigné)",
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

        var yearLabel = prerequisites.CurrentAcademicYearLabel ?? academicYearId.ToString();
        try
        {
            _studentDossierStorage.EnsureStudentFolder(
                student.LastName,
                student.FirstName,
                student.RegistrationNumber,
                yearLabel);
        }
        catch
        {
            // La fiche tentera aussi de créer le dossier ; on conserve l'erreur détaillée ci-dessous.
        }

        var ficheMessage = string.Empty;
        try
        {
            await _enrollmentFormService.SaveToStudentDossierAsync(
                schoolId,
                enrollment.Id,
                parentAccessAccounts,
                cancellationToken);
            ficheMessage = " Fiche d'inscription (PDF) enregistrée dans le dossier élève.";
        }
        catch (Exception ex)
        {
            ficheMessage = $" Fiche d'inscription (PDF) non enregistrée : {ex.Message}";
        }

        var className = classRoom.PedagogicalClassId.HasValue
            && pedagogicalMap.TryGetValue(classRoom.PedagogicalClassId.Value, out var pc)
            ? $"{pc.DisplayName} {classRoom.Name}"
            : classRoom.Name;

        var financialMessage = balanceLineCount > 0
            ? $"Inscription validée. Dossier financier initialisé (dû {totalDue:N2} {currency})."
            : "Inscription validée. Aucun tarif applicable pour cette classe — configurez les frais scolaires (catégorie GENERAL) pour cette classe.";

        var parentAccessMessage = string.IsNullOrEmpty(parentAccessWarning)
            ? BuildParentAccessMessage(parentAccessAccounts)
            : parentAccessWarning;

        try
        {
            await _notifications.NotifyStudentParentsAsync(
                schoolId,
                student.Id,
                NotificationCategory.Administration,
                NotificationEventType.EnrollmentCreated,
                "📝 Inscription confirmée",
                $"{StudentDisplayName.Format(student)} a été inscrit(e) en {className}.",
                dataJson: $"{{\"enrollmentId\":\"{enrollment.Id}\",\"studentId\":\"{student.Id}\"}}",
                deepLink: "/parent",
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Ne jamais faire échouer l'inscription si la notification échoue.
        }

        return new CompleteEnrollmentResultDto(
            student.Id,
            enrollment.Id,
            student.RegistrationNumber,
            StudentDisplayName.Format(student),
            className,
            totalDue,
            financialMessage + ficheMessage + parentAccessMessage,
            parentAccessAccounts);
    }

    private async Task<EnrollmentFeeSummaryDto?> ResolveEnrollmentFeeSummaryAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid? pedagogicalClassId,
        EnrollmentFeeSummaryDto? requestedSummary,
        CancellationToken cancellationToken)
    {
        if (requestedSummary is { Lines.Count: > 0 })
        {
            return requestedSummary;
        }

        if (!pedagogicalClassId.HasValue)
        {
            return null;
        }

        return await CalculateFeesAsync(
            schoolId,
            pedagogicalClassId,
            academicYearId,
            cancellationToken: cancellationToken);
    }

    private async Task PersistDocumentsAsync(
        Guid studentId,
        Student student,
        string academicYearLabel,
        IReadOnlyList<EnrollmentDocumentStatusDto> documents,
        CancellationToken cancellationToken)
    {
        foreach (var doc in documents.Where(d =>
                     d.Status.Equals("Complet", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(d.FileName)))
        {
            var storagePath = await EnsureDossierStoragePathAsync(
                student,
                academicYearLabel,
                doc.DocumentType,
                doc.FileName!,
                doc.StoragePath,
                cancellationToken);

            await _studentDocumentRepository.AddAsync(new StudentDocument
            {
                StudentId = studentId,
                DocumentType = doc.DocumentType,
                FileName = doc.FileName!,
                StoragePath = storagePath,
                MimeType = GuessMimeType(doc.FileName),
                FileSizeBytes = doc.FileSizeBytes
            }, cancellationToken);

            if (doc.DocumentType.Equals("Photo", StringComparison.OrdinalIgnoreCase))
            {
                student.PhotoPath = storagePath;
            }
        }
    }

    private async Task<string> EnsureDossierStoragePathAsync(
        Student student,
        string academicYearLabel,
        string documentType,
        string fileName,
        string? storagePath,
        CancellationToken cancellationToken)
    {
        if (StudentDossierPathHelper.IsServerStoragePath(storagePath))
        {
            return storagePath!;
        }

        if (string.IsNullOrWhiteSpace(storagePath) || !File.Exists(storagePath))
        {
            throw new DomainException(
                $"Le fichier « {documentType} » doit être enregistré dans le dossier partagé avant validation.");
        }

        await using var stream = File.OpenRead(storagePath);
        var saved = await _studentDossierStorage.SaveStudentFileAsync(
            new StudentDossierFileRequest(
                student.LastName,
                student.FirstName,
                student.RegistrationNumber,
                academicYearLabel,
                documentType,
                fileName),
            stream,
            cancellationToken);

        return saved.StoragePath;
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
                         && s.LastName == request.LastName
                         && s.MiddleName == request.MiddleName
                         && s.DateOfBirth == request.DateOfBirth,
                    cancellationToken);

                if (duplicate.Count > 0)
                {
                    issues.Add(new(
                        "duplicate",
                        "Un élève avec le même nom, postnom et date de naissance existe déjà.",
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
                else
                {
                    var lastClassLevel = await GetStudentLastClassLevelAsync(
                        request.ExistingStudentId.Value,
                        prerequisites.CurrentAcademicYearId.Value,
                        cancellationToken);

                    if (lastClassLevel.HasValue && classRoom.Level < lastClassLevel.Value)
                    {
                        issues.Add(new(
                            "class_level",
                            "La classe sélectionnée est inférieure à la dernière classe de l'élève.",
                            "scolarite"));
                    }
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

    private async Task<Student> CreateStudentEntityAsync(
        Guid schoolId,
        string registrationNumber,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken)
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
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            PhotoPath = request.PhotoPath,
            BloodGroup = request.Medical.BloodGroup?.Trim(),
            MedicalNotes = BuildMedicalNotes(request)
        };

        await ApplyAddressFieldsAsync(student, request, cancellationToken);
        return student;
    }

    private async Task ApplyStudentFieldsAsync(
        Student student,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        student.FirstName = request.FirstName.Trim();
        student.LastName = request.LastName.Trim();
        student.MiddleName = request.MiddleName?.Trim();
        student.Gender = request.Gender;
        student.DateOfBirth = request.DateOfBirth;
        student.PlaceOfBirth = request.PlaceOfBirth?.Trim();
        student.Nationality = request.Nationality?.Trim() ?? student.Nationality;
        student.Phone = request.Phone?.Trim();
        student.Email = request.Email?.Trim();
        student.PhotoPath = request.PhotoPath ?? student.PhotoPath;
        student.BloodGroup = request.Medical.BloodGroup?.Trim();
        student.MedicalNotes = BuildMedicalNotes(request);
        await ApplyAddressFieldsAsync(student, request, cancellationToken);
    }

    private async Task ApplyAddressFieldsAsync(
        Student student,
        CompleteEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        student.AddressId = await _addressService.UpsertAsync(
            request.ResidenceAddress,
            student.AddressId,
            cancellationToken);

        var countries = await _addressService.GetCountryNamesAsync(cancellationToken);
        var provinces = await _addressService.GetProvinceNamesAsync(cancellationToken);
        var cities = await _addressService.GetCityNamesAsync(cancellationToken);
        var communes = await _addressService.GetCommuneNamesAsync(cancellationToken);
        student.Address = AddressFormatting.ToLegacyStorage(
            request.ResidenceAddress,
            countries,
            provinces,
            cities,
            communes,
            request.Language,
            request.Religion);
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

    private async Task<IReadOnlyList<Guardian>> ReplaceGuardiansAsync(
        Guid schoolId,
        Guid studentId,
        IReadOnlyList<GuardianInputDto> guardians,
        AddressInputDto? studentAddress,
        CancellationToken cancellationToken)
    {
        var existingLinks = (await _studentGuardianRepository.FindIncludingDeletedAsync(
            sg => sg.StudentId == studentId,
            cancellationToken)).ToList();

        var schoolGuardians = (await _guardianRepository.FindAsync(g => g.SchoolId == schoolId, cancellationToken)).ToList();
        var countries = await _addressService.GetCountryNamesAsync(cancellationToken);
        var provinces = await _addressService.GetProvinceNamesAsync(cancellationToken);
        var cities = await _addressService.GetCityNamesAsync(cancellationToken);
        var communes = await _addressService.GetCommuneNamesAsync(cancellationToken);
        var linkedGuardianIds = new HashSet<Guid>();
        var linkedGuardians = new List<Guardian>();

        foreach (var input in guardians.Where(g =>
                     !string.IsNullOrWhiteSpace(g.FirstName) || !string.IsNullOrWhiteSpace(g.LastName)))
        {
            Guardian? guardian = null;
            if (input.ExistingGuardianId.HasValue)
            {
                guardian = schoolGuardians.FirstOrDefault(g => g.Id == input.ExistingGuardianId.Value);
            }

            guardian ??= FindExistingGuardian(schoolGuardians, input)
                ?? await CreateGuardianAsync(schoolId, input, studentAddress, cancellationToken);

            if (!linkedGuardianIds.Add(guardian.Id))
            {
                continue;
            }

            linkedGuardians.Add(guardian);

            if (!schoolGuardians.Any(g => g.Id == guardian.Id))
            {
                schoolGuardians.Add(guardian);
            }
            else if (!input.UsesStudentAddress)
            {
                guardian.AddressId = await _addressService.UpsertAsync(
                    input.ResidenceAddress,
                    guardian.AddressId,
                    cancellationToken);
                guardian.Address = AddressFormatting.ToLegacyStorage(
                    input.ResidenceAddress,
                    countries,
                    provinces,
                    cities,
                    communes);
                await _guardianRepository.UpdateAsync(guardian, cancellationToken);
            }

            var relationship = string.IsNullOrWhiteSpace(input.Relationship) ? "Responsable" : input.Relationship.Trim();
            var existingLink = existingLinks.FirstOrDefault(l => l.GuardianId == guardian.Id);
            if (existingLink is not null)
            {
                existingLink.Relationship = relationship;
                existingLink.IsPrimary = input.IsPrimary;
                existingLink.CanPickup = input.CanPickup;
                existingLink.UsesStudentAddress = input.UsesStudentAddress;
                existingLink.IsDeleted = false;
                existingLink.DeletedAt = null;
                existingLink.DeletedBy = null;
                await _studentGuardianRepository.UpdateAsync(existingLink, cancellationToken);
                continue;
            }

            await _studentGuardianRepository.AddAsync(new StudentGuardian
            {
                StudentId = studentId,
                GuardianId = guardian.Id,
                Relationship = relationship,
                IsPrimary = input.IsPrimary,
                CanPickup = input.CanPickup,
                UsesStudentAddress = input.UsesStudentAddress
            }, cancellationToken);
        }

        foreach (var link in existingLinks.Where(l => !l.IsDeleted && !linkedGuardianIds.Contains(l.GuardianId)))
        {
            await _studentGuardianRepository.DeleteAsync(link, cancellationToken);
        }

        return linkedGuardians;
    }

    internal static string BuildParentAccessMessage(IReadOnlyList<ParentAppAccessCredentialDto> accounts)
    {
        if (accounts.Count == 0)
        {
            return string.Empty;
        }

        var created = accounts.Count(a => a.WasCreated);
        if (created == 0)
        {
            return $" Accès application parent déjà existant ({accounts.Count} compte(s)).";
        }

        return $" Accès application parent créés ({created} nouveau(x) compte(s)).";
    }

    private async Task<Guardian> CreateGuardianAsync(
        Guid schoolId,
        GuardianInputDto input,
        AddressInputDto? studentAddress,
        CancellationToken cancellationToken)
    {
        var addressInput = input.UsesStudentAddress ? studentAddress : input.ResidenceAddress;
        var guardian = new Guardian
        {
            SchoolId = schoolId,
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            Phone = input.Phone?.Trim(),
            Email = input.Email?.Trim(),
            Gender = input.Gender,
            Profession = string.IsNullOrWhiteSpace(input.Employer)
                ? input.Profession?.Trim()
                : $"{input.Profession?.Trim()} — {input.Employer.Trim()}"
        };

        guardian.AddressId = await _addressService.UpsertAsync(addressInput, null, cancellationToken);
        var countries = await _addressService.GetCountryNamesAsync(cancellationToken);
        var provinces = await _addressService.GetProvinceNamesAsync(cancellationToken);
        var cities = await _addressService.GetCityNamesAsync(cancellationToken);
        var communes = await _addressService.GetCommuneNamesAsync(cancellationToken);
        guardian.Address = AddressFormatting.ToLegacyStorage(
            addressInput,
            countries,
            provinces,
            cities,
            communes);

        await _guardianRepository.AddAsync(guardian, cancellationToken);
        return guardian;
    }

    private static Guardian? FindExistingGuardian(IEnumerable<Guardian> guardians, GuardianInputDto input)
    {
        var candidates = guardians.AsEnumerable();

        if (input.Gender.HasValue)
        {
            candidates = candidates.Where(g => !g.Gender.HasValue || g.Gender == input.Gender);
        }

        var phoneKey = NormalizePhoneDigits(input.Phone);
        if (phoneKey.Length >= 9)
        {
            return candidates.FirstOrDefault(g => NormalizePhoneDigits(g.Phone) == phoneKey);
        }

        var lastName = input.LastName.Trim();
        var firstName = input.FirstName.Trim();
        var email = input.Email?.Trim();

        return candidates.FirstOrDefault(g =>
            g.LastName.Equals(lastName, StringComparison.OrdinalIgnoreCase)
            && g.FirstName.Equals(firstName, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(email)
                || string.Equals(g.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase)));
    }

    private static void ValidateGuardianGenders(
        IReadOnlyList<GuardianInputDto> guardians,
        List<EnrollmentValidationIssueDto> issues)
    {
        foreach (var guardian in guardians)
        {
            if (string.IsNullOrWhiteSpace(guardian.LastName) && string.IsNullOrWhiteSpace(guardian.FirstName))
            {
                continue;
            }

            if (IsFatherRelationship(guardian.Relationship))
            {
                if (guardian.Gender != Gender.Masculin)
                {
                    issues.Add(new("guardian_gender", "Le père doit être de sexe masculin.", "guardians"));
                }
            }
            else if (IsMotherRelationship(guardian.Relationship))
            {
                if (guardian.Gender != Gender.Feminin)
                {
                    issues.Add(new("guardian_gender", "La mère doit être de sexe féminin.", "guardians"));
                }
            }
            else if (RequiresExplicitGender(guardian.Relationship) && !guardian.Gender.HasValue)
            {
                issues.Add(new(
                    "guardian_gender",
                    $"Le sexe est obligatoire pour le responsable « {guardian.Relationship} ».",
                    "guardians"));
            }
        }
    }

    private static bool IsFatherRelationship(string relationship) =>
        relationship.Contains("père", StringComparison.OrdinalIgnoreCase)
        || relationship.Contains("pere", StringComparison.OrdinalIgnoreCase)
        || relationship.Contains("father", StringComparison.OrdinalIgnoreCase);

    private static bool IsMotherRelationship(string relationship) =>
        relationship.Contains("mère", StringComparison.OrdinalIgnoreCase)
        || relationship.Contains("mere", StringComparison.OrdinalIgnoreCase)
        || relationship.Contains("mother", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresExplicitGender(string relationship) =>
        !IsFatherRelationship(relationship) && !IsMotherRelationship(relationship);

    private static string NormalizePhoneDigits(string? phone) =>
        string.IsNullOrWhiteSpace(phone)
            ? string.Empty
            : new string(phone.Where(char.IsDigit).ToArray());

    private async Task<int?> GetStudentLastClassLevelAsync(
        Guid studentId,
        Guid currentAcademicYearId,
        CancellationToken cancellationToken)
    {
        var enrollments = await _enrollmentRepository.FindAsync(
            e => e.StudentId == studentId && e.IsActive,
            cancellationToken);

        var referenceEnrollment = enrollments
            .Where(e => e.AcademicYearId != currentAcademicYearId)
            .OrderByDescending(e => e.EnrollmentDate)
            .FirstOrDefault()
            ?? enrollments.OrderByDescending(e => e.EnrollmentDate).FirstOrDefault();

        if (referenceEnrollment is null)
        {
            return null;
        }

        var classRoom = (await _classRoomRepository.FindAsync(
            c => c.Id == referenceEnrollment.ClassRoomId,
            cancellationToken)).FirstOrDefault();

        return classRoom?.Level;
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
