namespace SchoolManagement.Application.Students.Services;

using Mapster;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Application.Students.Interfaces;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Exceptions;

public sealed class StudentService : IStudentService
{
    private readonly IRepository<Student> _studentRepository;
    private readonly IRepository<Enrollment> _enrollmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StudentService(
        IRepository<Student> studentRepository,
        IRepository<Enrollment> enrollmentRepository,
        IUnitOfWork unitOfWork)
    {
        _studentRepository = studentRepository;
        _enrollmentRepository = enrollmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<StudentDto?> GetByIdAsync(Guid schoolId, Guid studentId, CancellationToken cancellationToken = default)
    {
        var students = await _studentRepository.FindAsync(
            s => s.Id == studentId && s.SchoolId == schoolId, cancellationToken);
        return students.FirstOrDefault()?.Adapt<StudentDto>();
    }

    public async Task<StudentListDto> SearchAsync(Guid schoolId, StudentSearchRequest request, CancellationToken cancellationToken = default)
    {
        var all = await _studentRepository.FindAsync(s => s.SchoolId == schoolId && !s.IsArchived, cancellationToken);

        var query = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.FirstName.ToLower().Contains(term) ||
                s.LastName.ToLower().Contains(term) ||
                s.RegistrationNumber.ToLower().Contains(term));
        }

        if (request.ClassRoomId.HasValue)
        {
            var enrollments = await _enrollmentRepository.FindAsync(
                e => e.ClassRoomId == request.ClassRoomId.Value && e.IsActive,
                cancellationToken);
            var studentIds = enrollments.Select(e => e.StudentId).ToHashSet();
            query = query.Where(s => studentIds.Contains(s.Id));
        }

        var total = query.Count();
        var items = query
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Adapt<List<StudentDto>>();

        return new StudentListDto
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total
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
        return student.Adapt<StudentDto>();
    }

    public async Task<StudentDto> UpdateAsync(Guid schoolId, Guid studentId, UpdateStudentRequest request, CancellationToken cancellationToken = default)
    {
        var student = (await _studentRepository.FindAsync(
            s => s.Id == studentId && s.SchoolId == schoolId, cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Élève introuvable.");

        request.Adapt(student);
        await _studentRepository.UpdateAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return student.Adapt<StudentDto>();
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
}
