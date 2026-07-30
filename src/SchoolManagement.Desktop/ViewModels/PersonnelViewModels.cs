using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Personnel.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class PersonnelHubViewModel : ViewModelBase
{
    public PersonnelHubViewModel(
        PersonnelListViewModel list,
        PersonnelEditViewModel edit,
        PersonnelDepartmentsViewModel departments,
        PersonnelFunctionsViewModel functions,
        PersonnelPlaceholderViewModel placeholder)
    {
        List = list;
        Edit = edit;
        Departments = departments;
        Functions = functions;
        Placeholder = placeholder;

        PersonnelNavigationBridge.EditPersonnelRequested += OnEditPersonnelRequested;
        ApplyNavigation(PersonnelNavCatalog.DefaultItem);
    }

    public PersonnelListViewModel List { get; }
    public PersonnelEditViewModel Edit { get; }
    public PersonnelDepartmentsViewModel Departments { get; }
    public PersonnelFunctionsViewModel Functions { get; }
    public PersonnelPlaceholderViewModel Placeholder { get; }

    [ObservableProperty] private PersonnelSection _selectedSection = PersonnelSection.Liste;

    public bool IsListeSelected => SelectedSection == PersonnelSection.Liste;
    public bool IsNouveauSelected => SelectedSection == PersonnelSection.Nouveau;
    public bool IsFonctionsSelected => SelectedSection == PersonnelSection.Fonctions;
    public bool IsDepartementsSelected => SelectedSection == PersonnelSection.Departements;
    public bool IsPlaceholderSelected =>
        SelectedSection is PersonnelSection.Contrats
            or PersonnelSection.Presences
            or PersonnelSection.Conges
            or PersonnelSection.Documents
            or PersonnelSection.Historique;

    public string? ActiveNavKey { get; private set; }

    public string SelectedSectionTitle =>
        PersonnelNavCatalog.FindByKey(ActiveNavKey ?? string.Empty)?.Title ?? "Personnel";

    public string SelectedSectionDescription =>
        PersonnelNavCatalog.FindByKey(ActiveNavKey ?? string.Empty)?.Subtitle
        ?? "Gestion des ressources humaines";

    public void ApplyNavigation(PersonnelNavItem item)
    {
        ActiveNavKey = item.Key;
        SelectedSection = item.Section;
        Placeholder.Configure(item.Title, item.Subtitle);

        if (item.Section == PersonnelSection.Liste)
        {
            _ = List.EnsureLoadedAsync();
        }
        else if (item.Section == PersonnelSection.Nouveau && Edit.PersonnelId is null && !Edit.IsLoaded)
        {
            Edit.BeginNew();
        }
        else if (item.Section == PersonnelSection.Departements)
        {
            _ = Departments.EnsureLoadedAsync();
        }
        else if (item.Section == PersonnelSection.Fonctions)
        {
            _ = Functions.EnsureLoadedAsync();
        }

        OnPropertyChanged(nameof(ActiveNavKey));
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionDescription));
        OnPropertyChanged(nameof(IsListeSelected));
        OnPropertyChanged(nameof(IsNouveauSelected));
        OnPropertyChanged(nameof(IsFonctionsSelected));
        OnPropertyChanged(nameof(IsDepartementsSelected));
        OnPropertyChanged(nameof(IsPlaceholderSelected));
    }

    private void OnEditPersonnelRequested(Guid? personnelId)
    {
        if (personnelId.HasValue)
        {
            _ = Edit.LoadAsync(personnelId.Value);
        }
        else
        {
            Edit.BeginNew();
        }

        var item = PersonnelNavCatalog.FindByKey("nouveau") ?? PersonnelNavCatalog.DefaultItem;
        ApplyNavigation(item);
        PersonnelNavigationBridge.Select(item);
    }

    partial void OnSelectedSectionChanged(PersonnelSection value)
    {
        OnPropertyChanged(nameof(IsListeSelected));
        OnPropertyChanged(nameof(IsNouveauSelected));
        OnPropertyChanged(nameof(IsFonctionsSelected));
        OnPropertyChanged(nameof(IsDepartementsSelected));
        OnPropertyChanged(nameof(IsPlaceholderSelected));
    }
}

public partial class PersonnelListViewModel : ViewModelBase
{
    private readonly IPersonnelApiService _personnelApi;

    public PersonnelListViewModel(IPersonnelApiService personnelApi)
    {
        _personnelApi = personnelApi;
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ConfigureItemsView();
    }

    public ICollectionView ItemsView { get; }

