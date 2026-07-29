namespace SchoolManagement.Application.Common.Interfaces;

public interface ICurriculumSeedService
{
    Task EnsureCurriculumAsync(Guid schoolId, CancellationToken cancellationToken = default);
}
