namespace SchoolManagement.Application.Schools;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Settings;

public static class SchoolCourseScope
{
    public static async Task<HashSet<Guid>> GetCourseIdsAsync(
        IRepository<PedagogicalClassCourse> linkRepository,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var links = await linkRepository.FindAsync(l => l.SchoolId == schoolId, cancellationToken);
        return links.Select(l => l.CourseId).ToHashSet();
    }

    public static async Task<IReadOnlyList<Course>> GetCoursesAsync(
        IRepository<Course> courseRepository,
        IRepository<PedagogicalClassCourse> linkRepository,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var courseIds = await GetCourseIdsAsync(linkRepository, schoolId, cancellationToken);
        if (courseIds.Count == 0)
        {
            return [];
        }

        return await courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken);
    }

    public static async Task<Course?> GetCourseAsync(
        IRepository<Course> courseRepository,
        IRepository<PedagogicalClassCourse> linkRepository,
        Guid schoolId,
        Guid courseId,
        CancellationToken cancellationToken = default)
    {
        var courseIds = await GetCourseIdsAsync(linkRepository, schoolId, cancellationToken);
        if (!courseIds.Contains(courseId))
        {
            return null;
        }

        return (await courseRepository.FindAsync(c => c.Id == courseId, cancellationToken)).FirstOrDefault();
    }
}