    public ObservableCollection<PersonnelListItemDto> Items { get; } = [];
    public ObservableCollection<HrDepartmentDto> Departments { get; } = [];
    public ObservableCollection<HrJobFunctionDto> JobFunctions { get; } = [];
    public ObservableCollection<PersonnelFilterOption> StatusOptions { get; } = [];
    public ObservableCollection<PersonnelFilterOption> ContractOptions { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private HrDepartmentDto? _selectedDepartment;
    [ObservableProperty] private HrJobFunctionDto? _selectedFunction;
    [ObservableProperty] private PersonnelFilterOption? _selectedStatus;
    [ObservableProperty] private PersonnelFilterOption? _selectedContractType;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isFiltersExpanded;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _onLeaveCount;
    [ObservableProperty] private int _contractEndingCount;

    public bool HasNoResults => !IsBusy && Items.Count == 0;
    public string FiltersHeaderText => IsFiltersExpanded ? "Masquer les filtres" : "Afficher les filtres";

    public async Task EnsureLoadedAsync()
    {
        if (StatusOptions.Count == 0)
        {
            StatusOptions.Add(new PersonnelFilterOption(null, "Tous les statuts"));
            foreach (PersonnelStatus status in Enum.GetValues<PersonnelStatus>())
            {
                StatusOptions.Add(new PersonnelFilterOption((int)status, GetStatusLabel(status)));
            }

            ContractOptions.Add(new PersonnelFilterOption(null, "Tous les contrats"));
            foreach (PersonnelContractType type in Enum.GetValues<PersonnelContractType>())
            {
                ContractOptions.Add(new PersonnelFilterOption((int)type, GetContractLabel(type)));
            }
        }

        await LoadLookupsAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        OnPropertyChanged(nameof(HasNoResults));
        try
        {
            var kpis = await _personnelApi.GetKpisAsync();
            TotalCount = kpis.Total;
            ActiveCount = kpis.Active;
            OnLeaveCount = kpis.OnLeave;
            ContractEndingCount = kpis.ContractEnding;

            var items = await _personnelApi.GetPersonnelAsync(
                SelectedDepartment?.Id,
                SelectedFunction?.Id,
                SelectedStatus?.Value,
                SelectedContractType?.Value,
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim());

            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            StatusMessage = $"{Items.Count} membre(s) affiché(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasNoResults));
        }
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync() => await RefreshAsync();

    [RelayCommand]
    private void ToggleFilters() => IsFiltersExpanded = !IsFiltersExpanded;

    partial void OnIsFiltersExpandedChanged(bool value) => OnPropertyChanged(nameof(FiltersHeaderText));

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedDepartment = null;
        SelectedFunction = null;
        SelectedStatus = StatusOptions.FirstOrDefault();
        SelectedContractType = ContractOptions.FirstOrDefault();
        SearchText = string.Empty;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private void NewPersonnel() => PersonnelNavigationBridge.RequestEdit(null);

    [RelayCommand]
    private void ViewPersonnel(PersonnelListItemDto? item)
    {
        if (item is null) return;
        PersonnelNavigationBridge.RequestEdit(item.Id);
    }

    [RelayCommand]
    private void EditPersonnel(PersonnelListItemDto? item) => ViewPersonnel(item);

    [RelayCommand]
    private void PrintPersonnel(PersonnelListItemDto? item) =>
        StatusMessage = item is null
            ? null
            : $"Impression de {item.FullName} — disponible prochainement.";

    [RelayCommand]
    private void ExportPersonnelPdf(PersonnelListItemDto? item) =>
        StatusMessage = item is null
            ? null
            : $"Export PDF de {item.FullName} — disponible prochainement.";

    [RelayCommand]
    private void ExportPersonnelExcel(PersonnelListItemDto? item) =>
        StatusMessage = item is null
            ? null
            : $"Export Excel de {item.FullName} — disponible prochainement.";

    [RelayCommand]
    private async Task DeactivatePersonnelAsync(PersonnelListItemDto? item)
    {
        if (item is null) return;

        if (MessageBox.Show(
                $"Désactiver {item.FullName} ?",
                "Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var detail = await _personnelApi.GetPersonnelByIdAsync(item.Id);
            var request = BuildDeactivateRequest(detail);
            await _personnelApi.UpdatePersonnelAsync(item.Id, request);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void ExportExcel() =>
        StatusMessage = "Export Excel — disponible prochainement.";

    [RelayCommand]
    private void ExportPdf() =>
        StatusMessage = "Export PDF — disponible prochainement.";

    [RelayCommand]
    private void PrintList() =>
        StatusMessage = "Impression — disponible prochainement.";

    private void ConfigureItemsView()
    {
        using (ItemsView.DeferRefresh())
        {
            ItemsView.GroupDescriptions.Clear();
            ItemsView.SortDescriptions.Clear();
            ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PersonnelListItemDto.CategoryLabel)));
            ItemsView.SortDescriptions.Add(new SortDescription(
                nameof(PersonnelListItemDto.CategoryLabel),
                ListSortDirection.Ascending));
            ItemsView.SortDescriptions.Add(new SortDescription(
                nameof(PersonnelListItemDto.FullName),
                ListSortDirection.Ascending));
        }
    }

    private async Task LoadLookupsAsync()
    {
        Departments.Clear();
        foreach (var dept in await _personnelApi.GetDepartmentsAsync())
        {
            Departments.Add(dept);
        }

        JobFunctions.Clear();
        foreach (var fn in await _personnelApi.GetJobFunctionsAsync())
        {
            JobFunctions.Add(fn);
        }
    }

    private static SavePersonnelRequest BuildDeactivateRequest(PersonnelDetailDto detail) =>
        new(
            detail.EmployeeNumber,
            detail.FirstName,
            detail.MiddleName,
            detail.LastName,
            detail.Phone,
            detail.Email,
            detail.Specialization,
            detail.HireDate,
            false,
            detail.ResidenceAddress,
            detail.Category,
            detail.Gender,
            detail.BirthDate,
            detail.BirthPlace,
            detail.Nationality,
            detail.MaritalStatus,
            detail.ChildrenCount,
            detail.IdCardNumber,
            detail.DepartmentId,
            detail.JobFunctionId,
            detail.Grade,
            detail.Service,
            detail.SupervisorName,
            detail.WorkLocation,
            detail.ContractType,
            detail.ContractStartDate,
            detail.ContractEndDate,
            detail.BaseSalary,
            detail.CurrencyCode,
            detail.PaymentMethod,
            detail.BankName,
            detail.BankAccountNumber,
            detail.BankAccountHolder,
            detail.PayDay,
            detail.EmergencyContactName,
            detail.EmergencyContactRelation,
            detail.EmergencyContactPhone,
            detail.EmergencyContactAddress,
            PersonnelStatus.Inactif,
            detail.SystemUsername,
            null,
            null,
            null,
            detail.AllowSystemLogin,
            false);

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
}

public sealed record PersonnelFilterOption(int? Value, string Label)
{
    public override string ToString() => Label;
}

public partial class PersonnelPlaceholderViewModel : ViewModelBase
{
    [ObservableProperty] private string _title = "Module en préparation";
    [ObservableProperty] private string _message = "Disponible prochainement.";

    public void Configure(string title, string message)
    {
        Title = title;
        Message = message;
    }
}

public partial class PersonnelDepartmentsViewModel : ViewModelBase
{
    private readonly IPersonnelApiService _personnelApi;

    public PersonnelDepartmentsViewModel(IPersonnelApiService personnelApi) => _personnelApi = personnelApi;

    public ObservableCollection<HrDepartmentDto> Items { get; } = [];

    [ObservableProperty] private string _newCode = string.Empty;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    public async Task EnsureLoadedAsync()
    {
        IsBusy = true;
        try
        {
            Items.Clear();
            foreach (var item in await _personnelApi.GetDepartmentsAsync())
            {
                Items.Add(item);
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

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCode) || string.IsNullOrWhiteSpace(NewName))
        {
            StatusMessage = "Code et nom requis.";
            return;
        }

        IsBusy = true;
        try
        {
            await _personnelApi.CreateDepartmentAsync(new CreateHrDepartmentRequest(NewCode, NewName));
            NewCode = string.Empty;
            NewName = string.Empty;
            await EnsureLoadedAsync();
            StatusMessage = "Département ajouté.";
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
}

public partial class PersonnelFunctionsViewModel : ViewModelBase
{
    private readonly IPersonnelApiService _personnelApi;

    public PersonnelFunctionsViewModel(IPersonnelApiService personnelApi) => _personnelApi = personnelApi;

    public ObservableCollection<HrJobFunctionDto> Items { get; } = [];
    public ObservableCollection<HrDepartmentDto> Departments { get; } = [];

    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private HrDepartmentDto? _selectedDepartment;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    public async Task EnsureLoadedAsync()
    {
        IsBusy = true;
        try
        {
            Departments.Clear();
            foreach (var dept in await _personnelApi.GetDepartmentsAsync())
            {
                Departments.Add(dept);
            }

            Items.Clear();
            foreach (var item in await _personnelApi.GetJobFunctionsAsync())
            {
                Items.Add(item);
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

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            StatusMessage = "Nom de la fonction requis.";
            return;
        }

        IsBusy = true;
        try
        {
            await _personnelApi.CreateJobFunctionAsync(new CreateHrJobFunctionRequest(SelectedDepartment?.Id, NewName));
            NewName = string.Empty;
            await EnsureLoadedAsync();
            StatusMessage = "Fonction ajoutée.";
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
}
