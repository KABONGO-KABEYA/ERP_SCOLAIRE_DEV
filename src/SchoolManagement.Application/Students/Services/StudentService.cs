namespace SchoolManagement.Application.Students.Services;



using Mapster;

using SchoolManagement.Application.Common.Interfaces;

using SchoolManagement.Application.Students;

using SchoolManagement.Application.Students.DTOs;

using SchoolManagement.Application.Students.Interfaces;

using SchoolManagement.Domain.Entities.Settings;

using SchoolManagement.Domain.Entities.Students;

using SchoolManagement.Domain.Enums;

using SchoolManagement.Domain.Exceptions;



public sealed class StudentService : IStudentService

{

    private readonly IRepository<Student> _studentRepository;

    private readonly IRepository<Enrollment> _enrollmentRepository;

    private readonly IRepository<StudentStatusHistory> _statusHistoryRepository;

    private readonly IRepository<ClassRoom> _classRoomRepository;

    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;

    private readonly IRepository<AcademicYear> _yearRepository;

    private readonly IRepository<Section> _sectionRepository;

    private readonly IUnitOfWork _unitOfWork;

    private readonly IStudentDossierStorageService _studentDossierStorage;



    public StudentService(

        IRepository<Student> studentRepository,

        IRepository<Enrollment> enrollmentRepository,

        IRepository<StudentStatusHistory> statusHistoryRepository,

        IRepository<ClassRoom> classRoomRepository,

        IRepository<PedagogicalClass> pedagogicalClassRepository,

        IRepository<AcademicYear> yearRepository,

        IRepository<Section> sectionRepository,

        IUnitOfWork unitOfWork,

        IStudentDossierStorageService studentDossierStorage)

    {

        _studentRepository = studentRepository;

        _enrollmentRepository = enrollmentRepository;

        _statusHistoryRepository = statusHistoryRepository;

        _classRoomRepository = classRoomRepository;

        _pedagogicalClassRepository = pedagogicalClassRepository;

        _yearRepository = yearRepository;

        _sectionRepository = sectionRepository;

        _unitOfWork = unitOfWork;

        _studentDossierStorage = studentDossierStorage;

    }



    public async Task<StudentDto?> GetByIdAsync(Guid schoolId, Guid studentId, CancellationToken cancellationToken = default)

    {

        var student = (await _studentRepository.FindAsync(

            s => s.Id == studentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault();

        return student is null ? null : await MapStudentAsync(schoolId, student, academicYearId: null, cancellationToken);

    }



    public async Task<StudentProfileDto?> GetProfileAsync(

        Guid schoolId,

        Guid studentId,

        CancellationToken cancellationToken = default)

    {

        var student = (await _studentRepository.FindAsync(

            s => s.Id == studentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()

            ?? throw new KeyNotFoundException("Élève introuvable.");



        var years = await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);

        var yearMap = years.ToDictionary(y => y.Id);

        var enrollments = (await _enrollmentRepository.FindAsync(e => e.StudentId == studentId, cancellationToken))

            .OrderByDescending(e => e.EnrollmentDate)

            .ToList();



        var classRooms = await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);

        var classRoomMap = classRooms.ToDictionary(c => c.Id);

        var pedagogicalMap = (await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken))

            .ToDictionary(p => p.Id);

        var sections = await _sectionRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);

        var sectionMap = sections.ToDictionary(s => s.Id);



        var history = enrollments.Select(e =>

        {

            classRoomMap.TryGetValue(e.ClassRoomId, out var room);

            PedagogicalClass? pedagogical = null;

            if (room?.PedagogicalClassId is Guid pcId)

            {

                pedagogicalMap.TryGetValue(pcId, out pedagogical);

            }



            Section? section = null;

            if (room is not null)

            {

                sectionMap.TryGetValue(room.SectionId, out section);

            }



            yearMap.TryGetValue(e.AcademicYearId, out var year);

            var displayName = pedagogical is not null && room is not null

                ? $"{pedagogical.DisplayName} {room.Name}"

                : room?.Name ?? "—";



            return new StudentEnrollmentHistoryDto(

                e.Id,

                e.AcademicYearId,

                year?.Label ?? "—",

                year?.IsClosed ?? false,

                year?.IsCurrent ?? false,

                displayName,

                section?.Name ?? pedagogical?.HumanitiesSection,

                pedagogical?.StudyOption,

                room?.Name ?? "—",

                e.EnrollmentDate,

                e.Status,

                e.IsActive);

        }).ToList();



        var dto = await MapStudentAsync(schoolId, student, academicYearId: null, cancellationToken);

        return new StudentProfileDto(dto, history);

    }



