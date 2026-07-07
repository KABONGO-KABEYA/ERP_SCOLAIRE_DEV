namespace SchoolManagement.Application.Academic.Services;

using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Academic.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class AcademicService : IAcademicService
{
    private readonly IRepository<Section> _sectionRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IRepository<Student> _studentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AcademicService(
        IRepository<Section> sectionRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<Course> courseRepository,
        IRepository<Enrollment> enrollmentRepository,
        IRepository<Student> studentRepository,
        IUnitOfWork unitOfWork)
    {
        _sectionRepository = sectionRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _yearRepository = yearRepository;
        _courseRepository = courseRepository;
        _enrollmentRepository = enrollmentRepository;
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SectionDto>> GetSectionsAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var sections = await _sectionRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        return sections
            .OrderBy(s => s.Name)
            .Select(s => new SectionDto(s.Id, s.Code, s.Name, s.Cycle))
            .ToList();
    }

    public async Task<IReadOnlyList<ClassRoomDto>> GetClassRoomsAsync(
        Guid schoolId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var classes = await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        if (academicYearId.HasValue)
        {
            classes = classes.Where(c => c.AcademicYearId == academicYearId.Value).ToList();
        }

        var pedagogicalClasses = await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        var pedagogicalMap = ClassRoomAvailability.BuildMap(pedagogicalClasses);
        classes = classes.Where(c => ClassRoomAvailability.IsSelectable(c, pedagogicalMap)).ToList();

        var sections = await _sectionRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var sectionMap = sections.ToDictionary(s => s.Id);

        return classes
            .OrderBy(c => c.Level)
            .ThenBy(c => c.Name)
            .Select(c => MapClassRoom(c, sectionMap, pedagogicalMap))
            .ToList();
    }

    public async Task<ClassRoomDto> CreateClassRoomAsync(
        Guid schoolId,
        CreateClassRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository, schoolId, request.AcademicYearId, cancellationToken);

        var existing = await _classRoomRepository.FindAsync(
            c => c.SchoolId == schoolId && c.Code == request.Code && c.AcademicYearId == request.AcademicYearId,
            cancellationToken);

        if (existing.Count > 0)
        {
            throw new DomainException($"La classe '{request.Code}' existe déjà pour cette année.");
        }

        var section = (await _sectionRepository.FindAsync(
            s => s.Id == request.SectionId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Section introuvable.");

        var classRoom = new ClassRoom
        {
            SchoolId = schoolId,
            AcademicYearId = request.AcademicYearId,
            SectionId = request.SectionId,
            Code = request.Code,
            Name = request.Name,
            Level = request.Level,
            MaxCapacity = request.MaxCapacity,
            IsActive = true
        };

        await _classRoomRepository.AddAsync(classRoom, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapClassRoom(classRoom, new Dictionary<Guid, Section> { [section.Id] = section }, new Dictionary<Guid, PedagogicalClass>());
    }

    public async Task<IReadOnlyList<CourseDto>> GetCoursesAsync(
        Guid schoolId,
        Guid? classRoomId = null,
        CancellationToken cancellationToken = default)
    {
        var courses = await _courseRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        if (classRoomId.HasValue)
        {
            courses = courses.Where(c => c.ClassRoomId == classRoomId.Value).ToList();
        }

        return courses
            .OrderBy(c => c.Name)
            .Select(c => new CourseDto(c.Id, c.Code, c.Name, c.ClassRoomId, c.Coefficient, c.MaxScore))
            .ToList();
    }

    public async Task<CourseDto> CreateCourseAsync(
        Guid schoolId,
        CreateCourseRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _courseRepository.FindAsync(
            c => c.SchoolId == schoolId && c.Code == request.Code, cancellationToken);

        if (existing.Count > 0)
        {
            throw new DomainException($"Le cours '{request.Code}' existe déjà.");
        }

        if (request.ClassRoomId.HasValue)
        {
            await EnsureSelectableClassRoomAsync(schoolId, request.ClassRoomId.Value, cancellationToken);
        }

        var course = new Course
        {
            SchoolId = schoolId,
            ClassRoomId = request.ClassRoomId,
            Code = request.Code,
            Name = request.Name,
            Coefficient = request.Coefficient,
            MaxScore = request.MaxScore
        };

        await _courseRepository.AddAsync(course, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CourseDto(course.Id, course.Code, course.Name, course.ClassRoomId, course.Coefficient, course.MaxScore);
    }

    public async Task<IReadOnlyList<EnrollmentDto>> GetEnrollmentsAsync(
        Guid schoolId,
        Guid? classRoomId = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var enrollments = await _enrollmentRepository.FindAsync(e => e.IsActive, cancellationToken);

        if (classRoomId.HasValue)
        {
            enrollments = enrollments.Where(e => e.ClassRoomId == classRoomId.Value).ToList();
        }

        if (academicYearId.HasValue)
        {
            enrollments = enrollments.Where(e => e.AcademicYearId == academicYearId.Value).ToList();
        }

        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        var students = await _studentRepository.FindAsync(s => studentIds.Contains(s.Id) && s.SchoolId == schoolId, cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);

        var classIds = enrollments.Select(e => e.ClassRoomId).Distinct().ToList();
        var classes = await _classRoomRepository.FindAsync(c => classIds.Contains(c.Id) && c.SchoolId == schoolId, cancellationToken);
        var classMap = classes.ToDictionary(c => c.Id);
        var pedagogicalMap = ClassRoomAvailability.BuildMap(
            await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken));

        return enrollments
            .Where(e => studentMap.ContainsKey(e.StudentId))
            .OrderBy(e => studentMap[e.StudentId].LastName)
            .Select(e =>
            {
                var student = studentMap[e.StudentId];
                var classRoom = classMap.GetValueOrDefault(e.ClassRoomId);
                var className = classRoom is null
                    ? "—"
                    : MapClassRoom(classRoom, new Dictionary<Guid, Section>(), pedagogicalMap).FullDisplayName;
                return new EnrollmentDto(
                    e.Id,
                    e.StudentId,
                    $"{student.LastName} {student.FirstName}",
                    student.RegistrationNumber,
                    e.ClassRoomId,
                    className,
                    e.AcademicYearId,
                    e.Status,
                    e.IsActive);
            })
            .ToList();
    }

    public async Task<EnrollmentDto> CreateEnrollmentAsync(
        Guid schoolId,
        CreateEnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var student = (await _studentRepository.FindAsync(
            s => s.Id == request.StudentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        await SchoolConfigurationGuards.EnsureActiveAcademicYearAsync(
            _yearRepository, schoolId, request.AcademicYearId, cancellationToken);

        var classRoom = await EnsureSelectableClassRoomAsync(schoolId, request.ClassRoomId, cancellationToken);

        var active = await _enrollmentRepository.FindAsync(
            e => e.StudentId == request.StudentId && e.AcademicYearId == request.AcademicYearId && e.IsActive,
            cancellationToken);

        if (active.Count > 0)
        {
            throw new DomainException("Cet élève est déjà inscrit pour cette année scolaire.");
        }

        var enrollment = new Enrollment
        {
            StudentId = request.StudentId,
            AcademicYearId = request.AcademicYearId,
            ClassRoomId = request.ClassRoomId,
            EnrollmentDate = request.EnrollmentDate,
            Status = EnrollmentStatus.Inscrit,
            IsActive = true
        };

        await _enrollmentRepository.AddAsync(enrollment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var pedagogicalMap = ClassRoomAvailability.BuildMap(
            await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken));

        return new EnrollmentDto(
            enrollment.Id,
            enrollment.StudentId,
            $"{student.LastName} {student.FirstName}",
            student.RegistrationNumber,
            enrollment.ClassRoomId,
            MapClassRoom(classRoom, new Dictionary<Guid, Section>(), pedagogicalMap).FullDisplayName,
            enrollment.AcademicYearId,
            enrollment.Status,
            enrollment.IsActive);
    }

    private async Task<ClassRoom> EnsureSelectableClassRoomAsync(
        Guid schoolId,
        Guid classRoomId,
        CancellationToken cancellationToken)
    {
        var classRoom = (await _classRoomRepository.FindAsync(
            c => c.Id == classRoomId && c.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Classe introuvable.");

        var pedagogicalMap = ClassRoomAvailability.BuildMap(
            await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken));

        if (!ClassRoomAvailability.IsSelectable(classRoom, pedagogicalMap))
        {
            throw new DomainException("Cette classe n'est pas active dans le paramétrage pédagogique.");
        }

        return classRoom;
    }

    private static ClassRoomDto MapClassRoom(
        ClassRoom classRoom,
        IReadOnlyDictionary<Guid, Section> sectionMap,
        IReadOnlyDictionary<Guid, PedagogicalClass> pedagogicalMap)
    {
        var sectionName = sectionMap.GetValueOrDefault(classRoom.SectionId)?.Name ?? "—";
        var fullName = classRoom.PedagogicalClassId.HasValue
            && pedagogicalMap.TryGetValue(classRoom.PedagogicalClassId.Value, out var pedagogicalClass)
            ? $"{pedagogicalClass.DisplayName} {classRoom.Name}"
            : classRoom.Name;

        return new ClassRoomDto(
            classRoom.Id,
            classRoom.Code,
            classRoom.Name,
            fullName,
            classRoom.AcademicYearId,
            classRoom.SectionId,
            sectionName,
            classRoom.PedagogicalClassId,
            classRoom.Level,
            classRoom.MaxCapacity,
            classRoom.Observations,
            classRoom.IsActive);
    }
}
