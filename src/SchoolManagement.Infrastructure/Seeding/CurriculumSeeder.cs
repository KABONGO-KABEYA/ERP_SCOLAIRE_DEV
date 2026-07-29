namespace SchoolManagement.Infrastructure.Seeding;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Schools.Catalog;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Infrastructure.Persistence;

public sealed class CurriculumSeeder : ICurriculumSeedService
{
    private readonly SchoolDbContext _context;
    private readonly ILogger<CurriculumSeeder> _logger;

    public CurriculumSeeder(SchoolDbContext context, ILogger<CurriculumSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsureCurriculumAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var branchMap = await EnsureBranchesAsync(schoolId, cancellationToken);
        var courseMap = await EnsureMasterCoursesAsync(schoolId, branchMap, cancellationToken);
        await EnsurePedagogicalClassLinksAsync(schoolId, courseMap, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, Branch>> EnsureBranchesAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        var existing = await _context.Branches
            .Where(b => b.SchoolId == schoolId)
            .ToListAsync(cancellationToken);
        var map = existing.ToDictionary(b => b.Code, StringComparer.OrdinalIgnoreCase);
        var created = 0;

        foreach (var definition in RdcCurriculumCatalog.GetBranches())
        {
            if (map.TryGetValue(definition.Code, out var existingBranch))
            {
                existingBranch.Name = definition.Name;
                existingBranch.Program = definition.Program;
                existingBranch.IsActive = true;
                continue;
            }

            var branch = new Branch
            {
                SchoolId = schoolId,
                Code = definition.Code,
                Name = definition.Name,
                Program = definition.Program,
                IsActive = true
            };

            _context.Branches.Add(branch);
            map[definition.Code] = branch;
            created++;
        }

        if (created > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("{Count} branche(s) curriculum créée(s) pour l'école {SchoolId}.", created, schoolId);
        }
        else
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return map;
    }

    private async Task<IReadOnlyDictionary<string, Course>> EnsureMasterCoursesAsync(
        Guid schoolId,
        IReadOnlyDictionary<string, Branch> branchMap,
        CancellationToken cancellationToken)
    {
        var existing = await _context.Courses
            .Where(c => !c.IsDeleted && c.SchoolId == schoolId)
            .ToListAsync(cancellationToken);
        var map = existing.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var created = 0;

        foreach (var definition in RdcCurriculumCatalog.GetAllDistinctCourses())
        {
            Branch? branch = null;
            if (!string.IsNullOrWhiteSpace(definition.BranchCode))
            {
                branchMap.TryGetValue(definition.BranchCode, out branch);
            }

            if (map.TryGetValue(definition.Code, out var existingCourse))
            {
                ApplyMasterCourseDefinition(existingCourse, definition, branch, schoolId);
                continue;
            }

            existingCourse = existing.FirstOrDefault(c =>
                string.Equals(c.Code, definition.Code, StringComparison.OrdinalIgnoreCase)
                || (string.Equals(c.Name, definition.Name, StringComparison.OrdinalIgnoreCase)
                    && c.BranchId == branch?.Id));

            if (existingCourse is not null)
            {
                if (!string.Equals(existingCourse.Code, definition.Code, StringComparison.OrdinalIgnoreCase))
                {
                    map.Remove(existingCourse.Code);
                    existingCourse.Code = definition.Code;
                    map[definition.Code] = existingCourse;
                }

                ApplyMasterCourseDefinition(existingCourse, definition, branch, schoolId);
                continue;
            }

            var course = new Course
            {
                SchoolId = schoolId,
                BranchId = branch?.Id,
                Code = definition.Code,
                Name = definition.Name,
                Coefficient = definition.Coefficient,
                MaxScore = definition.MaxScore
            };

            _context.Courses.Add(course);
            map[definition.Code] = course;
            created++;
        }

        if (created > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("{Count} cours curriculum créé(s) pour l'école {SchoolId}.", created, schoolId);
        }
        else
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return map;
    }

    private static void ApplyMasterCourseDefinition(
        Course course,
        CurriculumCourseDefinition definition,
        Branch? branch,
        Guid schoolId)
    {
        course.SchoolId = schoolId;
        course.Name = definition.Name;
        course.BranchId = branch?.Id;
        course.Coefficient = definition.Coefficient;
        course.MaxScore = definition.MaxScore;
    }

    private async Task EnsurePedagogicalClassLinksAsync(
        Guid schoolId,
        IReadOnlyDictionary<string, Course> courseMap,
        CancellationToken cancellationToken)
    {
        var pedagogicalClasses = await _context.PedagogicalClasses
            .Where(p => p.SchoolId == schoolId)
            .ToListAsync(cancellationToken);

        var existingLinks = await _context.PedagogicalClassCourses
            .Where(l => l.SchoolId == schoolId)
            .Select(l => new { l.PedagogicalClassId, l.CourseId })
            .ToListAsync(cancellationToken);
        var linkSet = existingLinks
            .Select(l => (l.PedagogicalClassId, l.CourseId))
            .ToHashSet();

        var created = 0;

        foreach (var pedagogicalClass in pedagogicalClasses)
        {
            var courseDefinitions = RdcCurriculumCatalog.GetCoursesForTemplate(pedagogicalClass.TemplateCode);
            foreach (var definition in courseDefinitions)
            {
                if (!courseMap.TryGetValue(definition.Code, out var course))
                {
                    continue;
                }

                var key = (pedagogicalClass.Id, course.Id);
                if (linkSet.Contains(key))
                {
                    continue;
                }

                _context.PedagogicalClassCourses.Add(new PedagogicalClassCourse
                {
                    SchoolId = schoolId,
                    PedagogicalClassId = pedagogicalClass.Id,
                    CourseId = course.Id,
                    MaxScore = definition.MaxScore
                });
                linkSet.Add(key);
                created++;
            }
        }

        if (created > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "{Count} liaison(s) pedagogical_class_course créée(s) pour l'école {SchoolId}.",
                created,
                schoolId);
        }
    }
}