    public async Task<StudentListDto> SearchAsync(Guid schoolId, StudentSearchRequest request, CancellationToken cancellationToken = default)

    {

        var hasSearch = !string.IsNullOrWhiteSpace(request.Search);

        var hasFilters = request.ApplyFilters && HasAnyFilter(request);

        var hasScope = HasEnrollmentScope(request);



        if (!request.IncludeAll && !hasSearch && !hasFilters && !hasScope)

        {

            return EmptyResult(request);

        }



        if (!request.IncludeInscrits && !request.IncludeExcluded && !request.IncludeAbandoned)

        {

            return EmptyResult(request);

        }



        var all = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);

        var query = all.AsEnumerable();



        if (hasSearch)

        {

            var term = request.Search!.Trim().ToLowerInvariant();

            query = query.Where(s =>

                s.FirstName.ToLower().Contains(term) ||

                s.LastName.ToLower().Contains(term) ||

                s.RegistrationNumber.ToLower().Contains(term));

        }



        var academicYearId = await ResolveAcademicYearIdAsync(schoolId, request.AcademicYearId, cancellationToken);

        if (academicYearId.HasValue)

        {

            var scopedIds = await ResolveStudentIdsByEnrollmentScopeAsync(

                schoolId,

                academicYearId.Value,

                request,

                cancellationToken);

            query = query.Where(s => scopedIds.Contains(s.Id));

        }



        if (hasFilters)

        {

            var studentIds = await ResolveFilteredStudentIdsAsync(schoolId, request, cancellationToken);

            query = query.Where(s => studentIds.Contains(s.Id));

        }



        var ordered = query

            .OrderBy(s => s.LastName)

            .ThenBy(s => s.FirstName)

            .ToList();



        var pageStudents = ordered

            .Skip((request.Page - 1) * request.PageSize)

            .Take(request.PageSize)

            .ToList();



        var items = new List<StudentDto>();

        foreach (var student in pageStudents)

        {

            items.Add(await MapStudentAsync(schoolId, student, request.AcademicYearId, cancellationToken));

        }



        return new StudentListDto

