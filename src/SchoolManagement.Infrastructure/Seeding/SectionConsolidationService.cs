namespace SchoolManagement.Infrastructure.Seeding;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Schools;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Infrastructure.Persistence;

public sealed class SectionConsolidationService : ISectionConsolidationService
{
    private readonly SchoolDbContext _context;
    private readonly ILogger<SectionConsolidationService> _logger;

    public SectionConsolidationService(
        SchoolDbContext context,
        ILogger<SectionConsolidationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SectionConsolidationResult> ConsolidateAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanonicalSectionsAsync(schoolId, cancellationToken);

        var sections = await _context.Sections
            .IgnoreQueryFilters()
            .Where(s => s.SchoolId == schoolId)
            .ToListAsync(cancellationToken);

        var activeSections = sections.Where(s => !s.IsDeleted).ToList();
        var sectionById = activeSections.ToDictionary(s => s.Id);
        var sectionByCode = activeSections
            .GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var pedagogicalMap = await _context.PedagogicalClasses
            .Where(p => p.SchoolId == schoolId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var classRooms = await _context.ClassRooms
            .Where(c => c.SchoolId == schoolId)
            .ToListAsync(cancellationToken);

        var roomsRepaired = 0;
        foreach (var room in classRooms)
        {
            if (!sectionById.TryGetValue(room.SectionId, out var currentSection))
            {
                continue;
            }

            var targetCode = ResolveTargetSectionCode(room, currentSection, pedagogicalMap);
            if (!sectionByCode.TryGetValue(targetCode, out var targetSection)
                || room.SectionId == targetSection.Id)
            {
                continue;
            }

            room.SectionId = targetSection.Id;
            roomsRepaired++;
        }

        var sectionsRemoved = 0;
        foreach (var section in activeSections)
        {
            if (PedagogicalSectionCatalog.CanonicalCodes.Contains(section.Code))
            {
                continue;
            }

            var stillReferenced = classRooms.Any(r => r.SectionId == section.Id);
            if (stillReferenced)
            {
                _logger.LogWarning(
                    "Section {Code} ({SectionId}) encore référencée — suppression ignorée.",
                    section.Code,
                    section.Id);
                continue;
            }

            section.IsDeleted = true;
            sectionsRemoved++;
        }

        if (roomsRepaired > 0 || sectionsRemoved > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Sections consolidées pour {SchoolId}: {RoomsRepaired} salles réaffectées, {SectionsRemoved} sections supprimées.",
            schoolId,
            roomsRepaired,
            sectionsRemoved);

        return new SectionConsolidationResult(roomsRepaired, sectionsRemoved);
    }

    private async Task EnsureCanonicalSectionsAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var sections = await _context.Sections
            .IgnoreQueryFilters()
            .Where(s => s.SchoolId == schoolId)
            .ToListAsync(cancellationToken);

        foreach (var (code, name, cycle) in PedagogicalSectionCatalog.RequiredSections)
        {
            var existing = sections.FirstOrDefault(s =>
                s.Code.Equals(code, StringComparison.OrdinalIgnoreCase) && !s.IsDeleted);

            if (existing is null)
            {
                var deleted = sections.FirstOrDefault(s =>
                    s.Code.Equals(code, StringComparison.OrdinalIgnoreCase) && s.IsDeleted);
                if (deleted is not null)
                {
                    deleted.IsDeleted = false;
                    deleted.Name = name;
                    deleted.Cycle = cycle;
                    continue;
                }

                _context.Sections.Add(new Section
                {
                    SchoolId = schoolId,
                    Code = code,
                    Name = name,
                    Cycle = cycle
                });
                continue;
            }

            existing.Name = name;
            existing.Cycle = cycle;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string ResolveTargetSectionCode(
        ClassRoom room,
        Section currentSection,
        IReadOnlyDictionary<Guid, PedagogicalClass> pedagogicalMap)
    {
        if (room.PedagogicalClassId.HasValue
            && pedagogicalMap.TryGetValue(room.PedagogicalClassId.Value, out var pedagogical))
        {
            return PedagogicalSectionMapping.GetSectionCode(pedagogical.Program);
        }

        return PedagogicalSectionCatalog.ResolveLegacySectionCode(currentSection.Code);
    }
}
