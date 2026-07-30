namespace SchoolManagement.Application.Schools.Interfaces;

public interface ISectionConsolidationService
{
    Task<SectionConsolidationResult> ConsolidateAsync(Guid schoolId, CancellationToken cancellationToken = default);
}

public sealed record SectionConsolidationResult(int ClassRoomsRepaired, int SectionsRemoved);
