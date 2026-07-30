using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Admin.DTOs;
using SchoolManagement.Application.Personnel.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class PersonnelEditViewModel : ViewModelBase
{
    private readonly IPersonnelApiService _personnelApi;
    private readonly IAdminApiService _adminApi;
    private readonly IGeographyApiService _geographyApi;

    public PersonnelEditViewModel(
        IPersonnelApiService personnelApi,
        IAdminApiService adminApi,
        IGeographyApiService geographyApi)
    {
        _personnelApi = personnelApi;
        _adminApi = adminApi;
        _geographyApi = geographyApi;
        AddressEditor = new AddressEditorViewModel(_geographyApi);
        InitializeLookups();
    }

    public AddressEditorViewModel AddressEditor { get; }

    public ObservableCollection<PersonnelCategoryOption> Categories { get; } = [];
    public ObservableCollection<GenderOption> Genders { get; } = [];
    public ObservableCollection<HrDepartmentDto> Departments { get; } = [];
    public ObservableCollection<HrJobFunctionDto> JobFunctions { get; } = [];
    public ObservableCollection<PersonnelStatusOption> StatusOptions { get; } = [];
    public ObservableCollection<PersonnelContractTypeOption> ContractTypes { get; } = [];
    public ObservableCollection<PersonnelPaymentMethodOption> PaymentMethods { get; } = [];
    public ObservableCollection<RoleDto> SystemRoles { get; } = [];
    public ObservableCollection<PersonnelHistoryItemDto> HistoryItems { get; } = [];

    public Guid? PersonnelId { get; private set; }
    public bool IsLoaded { get; private set; }

    [ObservableProperty] private string _formTitle = "Nouveau personnel";
    [ObservableProperty] private string _formSubtitle = "Création d'une fiche RH";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _validationMessage;

    [ObservableProperty] private string _employeeNumber = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _middleName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _specialization = string.Empty;
    [ObservableProperty] private DateTime? _hireDate = DateTime.Today;
    [ObservableProperty] private bool _isActive = true;

    [ObservableProperty] private PersonnelCategoryOption? _category;
    [ObservableProperty] private GenderOption? _selectedGender;
    [ObservableProperty] private DateTime? _birthDate;
    [ObservableProperty] private string _birthPlace = string.Empty;
    [ObservableProperty] private string _nationality = "Congolaise";
    [ObservableProperty] private string _maritalStatus = string.Empty;
    [ObservableProperty] private string _childrenCount = string.Empty;
    [ObservableProperty] private string _idCardNumber = string.Empty;

    [ObservableProperty] private HrDepartmentDto? _selectedDepartment;
    [ObservableProperty] private HrJobFunctionDto? _selectedFunction;
    [ObservableProperty] private string _grade = string.Empty;
    [ObservableProperty] private string _service = string.Empty;
    [ObservableProperty] private string _supervisorName = string.Empty;
    [ObservableProperty] private string _workLocation = string.Empty;
    [ObservableProperty] private PersonnelStatusOption? _selectedStatus;

    [ObservableProperty] private PersonnelContractTypeOption? _selectedContractType;
    [ObservableProperty] private DateTime? _contractStartDate;
    [ObservableProperty] private DateTime? _contractEndDate;
    [ObservableProperty] private string _baseSalary = string.Empty;
    [ObservableProperty] private string _currencyCode = "CDF";
    [ObservableProperty] private PersonnelPaymentMethodOption? _selectedPaymentMethod;
    [ObservableProperty] private string _bankName = string.Empty;
    [ObservableProperty] private string _bankAccountNumber = string.Empty;
    [ObservableProperty] private string _bankAccountHolder = string.Empty;
    [ObservableProperty] private string _payDay = string.Empty;

    [ObservableProperty] private string _emergencyContactName = string.Empty;
    [ObservableProperty] private string _emergencyContactRelation = string.Empty;
    [ObservableProperty] private string _emergencyContactPhone = string.Empty;
    [ObservableProperty] private string _emergencyContactAddress = string.Empty;

    [ObservableProperty] private bool _allowSystemLogin;
    [ObservableProperty] private bool _createSystemAccount;
    [ObservableProperty] private string _systemUsername = string.Empty;
    [ObservableProperty] private string _systemPassword = string.Empty;
    [ObservableProperty] private string _systemPasswordConfirm = string.Empty;
    [ObservableProperty] private RoleDto? _selectedSystemRole;

    public string SummaryFullName =>
        string.Join(" ", new[] { LastName, FirstName, MiddleName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string SummaryEmployeeNumber => EmployeeNumber;
    public string SummaryCategoryLabel => Category?.Label ?? "—";
    public string SummaryDepartmentName => SelectedDepartment?.Name ?? "—";
    public string SummaryFunctionName => SelectedFunction?.Name ?? "—";
    public string SummaryContractLabel => SelectedContractType?.Label ?? "—";
    public string SummaryStatusLabel => SelectedStatus?.Label ?? "—";

    partial void OnEmployeeNumberChanged(string value) => RefreshSummary();
    partial void OnFirstNameChanged(string value) => RefreshSummary();
    partial void OnMiddleNameChanged(string value) => RefreshSummary();
    partial void OnLastNameChanged(string value) => RefreshSummary();
    partial void OnCategoryChanged(PersonnelCategoryOption? value) => RefreshSummary();
    partial void OnSelectedDepartmentChanged(HrDepartmentDto? value) => RefreshSummary();
    partial void OnSelectedFunctionChanged(HrJobFunctionDto? value) => RefreshSummary();
    partial void OnSelectedContractTypeChanged(PersonnelContractTypeOption? value) => RefreshSummary();
    partial void OnSelectedStatusChanged(PersonnelStatusOption? value) => RefreshSummary();

    public void BeginNew()
    {
        _ = BeginNewAsync();
    }

    private async Task BeginNewAsync()
    {
        PersonnelId = null;
        IsLoaded = true;
        FormTitle = "Nouveau personnel";
        FormSubtitle = "Création d'une fiche RH";
        ResetFormFields();
        await LoadLookupsAsync();
        await InitializeAddressEditorAsync();
    }

    public async Task LoadAsync(Guid personnelId)
    {
        IsBusy = true;
        try
        {
            await LoadLookupsAsync();
            var detail = await _personnelApi.GetPersonnelByIdAsync(personnelId);
            PersonnelId = detail.Id;
            IsLoaded = true;
            FormTitle = detail.FullName;
            FormSubtitle = "Modification de la fiche personnel";

            EmployeeNumber = detail.EmployeeNumber;
            FirstName = detail.FirstName;
            MiddleName = detail.MiddleName ?? string.Empty;
            LastName = detail.LastName;
            Phone = detail.Phone ?? string.Empty;
            Email = detail.Email ?? string.Empty;
            Specialization = detail.Specialization ?? string.Empty;
            HireDate = detail.HireDate?.ToDateTime(TimeOnly.MinValue);
            IsActive = detail.IsActive;

            Category = Categories.FirstOrDefault(c => c.Value == detail.Category) ?? Categories.First();
            SelectedGender = detail.Gender.HasValue
                ? Genders.FirstOrDefault(g => g.Value == detail.Gender.Value)
                : null;
            BirthDate = detail.BirthDate?.ToDateTime(TimeOnly.MinValue);
            BirthPlace = detail.BirthPlace ?? string.Empty;
            Nationality = detail.Nationality ?? "Congolaise";
            MaritalStatus = detail.MaritalStatus ?? string.Empty;
            ChildrenCount = detail.ChildrenCount?.ToString() ?? string.Empty;
            IdCardNumber = detail.IdCardNumber ?? string.Empty;

            SelectedDepartment = Departments.FirstOrDefault(d => d.Id == detail.DepartmentId);
            SelectedFunction = JobFunctions.FirstOrDefault(f => f.Id == detail.JobFunctionId);
            Grade = detail.Grade ?? string.Empty;
            Service = detail.Service ?? string.Empty;
            SupervisorName = detail.SupervisorName ?? string.Empty;
            WorkLocation = detail.WorkLocation ?? string.Empty;
            SelectedStatus = StatusOptions.FirstOrDefault(s => s.Value == detail.Status) ?? StatusOptions.First();

            SelectedContractType = detail.ContractType.HasValue
                ? ContractTypes.FirstOrDefault(c => c.Value == detail.ContractType.Value)
                : null;
            ContractStartDate = detail.ContractStartDate?.ToDateTime(TimeOnly.MinValue);
            ContractEndDate = detail.ContractEndDate?.ToDateTime(TimeOnly.MinValue);
            BaseSalary = detail.BaseSalary?.ToString("0.##") ?? string.Empty;
            CurrencyCode = detail.CurrencyCode ?? "CDF";
            SelectedPaymentMethod = detail.PaymentMethod.HasValue
                ? PaymentMethods.FirstOrDefault(p => p.Value == detail.PaymentMethod.Value)
                : null;
            BankName = detail.BankName ?? string.Empty;
            BankAccountNumber = detail.BankAccountNumber ?? string.Empty;
            BankAccountHolder = detail.BankAccountHolder ?? string.Empty;
            PayDay = detail.PayDay?.ToString() ?? string.Empty;

            EmergencyContactName = detail.EmergencyContactName ?? string.Empty;
            EmergencyContactRelation = detail.EmergencyContactRelation ?? string.Empty;
            EmergencyContactPhone = detail.EmergencyContactPhone ?? string.Empty;
            EmergencyContactAddress = detail.EmergencyContactAddress ?? string.Empty;

            AllowSystemLogin = detail.AllowSystemLogin;
            SystemUsername = detail.SystemUsername ?? string.Empty;
            CreateSystemAccount = false;

            AddressEditor.Reset();
            var addressLoaded = false;
            if (detail.ResidenceAddress is not null)
            {
                await AddressEditor.LoadFromInputAsync(detail.ResidenceAddress);
                addressLoaded = true;
            }
            else if (detail.AddressId is Guid addressId)
            {
                var address = await _geographyApi.GetAddressAsync(addressId);
                if (address is not null)
                {
                    await AddressEditor.LoadFromDtoAsync(address);
                    addressLoaded = true;
                }
            }

            if (!addressLoaded)
            {
                await InitializeAddressEditorAsync();
            }

            HistoryItems.Clear();
            foreach (var item in detail.History)
            {
                HistoryItems.Add(item);
            }

            RefreshSummary();
            StatusMessage = null;
            ValidationMessage = null;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync() => await SaveInternalAsync(resetAfterSave: false);

    [RelayCommand]
    private async Task SaveAndNewAsync() => await SaveInternalAsync(resetAfterSave: true);

    [RelayCommand]
    private void Cancel()
    {
        if (PersonnelId.HasValue)
        {
            _ = LoadAsync(PersonnelId.Value);
            return;
        }

        BeginNew();
    }

    private async Task SaveInternalAsync(bool resetAfterSave)
    {
        ValidationMessage = ValidateForm();
        if (!string.IsNullOrWhiteSpace(ValidationMessage))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var request = BuildSaveRequest();
            if (PersonnelId.HasValue)
            {
                await _personnelApi.UpdatePersonnelAsync(PersonnelId.Value, request);
                StatusMessage = "Fiche personnel enregistrée.";
            }
            else
            {
                var created = await _personnelApi.CreatePersonnelAsync(request);
                PersonnelId = created.Id;
                StatusMessage = "Personnel créé avec succès.";
            }

            if (resetAfterSave)
            {
                BeginNew();
            }
            else if (PersonnelId.HasValue)
            {
                await LoadAsync(PersonnelId.Value);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string? ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(EmployeeNumber))
            return "Le matricule est obligatoire.";
        if (string.IsNullOrWhiteSpace(LastName) || string.IsNullOrWhiteSpace(FirstName))
            return "Le nom et le prénom sont obligatoires.";
        if (CreateSystemAccount && string.IsNullOrWhiteSpace(SystemUsername))
            return "Le nom d'utilisateur est obligatoire pour créer un compte.";
        if (CreateSystemAccount && string.IsNullOrWhiteSpace(SystemPassword))
            return "Le mot de passe est obligatoire pour créer un compte.";
        if (CreateSystemAccount && SystemPassword != SystemPasswordConfirm)
            return "La confirmation du mot de passe ne correspond pas.";
        return null;
    }

    private SavePersonnelRequest BuildSaveRequest()
    {
        int? children = int.TryParse(ChildrenCount, out var c) ? c : null;
        decimal? salary = decimal.TryParse(BaseSalary, out var s) ? s : null;
        int? payDay = int.TryParse(PayDay, out var p) ? p : null;

        return new SavePersonnelRequest(
            EmployeeNumber.Trim(),
            FirstName.Trim(),
            string.IsNullOrWhiteSpace(MiddleName) ? null : MiddleName.Trim(),
            LastName.Trim(),
            NullIfEmpty(Phone),
            NullIfEmpty(Email),
            NullIfEmpty(Specialization),
            HireDate.HasValue ? DateOnly.FromDateTime(HireDate.Value) : null,
            IsActive,
            AddressEditor.HasContent() ? AddressEditor.ToInputDto() : null,
            Category?.Value ?? PersonnelCategory.Enseignant,
            SelectedGender?.Value,
            BirthDate.HasValue ? DateOnly.FromDateTime(BirthDate.Value) : null,
            NullIfEmpty(BirthPlace),
            NullIfEmpty(Nationality),
            NullIfEmpty(MaritalStatus),
            children,
            NullIfEmpty(IdCardNumber),
            SelectedDepartment?.Id,
            SelectedFunction?.Id,
            NullIfEmpty(Grade),
            NullIfEmpty(Service),
            NullIfEmpty(SupervisorName),
            NullIfEmpty(WorkLocation),
            SelectedContractType?.Value,
            ContractStartDate.HasValue ? DateOnly.FromDateTime(ContractStartDate.Value) : null,
            ContractEndDate.HasValue ? DateOnly.FromDateTime(ContractEndDate.Value) : null,
            salary,
            NullIfEmpty(CurrencyCode),
            SelectedPaymentMethod?.Value,
            NullIfEmpty(BankName),
            NullIfEmpty(BankAccountNumber),
            NullIfEmpty(BankAccountHolder),
            payDay,
            NullIfEmpty(EmergencyContactName),
            NullIfEmpty(EmergencyContactRelation),
            NullIfEmpty(EmergencyContactPhone),
            NullIfEmpty(EmergencyContactAddress),
            SelectedStatus?.Value ?? PersonnelStatus.Actif,
            NullIfEmpty(SystemUsername),
            NullIfEmpty(SystemPassword),
            NullIfEmpty(SystemPasswordConfirm),
            SelectedSystemRole?.Id,
            AllowSystemLogin,
            CreateSystemAccount);
    }

    private async Task LoadLookupsAsync()
    {
        if (Departments.Count == 0)
        {
            foreach (var dept in await _personnelApi.GetDepartmentsAsync())
            {
                Departments.Add(dept);
            }
        }

        JobFunctions.Clear();
        foreach (var fn in await _personnelApi.GetJobFunctionsAsync(SelectedDepartment?.Id))
        {
            JobFunctions.Add(fn);
        }

        if (SystemRoles.Count == 0)
        {
            foreach (var role in await _adminApi.GetRolesAsync())
            {
                SystemRoles.Add(role);
            }
        }
    }

    private async Task InitializeAddressEditorAsync(CancellationToken cancellationToken = default)
    {
        await AddressEditor.InitializeAsync(cancellationToken);

        var defaultCountry = AddressEditor.Countries.FirstOrDefault(c =>
            c.Code.Equals("RDC", StringComparison.OrdinalIgnoreCase))
            ?? AddressEditor.Countries.FirstOrDefault();
        if (defaultCountry is null)
        {
            return;
        }

        await AddressEditor.SetCountryAsync(defaultCountry, cancellationToken);

        var defaultProvince = AddressEditor.Provinces.FirstOrDefault(p =>
            p.Code.Equals("KIN", StringComparison.OrdinalIgnoreCase))
            ?? AddressEditor.Provinces.FirstOrDefault();
        if (defaultProvince is null)
        {
            return;
        }

        await AddressEditor.SetProvinceAsync(defaultProvince, cancellationToken);

        var defaultCity = AddressEditor.Cities.FirstOrDefault(c =>
            c.Code.Equals("KIN", StringComparison.OrdinalIgnoreCase))
            ?? AddressEditor.Cities.FirstOrDefault();
        if (defaultCity is not null)
        {
            await AddressEditor.SetCityAsync(defaultCity, cancellationToken);
        }
    }

    private void InitializeLookups()
    {
        foreach (PersonnelCategory cat in Enum.GetValues<PersonnelCategory>())
        {
            Categories.Add(new PersonnelCategoryOption(cat, GetCategoryLabel(cat)));
        }

        Genders.Add(new GenderOption(Gender.Masculin, "Masculin"));
        Genders.Add(new GenderOption(Gender.Feminin, "Féminin"));

        foreach (PersonnelStatus status in Enum.GetValues<PersonnelStatus>())
        {
            StatusOptions.Add(new PersonnelStatusOption(status, GetStatusLabel(status)));
        }

        foreach (PersonnelContractType type in Enum.GetValues<PersonnelContractType>())
        {
            ContractTypes.Add(new PersonnelContractTypeOption(type, GetContractLabel(type)));
        }

        foreach (PersonnelPaymentMethod method in Enum.GetValues<PersonnelPaymentMethod>())
        {
            PaymentMethods.Add(new PersonnelPaymentMethodOption(method, GetPaymentLabel(method)));
        }

        Category = Categories.First();
        SelectedStatus = StatusOptions.First();
    }

    private void ResetFormFields()
    {
        EmployeeNumber = string.Empty;
        FirstName = string.Empty;
        MiddleName = string.Empty;
        LastName = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Specialization = string.Empty;
        HireDate = DateTime.Today;
        IsActive = true;
        Category = Categories.FirstOrDefault(c => c.Value == PersonnelCategory.Enseignant);
        SelectedGender = null;
        BirthDate = null;
        BirthPlace = string.Empty;
        Nationality = "Congolaise";
        MaritalStatus = string.Empty;
        ChildrenCount = string.Empty;
        IdCardNumber = string.Empty;
        SelectedDepartment = null;
        SelectedFunction = null;
        Grade = string.Empty;
        Service = string.Empty;
        SupervisorName = string.Empty;
        WorkLocation = string.Empty;
        SelectedStatus = StatusOptions.FirstOrDefault(s => s.Value == PersonnelStatus.Actif);
        SelectedContractType = null;
        ContractStartDate = null;
        ContractEndDate = null;
        BaseSalary = string.Empty;
        CurrencyCode = "CDF";
        SelectedPaymentMethod = null;
        BankName = string.Empty;
        BankAccountNumber = string.Empty;
        BankAccountHolder = string.Empty;
        PayDay = string.Empty;
        EmergencyContactName = string.Empty;
        EmergencyContactRelation = string.Empty;
        EmergencyContactPhone = string.Empty;
        EmergencyContactAddress = string.Empty;
        AllowSystemLogin = false;
        CreateSystemAccount = false;
        SystemUsername = string.Empty;
        SystemPassword = string.Empty;
        SystemPasswordConfirm = string.Empty;
        SelectedSystemRole = null;
        AddressEditor.Reset();
        HistoryItems.Clear();
        ValidationMessage = null;
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(SummaryFullName));
        OnPropertyChanged(nameof(SummaryEmployeeNumber));
        OnPropertyChanged(nameof(SummaryCategoryLabel));
        OnPropertyChanged(nameof(SummaryDepartmentName));
        OnPropertyChanged(nameof(SummaryFunctionName));
        OnPropertyChanged(nameof(SummaryContractLabel));
        OnPropertyChanged(nameof(SummaryStatusLabel));
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
        PersonnelCategory.Entretien => "Agent d'entretien",
        PersonnelCategory.Sentinelle => "Sentinelle",
        PersonnelCategory.Cuisine => "Personnel de cuisine",
        _ => "Autre"
    };

    private static string GetStatusLabel(PersonnelStatus status) => status switch
    {
        PersonnelStatus.Actif => "En activité",
        PersonnelStatus.Conge => "En congé",
        PersonnelStatus.FinContrat => "Fin de contrat",
        PersonnelStatus.Inactif => "Inactif",
        _ => status.ToString()
    };

    private static string GetContractLabel(PersonnelContractType type) => type switch
    {
        PersonnelContractType.Cdi => "CDI",
        PersonnelContractType.Cdd => "CDD",
        PersonnelContractType.Stage => "Stage",
        PersonnelContractType.Vacataire => "Vacataire",
        PersonnelContractType.Prestation => "Prestation",
        _ => type.ToString()
    };

    private static string GetPaymentLabel(PersonnelPaymentMethod method) => method switch
    {
        PersonnelPaymentMethod.Virement => "Virement bancaire",
        PersonnelPaymentMethod.Espece => "Espèces",
        PersonnelPaymentMethod.MobileMoney => "Mobile Money",
        PersonnelPaymentMethod.Cheque => "Chèque",
        _ => method.ToString()
    };
}

public sealed record PersonnelCategoryOption(PersonnelCategory Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record GenderOption(Gender Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record PersonnelStatusOption(PersonnelStatus Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record PersonnelContractTypeOption(PersonnelContractType Value, string Label)
{
    public override string ToString() => Label;
}

public sealed record PersonnelPaymentMethodOption(PersonnelPaymentMethod Value, string Label)
{
    public override string ToString() => Label;
}
