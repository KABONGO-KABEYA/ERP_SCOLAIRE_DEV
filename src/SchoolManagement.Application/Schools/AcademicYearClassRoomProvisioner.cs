namespace SchoolManagement.Application.Schools;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Settings;

public static class AcademicYearClassRoomProvisioner
{
    public static async Task ProvisionForYearAsync(
        Guid schoolId,
        Guid targetYearId,
        Guid? sourceYearId,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<Section> sectionRepository,
        IRepository<AcademicYear> yearRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        var pedagogicalClasses = await pedagogicalClassRepository.FindAsync(
            p => p.SchoolId == schoolId,
            cancellationToken);
        var pedagogicalMap = pedagogicalClasses.ToDictionary(p => p.Id);
        var sections = await sectionRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var enabledIds = pedagogicalClasses
            .Where(p => p.IsEnabled)
            .Select(p => p.Id)
            .ToHashSet();

        if (enabledIds.Count == 0)
        {
            return;
        }

        var years = await yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);
        var targetYear = years.FirstOrDefault(y => y.Id == targetYearId)
            ?? throw new KeyNotFoundException("Année scolaire cible introuvable.");

        if (sourceYearId is null)
        {
            sourceYearId = years
                .Where(y => y.Id != targetYearId && y.StartDate < targetYear.StartDate)
                .OrderByDescending(y => y.StartDate)
                .FirstOrDefault()
                ?.Id;
        }

        if (sourceYearId is null || sourceYearId == targetYearId)
        {
            return;
        }

        var sourceLocals = (await classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId
                && c.AcademicYearId == sourceYearId
                && c.PedagogicalClassId.HasValue,
            cancellationToken))
            .Where(c => c.IsActive && enabledIds.Contains(c.PedagogicalClassId!.Value))
            .ToList();

        if (sourceLocals.Count == 0)
        {
            return;
        }

        var targetLocals = (await classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId
                && c.AcademicYearId == targetYearId
                && c.PedagogicalClassId.HasValue,
            cancellationToken)).ToList();

        var existingKeys = targetLocals
            .Select(l => (l.PedagogicalClassId!.Value, l.Name.Trim().ToUpperInvariant()))
            .ToHashSet();

        foreach (var source in sourceLocals)
        {
            var key = (source.PedagogicalClassId!.Value, source.Name.Trim().ToUpperInvariant());
            if (existingKeys.Contains(key))
            {
                continue;
            }

            var siblings = targetLocals
                .Where(l => l.PedagogicalClassId == source.PedagogicalClassId)
                .ToList();

            var sectionId = source.SectionId;
            if (pedagogicalMap.TryGetValue(source.PedagogicalClassId!.Value, out var pedagogical))
            {
                var sectionCode = PedagogicalSectionMapping.GetSectionCode(pedagogical.Program);
                sectionId = sections.FirstOrDefault(s => s.Code == sectionCode)?.Id ?? source.SectionId;
            }

            var local = new ClassRoom
            {
                SchoolId = schoolId,
                AcademicYearId = targetYearId,
                PedagogicalClassId = source.PedagogicalClassId,
                SectionId = sectionId,
                StudyOptionId = source.StudyOptionId,
                Code = ClassLocalCodeBuilder.BuildFromSourceCode(source.Code, source.Name, siblings.Count),
                Name = source.Name,
                Level = source.Level,
                MaxCapacity = source.MaxCapacity,
                Observations = source.Observations,
                IsActive = true
            };

            await classRoomRepository.AddAsync(local, cancellationToken);
            targetLocals.Add(local);
            existingKeys.Add(key);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string BuildLocalCode(string sourceCode, string localName, int existingCount) =>
        ClassLocalCodeBuilder.BuildFromSourceCode(sourceCode, localName, existingCount);
}
