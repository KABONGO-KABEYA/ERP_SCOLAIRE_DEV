namespace SchoolManagement.Application.Schools.Services;

using Mapster;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Exceptions;

public sealed class SchoolService : ISchoolService
{
    private readonly IRepository<School> _schoolRepository;
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<AcademicPeriod> _periodRepository;
    private readonly IRepository<ClassRoom> _classRoomRepository;
    private readonly IRepository<PedagogicalClass> _pedagogicalClassRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<CashRegister> _cashRegisterRepository;
    private readonly IRepository<AppConfiguration> _appConfigurationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SchoolService(
        IRepository<School> schoolRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<AcademicPeriod> periodRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<Course> courseRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<CashRegister> cashRegisterRepository,
        IRepository<AppConfiguration> appConfigurationRepository,
        IUnitOfWork unitOfWork)
    {
        _schoolRepository = schoolRepository;
        _yearRepository = yearRepository;
        _periodRepository = periodRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _courseRepository = courseRepository;
        _feeTypeRepository = feeTypeRepository;
        _cashRegisterRepository = cashRegisterRepository;
        _appConfigurationRepository = appConfigurationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SchoolDto?> GetSchoolAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var school = await _schoolRepository.GetByIdAsync(schoolId, cancellationToken);
        return school?.Adapt<SchoolDto>();
    }

    public async Task<SchoolDto> UpdateSchoolAsync(Guid schoolId, UpdateSchoolRequest request, CancellationToken cancellationToken = default)
    {
        var school = await _schoolRepository.GetByIdAsync(schoolId, cancellationToken)
            ?? throw new KeyNotFoundException("École introuvable.");

        request.Adapt(school);
        await _schoolRepository.UpdateAsync(school, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return school.Adapt<SchoolDto>();
    }

    public async Task<IReadOnlyList<AcademicYearDto>> GetAcademicYearsAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var years = await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);
        return years.OrderByDescending(y => y.StartDate).Adapt<List<AcademicYearDto>>();
    }

    public async Task<AcademicYearDto> CreateAcademicYearAsync(Guid schoolId, CreateAcademicYearRequest request, CancellationToken cancellationToken = default)
    {
        if (request.EndDate <= request.StartDate)
        {
            throw new DomainException("La date de fin doit être postérieure à la date de début.");
        }

        if (request.SetAsCurrent)
        {
            var currentYears = await _yearRepository.FindAsync(y => y.SchoolId == schoolId && y.IsCurrent, cancellationToken);
            foreach (var year in currentYears)
            {
                year.IsCurrent = false;
                await _yearRepository.UpdateAsync(year, cancellationToken);
            }
        }

        var academicYear = request.Adapt<AcademicYear>();
        academicYear.SchoolId = schoolId;
        await _yearRepository.AddAsync(academicYear, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return academicYear.Adapt<AcademicYearDto>();
    }

    public async Task SetCurrentAcademicYearAsync(Guid schoolId, Guid academicYearId, CancellationToken cancellationToken = default)
    {
        var years = await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);
        var target = years.FirstOrDefault(y => y.Id == academicYearId)
            ?? throw new KeyNotFoundException("Année scolaire introuvable.");

        foreach (var year in years)
        {
            year.IsCurrent = year.Id == academicYearId;
            await _yearRepository.UpdateAsync(year, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SchoolLookupsDto> GetLookupsAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var years = await GetAcademicYearsAsync(schoolId, cancellationToken);
        var yearIds = years.Select(y => y.Id).ToList();

        var periods = await _periodRepository.FindAsync(p => yearIds.Contains(p.AcademicYearId), cancellationToken);
        var classes = await _classRoomRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        var pedagogicalMap = ClassRoomAvailability.BuildMap(
            await _pedagogicalClassRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken));
        classes = classes.Where(c => ClassRoomAvailability.IsSelectable(c, pedagogicalMap)).ToList();
        var courses = await _courseRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);
        var feeTypes = await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken);
        var cashRegisters = await _cashRegisterRepository.FindAsync(c => c.SchoolId == schoolId, cancellationToken);

        return new SchoolLookupsDto(
            years,
            periods.OrderBy(p => p.OrderIndex).Select(p => new AcademicPeriodLookupDto(p.Id, p.Name, p.AcademicYearId, p.OrderIndex)).ToList(),
            classes.Select(c =>
            {
                var displayName = c.PedagogicalClassId.HasValue
                    && pedagogicalMap.TryGetValue(c.PedagogicalClassId.Value, out var pedagogicalClass)
                    ? $"{pedagogicalClass.DisplayName} {c.Name}"
                    : c.Name;
                return new ClassRoomLookupDto(c.Id, c.Code, displayName, c.AcademicYearId);
            }).ToList(),
            courses.Select(c => new CourseLookupDto(c.Id, c.Code, c.Name, c.ClassRoomId)).ToList(),
            feeTypes.Select(f => new FeeTypeLookupDto(f.Id, f.Code, f.Name, f.DefaultAmount, f.Currency)).ToList(),
            cashRegisters.Select(c => new CashRegisterLookupDto(c.Id, c.Code, c.Name, c.Currency)).ToList());
    }

    public async Task<SchoolRegulationDto> GetRegulationAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var config = await GetRegulationConfigurationAsync(schoolId, cancellationToken);
        return new SchoolRegulationDto(config?.Value ?? string.Empty, config?.UpdatedAt ?? config?.CreatedAt);
    }

    public async Task<SchoolRegulationDto> UpdateRegulationAsync(Guid schoolId, UpdateSchoolRegulationRequest request, CancellationToken cancellationToken = default)
    {
        var school = await _schoolRepository.GetByIdAsync(schoolId, cancellationToken)
            ?? throw new KeyNotFoundException("École introuvable.");

        var config = await GetRegulationConfigurationAsync(schoolId, cancellationToken);
        if (config is null)
        {
            config = new AppConfiguration
            {
                SchoolId = school.Id,
                Key = "school.regulation",
                Description = "Règlement d'ordre intérieur de l'établissement",
                Value = request.Content ?? string.Empty
            };

            await _appConfigurationRepository.AddAsync(config, cancellationToken);
        }
        else
        {
            config.Value = request.Content ?? string.Empty;
            await _appConfigurationRepository.UpdateAsync(config, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SchoolRegulationDto(config.Value, config.UpdatedAt ?? config.CreatedAt);
    }

    private async Task<AppConfiguration?> GetRegulationConfigurationAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        return (await _appConfigurationRepository.FindAsync(
            c => c.SchoolId == schoolId && c.Key == "school.regulation",
            cancellationToken)).FirstOrDefault();
    }
}
