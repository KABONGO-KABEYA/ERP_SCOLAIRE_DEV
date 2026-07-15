namespace SchoolManagement.Application.Admin.Services;

using SchoolManagement.Application.Admin.DTOs;
using SchoolManagement.Application.Admin.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Application.Geography.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Exceptions;

public sealed class TeacherAdminService : ITeacherAdminService
{
    private readonly IRepository<Teacher> _teacherRepository;
    private readonly IAddressService _addressService;
    private readonly IUnitOfWork _unitOfWork;

    public TeacherAdminService(
        IRepository<Teacher> teacherRepository,
        IAddressService addressService,
        IUnitOfWork unitOfWork)
    {
        _teacherRepository = teacherRepository;
        _addressService = addressService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<TeacherAdminDto>> GetTeachersAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var teachers = await _teacherRepository.FindAsync(t => t.SchoolId == schoolId, cancellationToken);
        var result = new List<TeacherAdminDto>();

        foreach (var teacher in teachers.OrderBy(t => t.LastName).ThenBy(t => t.FirstName))
        {
            result.Add(await MapTeacherAsync(teacher, cancellationToken));
        }

        return result;
    }

    public async Task<TeacherAdminDto> CreateTeacherAsync(
        Guid schoolId,
        CreateTeacherAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        var employeeNumber = request.EmployeeNumber.Trim();
        var existing = await _teacherRepository.FindAsync(
            t => t.SchoolId == schoolId && t.EmployeeNumber == employeeNumber,
            cancellationToken);

        if (existing.Count > 0)
        {
            throw new DomainException($"Le matricule enseignant '{employeeNumber}' existe déjà.");
        }

        var addressId = await _addressService.UpsertAsync(request.ResidenceAddress, null, cancellationToken);

        var teacher = new Teacher
        {
            SchoolId = schoolId,
            EmployeeNumber = employeeNumber,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Specialization = string.IsNullOrWhiteSpace(request.Specialization) ? null : request.Specialization.Trim(),
            HireDate = request.HireDate,
            AddressId = addressId,
            IsActive = true
        };

        await _teacherRepository.AddAsync(teacher, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapTeacherAsync(teacher, cancellationToken);
    }

    public async Task<TeacherAdminDto> UpdateTeacherAsync(
        Guid schoolId,
        Guid teacherId,
        UpdateTeacherAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        var teacher = await GetTeacherOrThrowAsync(schoolId, teacherId, cancellationToken);
        var employeeNumber = request.EmployeeNumber.Trim();

        var duplicate = await _teacherRepository.FindAsync(
            t => t.SchoolId == schoolId && t.EmployeeNumber == employeeNumber && t.Id != teacherId,
            cancellationToken);

        if (duplicate.Count > 0)
        {
            throw new DomainException($"Le matricule enseignant '{employeeNumber}' existe déjà.");
        }

        teacher.EmployeeNumber = employeeNumber;
        teacher.FirstName = request.FirstName.Trim();
        teacher.LastName = request.LastName.Trim();
        teacher.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        teacher.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        teacher.Specialization = string.IsNullOrWhiteSpace(request.Specialization) ? null : request.Specialization.Trim();
        teacher.HireDate = request.HireDate;
        teacher.IsActive = request.IsActive;

        if (request.UpdateAddress)
        {
            teacher.AddressId = await _addressService.UpsertAsync(
                request.ResidenceAddress,
                teacher.AddressId,
                cancellationToken);
        }

        await _teacherRepository.UpdateAsync(teacher, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await MapTeacherAsync(teacher, cancellationToken);
    }

    private async Task<Teacher> GetTeacherOrThrowAsync(
        Guid schoolId,
        Guid teacherId,
        CancellationToken cancellationToken)
    {
        return (await _teacherRepository.FindAsync(
            t => t.Id == teacherId && t.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Enseignant introuvable.");
    }

    private async Task<TeacherAdminDto> MapTeacherAsync(Teacher teacher, CancellationToken cancellationToken)
    {
        string? addressLine = null;
        if (teacher.AddressId.HasValue)
        {
            var address = await _addressService.GetAsync(teacher.AddressId.Value, cancellationToken);
            addressLine = address?.FormattedLine;
        }

        return new TeacherAdminDto(
            teacher.Id,
            teacher.EmployeeNumber,
            teacher.FirstName,
            teacher.LastName,
            $"{teacher.LastName} {teacher.FirstName}",
            teacher.Phone,
            teacher.Email,
            teacher.Specialization,
            teacher.HireDate,
            teacher.IsActive,
            teacher.AddressId,
            addressLine);
    }
}
