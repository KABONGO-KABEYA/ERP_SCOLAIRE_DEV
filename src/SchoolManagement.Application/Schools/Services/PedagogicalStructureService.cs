namespace SchoolManagement.Application.Schools.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Application.Schools.Catalog;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class PedagogicalStructureService : IPedagogicalStructureService
{
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<Section> _sectionRepository;
    private readonly IRepository<StudyOption> _studyOptionRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly ICurriculumSeedService _curriculumSeedService;
    private readonly ISectionConsolidationService _sectionConsolidationService;
    private readonly IUnitOfWork _unitOfWork;

    public PedagogicalStructureService(
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<Section> sectionRepository,
        IRepository<StudyOption> studyOptionRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<Enrollment> enrollmentRepository,
        ICurriculumSeedService curriculumSeedService,
        ISectionConsolidationService sectionConsolidationService,
        IUnitOfWork unitOfWork)
    {
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _classRoomRepository = classRoomRepository;
        _sectionRepository = sectionRepository;
        _studyOptionRepository = studyOptionRepository;
        _yearRepository = yearRepository;
        _enrollmentRepository = enrollmentRepository;
        _curriculumSeedService = curriculumSeedService;
        _sectionConsolidationService = sectionConsolidationService;
        _unitOfWork = unitOfWork;
    }

    public async Task EnsureInitializedAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        await SyncLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureSectionsAsync(schoolId, cancellationToken);
            await _sectionConsolidationService.ConsolidateAsync(schoolId, cancellationToken);

            var existing = await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
            var existingByCode = existing
                .GroupBy(p => p.TemplateCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var template in RdcPedagogicalCatalog.GetAll())
            {
                if (existingByCode.ContainsKey(template.TemplateCode))
                {
                    continue;
                }

                await _pedagogicalClassRepository.AddAsync(new PedagogicalClass
                {
                    SchoolId = schoolId,
                    TemplateCode = template.TemplateCode,
                    Program = template.Program,
                    LevelOrder = template.LevelOrder,
                    DisplayName = template.DisplayName,
                    HumanitiesSection = template.HumanitiesSection,
                    StudyOption = template.StudyOption,
                    MinAge = template.MinAge,
                    MaxAge = template.MaxAge,
                    IsEnabled = false
                }, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _curriculumSeedService.EnsureCurriculumAsync(schoolId, cancellationToken);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    public async Task<PedagogicalStructureSummaryDto> GetSummaryAsync(
        Guid schoolId,
        bool skipEnsure = false,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        if (!skipEnsure)
        {
            await EnsureInitializedAsync(schoolId, cancellationToken);
        }

        var classes = await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        var validCodes = RdcPedagogicalCatalog.GetAll()
            .Select(t => t.TemplateCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        classes = classes.Where(c => validCodes.Contains(c.TemplateCode)).ToList();
        var classIds = classes.Select(c => c.Id).ToList();
        var locals = classIds.Count == 0
            ? []
            : await _classRoomRepository.FindAsync(
                c => c.SchoolId == schoolId && c.PedagogicalClassId.HasValue && classIds.Contains(c.PedagogicalClassId.Value),
                cancellationToken);

        if (academicYearId.HasValue)
        {
            locals = locals.Where(l => l.AcademicYearId == academicYearId.Value).ToList();
        }

        return new PedagogicalStructureSummaryDto(
            classes.Count,
            classes.Count(c => c.IsEnabled),
            locals.Count);
    }

    public async Task<IReadOnlyList<PedagogicalClassDto>> GetClassesAsync(
        Guid schoolId,
        string? search = null,
        SchoolProgram? program = null,
        bool? enabledOnly = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(schoolId, cancellationToken);

        var classes = await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        var validCodes = RdcPedagogicalCatalog.GetAll()
            .Select(t => t.TemplateCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        classes = classes.Where(c => validCodes.Contains(c.TemplateCode)).ToList();
        var locals = await _classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId && c.PedagogicalClassId.HasValue,
            cancellationToken);

        if (academicYearId.HasValue)
        {
            locals = locals.Where(l => l.AcademicYearId == academicYearId.Value).ToList();
        }

        var localCounts = locals
            .GroupBy(l => l.PedagogicalClassId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        IEnumerable<PedagogicalClass> query = classes;

        if (program.HasValue)
        {
            query = query.Where(c => c.Program == program.Value);
        }

        if (enabledOnly == true)
        {
            query = query.Where(c => c.IsEnabled);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c =>
                c.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || c.TemplateCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (c.HumanitiesSection?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (c.StudyOption?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return query
            .OrderBy(c => c.Program)
            .ThenBy(c => c.HumanitiesSection)
            .ThenBy(c => c.StudyOption)
            .ThenBy(c => c.LevelOrder)
            .Select(c => MapClass(c, localCounts.GetValueOrDefault(c.Id)))
            .ToList();
    }

    public async Task<PedagogicalClassDto> UpdateClassAsync(
        Guid schoolId,
        Guid classId,
        UpdatePedagogicalClassRequest request,
        CancellationToken cancellationToken = default)
    {
        var pedagogicalClass = await GetClassOrThrowAsync(schoolId, classId, cancellationToken);

        if (!request.IsEnabled && pedagogicalClass.IsEnabled)
        {
            await EnsureNoCurrentYearEnrollmentsForClassAsync(schoolId, classId, cancellationToken);
        }

        pedagogicalClass.IsEnabled = request.IsEnabled;
        pedagogicalClass.MinAge = request.MinAge;
        pedagogicalClass.MaxAge = request.MaxAge;

        await _pedagogicalClassRepository.UpdateAsync(pedagogicalClass, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var localCount = (await _classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId && c.PedagogicalClassId == classId,
            cancellationToken)).Count;

        return MapClass(pedagogicalClass, localCount);
    }

    public async Task<IReadOnlyList<PedagogicalClassDto>> BulkUpdateClassesAsync(
        Guid schoolId,
        BulkUpdatePedagogicalClassesRequest request,
        CancellationToken cancellationToken = default)
    {
        var classes = await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        var validCodes = RdcPedagogicalCatalog.GetAll()
            .Select(t => t.TemplateCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        classes = classes.Where(c => validCodes.Contains(c.TemplateCode)).ToList();
        var map = classes.ToDictionary(c => c.Id);

        foreach (var item in request.Classes)
        {
            if (!map.TryGetValue(item.Id, out var pedagogicalClass))
            {
                continue;
            }

            if (!item.IsEnabled && pedagogicalClass.IsEnabled)
            {
                await EnsureNoCurrentYearEnrollmentsForClassAsync(schoolId, item.Id, cancellationToken);
            }

            pedagogicalClass.IsEnabled = item.IsEnabled;
            pedagogicalClass.MinAge = item.MinAge;
            pedagogicalClass.MaxAge = item.MaxAge;
            await _pedagogicalClassRepository.UpdateAsync(pedagogicalClass, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetClassesAsync(schoolId, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ClassLocalDto>> GetLocalsAsync(
        Guid schoolId,
        Guid pedagogicalClassId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var pedagogicalClass = await GetClassOrThrowAsync(schoolId, pedagogicalClassId, cancellationToken);

        var locals = await _classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId && c.PedagogicalClassId == pedagogicalClassId,
            cancellationToken);

        if (academicYearId.HasValue)
        {
            locals = locals.Where(l => l.AcademicYearId == academicYearId.Value).ToList();
        }

        return locals
            .OrderBy(l => l.Name)
            .Select(l => MapLocal(l, pedagogicalClass))
            .ToList();
    }

    public async Task<ClassLocalDto> CreateLocalAsync(
        Guid schoolId,
        CreateClassLocalRequest request,
        CancellationToken cancellationToken = default)
    {
        var pedagogicalClass = await GetClassOrThrowAsync(schoolId, request.PedagogicalClassId, cancellationToken);
        if (!pedagogicalClass.IsEnabled)
        {
            pedagogicalClass.IsEnabled = true;
            await _pedagogicalClassRepository.UpdateAsync(pedagogicalClass, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var year = (await _yearRepository.FindAsync(
            y => y.Id == request.AcademicYearId && y.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Année scolaire introuvable.");

        var localName = request.LocalName.Trim();
        if (string.IsNullOrWhiteSpace(localName))
        {
            throw new DomainException("Le nom du local est obligatoire.");
        }

        var existingLocals = await _classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId
                && c.PedagogicalClassId == request.PedagogicalClassId
                && c.AcademicYearId == request.AcademicYearId,
            cancellationToken);

        if (existingLocals.Any(l => l.Name.Equals(localName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Le local '{localName}' existe déjà pour cette classe.");
        }

        var sectionId = await ResolveSectionIdAsync(schoolId, pedagogicalClass, cancellationToken);
        var studyOptionId = await ResolveStudyOptionIdAsync(schoolId, pedagogicalClass, cancellationToken);
        var code = ClassLocalCodeBuilder.Build(pedagogicalClass, localName, existingLocals.Count);
        code = await EnsureUniqueLocalCodeAsync(schoolId, year.Id, code, cancellationToken);

        var local = new ClassRoom
        {
            SchoolId = schoolId,
            AcademicYearId = year.Id,
            PedagogicalClassId = pedagogicalClass.Id,
            SectionId = sectionId,
            StudyOptionId = studyOptionId,
            Code = code,
            Name = localName,
            Level = pedagogicalClass.LevelOrder,
            MaxCapacity = request.MaxCapacity,
            Observations = request.Observations?.Trim(),
            IsActive = true
        };

        await _classRoomRepository.AddAsync(local, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapLocal(local, pedagogicalClass);
    }

    public async Task<ClassLocalDto> UpdateLocalAsync(
        Guid schoolId,
        Guid localId,
        UpdateClassLocalRequest request,
        CancellationToken cancellationToken = default)
    {
        var local = await GetLocalOrThrowAsync(schoolId, localId, cancellationToken);
        var pedagogicalClass = await GetClassOrThrowAsync(schoolId, local.PedagogicalClassId!.Value, cancellationToken);

        var localName = request.LocalName.Trim();
        if (string.IsNullOrWhiteSpace(localName))
        {
            throw new DomainException("Le nom du local est obligatoire.");
        }

        var siblings = await _classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId
                && c.PedagogicalClassId == local.PedagogicalClassId
                && c.AcademicYearId == local.AcademicYearId
                && c.Id != localId,
            cancellationToken);

        if (siblings.Any(l => l.Name.Equals(localName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Le local '{localName}' existe déjà pour cette classe.");
        }

        if (!request.IsActive && local.IsActive)
        {
            await EnsureNoCurrentYearEnrollmentsForLocalAsync(schoolId, localId, cancellationToken);
        }

        local.Name = localName;
        local.MaxCapacity = request.MaxCapacity;
        local.Observations = request.Observations?.Trim();
        local.IsActive = request.IsActive;

        await _classRoomRepository.UpdateAsync(local, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapLocal(local, pedagogicalClass);
    }

    public async Task DeleteLocalAsync(Guid schoolId, Guid localId, CancellationToken cancellationToken = default)
    {
        var local = await GetLocalOrThrowAsync(schoolId, localId, cancellationToken);
        await EnsureNoCurrentYearEnrollmentsForLocalAsync(schoolId, localId, cancellationToken);
        await _classRoomRepository.DeleteAsync(local, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<AcademicYear> GetCurrentYearOrThrowAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        return (await _yearRepository.FindAsync(
            y => y.SchoolId == schoolId && y.IsCurrent && !y.IsClosed,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Aucune année scolaire courante ouverte.");
    }

    private async Task EnsureNoCurrentYearEnrollmentsForLocalAsync(
        Guid schoolId,
        Guid localId,
        CancellationToken cancellationToken)
    {
        var currentYear = await GetCurrentYearOrThrowAsync(schoolId, cancellationToken);
        var local = await GetLocalOrThrowAsync(schoolId, localId, cancellationToken);

        if (local.AcademicYearId != currentYear.Id)
        {
            return;
        }

        var enrollments = await _enrollmentRepository.FindAsync(
            e => e.ClassRoomId == localId
                && e.AcademicYearId == currentYear.Id
                && e.IsActive,
            cancellationToken);

        if (enrollments.Count > 0)
        {
            throw new DomainException(
                "Impossible de désactiver ou supprimer ce local : des élèves y sont inscrits pour l'année scolaire courante.");
        }
    }

    private async Task EnsureNoCurrentYearEnrollmentsForClassAsync(
        Guid schoolId,
        Guid pedagogicalClassId,
        CancellationToken cancellationToken)
    {
        var currentYear = await GetCurrentYearOrThrowAsync(schoolId, cancellationToken);
        var locals = await _classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId
                && c.PedagogicalClassId == pedagogicalClassId
                && c.AcademicYearId == currentYear.Id,
            cancellationToken);

        if (locals.Count == 0)
        {
            return;
        }

        var localIds = locals.Select(l => l.Id).ToHashSet();
        var enrollments = await _enrollmentRepository.FindAsync(
            e => e.AcademicYearId == currentYear.Id && e.IsActive,
            cancellationToken);

        if (enrollments.Any(e => localIds.Contains(e.ClassRoomId)))
        {
            throw new DomainException(
                "Impossible de désactiver cette classe : des élèves y sont inscrits pour l'année scolaire courante.");
        }
    }

    private async Task<PedagogicalClass> GetClassOrThrowAsync(Guid schoolId, Guid classId, CancellationToken cancellationToken)
    {
        return (await _pedagogicalClassRepository.FindAsync(
            p => p.Id == classId && p.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Classe pédagogique introuvable.");
    }

    private async Task<ClassRoom> GetLocalOrThrowAsync(Guid schoolId, Guid localId, CancellationToken cancellationToken)
    {
        return (await _classRoomRepository.FindAsync(
            c => c.Id == localId && c.SchoolId == schoolId && c.PedagogicalClassId.HasValue, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Local introuvable.");
    }

    private static PedagogicalClassDto MapClass(PedagogicalClass c, int localCount) =>
        new(
            c.Id,
            c.TemplateCode,
            c.Program,
            GetProgramLabel(c.Program),
            c.LevelOrder,
            c.DisplayName,
            c.HumanitiesSection,
            c.StudyOption,
            c.MinAge,
            c.MaxAge,
            c.IsEnabled,
            localCount);

    private static ClassLocalDto MapLocal(ClassRoom local, PedagogicalClass pedagogicalClass) =>
        new(
            local.Id,
            pedagogicalClass.Id,
            local.AcademicYearId,
            pedagogicalClass.DisplayName,
            local.Name,
            local.Code,
            $"{pedagogicalClass.DisplayName} {local.Name}",
            local.MaxCapacity,
            local.Observations,
            local.IsActive);

    private static string GetProgramLabel(SchoolProgram program) => program switch
    {
        SchoolProgram.Maternelle => "Maternelle",
        SchoolProgram.Primaire => "Primaire",
        SchoolProgram.CTEB => "Secondaire générale",
        SchoolProgram.Humanites => "Humanité",
        SchoolProgram.HumanitesProfessionnelles => "Humanité",
        SchoolProgram.FilieresSpecialisees => "Humanité",
        _ => program.ToString()
    };

    private async Task<string> EnsureUniqueLocalCodeAsync(
        Guid schoolId,
        Guid academicYearId,
        string baseCode,
        CancellationToken cancellationToken)
    {
        var code = baseCode;
        for (var sequence = 2; sequence <= 99; sequence++)
        {
            var duplicates = await _classRoomRepository.FindAsync(
                c => c.SchoolId == schoolId && c.AcademicYearId == academicYearId && c.Code == code,
                cancellationToken);
            if (duplicates.Count == 0)
            {
                return code;
            }

            code = ClassLocalCodeBuilder.WithSuffix(baseCode, sequence);
        }

        throw new DomainException("Impossible de générer un code local unique.");
    }

    private static string BuildLocalCode(PedagogicalClass pedagogicalClass, string localName, int existingCount) =>
        ClassLocalCodeBuilder.Build(pedagogicalClass, localName, existingCount);

    private async Task EnsureSectionsAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var sections = await _sectionRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);

        foreach (var (code, name, cycle) in PedagogicalSectionCatalog.RequiredSections)
        {
            var existing = sections.FirstOrDefault(s =>
                s.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Name = name;
                existing.Cycle = cycle;
                continue;
            }

            await _sectionRepository.AddAsync(new Section
            {
                SchoolId = schoolId,
                Code = code,
                Name = name,
                Cycle = cycle
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> ResolveSectionIdAsync(
        Guid schoolId,
        PedagogicalClass pedagogicalClass,
        CancellationToken cancellationToken)
    {
        var code = PedagogicalSectionMapping.GetSectionCode(pedagogicalClass.Program);

        var section = (await _sectionRepository.FindAsync(
            s => s.SchoolId == schoolId && s.Code == code, cancellationToken)).FirstOrDefault()
            ?? throw new DomainException($"Section '{code}' introuvable. Réinitialisez la structure pédagogique.");

        return section.Id;
    }

    private async Task<Guid?> ResolveStudyOptionIdAsync(
        Guid schoolId,
        PedagogicalClass pedagogicalClass,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pedagogicalClass.StudyOption)
            || pedagogicalClass.Program is not (
                SchoolProgram.Humanites
                or SchoolProgram.HumanitesProfessionnelles
                or SchoolProgram.FilieresSpecialisees))
        {
            return null;
        }

        var optionCode = new string(
            $"{pedagogicalClass.HumanitiesSection}-{pedagogicalClass.StudyOption}"
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .Take(20)
                .ToArray());

        var options = await _studyOptionRepository.FindAsync(o => o.SchoolId == schoolId, cancellationToken);
        var existing = options.FirstOrDefault(o => o.Code == optionCode);
        if (existing is not null)
        {
            return existing.Id;
        }

        var option = new StudyOption
        {
            SchoolId = schoolId,
            Code = optionCode,
            Name = pedagogicalClass.StudyOption,
            Cycle = EducationCycle.Secondaire,
            HumanitiesSection = pedagogicalClass.HumanitiesSection
        };

        await _studyOptionRepository.AddAsync(option, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return option.Id;
    }
}