        {

            Items = items,

            Page = request.Page,

            PageSize = request.PageSize,

            TotalCount = ordered.Count

        };

    }



    public async Task<StudentDto> CreateAsync(Guid schoolId, CreateStudentRequest request, CancellationToken cancellationToken = default)

    {

        var existing = await _studentRepository.FindAsync(

            s => s.SchoolId == schoolId && s.RegistrationNumber == request.RegistrationNumber,

            cancellationToken);



        if (existing.Count > 0)

        {

            throw new DomainException($"Le matricule '{request.RegistrationNumber}' existe déjà.");

        }



        var student = request.Adapt<Student>();

        student.SchoolId = schoolId;

        await _studentRepository.AddAsync(student, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapStudentAsync(schoolId, student, academicYearId: null, cancellationToken);

    }



    public async Task<StudentDto> UpdateAsync(Guid schoolId, Guid studentId, UpdateStudentRequest request, CancellationToken cancellationToken = default)

    {

        var student = (await _studentRepository.FindAsync(

            s => s.Id == studentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()

            ?? throw new KeyNotFoundException("Élève introuvable.");



        request.Adapt(student);

        await _studentRepository.UpdateAsync(student, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapStudentAsync(schoolId, student, academicYearId: null, cancellationToken);

    }



    public async Task ArchiveAsync(Guid schoolId, Guid studentId, CancellationToken cancellationToken = default)

    {

        var student = (await _studentRepository.FindAsync(

            s => s.Id == studentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()

            ?? throw new KeyNotFoundException("Élève introuvable.");



        student.IsArchived = true;

        await _studentRepository.UpdateAsync(student, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

    }



    public async Task WithdrawFromCurrentYearAsync(

        Guid schoolId,

        Guid studentId,

        WithdrawFromCurrentYearRequest request,

        CancellationToken cancellationToken = default)

    {

        var currentYear = (await _yearRepository.FindAsync(

            y => y.SchoolId == schoolId && y.IsCurrent && !y.IsClosed,

            cancellationToken)).FirstOrDefault()

            ?? throw new DomainException("Aucune année scolaire courante ouverte.");



        var enrollment = (await _enrollmentRepository.FindAsync(

            e => e.StudentId == studentId

                && e.AcademicYearId == currentYear.Id

                && e.IsActive,

            cancellationToken)).FirstOrDefault()

            ?? throw new DomainException("Cet élève n'est pas inscrit pour l'année scolaire courante.");



        var reasonLabel = StudentWithdrawalReasons.ResolveLabel(

            request.WithdrawalType,

            request.ReasonCode,

            request.CustomReason);



        var newStatus = request.WithdrawalType == StudentWithdrawalType.Exclusion

            ? EnrollmentStatus.Exclusion

            : EnrollmentStatus.Abandon;



        var previousStatus = enrollment.Status;

        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);



        enrollment.IsActive = false;

        enrollment.Status = newStatus;

        enrollment.EndDate = endDate;

        enrollment.Notes = $"Raison: {reasonLabel}";



        await _enrollmentRepository.UpdateAsync(enrollment, cancellationToken);



        await _statusHistoryRepository.AddAsync(new StudentStatusHistory

        {

            StudentId = studentId,

            AcademicYearId = currentYear.Id,

            PreviousStatus = previousStatus,

            NewStatus = newStatus,

            EffectiveDate = endDate,

            Reason = reasonLabel

        }, cancellationToken);



        await _unitOfWork.SaveChangesAsync(cancellationToken);

    }



    public WithdrawalReasonsDto GetWithdrawalReasons() => StudentWithdrawalReasons.ToDto();



    private async Task<StudentDto> MapStudentAsync(

        Guid schoolId,

        Student student,

        Guid? academicYearId,

        CancellationToken cancellationToken)

    {

        var dto = student.Adapt<StudentDto>();

        var year = await ResolveAcademicYearEntityAsync(schoolId, academicYearId, cancellationToken);



        if (year is null)

        {

            return dto with

            {

                IsEnrolledCurrentYear = false,

                CurrentYearClassName = null,

                CurrentYearStatus = null,

                WithdrawalReason = null,

                WithdrawalDate = null

            };

        }



        var enrollments = (await _enrollmentRepository.FindAsync(

            e => e.StudentId == student.Id && e.AcademicYearId == year.Id,

            cancellationToken))

            .OrderByDescending(e => e.IsActive)

            .ThenByDescending(e => e.EnrollmentDate)

            .ToList();



        var enrollment = enrollments.FirstOrDefault();

        if (enrollment is null)

        {

            return dto with

            {

                IsEnrolledCurrentYear = false,

                CurrentYearClassName = null,

                CurrentYearStatus = null,

                WithdrawalReason = null,

                WithdrawalDate = null

            };

        }



        var className = await ResolveClassDisplayNameAsync(schoolId, enrollment.ClassRoomId, cancellationToken);

        var isActiveEnrollment = enrollment.IsActive

            && enrollment.Status is not EnrollmentStatus.Exclusion and not EnrollmentStatus.Abandon;



        if (isActiveEnrollment)

        {

            return dto with

            {

                IsEnrolledCurrentYear = true,

                CurrentYearClassName = className,

                CurrentYearStatus = enrollment.Status,

                WithdrawalReason = null,

                WithdrawalDate = null

            };

        }



        if (enrollment.Status is EnrollmentStatus.Exclusion or EnrollmentStatus.Abandon)

        {

            return dto with

            {

                IsEnrolledCurrentYear = false,

                CurrentYearClassName = className,

                CurrentYearStatus = enrollment.Status,

                WithdrawalReason = ExtractWithdrawalReason(enrollment.Notes),

                WithdrawalDate = enrollment.EndDate

            };

        }



        return dto with

        {

            IsEnrolledCurrentYear = false,

            CurrentYearClassName = className,

            CurrentYearStatus = enrollment.Status,

            WithdrawalReason = null,

            WithdrawalDate = enrollment.EndDate

        };

    }



    private static string? ExtractWithdrawalReason(string? notes)

    {

        if (string.IsNullOrWhiteSpace(notes))

        {

            return null;

        }



        const string prefix = "Raison:";

        if (notes.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))

        {

            return notes[prefix.Length..].Trim();

        }



        return notes.Trim();

    }



    private async Task<string?> ResolveClassDisplayNameAsync(

        Guid schoolId,

        Guid classRoomId,

        CancellationToken cancellationToken)

    {

        var room = (await _classRoomRepository.FindAsync(

            c => c.Id == classRoomId && c.SchoolId == schoolId,

            cancellationToken)).FirstOrDefault();



        if (room is null)

        {

            return null;

        }



        if (room.PedagogicalClassId is not Guid pedagogicalClassId)

        {

            return room.Name;

        }



        var pedagogical = (await _pedagogicalClassRepository.FindAsync(

            p => p.Id == pedagogicalClassId,

            cancellationToken)).FirstOrDefault();



        return pedagogical is not null

            ? $"{pedagogical.DisplayName} {room.Name}"

            : room.Name;

    }



    private static bool HasAnyFilter(StudentSearchRequest request) =>

        request.AcademicYearId.HasValue

        || request.SectionId.HasValue

        || request.PedagogicalClassId.HasValue

        || request.ClassRoomId.HasValue

        || !string.IsNullOrWhiteSpace(request.StudyOption);



    private static bool HasEnrollmentScope(StudentSearchRequest request) =>

        request.IncludeExcluded

        || request.IncludeAbandoned

        || !request.IncludeInscrits;



    private static bool MatchesEnrollmentScope(Enrollment enrollment, StudentSearchRequest request)

    {

        if (enrollment.IsActive

            && enrollment.Status is not EnrollmentStatus.Exclusion and not EnrollmentStatus.Abandon)

        {

            return request.IncludeInscrits;

        }



        if (!enrollment.IsActive && enrollment.Status == EnrollmentStatus.Exclusion)

        {

            return request.IncludeExcluded;

        }



        if (!enrollment.IsActive && enrollment.Status == EnrollmentStatus.Abandon)

        {

            return request.IncludeAbandoned;

        }



        return false;

    }



    private static StudentListDto EmptyResult(StudentSearchRequest request) =>

        new()

        {

            Items = [],

            Page = request.Page,

            PageSize = request.PageSize,

            TotalCount = 0

        };



    private async Task<Guid?> ResolveAcademicYearIdAsync(

        Guid schoolId,

        Guid? academicYearId,

        CancellationToken cancellationToken)

    {

        if (academicYearId.HasValue)

        {

            return academicYearId.Value;

        }



        var currentYear = (await _yearRepository.FindAsync(

            y => y.SchoolId == schoolId && y.IsCurrent && !y.IsClosed,

            cancellationToken)).FirstOrDefault();



        return currentYear?.Id;

    }



    private async Task<AcademicYear?> ResolveAcademicYearEntityAsync(

        Guid schoolId,

        Guid? academicYearId,

        CancellationToken cancellationToken)

    {

        if (academicYearId.HasValue)

        {

            return (await _yearRepository.FindAsync(

                y => y.SchoolId == schoolId && y.Id == academicYearId.Value,

                cancellationToken)).FirstOrDefault();

        }



        return (await _yearRepository.FindAsync(

            y => y.SchoolId == schoolId && y.IsCurrent && !y.IsClosed,

            cancellationToken)).FirstOrDefault();

    }



    private async Task<HashSet<Guid>> ResolveStudentIdsByEnrollmentScopeAsync(

        Guid schoolId,

        Guid academicYearId,

        StudentSearchRequest request,

        CancellationToken cancellationToken)

    {

        var enrollments = await _enrollmentRepository.FindAsync(

            e => e.AcademicYearId == academicYearId,

            cancellationToken);



        return enrollments

            .Where(e => MatchesEnrollmentScope(e, request))

            .Select(e => e.StudentId)

            .ToHashSet();

    }



    private async Task<HashSet<Guid>> ResolveFilteredStudentIdsAsync(

        Guid schoolId,

        StudentSearchRequest request,

        CancellationToken cancellationToken)

    {

        var classRooms = await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);

        var pedagogicalClasses = await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);

        var pedagogicalMap = pedagogicalClasses.ToDictionary(p => p.Id);

        var sections = await _sectionRepository.FindAsync(s => s.SchoolId == schoolId, cancellationToken);



        if (request.AcademicYearId.HasValue)

        {

            classRooms = classRooms.Where(c => c.AcademicYearId == request.AcademicYearId.Value).ToList();

        }



        if (request.SectionId.HasValue)

        {

            var selected = sections.FirstOrDefault(s => s.Id == request.SectionId.Value);

            if (selected is not null)

            {

                var matchingSectionIds = sections

                    .Where(s => string.Equals(s.Name, selected.Name, StringComparison.OrdinalIgnoreCase))

                    .Select(s => s.Id)

                    .ToHashSet();

                classRooms = classRooms.Where(c => matchingSectionIds.Contains(c.SectionId)).ToList();

            }

            else

            {

                classRooms = classRooms.Where(c => c.SectionId == request.SectionId.Value).ToList();

            }

        }



        if (request.PedagogicalClassId.HasValue)

        {

            classRooms = classRooms.Where(c => c.PedagogicalClassId == request.PedagogicalClassId.Value).ToList();

        }



        if (request.ClassRoomId.HasValue)

        {

            classRooms = classRooms.Where(c => c.Id == request.ClassRoomId.Value).ToList();

        }



        if (!string.IsNullOrWhiteSpace(request.StudyOption))

        {

            var option = request.StudyOption.Trim();

            classRooms = classRooms

                .Where(c => c.PedagogicalClassId.HasValue

                    && pedagogicalMap.TryGetValue(c.PedagogicalClassId.Value, out var pedagogical)

                    && string.Equals(pedagogical.StudyOption, option, StringComparison.OrdinalIgnoreCase))

                .ToList();

        }



        var classRoomIds = classRooms.Select(c => c.Id).ToHashSet();

        if (classRoomIds.Count == 0)

        {

            return [];

        }



        var enrollments = await _enrollmentRepository.FindAsync(e => classRoomIds.Contains(e.ClassRoomId), cancellationToken);

        if (request.AcademicYearId.HasValue)

        {

            enrollments = enrollments.Where(e => e.AcademicYearId == request.AcademicYearId.Value).ToList();

        }



        return enrollments

            .Where(e => MatchesEnrollmentScope(e, request))

            .Select(e => e.StudentId)

            .ToHashSet();

    }



    public async Task<IReadOnlyList<StudentDossierFileDto>> ListDossierFilesAsync(

        Guid schoolId,

        Guid studentId,

        CancellationToken cancellationToken = default)

    {

        var student = (await _studentRepository.FindAsync(

            s => s.Id == studentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()

            ?? throw new KeyNotFoundException("Élève introuvable.");



        var years = await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);

        var currentYear = years.FirstOrDefault(y => y.IsCurrent) ?? years.OrderByDescending(y => y.StartDate).FirstOrDefault();

        var academicYearLabel = currentYear?.Label ?? DateTime.UtcNow.Year.ToString();



        var files = _studentDossierStorage.ListStudentFiles(

            student.LastName,

            student.FirstName,

            student.RegistrationNumber,

            academicYearLabel);



        return files

            .Select(f => new StudentDossierFileDto(f.FileName, f.StoragePath, f.SizeBytes, f.LastModifiedUtc))

            .ToList();

    }

}


