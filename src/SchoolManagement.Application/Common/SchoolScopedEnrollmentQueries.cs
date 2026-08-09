namespace SchoolManagement.Application.Common;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Students;

/// <summary>
/// Requêtes métier scoping école pour entités sans SchoolId direct.
/// </summary>
public static class SchoolScopedEnrollmentQueries
{
    public static async Task<IReadOnlyList<Enrollment>> GetActiveForStudentsAsync(
        IRepository<Enrollment> enrollmentRepository,
        IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken = default)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        var idSet = studentIds as HashSet<Guid> ?? studentIds.ToHashSet();
        return await enrollmentRepository.FindAsync(
            e => e.IsActive && idSet.Contains(e.StudentId),
            cancellationToken);
    }

    public static async Task<IReadOnlyList<StudentGuardian>> GetLinksForStudentsAsync(
        IRepository<StudentGuardian> linkRepository,
        IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken = default)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        var idSet = studentIds as HashSet<Guid> ?? studentIds.ToHashSet();
        return await linkRepository.FindAsync(l => idSet.Contains(l.StudentId), cancellationToken);
    }
}
