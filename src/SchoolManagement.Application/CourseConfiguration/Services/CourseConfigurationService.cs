namespace SchoolManagement.Application.CourseConfiguration.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.CourseConfiguration.DTOs;
using SchoolManagement.Application.CourseConfiguration.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class CourseConfigurationService : ICourseConfigurationService
{
    private const int DefaultMaximum = 20;
    private const int MaxAllowedMaximum = 1000;

    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<Branch> _branchRepository;
    private readonly IRepository<PedagogicalClassCourse> _pedagogicalClassCourseRepository;
    private readonly IRepository<CourseAssignment> _assignmentRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<Teacher> _teacherRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CourseConfigurationService(
        IRepository<Course> courseRepository,
        IRepository<Branch> branchRepository,
        IRepository<PedagogicalClassCourse> pedagogicalClassCourseRepository,
        IRepository<CourseAssignment> assignmentRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<Teacher> teacherRepository,
        IUnitOfWork unitOfWork)
    {
        _courseRepository = courseRepository;
        _branchRepository = branchRepository;
        _pedagogicalClassCourseRepository = pedagogicalClassCourseRepository;
        _assignmentRepository = assignmentRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<BranchOptionDto>> GetBranchesAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var branches = await _branchRepository.FindAsync(
            b => b.SchoolId == schoolId && b.IsActive,
            cancellationToken);

        return branches
            .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .Select(b => new BranchOptionDto(b.Id, b.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<AvailableCourseBranchGroupDto>> GetAvailableCoursesAsync(
        Guid schoolId,
        Guid pedagogicalClassId,
        CancellationToken cancellationToken = default)
    {
        _ = await EnsurePedagogicalClassAsync(schoolId, pedagogicalClassId, cancellationToken);

        var links = await _pedagogicalClassCourseRepository.FindAsync(
            l => l.SchoolId == schoolId && l.PedagogicalClassId == pedagogicalClassId,
            cancellationToken);

        if (links.Count == 0)
        {
            return [];
        }

        var linkMap = links
            .GroupBy(l => l.CourseId)
            .ToDictionary(g => g.Key, g => g.First());

        var courseIds = linkMap.Keys.ToList();
        var courses = (await _courseRepository.FindAsync(
            c => courseIds.Contains(c.Id),
            cancellationToken))
            .DistinctBy(c => c.Id)
            .GroupBy(NormalizeCourseKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var branchIds = courses.Where(c => c.BranchId.HasValue).Select(c => c.BranchId!.Value).Distinct().ToList();
        var branches = branchIds.Count == 0
            ? []
            : await _branchRepository.FindAsync(b => branchIds.Contains(b.Id), cancellationToken);
        var branchMap = branches.ToDictionary(b => b.Id);

        return courses
            .GroupBy(c => ResolveBranchName(c, branchMap), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var branchName = group.Key;
                var first = group.First();
                linkMap.TryGetValue(first.Id, out var link);

                return new AvailableCourseBranchGroupDto(
                    first.BranchId,
                    branchName,
                    group
                        .DistinctBy(c => c.Id)
                        .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(c =>
                        {
                            linkMap.TryGetValue(c.Id, out var courseLink);
                            return new AvailableCourseDto(
                                c.Id,
                                c.Code,
                                c.Name,
                                c.BranchId,
                                branchName == "Sans branche" ? null : branchName,
                                courseLink?.MaxScore ?? DefaultMaximum);
                        })
                        .ToList());
            })
            .Where(g => g.Courses.Count > 0)
            .OrderBy(g => g.BranchName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CourseConfigurationDto> GetConfigurationAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid classRoomId,
        CancellationToken cancellationToken = default)
    {
        var pedagogicalClass = await EnsureContextAsync(
            schoolId,
            academicYearId,
            pedagogicalClassId,
            classRoomId,
            cancellationToken);

        var assignments = (await _assignmentRepository.FindAsync(
            a => a.ClassRoomId == classRoomId && a.AcademicYearId == academicYearId,
            cancellationToken))
            .OrderBy(a => a.CourseId)
            .ToList();

        var isConfigured = assignments.Count > 0;
        var items = isConfigured
            ? await MapAssignmentsAsync(schoolId, pedagogicalClass.Id, assignments, cancellationToken)
            : await LoadDefaultItemsAsync(schoolId, pedagogicalClass.Id, cancellationToken);

        return new CourseConfigurationDto(
            isConfigured,
            IsPrimaryLevel(pedagogicalClass.Program),
            items);
    }

    public async Task<CourseConfigurationDto> SaveConfigurationAsync(
        Guid schoolId,
        SaveCourseConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var pedagogicalClass = await EnsureContextAsync(
            schoolId,
            request.AcademicYearId,
            request.PedagogicalClassId,
            request.ClassRoomId,
            cancellationToken);

        if (request.Items.Count == 0)
        {
            throw new DomainException("Sélectionnez au moins un cours pour enregistrer la configuration.");
        }

        if (request.Items.GroupBy(i => i.CourseId).Any(g => g.Count() > 1))
        {
            throw new DomainException("Un même cours ne peut pas être configuré plusieurs fois pour la même salle.");
        }

        foreach (var item in request.Items)
        {
            ValidateMaximum(item.Maximum);
        }

        var courseIds = request.Items.Select(i => i.CourseId).ToHashSet();
        var schoolCourseIds = await SchoolCourseScope.GetCourseIdsAsync(
            _pedagogicalClassCourseRepository,
            schoolId,
            cancellationToken);

        if (courseIds.Any(id => !schoolCourseIds.Contains(id)))
        {
            throw new DomainException("Un ou plusieurs cours sélectionnés sont invalides.");
        }

        var curriculumLinks = await _pedagogicalClassCourseRepository.FindAsync(
            l => l.SchoolId == schoolId && l.PedagogicalClassId == pedagogicalClass.Id,
            cancellationToken);
        var allowedCourseIds = curriculumLinks.Select(l => l.CourseId).ToHashSet();
        if (courseIds.Any(id => !allowedCourseIds.Contains(id)))
        {
            throw new DomainException("Un ou plusieurs cours ne font pas partie du programme par défaut de la classe.");
        }

        var linkMaxMap = curriculumLinks.ToDictionary(l => l.CourseId, l => l.MaxScore);

        var teacherIds = request.Items
            .Where(i => i.TeacherId.HasValue)
            .Select(i => i.TeacherId!.Value)
            .Distinct()
            .ToList();

        if (teacherIds.Count > 0)
        {
            var validTeachers = await _teacherRepository.FindAsync(
                t => t.SchoolId == schoolId && teacherIds.Contains(t.Id) && t.IsActive,
                cancellationToken);

            if (validTeachers.Count != teacherIds.Count)
            {
                throw new DomainException("Un ou plusieurs enseignants sélectionnés sont invalides.");
            }
        }

        var existingList = (await _assignmentRepository.FindIncludingDeletedAsync(
            a => a.ClassRoomId == request.ClassRoomId && a.AcademicYearId == request.AcademicYearId,
            cancellationToken)).ToList();

        var incomingCourseIds = request.Items.Select(i => i.CourseId).ToHashSet();

        foreach (var removed in existingList.Where(a => !a.IsDeleted && !incomingCourseIds.Contains(a.CourseId)))
        {
            removed.IsDeleted = true;
            removed.DeletedAt = DateTime.UtcNow;
            await _assignmentRepository.UpdateAsync(removed, cancellationToken);
        }

        foreach (var item in request.Items)
        {
            var defaultMax = linkMaxMap.TryGetValue(item.CourseId, out var linkMax)
                ? linkMax
                : DefaultMaximum;

            var assignment = existingList.FirstOrDefault(a => a.CourseId == item.CourseId);
            if (assignment is null)
            {
                assignment = new CourseAssignment
                {
                    ClassRoomId = request.ClassRoomId,
                    AcademicYearId = request.AcademicYearId,
                    PedagogicalClassId = pedagogicalClass.Id,
                    CourseId = item.CourseId,
                    TeacherId = item.TeacherId,
                    IsActive = item.IsActive,
                    MaxScore = item.Maximum > 0 ? item.Maximum : defaultMax
                };
                await _assignmentRepository.AddAsync(assignment, cancellationToken);
                existingList.Add(assignment);
            }
            else
            {
                assignment.PedagogicalClassId = pedagogicalClass.Id;
                assignment.TeacherId = item.TeacherId;
                assignment.IsActive = item.IsActive;
                assignment.MaxScore = item.Maximum;
                assignment.IsDeleted = false;
                assignment.DeletedAt = null;
                assignment.DeletedBy = null;
                await _assignmentRepository.UpdateAsync(assignment, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetConfigurationAsync(
            schoolId,
            request.AcademicYearId,
            request.PedagogicalClassId,
            request.ClassRoomId,
            cancellationToken);
    }

    public async Task<CreateCatalogCourseResultDto> CreateCatalogCourseAsync(
        Guid schoolId,
        CreateCatalogCourseRequest request,
        CancellationToken cancellationToken = default)
    {
        var pedagogicalClass = await EnsurePedagogicalClassAsync(schoolId, request.PedagogicalClassId, cancellationToken);
        ValidateMaximum(request.MaxScore);

        var courseName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(courseName))
        {
            throw new DomainException("Le nom du cours est obligatoire.");
        }

        Branch? branch = null;
        if (request.BranchId.HasValue)
        {
            branch = (await _branchRepository.FindAsync(
                b => b.Id == request.BranchId.Value && b.SchoolId == schoolId && b.IsActive,
                cancellationToken)).FirstOrDefault()
                ?? throw new DomainException("Branche invalide.");
        }

        var code = await BuildUniqueCourseCodeAsync(courseName, cancellationToken);
        var course = new Course
        {
            BranchId = branch?.Id,
            Code = code,
            Name = courseName,
            Coefficient = 1,
            MaxScore = request.MaxScore
        };

        await _courseRepository.AddAsync(course, cancellationToken);

        var existingLink = await _pedagogicalClassCourseRepository.FindAsync(
            l => l.SchoolId == schoolId
                && l.PedagogicalClassId == pedagogicalClass.Id
                && l.CourseId == course.Id,
            cancellationToken);

        if (existingLink.Count == 0)
        {
            await _pedagogicalClassCourseRepository.AddAsync(new PedagogicalClassCourse
            {
                SchoolId = schoolId,
                PedagogicalClassId = pedagogicalClass.Id,
                CourseId = course.Id,
                MaxScore = request.MaxScore
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateCatalogCourseResultDto(
            course.Id,
            course.Code,
            course.Name,
            branch?.Id,
            branch?.Name,
            request.MaxScore);
    }

    private async Task<PedagogicalClass> EnsurePedagogicalClassAsync(
        Guid schoolId,
        Guid pedagogicalClassId,
        CancellationToken cancellationToken)
    {
        var pedagogicalClass = (await _pedagogicalClassRepository.FindAsync(
            p => p.Id == pedagogicalClassId && p.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Classe pédagogique introuvable.");

        if (!pedagogicalClass.IsEnabled)
        {
            throw new DomainException("La classe pédagogique sélectionnée n'est pas activée.");
        }

        return pedagogicalClass;
    }

    private async Task<PedagogicalClass> EnsureContextAsync(
        Guid schoolId,
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid classRoomId,
        CancellationToken cancellationToken)
    {
        var classRoom = (await _classRoomRepository.FindAsync(
            c => c.Id == classRoomId && c.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Salle de classe introuvable.");

        if (classRoom.AcademicYearId != academicYearId)
        {
            throw new DomainException("La salle sélectionnée n'appartient pas à l'année académique choisie.");
        }

        if (classRoom.PedagogicalClassId != pedagogicalClassId)
        {
            throw new DomainException("La salle sélectionnée n'appartient pas à la classe pédagogique choisie.");
        }

        var pedagogicalClass = await EnsurePedagogicalClassAsync(schoolId, pedagogicalClassId, cancellationToken);

        if (!ClassRoomAvailability.IsSelectable(
                classRoom,
                new Dictionary<Guid, PedagogicalClass> { [pedagogicalClass.Id] = pedagogicalClass }))
        {
            throw new DomainException("La salle sélectionnée n'est pas disponible.");
        }

        return pedagogicalClass;
    }

    private async Task<IReadOnlyList<CourseConfigurationItemDto>> LoadDefaultItemsAsync(
        Guid schoolId,
        Guid pedagogicalClassId,
        CancellationToken cancellationToken)
    {
        var links = await _pedagogicalClassCourseRepository.FindAsync(
            l => l.SchoolId == schoolId && l.PedagogicalClassId == pedagogicalClassId,
            cancellationToken);

        if (links.Count == 0)
        {
            return [];
        }

        var courseIds = links.Select(l => l.CourseId).Distinct().ToList();
        var courses = await _courseRepository.FindAsync(c => courseIds.Contains(c.Id), cancellationToken);
        var courseMap = courses.ToDictionary(c => c.Id);
        var branchMap = await BuildBranchMapAsync(courses, cancellationToken);

        return links
            .Where(l => courseMap.ContainsKey(l.CourseId))
            .DistinctBy(l => l.CourseId)
            .Select(l =>
            {
                var course = courseMap[l.CourseId];
                return new CourseConfigurationItemDto(
                    null,
                    course.Id,
                    course.Code,
                    course.Name,
                    course.BranchId,
                    course.BranchId.HasValue && branchMap.TryGetValue(course.BranchId.Value, out var branch)
                        ? branch.Name
                        : null,
                    null,
                    null,
                    true,
                    l.MaxScore);
            })
            .OrderBy(i => i.CourseName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<CourseConfigurationItemDto>> MapAssignmentsAsync(
        Guid schoolId,
        Guid pedagogicalClassId,
        IReadOnlyList<CourseAssignment> assignments,
        CancellationToken cancellationToken)
    {
        var courseIds = assignments.Select(a => a.CourseId).Distinct().ToList();
        var teacherIds = assignments.Where(a => a.TeacherId.HasValue).Select(a => a.TeacherId!.Value).Distinct().ToList();

        var schoolCourseIds = await SchoolCourseScope.GetCourseIdsAsync(
            _pedagogicalClassCourseRepository,
            schoolId,
            cancellationToken);

        var courses = await _courseRepository.FindAsync(
            c => courseIds.Contains(c.Id) && schoolCourseIds.Contains(c.Id),
            cancellationToken);
        var courseMap = courses.ToDictionary(c => c.Id);
        var branchMap = await BuildBranchMapAsync(courses, cancellationToken);

        var links = await _pedagogicalClassCourseRepository.FindAsync(
            l => l.SchoolId == schoolId && l.PedagogicalClassId == pedagogicalClassId,
            cancellationToken);
        var linkMaxMap = links.ToDictionary(l => l.CourseId, l => l.MaxScore);

        var teachers = teacherIds.Count == 0
            ? []
            : await _teacherRepository.FindAsync(t => teacherIds.Contains(t.Id), cancellationToken);
        var teacherMap = teachers.ToDictionary(t => t.Id);

        return assignments
            .Where(a => courseMap.ContainsKey(a.CourseId))
            .GroupBy(a => a.CourseId)
            .Select(group =>
            {
                var a = group.First();
                var course = courseMap[a.CourseId];
                teacherMap.TryGetValue(a.TeacherId ?? Guid.Empty, out var teacher);
                var defaultMax = linkMaxMap.TryGetValue(course.Id, out var linkMax)
                    ? linkMax
                    : DefaultMaximum;

                return new CourseConfigurationItemDto(
                    a.Id,
                    course.Id,
                    course.Code,
                    course.Name,
                    course.BranchId,
                    course.BranchId.HasValue && branchMap.TryGetValue(course.BranchId.Value, out var branch)
                        ? branch.Name
                        : null,
                    a.TeacherId,
                    teacher is null ? null : $"{teacher.LastName} {teacher.FirstName}".Trim(),
                    a.IsActive,
                    a.MaxScore > 0 ? a.MaxScore : defaultMax);
            })
            .OrderBy(i => i.CourseName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<string> BuildUniqueCourseCodeAsync(
        string courseName,
        CancellationToken cancellationToken)
    {
        var baseCode = BuildCourseCode(courseName);
        var code = baseCode;
        var suffix = 1;

        while (true)
        {
            var existing = await _courseRepository.FindAsync(
                c => c.Code == code,
                cancellationToken);

            if (existing.Count == 0)
            {
                return code;
            }

            suffix++;
            code = $"{baseCode}-{suffix}";
        }
    }

    private static string BuildCourseCode(string courseName)
    {
        var chars = courseName.Trim().ToUpperInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
            .Take(80)
            .ToArray();

        var code = new string(chars);
        return string.IsNullOrWhiteSpace(code) ? "COURS" : code;
    }

    private static void ValidateMaximum(int maximum)
    {
        if (maximum <= 0)
        {
            throw new DomainException("Le maximum par période doit être supérieur à 0.");
        }

        if (maximum > MaxAllowedMaximum)
        {
            throw new DomainException($"Le maximum par période ne peut pas dépasser {MaxAllowedMaximum}.");
        }
    }

    private static string ResolveBranchName(Course course, IReadOnlyDictionary<Guid, Branch> branchMap) =>
        course.BranchId.HasValue && branchMap.TryGetValue(course.BranchId.Value, out var branch)
            ? branch.Name
            : "Sans branche";

    private static string NormalizeCourseKey(Course course)
    {
        if (!string.IsNullOrWhiteSpace(course.Code))
        {
            return course.Code.Trim();
        }

        return string.IsNullOrWhiteSpace(course.Name)
            ? course.Id.ToString()
            : course.Name.Trim();
    }

    private async Task<IReadOnlyDictionary<Guid, Branch>> BuildBranchMapAsync(
        IReadOnlyList<Course> courses,
        CancellationToken cancellationToken)
    {
        var branchIds = courses.Where(c => c.BranchId.HasValue).Select(c => c.BranchId!.Value).Distinct().ToList();
        if (branchIds.Count == 0)
        {
            return new Dictionary<Guid, Branch>();
        }

        var branches = await _branchRepository.FindAsync(b => branchIds.Contains(b.Id), cancellationToken);
        return branches.ToDictionary(b => b.Id);
    }

    private static bool IsPrimaryLevel(SchoolProgram program) =>
        program is SchoolProgram.Maternelle or SchoolProgram.Primaire;
}
