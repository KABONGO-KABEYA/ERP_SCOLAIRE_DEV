namespace SchoolManagement.Application.Personnel.Services;

using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Admin.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Application.Geography.Interfaces;
using SchoolManagement.Application.Personnel.DTOs;
using SchoolManagement.Application.Personnel.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Hr;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;

public sealed class PersonnelAdminService : IPersonnelAdminService
{
    private static readonly (string Code, string Name)[] DefaultDepartments =
    [
        ("DIR", "Direction"),
        ("PREF", "Préfecture"),
        ("ENS", "Enseignement"),
        ("COMPTA", "Comptabilité"),
        ("SEC", "Secrétariat"),
        ("SURV", "Surveillance"),
        ("BIB", "Bibliothèque"),
        ("LAB", "Laboratoire"),
        ("INFO", "Informatique"),
        ("INT", "Intendance"),
        ("TRANS", "Transport"),
        ("ENT", "Entretien"),
        ("SECU", "Sécurité"),
        ("CUIS", "Cuisine")
    ];

    private readonly IRepository<Teacher> _teacherRepository;
    private readonly IRepository<PersonnelHrProfile> _profileRepository;
    private readonly IRepository<HrDepartment> _departmentRepository;
    private readonly IRepository<HrJobFunction> _jobFunctionRepository;
    private readonly IRepository<UserAccount> _userRepository;
    private readonly IAddressService _addressService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public PersonnelAdminService(
        IRepository<Teacher> teacherRepository,
        IRepository<PersonnelHrProfile> profileRepository,
        IRepository<HrDepartment> departmentRepository,
        IRepository<HrJobFunction> jobFunctionRepository,
        IRepository<UserAccount> userRepository,
        IAddressService addressService,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _teacherRepository = teacherRepository;
        _profileRepository = profileRepository;
        _departmentRepository = departmentRepository;
        _jobFunctionRepository = jobFunctionRepository;
        _userRepository = userRepository;
        _addressService = addressService;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task EnsureDefaultLookupsAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var existing = await _departmentRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        foreach (var (code, name) in DefaultDepartments)
        {
            await _departmentRepository.AddAsync(new HrDepartment
            {
                SchoolId = schoolId,
                Code = code,
                Name = name,
                IsActive = true
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<PersonnelKpiDto> GetKpisAsync(Guid schoolId, CancellationToken cancellationToken = default)
    {
        var items = await BuildListItemsAsync(schoolId, cancellationToken);
        return new PersonnelKpiDto(
            items.Count,
            items.Count(i => i.Status == PersonnelStatus.Actif),
            items.Count(i => i.Status == PersonnelStatus.Conge),
            items.Count(i => i.Status == PersonnelStatus.FinContrat));
    }

    public async Task<IReadOnlyList<PersonnelListItemDto>> GetPersonnelAsync(
        Guid schoolId,
        Guid? departmentId = null,
        Guid? jobFunctionId = null,
        PersonnelStatus? status = null,
        PersonnelContractType? contractType = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var items = await BuildListItemsAsync(schoolId, cancellationToken);
        IEnumerable<PersonnelListItemDto> query = items;

        if (departmentId.HasValue)
        {
            var deptTeachers = await GetTeacherIdsForDepartmentAsync(schoolId, departmentId.Value, cancellationToken);
            query = query.Where(i => deptTeachers.Contains(i.Id));
        }

        if (jobFunctionId.HasValue)
        {
            var fnTeachers = await GetTeacherIdsForFunctionAsync(schoolId, jobFunctionId.Value, cancellationToken);
            query = query.Where(i => fnTeachers.Contains(i.Id));
        }

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        if (contractType.HasValue)
        {
            var contractTeachers = await GetTeacherIdsForContractTypeAsync(schoolId, contractType.Value, cancellationToken);
            query = query.Where(i => contractTeachers.Contains(i.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i =>
                i.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || i.EmployeeNumber.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (i.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Phone?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return query.OrderBy(i => i.FullName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<PersonnelDetailDto> GetPersonnelByIdAsync(
        Guid schoolId,
        Guid personnelId,
        CancellationToken cancellationToken = default)
    {
        var teacher = await GetTeacherOrThrowAsync(schoolId, personnelId, cancellationToken);
        var profile = (await _profileRepository.FindAsync(
            p => p.SchoolId == schoolId && p.TeacherId == personnelId,
            cancellationToken)).FirstOrDefault();

        return await MapDetailAsync(teacher, profile, cancellationToken);
    }

    public async Task<PersonnelDetailDto> CreatePersonnelAsync(
        Guid schoolId,
        SavePersonnelRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateSaveRequest(request);
        await EnsureUniqueEmployeeNumberAsync(schoolId, request.EmployeeNumber.Trim(), null, cancellationToken);

        var addressId = await _addressService.UpsertAsync(request.ResidenceAddress, null, cancellationToken);

        var teacher = new Teacher
        {
            SchoolId = schoolId,
            EmployeeNumber = request.EmployeeNumber.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Phone = NormalizeOptional(request.Phone),
            Email = NormalizeOptional(request.Email),
            Specialization = NormalizeOptional(request.Specialization),
            HireDate = request.HireDate,
            AddressId = addressId,
            IsActive = request.IsActive
        };

        await _teacherRepository.AddAsync(teacher, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var profile = BuildProfile(schoolId, teacher.Id, request);
        await _profileRepository.AddAsync(profile, cancellationToken);

        if (request.CreateSystemAccount && request.AllowSystemLogin)
        {
            await CreateLinkedUserAsync(schoolId, teacher, request, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(teacher, profile, cancellationToken);
    }

    public async Task<PersonnelDetailDto> UpdatePersonnelAsync(
        Guid schoolId,
        Guid personnelId,
        SavePersonnelRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateSaveRequest(request);
        var teacher = await GetTeacherOrThrowAsync(schoolId, personnelId, cancellationToken);
        await EnsureUniqueEmployeeNumberAsync(schoolId, request.EmployeeNumber.Trim(), personnelId, cancellationToken);

        teacher.EmployeeNumber = request.EmployeeNumber.Trim();
        teacher.FirstName = request.FirstName.Trim();
        teacher.LastName = request.LastName.Trim();
        teacher.Phone = NormalizeOptional(request.Phone);
        teacher.Email = NormalizeOptional(request.Email);
        teacher.Specialization = NormalizeOptional(request.Specialization);
        teacher.HireDate = request.HireDate;
        teacher.IsActive = request.IsActive;
        teacher.AddressId = await _addressService.UpsertAsync(request.ResidenceAddress, teacher.AddressId, cancellationToken);

        await _teacherRepository.UpdateAsync(teacher, cancellationToken);

        var profile = (await _profileRepository.FindAsync(
            p => p.SchoolId == schoolId && p.TeacherId == personnelId,
            cancellationToken)).FirstOrDefault();

        if (profile is null)
        {
            profile = BuildProfile(schoolId, personnelId, request);
            await _profileRepository.AddAsync(profile, cancellationToken);
        }
        else
        {
            ApplyProfile(profile, request);
            await _profileRepository.UpdateAsync(profile, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(teacher, profile, cancellationToken);
    }

    public async Task<IReadOnlyList<HrDepartmentDto>> GetDepartmentsAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultLookupsAsync(schoolId, cancellationToken);
        var departments = await _departmentRepository.FindAsync(d => d.SchoolId == schoolId && d.IsActive, cancellationToken);
        return departments.OrderBy(d => d.Name).Select(d => new HrDepartmentDto(d.Id, d.Code, d.Name, d.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<HrJobFunctionDto>> GetJobFunctionsAsync(
        Guid schoolId,
        Guid? departmentId = null,
        CancellationToken cancellationToken = default)
    {
        var functions = await _jobFunctionRepository.FindAsync(
            f => f.SchoolId == schoolId && f.IsActive
                && (!departmentId.HasValue || f.DepartmentId == departmentId),
            cancellationToken);

        return functions.OrderBy(f => f.Name).Select(f => new HrJobFunctionDto(f.Id, f.DepartmentId, f.Name, f.IsActive)).ToList();
    }

    public async Task<HrDepartmentDto> CreateDepartmentAsync(
        Guid schoolId,
        CreateHrDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var existing = await _departmentRepository.FindAsync(
            d => d.SchoolId == schoolId && d.Code == code,
            cancellationToken);

        if (existing.Count > 0)
        {
            throw new DomainException($"Le département '{code}' existe déjà.");
        }

        var department = new HrDepartment
        {
            SchoolId = schoolId,
            Code = code,
            Name = request.Name.Trim(),
            IsActive = true
        };

        await _departmentRepository.AddAsync(department, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new HrDepartmentDto(department.Id, department.Code, department.Name, department.IsActive);
    }

    public async Task<HrJobFunctionDto> CreateJobFunctionAsync(
        Guid schoolId,
        CreateHrJobFunctionRequest request,
        CancellationToken cancellationToken = default)
    {
        var function = new HrJobFunction
        {
            SchoolId = schoolId,
            DepartmentId = request.DepartmentId,
            Name = request.Name.Trim(),
            IsActive = true
        };

        await _jobFunctionRepository.AddAsync(function, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new HrJobFunctionDto(function.Id, function.DepartmentId, function.Name, function.IsActive);
    }

    private async Task<IReadOnlyList<PersonnelListItemDto>> BuildListItemsAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        var teachers = await _teacherRepository.FindAsync(t => t.SchoolId == schoolId, cancellationToken);
        var profiles = await _profileRepository.FindAsync(p => p.SchoolId == schoolId, cancellationToken);
        var profileMap = profiles.ToDictionary(p => p.TeacherId);
        var departments = (await _departmentRepository.FindAsync(d => d.SchoolId == schoolId, cancellationToken))
            .ToDictionary(d => d.Id);
        var functions = (await _jobFunctionRepository.FindAsync(f => f.SchoolId == schoolId, cancellationToken))
            .ToDictionary(f => f.Id);

        return teachers
            .Select(t =>
            {
                profileMap.TryGetValue(t.Id, out var profile);
                var status = ResolveStatus(t, profile);
                departments.TryGetValue(profile?.DepartmentId ?? Guid.Empty, out var dept);
                functions.TryGetValue(profile?.JobFunctionId ?? Guid.Empty, out var fn);
                var category = profile?.Category ?? PersonnelCategory.Enseignant;

                return new PersonnelListItemDto(
                    t.Id,
                    t.EmployeeNumber,
                    FormatFullName(t.FirstName, profile?.MiddleName, t.LastName),
                    profile?.PhotoPath,
                    category,
                    GetCategoryLabel(category),
                    fn?.Name,
                    dept?.Name,
                    t.Phone,
                    t.Email,
                    FormatSeniority(t.HireDate),
                    GetContractLabel(profile?.ContractType),
                    status,
                    GetStatusLabel(status),
                    t.IsActive);
            })
            .ToList();
    }

    private async Task<PersonnelDetailDto> MapDetailAsync(
        Teacher teacher,
        PersonnelHrProfile? profile,
        CancellationToken cancellationToken)
    {
        string? addressLine = null;
        AddressInputDto? address = null;
        if (teacher.AddressId.HasValue)
        {
            var addr = await _addressService.GetAsync(teacher.AddressId.Value, cancellationToken);
            addressLine = addr?.FormattedLine;
            address = new AddressInputDto(
                addr.CountryId,
                addr.ProvinceId,
                addr.CityId,
                addr.CommuneId,
                addr.Neighborhood,
                addr.Avenue,
                addr.HouseNumber);
        }

        var departments = profile?.DepartmentId.HasValue == true
            ? await _departmentRepository.FindAsync(d => d.Id == profile.DepartmentId, cancellationToken)
            : [];
        var functions = profile?.JobFunctionId.HasValue == true
            ? await _jobFunctionRepository.FindAsync(f => f.Id == profile.JobFunctionId, cancellationToken)
            : [];

        var linkedUser = (await _userRepository.FindAsync(
            u => u.TeacherId == teacher.Id,
            cancellationToken)).FirstOrDefault();

        var category = profile?.Category ?? PersonnelCategory.Enseignant;
        var status = ResolveStatus(teacher, profile);

        return new PersonnelDetailDto(
            teacher.Id,
            teacher.EmployeeNumber,
            teacher.FirstName,
            profile?.MiddleName,
            teacher.LastName,
            FormatFullName(teacher.FirstName, profile?.MiddleName, teacher.LastName),
            teacher.Phone,
            teacher.Email,
            teacher.Specialization,
            teacher.HireDate,
            teacher.IsActive,
            teacher.AddressId,
            addressLine,
            address,
            category,
            profile?.Gender,
            profile?.BirthDate,
            profile?.BirthPlace,
            profile?.Nationality,
            profile?.MaritalStatus,
            profile?.ChildrenCount,
            profile?.IdCardNumber,
            profile?.DepartmentId,
            departments.FirstOrDefault()?.Name,
            profile?.JobFunctionId,
            functions.FirstOrDefault()?.Name,
            profile?.Grade,
            profile?.Service,
            profile?.SupervisorName,
            profile?.WorkLocation,
            profile?.ContractType,
            profile?.ContractStartDate,
            profile?.ContractEndDate,
            profile?.BaseSalary,
            profile?.CurrencyCode,
            profile?.PaymentMethod,
            profile?.BankName,
            profile?.BankAccountNumber,
            profile?.BankAccountHolder,
            profile?.PayDay,
            profile?.EmergencyContactName,
            profile?.EmergencyContactRelation,
            profile?.EmergencyContactPhone,
            profile?.EmergencyContactAddress,
            profile?.PhotoPath,
            status,
            linkedUser?.UserName,
            linkedUser?.IsActive ?? false,
            BuildHistory(teacher, profile, linkedUser));
    }

    private static IReadOnlyList<PersonnelHistoryItemDto> BuildHistory(
        Teacher teacher,
        PersonnelHrProfile? profile,
        UserAccount? user)
    {
        var items = new List<PersonnelHistoryItemDto>
        {
            new(teacher.CreatedAt, "Fiche créée", "Création du dossier personnel", "AccountPlusOutline")
        };

        if (teacher.HireDate.HasValue)
        {
            items.Add(new(teacher.CreatedAt, "Date d'embauche", teacher.HireDate.Value.ToString("dd/MM/yyyy"), "CalendarCheck"));
        }

        if (profile?.ContractStartDate.HasValue == true)
        {
            items.Add(new(profile.CreatedAt, "Contrat ajouté", GetContractLabel(profile.ContractType), "FileDocumentOutline"));
        }

        if (profile?.BaseSalary.HasValue == true)
        {
            items.Add(new(profile.UpdatedAt ?? profile.CreatedAt, "Salaire défini", $"{profile.BaseSalary:N0} {profile.CurrencyCode ?? "CDF"}", "Cash"));
        }

        if (user is not null)
        {
            items.Add(new(user.CreatedAt, "Compte utilisateur créé", user.UserName, "AccountKeyOutline"));
        }

        return items.OrderByDescending(i => i.OccurredAt).ToList();
    }

    private static PersonnelHrProfile BuildProfile(Guid schoolId, Guid teacherId, SavePersonnelRequest request)
    {
        var profile = new PersonnelHrProfile { SchoolId = schoolId, TeacherId = teacherId };
        ApplyProfile(profile, request);
        return profile;
    }

    private static void ApplyProfile(PersonnelHrProfile profile, SavePersonnelRequest request)
    {
        profile.Category = request.Category;
        profile.MiddleName = NormalizeOptional(request.MiddleName);
        profile.Gender = request.Gender;
        profile.BirthDate = request.BirthDate;
        profile.BirthPlace = NormalizeOptional(request.BirthPlace);
        profile.Nationality = NormalizeOptional(request.Nationality);
        profile.MaritalStatus = NormalizeOptional(request.MaritalStatus);
        profile.ChildrenCount = request.ChildrenCount;
        profile.IdCardNumber = NormalizeOptional(request.IdCardNumber);
        profile.DepartmentId = request.DepartmentId;
        profile.JobFunctionId = request.JobFunctionId;
        profile.Grade = NormalizeOptional(request.Grade);
        profile.Service = NormalizeOptional(request.Service);
        profile.SupervisorName = NormalizeOptional(request.SupervisorName);
        profile.WorkLocation = NormalizeOptional(request.WorkLocation);
        profile.ContractType = request.ContractType;
        profile.ContractStartDate = request.ContractStartDate;
        profile.ContractEndDate = request.ContractEndDate;
        profile.BaseSalary = request.BaseSalary;
        profile.CurrencyCode = NormalizeOptional(request.CurrencyCode);
        profile.PaymentMethod = request.PaymentMethod;
        profile.BankName = NormalizeOptional(request.BankName);
        profile.BankAccountNumber = NormalizeOptional(request.BankAccountNumber);
        profile.BankAccountHolder = NormalizeOptional(request.BankAccountHolder);
        profile.PayDay = request.PayDay;
        profile.EmergencyContactName = NormalizeOptional(request.EmergencyContactName);
        profile.EmergencyContactRelation = NormalizeOptional(request.EmergencyContactRelation);
        profile.EmergencyContactPhone = NormalizeOptional(request.EmergencyContactPhone);
        profile.EmergencyContactAddress = NormalizeOptional(request.EmergencyContactAddress);
        profile.Status = request.Status;
    }

    private async Task CreateLinkedUserAsync(
        Guid schoolId,
        Teacher teacher,
        SavePersonnelRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SystemUsername)
            || string.IsNullOrWhiteSpace(request.SystemPassword))
        {
            throw new DomainException("Nom d'utilisateur et mot de passe requis pour créer un compte.");
        }

        if (request.SystemPassword != request.SystemPasswordConfirm)
        {
            throw new DomainException("La confirmation du mot de passe ne correspond pas.");
        }

        var existing = await _userRepository.FindAsync(
            u => u.SchoolId == schoolId && u.UserName == request.SystemUsername.Trim(),
            cancellationToken);

        if (existing.Count > 0)
        {
            throw new DomainException($"L'identifiant '{request.SystemUsername}' existe déjà.");
        }

        var user = new UserAccount
        {
            SchoolId = schoolId,
            TeacherId = teacher.Id,
            UserName = request.SystemUsername.Trim(),
            Email = teacher.Email ?? $"{request.SystemUsername.Trim()}@local",
            PasswordHash = _passwordHasher.Hash(request.SystemPassword),
            FirstName = teacher.FirstName,
            LastName = teacher.LastName,
            IsActive = request.AllowSystemLogin,
            MustChangePassword = true
        };

        await _userRepository.AddAsync(user, cancellationToken);
    }

    private static void ValidateSaveRequest(SavePersonnelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeNumber))
        {
            throw new DomainException("Le matricule est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            throw new DomainException("Le nom et le prénom sont obligatoires.");
        }
    }

    private async Task EnsureUniqueEmployeeNumberAsync(
        Guid schoolId,
        string employeeNumber,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var duplicate = await _teacherRepository.FindAsync(
            t => t.SchoolId == schoolId && t.EmployeeNumber == employeeNumber && t.Id != excludeId,
            cancellationToken);

        if (duplicate.Count > 0)
        {
            throw new DomainException($"Le matricule '{employeeNumber}' existe déjà.");
        }
    }

    private async Task<Teacher> GetTeacherOrThrowAsync(
        Guid schoolId,
        Guid personnelId,
        CancellationToken cancellationToken)
    {
        return (await _teacherRepository.FindAsync(
            t => t.Id == personnelId && t.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new KeyNotFoundException("Personnel introuvable.");
    }

    private async Task<HashSet<Guid>> GetTeacherIdsForDepartmentAsync(
        Guid schoolId,
        Guid departmentId,
        CancellationToken cancellationToken)
    {
        var profiles = await _profileRepository.FindAsync(
            p => p.SchoolId == schoolId && p.DepartmentId == departmentId,
            cancellationToken);
        return profiles.Select(p => p.TeacherId).ToHashSet();
    }

    private async Task<HashSet<Guid>> GetTeacherIdsForFunctionAsync(
        Guid schoolId,
        Guid jobFunctionId,
        CancellationToken cancellationToken)
    {
        var profiles = await _profileRepository.FindAsync(
            p => p.SchoolId == schoolId && p.JobFunctionId == jobFunctionId,
            cancellationToken);
        return profiles.Select(p => p.TeacherId).ToHashSet();
    }

    private async Task<HashSet<Guid>> GetTeacherIdsForContractTypeAsync(
        Guid schoolId,
        PersonnelContractType contractType,
        CancellationToken cancellationToken)
    {
        var profiles = await _profileRepository.FindAsync(
            p => p.SchoolId == schoolId && p.ContractType == contractType,
            cancellationToken);
        return profiles.Select(p => p.TeacherId).ToHashSet();
    }

    private static PersonnelStatus ResolveStatus(Teacher teacher, PersonnelHrProfile? profile)
    {
        if (!teacher.IsActive)
        {
            return PersonnelStatus.Inactif;
        }

        if (profile?.Status == PersonnelStatus.Conge)
        {
            return PersonnelStatus.Conge;
        }

        if (profile?.ContractEndDate is { } end && end <= DateOnly.FromDateTime(DateTime.Today))
        {
            return PersonnelStatus.FinContrat;
        }

        return profile?.Status ?? PersonnelStatus.Actif;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatFullName(string firstName, string? middleName, string lastName) =>
        string.Join(" ", new[] { lastName, firstName, middleName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string FormatSeniority(DateOnly? hireDate)
    {
        if (hireDate is null)
        {
            return "—";
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var years = today.Year - hireDate.Value.Year;
        if (hireDate.Value.AddYears(years) > today)
        {
            years--;
        }

        return years <= 0 ? "< 1 an" : $"{years} an{(years > 1 ? "s" : "")}";
    }

    private static string GetCategoryLabel(PersonnelCategory category) => category switch
    {
        PersonnelCategory.Enseignant => "Enseignant",
        PersonnelCategory.Direction => "Direction",
        PersonnelCategory.Prefecture => "Préfecture",
        PersonnelCategory.Comptabilite => "Comptabilité",
        PersonnelCategory.Secretariat => "Secrétariat",
        PersonnelCategory.Surveillance => "Surveillance",
        PersonnelCategory.Bibliotheque => "Bibliothèque",
        PersonnelCategory.Laboratoire => "Laboratoire",
        PersonnelCategory.Informatique => "Informatique",
        PersonnelCategory.Intendance => "Intendance",
        PersonnelCategory.Chauffeur => "Chauffeur",
        PersonnelCategory.Entretien => "Entretien",
        PersonnelCategory.Sentinelle => "Sentinelle",
        PersonnelCategory.Cuisine => "Cuisine",
        _ => "Autre"
    };

    private static string GetContractLabel(PersonnelContractType? contractType) => contractType switch
    {
        PersonnelContractType.Cdi => "CDI",
        PersonnelContractType.Cdd => "CDD",
        PersonnelContractType.Stage => "Stage",
        PersonnelContractType.Vacataire => "Vacataire",
        PersonnelContractType.Prestation => "Prestation",
        _ => "—"
    };

    private static string GetStatusLabel(PersonnelStatus status) => status switch
    {
        PersonnelStatus.Actif => "En activité",
        PersonnelStatus.Conge => "En congé",
        PersonnelStatus.FinContrat => "Fin de contrat",
        PersonnelStatus.Inactif => "Inactif",
        _ => "—"
    };
}
