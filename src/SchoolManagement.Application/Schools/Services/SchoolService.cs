namespace SchoolManagement.Application.Schools.Services;

using Mapster;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.PedagogicalPeriods.DTOs;
using SchoolManagement.Application.PedagogicalPeriods.Interfaces;
using SchoolManagement.Application.Schools;
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
    private readonly IRepository<Section> _sectionRepository;
    private readonly IRepository<FeeType> _feeTypeRepository;
    private readonly IRepository<Course> _courseRepository;
    private readonly IRepository<PedagogicalClassCourse> _pedagogicalClassCourseRepository;
    private readonly IRepository<CashRegister> _cashRegisterRepository;
    private readonly IRepository<AppConfiguration> _appConfigurationRepository;
    private readonly IPedagogicalPeriodService _pedagogicalPeriodService;
    private readonly IUnitOfWork _unitOfWork;

    public SchoolService(
        IRepository<School> schoolRepository,
        IRepository<AcademicYear> yearRepository,
        IRepository<AcademicPeriod> periodRepository,
        IRepository<ClassRoom> classRoomRepository,
        IRepository<PedagogicalClass> pedagogicalClassRepository,
        IRepository<Section> sectionRepository,
        IRepository<Course> courseRepository,
        IRepository<PedagogicalClassCourse> pedagogicalClassCourseRepository,
        IRepository<FeeType> feeTypeRepository,
        IRepository<CashRegister> cashRegisterRepository,
        IRepository<AppConfiguration> appConfigurationRepository,
        IPedagogicalPeriodService pedagogicalPeriodService,
        IUnitOfWork unitOfWork)
    {
        _schoolRepository = schoolRepository;
        _yearRepository = yearRepository;
        _periodRepository = periodRepository;
        _classRoomRepository = classRoomRepository;
        _pedagogicalClassRepository = pedagogicalClassRepository;
        _sectionRepository = sectionRepository;
        _courseRepository = courseRepository;
        _pedagogicalClassCourseRepository = pedagogicalClassCourseRepository;
        _feeTypeRepository = feeTypeRepository;
        _cashRegisterRepository = cashRegisterRepository;
        _appConfigurationRepository = appConfigurationRepository;
        _pedagogicalPeriodService = pedagogicalPeriodService;
        _unitOfWork = unitOfWork;
    }

    public async Task<SchoolDto?> GetSchoolAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var school = await _schoolRepository.GetByIdAsync(schoolId, cancellationToken);
        return school is null ? null : await MapSchoolDtoAsync(school, cancellationToken);
    }

    public async Task<SchoolDto> UpdateSchoolAsync(Guid schoolId, UpdateSchoolRequest request, CancellationToken cancellationToken = default)
    {
        var school = await _schoolRepository.GetByIdAsync(schoolId, cancellationToken)
            ?? throw new KeyNotFoundException("École introuvable.");

        request.Adapt(school);

        if (request.DefaultFeeTypeId is Guid feeTypeId)
        {
            var fee = await _feeTypeRepository.GetByIdAsync(feeTypeId, cancellationToken)
                ?? throw new DomainException("Le frais principal sélectionné est introuvable.");
            if (fee.SchoolId != schoolId || !fee.IsActive)
            {
                throw new DomainException("Le frais principal doit être un type de frais actif de cet établissement.");
            }

            school.DefaultFeeTypeId = fee.Id;
            school.DefaultCurrency = fee.Currency;
        }
        else
        {
            school.DefaultFeeTypeId = null;
        }

        await _schoolRepository.UpdateAsync(school, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapSchoolDtoAsync(school, cancellationToken);
    }

    private async Task<SchoolDto> MapSchoolDtoAsync(School school, CancellationToken cancellationToken)
    {
        string? feeName = null;
        if (school.DefaultFeeTypeId is Guid feeId)
        {
            var fee = await _feeTypeRepository.GetByIdAsync(feeId, cancellationToken);
            feeName = fee?.Name;
        }

        return new SchoolDto(
            school.Id,
            school.Name,
            school.LegalName,
            school.Address,
            school.City,
            school.Province,
            school.Phone,
            school.Email,
            school.DefaultCurrency,
            school.DefaultFeeTypeId,
            feeName,
            school.IsActive);
    }

    public async Task<IReadOnlyList<AcademicYearDto>> GetAcademicYearsAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var years = await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);
        return years
            .Adapt<List<AcademicYearDto>>()
            .GroupBy(y => y.Id)
            .Select(g => g.First())
            .GroupBy(y => y.Label.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(y => y.IsCurrent).ThenByDescending(y => y.StartDate).First())
            .OrderByDescending(y => y.StartDate)
            .ToList();
    }

    public async Task<AcademicYearDto> CreateAcademicYearAsync(Guid schoolId, CreateAcademicYearRequest request, CancellationToken cancellationToken = default)
    {
        if (request.EndDate <= request.StartDate)
        {
            throw new DomainException("La date de fin doit être postérieure à la date de début.");
        }

        Guid? previousCurrentYearId = null;
        if (request.SetAsCurrent)
        {
            var currentYears = await _yearRepository.FindAsync(y => y.SchoolId == schoolId && y.IsCurrent, cancellationToken);
            previousCurrentYearId = currentYears.FirstOrDefault()?.Id;
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

        if (request.SetAsCurrent)
        {
            await AcademicYearClassRoomProvisioner.ProvisionForYearAsync(
                schoolId,
                academicYear.Id,
                previousCurrentYearId,
                _classRoomRepository,
                _pedagogicalClassRepository,
                _sectionRepository,
                _yearRepository,
                _unitOfWork,
                cancellationToken);
        }

        // Étape 1 du calendrier : générer automatiquement la structure des périodes.
        await _pedagogicalPeriodService.CreateDefaultStructureAsync(
            schoolId,
            new CreatePedagogicalStructureRequest(academicYear.Id, ReplaceExisting: false),
            cancellationToken);

        return academicYear.Adapt<AcademicYearDto>();
    }

    public async Task SetCurrentAcademicYearAsync(Guid schoolId, Guid academicYearId, CancellationToken cancellationToken = default)
    {
        var years = await _yearRepository.FindAsync(y => y.SchoolId == schoolId, cancellationToken);
        var target = years.FirstOrDefault(y => y.Id == academicYearId)
            ?? throw new KeyNotFoundException("Année scolaire introuvable.");

        var previousCurrentYearId = years.FirstOrDefault(y => y.IsCurrent && y.Id != academicYearId)?.Id;

        foreach (var year in years)
        {
            year.IsCurrent = year.Id == academicYearId;
            await _yearRepository.UpdateAsync(year, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await AcademicYearClassRoomProvisioner.ProvisionForYearAsync(
            schoolId,
            academicYearId,
            previousCurrentYearId,
            _classRoomRepository,
            _pedagogicalClassRepository,
            _sectionRepository,
            _yearRepository,
            _unitOfWork,
            cancellationToken);
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
        var courses = await SchoolCourseScope.GetCoursesAsync(
            _courseRepository,
            _pedagogicalClassCourseRepository,
            schoolId,
            cancellationToken);
        var feeTypes = await _feeTypeRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken);
        // CashRegisters : table dépréciée — plus exposée aux écrans d'encaissement.
        _ = _cashRegisterRepository;
        IReadOnlyList<CashRegisterLookupDto> cashRegisters = [];

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
            courses.Select(c => new CourseLookupDto(c.Id, c.Code, c.Name, null)).ToList(),
            feeTypes.Select(f => new FeeTypeLookupDto(f.Id, f.Code, f.Name, f.Currency)).ToList(),
            cashRegisters);
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
