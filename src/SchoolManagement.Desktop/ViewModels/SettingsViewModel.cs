using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Admin.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISchoolApiService _schoolApiService;
    private readonly IAcademicApiService _academicApiService;
    private readonly IAdminApiService _adminApiService;
    private readonly IGeographyApiService _geographyApiService;

    public SettingsViewModel(
        ISchoolApiService schoolApiService,
        IAcademicApiService academicApiService,
        IAdminApiService adminApiService,
        IGeographyApiService geographyApiService,
        DocumentBrandingViewModel documentBranding,
        GeographyAdminViewModel geographyAdmin,
        SchoolFeeConfigurationViewModel schoolFeeConfiguration)
    {
        _schoolApiService = schoolApiService;
        _academicApiService = academicApiService;
        _adminApiService = adminApiService;
        _geographyApiService = geographyApiService;
        DocumentBranding = documentBranding;
        GeographyAdmin = geographyAdmin;
        SchoolFeeConfiguration = schoolFeeConfiguration;
        NewAdminUserAddressEditor = new AddressEditorViewModel(_geographyApiService);
        SelectedAdminUserAddressEditor = new AddressEditorViewModel(_geographyApiService);
        NewTeacherAddressEditor = new AddressEditorViewModel(_geographyApiService);
        SelectedTeacherAddressEditor = new AddressEditorViewModel(_geographyApiService);
        ProgramFilters =
        [
            new ProgramFilterItem(null, "Tous les programmes"),
            new ProgramFilterItem(SchoolProgram.Maternelle, "Maternelle"),
            new ProgramFilterItem(SchoolProgram.Primaire, "Primaire"),
            new ProgramFilterItem(SchoolProgram.CTEB, "CTEB (7e – 8e)"),
            new ProgramFilterItem(SchoolProgram.Humanites, "Humanités (cycle long)"),
            new ProgramFilterItem(SchoolProgram.HumanitesProfessionnelles, "Humanités professionnelles"),
            new ProgramFilterItem(SchoolProgram.FilieresSpecialisees, "Filières spécialisées")
        ];

        SettingsNodes =
        [
            new SettingsNodeViewModel(
                "Paramètres",
                "Cog",
                null,
                true,
                [
                    new SettingsNodeViewModel("Établissement", "Domain", SettingsSection.Etablissement),
                    new SettingsNodeViewModel("Règlement d'ordre intérieur de l'école", "TextBoxOutline", SettingsSection.Reglement),
                    new SettingsNodeViewModel("Structure pédagogique / Classes", "GoogleClassroom", SettingsSection.StructurePedagogique),
                    new SettingsNodeViewModel("Années scolaires", "CalendarRange", SettingsSection.AnneesScolaires),
                    new SettingsNodeViewModel("Frais scolaires", "CashMultiple", SettingsSection.FraisScolaires),
                    new SettingsNodeViewModel("Matières", "BookEducation", SettingsSection.Matieres),
                    new SettingsNodeViewModel("Géographie", "Earth", SettingsSection.Geographie),
                    new SettingsNodeViewModel("Utilisateurs", "AccountCog", SettingsSection.Utilisateurs),
                    new SettingsNodeViewModel("Enseignants", "HumanMaleBoard", SettingsSection.Enseignants)
                ])
        ];

        SelectedSettingsNode = SettingsNodes[0].Children
            .FirstOrDefault(node => node.Section == SettingsSection.Etablissement)
            ?? SettingsNodes[0].Children.FirstOrDefault();

        _ = LoadAsync();
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _legalName;

    [ObservableProperty]
    private string? _city;

    [ObservableProperty]
    private string? _province;

    [ObservableProperty]
    private string? _phone;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private Currency _defaultCurrency = Currency.CDF;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _classSearch = string.Empty;

    [ObservableProperty]
    private ProgramFilterItem? _selectedProgramFilter;

    [ObservableProperty]
    private PedagogicalClassItemViewModel? _selectedPedagogicalClass;

    [ObservableProperty]
    private AcademicYearDto? _selectedAcademicYear;

    [ObservableProperty]
    private ClassLocalDto? _selectedLocal;

    [ObservableProperty]
    private string _localName = string.Empty;

    [ObservableProperty]
    private string? _localCapacityText;

    [ObservableProperty]
    private string? _localObservations;

    [ObservableProperty]
    private bool _localIsActive = true;

    [ObservableProperty]
    private string? _structureSummary;

    [ObservableProperty]
    private SettingsNodeViewModel? _selectedSettingsNode;

    [ObservableProperty]
    private string _regulationContent = string.Empty;

    [ObservableProperty]
    private DateTime? _regulationUpdatedAt;

    [ObservableProperty]
    private IReadOnlyList<FeeTypeLookupDto> _feeTypes = [];

    [ObservableProperty]
    private ClassRoomDto? _selectedSubjectClass;

    [ObservableProperty]
    private string _newSubjectCode = string.Empty;

    [ObservableProperty]
    private string _newSubjectName = string.Empty;

    [ObservableProperty]
    private UserAccountDto? _selectedAdminUser;

    [ObservableProperty]
    private RoleDto? _selectedAdminRole;

    [ObservableProperty]
    private string _newAdminUserName = string.Empty;

    [ObservableProperty]
    private string _newAdminEmail = string.Empty;

    [ObservableProperty]
    private string _newAdminPassword = string.Empty;

    [ObservableProperty]
    private string _newAdminFirstName = string.Empty;

    [ObservableProperty]
    private string _newAdminLastName = string.Empty;

    [ObservableProperty]
    private TeacherAdminDto? _selectedAdminTeacher;

    [ObservableProperty]
    private string _newTeacherEmployeeNumber = string.Empty;

    [ObservableProperty]
    private string _newTeacherFirstName = string.Empty;

    [ObservableProperty]
    private string _newTeacherLastName = string.Empty;

    [ObservableProperty]
    private string _newTeacherPhone = string.Empty;

    [ObservableProperty]
    private string _newTeacherEmail = string.Empty;

    [ObservableProperty]
    private string _newTeacherSpecialization = string.Empty;

    [ObservableProperty]
    private DateTime? _newTeacherHireDate = DateTime.Today;

    [ObservableProperty]
    private string _editTeacherEmployeeNumber = string.Empty;

    [ObservableProperty]
    private string _editTeacherFirstName = string.Empty;

    [ObservableProperty]
    private string _editTeacherLastName = string.Empty;

    [ObservableProperty]
    private string _editTeacherPhone = string.Empty;

    [ObservableProperty]
    private string _editTeacherEmail = string.Empty;

    [ObservableProperty]
    private string _editTeacherSpecialization = string.Empty;

    [ObservableProperty]
    private DateTime? _editTeacherHireDate;

    [ObservableProperty]
    private bool _editTeacherIsActive = true;

    [ObservableProperty]
    private string _newAcademicYearLabel = string.Empty;

    [ObservableProperty]
    private DateTime? _newAcademicYearStartDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _newAcademicYearEndDate = DateTime.Today.AddMonths(9);

    [ObservableProperty]
    private bool _newAcademicYearSetAsCurrent = true;

    public ObservableCollection<PedagogicalClassItemViewModel> PedagogicalClasses { get; } = [];

    public ObservableCollection<ClassLocalDto> ClassLocals { get; } = [];

    public ObservableCollection<ClassRoomDto> SubjectClasses { get; } = [];

    public ObservableCollection<CourseDto> SubjectCourses { get; } = [];

    public ObservableCollection<UserAccountDto> AdminUsers { get; } = [];

    public ObservableCollection<RoleDto> AdminRoles { get; } = [];

    public ObservableCollection<TeacherAdminDto> AdminTeachers { get; } = [];

    public AddressEditorViewModel NewAdminUserAddressEditor { get; }

    public AddressEditorViewModel SelectedAdminUserAddressEditor { get; }

    public AddressEditorViewModel NewTeacherAddressEditor { get; }

    public AddressEditorViewModel SelectedTeacherAddressEditor { get; }

    public IReadOnlyList<ProgramFilterItem> ProgramFilters { get; }

    public IReadOnlyList<SettingsNodeViewModel> SettingsNodes { get; }

    public DocumentBrandingViewModel DocumentBranding { get; }

    public GeographyAdminViewModel GeographyAdmin { get; }

    public SchoolFeeConfigurationViewModel SchoolFeeConfiguration { get; }

    public IReadOnlyList<AcademicYearDto> AcademicYears { get; private set; } = [];

    public bool IsEtablissementSelected => SelectedSettingsNode?.Section == SettingsSection.Etablissement;

    public bool IsStructurePedagogiqueSelected => SelectedSettingsNode?.Section == SettingsSection.StructurePedagogique;

    public bool IsAnneesScolairesSelected => SelectedSettingsNode?.Section == SettingsSection.AnneesScolaires;

    public bool IsFraisScolairesSelected => SelectedSettingsNode?.Section == SettingsSection.FraisScolaires;

    public bool IsScrollableSettingsContent => !IsStructurePedagogiqueSelected && !IsFraisScolairesSelected;

    public bool IsMatieresSelected => SelectedSettingsNode?.Section == SettingsSection.Matieres;

    public bool IsGeographieSelected => SelectedSettingsNode?.Section == SettingsSection.Geographie;

    public bool IsUtilisateursSelected => SelectedSettingsNode?.Section == SettingsSection.Utilisateurs;

    public bool IsEnseignantsSelected => SelectedSettingsNode?.Section == SettingsSection.Enseignants;

    public bool IsReglementSelected => SelectedSettingsNode?.Section == SettingsSection.Reglement;

    public bool IsPlaceholderSectionSelected => false;

    public string? ActiveNavKey { get; private set; }

    public string? ActiveNavTitle { get; private set; }

    public string SelectedSectionTitle => ActiveNavTitle ?? SelectedSettingsNode?.Title ?? "Paramètres";

    public string SelectedSectionDescription => ActiveNavKey switch
    {
        "frais-scolaires" => "Définissez les montants des frais par année scolaire, classe, type de frais et tranche.",
        _ => SelectedSettingsNode?.Section switch
        {
            SettingsSection.Etablissement => "Informations générales, logos, en-têtes, signatures et identité documentaire de l'établissement.",
            SettingsSection.Reglement => "Rédigez et enregistrez le règlement d'ordre intérieur de l'établissement.",
            SettingsSection.StructurePedagogique => "Activez uniquement les classes réellement organisées dans l'établissement. Toute la structure officielle RDC est déjà présente dans le système.",
            SettingsSection.AnneesScolaires => "Créez les années scolaires et définissez l'année courante utilisée dans les autres modules.",
            SettingsSection.FraisScolaires => "Définissez les montants des frais par année scolaire, classe, type de frais et tranche.",
            SettingsSection.Matieres => "Gérez les matières rattachées aux classes actives de l'établissement.",
            SettingsSection.Geographie => "Gérez les pays, provinces, villes et communes. Importez un fichier Excel selon le modèle fourni.",
            SettingsSection.Utilisateurs => "Gérez les comptes utilisateurs et l'affectation des rôles.",
            SettingsSection.Enseignants => "Gérez le personnel enseignant et leurs adresses.",
            _ => itemPlaceholderDescription(ActiveNavKey)
        }
    };

    public void ApplyNavigation(SettingsNavItem item)
    {
        ActiveNavKey = item.Key;
        ActiveNavTitle = item.Title;

        if (item.Section is SettingsSection section)
        {
            var node = SettingsNodes
                .SelectMany(group => group.Children)
                .FirstOrDefault(node => node.Section == section);

            if (node is not null)
            {
                SelectedSettingsNode = node;
            }
        }
        else
        {
            SelectedSettingsNode = null;
        }

        if (item.Key == "frais-scolaires")
        {
            SchoolFeeConfiguration.LoadCommand.Execute(null);
        }

        OnPropertyChanged(nameof(ActiveNavKey));
        OnPropertyChanged(nameof(ActiveNavTitle));
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionDescription));
        OnPropertyChanged(nameof(IsFraisScolairesSelected));
        OnPropertyChanged(nameof(IsScrollableSettingsContent));
    }

    private static string itemPlaceholderDescription(string? key) => key switch
    {
        "calendrier" => "Planification des périodes, vacances et événements scolaires.",
        "types-evaluations" => "Définition des types d'évaluations utilisés dans le système de notes.",
        "coefficients" => "Gestion des coefficients par matière et par niveau.",
        "sauvegarde" => "Sauvegarde et restauration des données de l'établissement.",
        "journal" => "Consultation du journal d'activités du système.",
        "parametres-systeme" => "Paramètres techniques et configuration système.",
        "personnalisation" => "Personnalisation de l'expérience utilisateur.",
        "design" => "Thème, couleurs et apparence de l'interface ERP.",
        _ => "Sélectionnez une rubrique."
    };

    partial void OnSelectedProgramFilterChanged(ProgramFilterItem? value)
    {
        if (IsStructurePedagogiqueSelected)
        {
            _ = LoadStructureAsync();
        }
    }

    partial void OnSelectedPedagogicalClassChanged(PedagogicalClassItemViewModel? value)
    {
        _ = LoadLocalsAsync();
    }

    partial void OnSelectedAcademicYearChanged(AcademicYearDto? value)
    {
        _ = LoadLocalsAsync();
        _ = LoadSubjectClassesAsync();
        if (IsStructurePedagogiqueSelected)
        {
            _ = LoadStructureAsync();
        }
    }

    partial void OnSelectedLocalChanged(ClassLocalDto? value)
    {
        if (value is null)
        {
            ClearLocalForm();
            return;
        }

        LocalName = value.LocalName;
        LocalCapacityText = value.MaxCapacity?.ToString();
        LocalObservations = value.Observations;
        LocalIsActive = value.IsActive;
    }

    partial void OnSelectedSubjectClassChanged(ClassRoomDto? value)
    {
        _ = LoadSubjectCoursesAsync();
    }

    partial void OnSelectedSettingsNodeChanged(SettingsNodeViewModel? value)
    {
        OnPropertyChanged(nameof(IsEtablissementSelected));
        OnPropertyChanged(nameof(IsStructurePedagogiqueSelected));
        OnPropertyChanged(nameof(IsAnneesScolairesSelected));
        OnPropertyChanged(nameof(IsFraisScolairesSelected));
        OnPropertyChanged(nameof(IsScrollableSettingsContent));
        OnPropertyChanged(nameof(IsMatieresSelected));
        OnPropertyChanged(nameof(IsGeographieSelected));
        OnPropertyChanged(nameof(IsUtilisateursSelected));
        OnPropertyChanged(nameof(IsEnseignantsSelected));
        OnPropertyChanged(nameof(IsReglementSelected));
        OnPropertyChanged(nameof(IsPlaceholderSectionSelected));
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionDescription));

        if (value?.Section == SettingsSection.StructurePedagogique)
        {
            _ = EnsureCurrentAcademicYearForStructureAsync();
            _ = LoadStructureAsync();
        }
        else if (value?.Section == SettingsSection.Reglement)
        {
            _ = LoadRegulationAsync();
        }
        else if (value?.Section == SettingsSection.Matieres)
        {
            _ = LoadSubjectClassesAsync();
        }
        else if (value?.Section == SettingsSection.Geographie)
        {
            GeographyAdmin.LoadCommand.Execute(null);
        }
        else if (value?.Section == SettingsSection.Utilisateurs)
        {
            _ = LoadUsersAsync();
        }
        else if (value?.Section == SettingsSection.Enseignants)
        {
            _ = LoadTeachersAsync();
        }
        else if (value?.Section == SettingsSection.AnneesScolaires)
        {
            _ = LoadAcademicYearsAsync();
        }
        else if (value?.Section == SettingsSection.Etablissement)
        {
            DocumentBranding.LoadCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var school = await _schoolApiService.GetCurrentSchoolAsync();
            if (school is not null)
            {
                Name = school.Name;
                LegalName = school.LegalName;
                City = school.City;
                Province = school.Province;
                Phone = school.Phone;
                Email = school.Email;
                DefaultCurrency = school.DefaultCurrency;
            }

            await LoadAcademicYearsAsync();

            await LoadStructureAsync();
            await LoadRegulationAsync();
            await LoadFeeTypesAsync();
            await LoadSubjectClassesAsync();
            await LoadUsersAsync();
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
    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            await _schoolApiService.UpdateSchoolAsync(new UpdateSchoolRequest(
                Name, LegalName, null, City, Province, Phone, Email, DefaultCurrency));
            StatusMessage = "Paramètres enregistrés.";
            await LoadAsync();
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
    private async Task LoadAcademicYearsAsync()
    {
        try
        {
            AcademicYears = await _schoolApiService.GetAcademicYearsAsync();
            OnPropertyChanged(nameof(AcademicYears));
            SelectedAcademicYear ??= AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();
            if (SelectedAcademicYear is not null)
            {
                SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.Id == SelectedAcademicYear.Id) ?? SelectedAcademicYear;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CreateAcademicYearAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAcademicYearLabel) || NewAcademicYearStartDate is null || NewAcademicYearEndDate is null)
        {
            StatusMessage = "Complétez le libellé et les dates de l'année scolaire.";
            return;
        }

        IsBusy = true;
        try
        {
            var setAsCurrent = NewAcademicYearSetAsCurrent;
            var year = await _schoolApiService.CreateAcademicYearAsync(new CreateAcademicYearRequest(
                NewAcademicYearLabel.Trim(),
                DateOnly.FromDateTime(NewAcademicYearStartDate.Value),
                DateOnly.FromDateTime(NewAcademicYearEndDate.Value),
                setAsCurrent));

            NewAcademicYearLabel = string.Empty;
            NewAcademicYearStartDate = DateTime.Today;
            NewAcademicYearEndDate = DateTime.Today.AddMonths(9);
            NewAcademicYearSetAsCurrent = true;
            StatusMessage = "Année scolaire créée.";
            await LoadAcademicYearsAsync();
            SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.Id == year.Id) ?? SelectedAcademicYear;
            if (setAsCurrent || year.IsCurrent)
            {
                AcademicYearRefreshBridge.NotifyCurrentYearChanged();
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
    private async Task SetCurrentAcademicYearAsync()
    {
        if (SelectedAcademicYear is null)
        {
            StatusMessage = "Sélectionnez une année scolaire.";
            return;
        }

        IsBusy = true;
        try
        {
            await _schoolApiService.SetCurrentAcademicYearAsync(SelectedAcademicYear.Id);
            StatusMessage = "Année courante mise à jour.";
            await LoadAcademicYearsAsync();
            AcademicYearRefreshBridge.NotifyCurrentYearChanged();
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
    private async Task SearchClassesAsync()
    {
        await LoadStructureAsync();
    }

    [RelayCommand]
    private async Task SaveClassesAsync()
    {
        if (PedagogicalClasses.Count == 0)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var request = new BulkUpdatePedagogicalClassesRequest(
                PedagogicalClasses.Select(c => new BulkPedagogicalClassItem(
                    c.Id, c.IsEnabled, c.MinAge, c.MaxAge)).ToList());

            var updated = await _schoolApiService.BulkUpdatePedagogicalClassesAsync(request);
            ApplyClasses(updated);
            StatusMessage = "Classes pédagogiques enregistrées.";
            await RefreshSummaryAsync();
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
    private async Task AddLocalAsync()
    {
        if (SelectedPedagogicalClass is null || SelectedAcademicYear is null)
        {
            StatusMessage = "Sélectionnez une classe et une année scolaire.";
            return;
        }

        if (string.IsNullOrWhiteSpace(LocalName))
        {
            StatusMessage = "Le nom du local est obligatoire.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            int? capacity = int.TryParse(LocalCapacityText, out var cap) ? cap : null;
            await _schoolApiService.CreateClassLocalAsync(new CreateClassLocalRequest(
                SelectedPedagogicalClass.Id,
                SelectedAcademicYear.Id,
                LocalName.Trim(),
                capacity,
                LocalObservations));

            ClearLocalForm();
            await LoadLocalsAsync();
            await LoadStructureAsync();
            StatusMessage = "Local ajouté.";
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
    private async Task UpdateLocalAsync()
    {
        if (SelectedLocal is null)
        {
            StatusMessage = "Sélectionnez un local à modifier.";
            return;
        }

        if (string.IsNullOrWhiteSpace(LocalName))
        {
            StatusMessage = "Le nom du local est obligatoire.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            int? capacity = int.TryParse(LocalCapacityText, out var cap) ? cap : null;
            await _schoolApiService.UpdateClassLocalAsync(
                SelectedLocal.Id,
                new UpdateClassLocalRequest(LocalName.Trim(), capacity, LocalObservations, LocalIsActive));

            await LoadLocalsAsync();
            await LoadStructureAsync();
            StatusMessage = "Local mis à jour.";
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
    private async Task DeleteLocalAsync()
    {
        if (SelectedLocal is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            await _schoolApiService.DeleteClassLocalAsync(SelectedLocal.Id);
            SelectedLocal = null;
            ClearLocalForm();
            await LoadLocalsAsync();
            await LoadStructureAsync();
            StatusMessage = "Local supprimé.";
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
    private void NewLocal()
    {
        SelectedLocal = null;
        ClearLocalForm();
    }

    [RelayCommand]
    private async Task LoadRegulationAsync()
    {
        try
        {
            var regulation = await _schoolApiService.GetRegulationAsync();
            RegulationContent = regulation.Content;
            RegulationUpdatedAt = regulation.UpdatedAt;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveRegulationAsync()
    {
        IsBusy = true;
        try
        {
            var regulation = await _schoolApiService.UpdateRegulationAsync(
                new UpdateSchoolRegulationRequest(RegulationContent));
            RegulationContent = regulation.Content;
            RegulationUpdatedAt = regulation.UpdatedAt;
            StatusMessage = "Règlement enregistré.";
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
    private async Task LoadFeeTypesAsync()
    {
        try
        {
            var lookups = await _schoolApiService.GetLookupsAsync();
            FeeTypes = lookups.FeeTypes;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadSubjectClassesAsync()
    {
        if (SelectedAcademicYear is null)
        {
            return;
        }

        try
        {
            var classes = await _academicApiService.GetClassRoomsAsync(SelectedAcademicYear.Id);
            var selectedId = SelectedSubjectClass?.Id;
            SubjectClasses.Clear();
            foreach (var item in classes)
            {
                SubjectClasses.Add(item);
            }

            SelectedSubjectClass = selectedId.HasValue
                ? SubjectClasses.FirstOrDefault(c => c.Id == selectedId.Value)
                : SubjectClasses.FirstOrDefault();

            if (SelectedSubjectClass is null)
            {
                SubjectCourses.Clear();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoadSubjectCoursesAsync()
    {
        if (SelectedSubjectClass is null)
        {
            SubjectCourses.Clear();
            return;
        }

        try
        {
            var courses = await _academicApiService.GetCoursesAsync(SelectedSubjectClass.Id);
            SubjectCourses.Clear();
            foreach (var course in courses)
            {
                SubjectCourses.Add(course);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CreateSubjectCourseAsync()
    {
        if (SelectedSubjectClass is null || string.IsNullOrWhiteSpace(NewSubjectCode) || string.IsNullOrWhiteSpace(NewSubjectName))
        {
            StatusMessage = "Sélectionnez une classe et renseignez le code ainsi que le nom de la matière.";
            return;
        }

        IsBusy = true;
        try
        {
            await _academicApiService.CreateCourseAsync(new CreateCourseRequest(
                SelectedSubjectClass.Id,
                NewSubjectCode.Trim(),
                NewSubjectName.Trim(),
                1,
                20));

            NewSubjectCode = string.Empty;
            NewSubjectName = string.Empty;
            StatusMessage = "Matière créée.";
            await LoadSubjectCoursesAsync();
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
    private async Task LoadUsersAsync()
    {
        try
        {
            var users = await _adminApiService.GetUsersAsync();
            var roles = await _adminApiService.GetRolesAsync();

            AdminUsers.Clear();
            foreach (var user in users)
            {
                AdminUsers.Add(user);
            }

            AdminRoles.Clear();
            foreach (var role in roles)
            {
                AdminRoles.Add(role);
            }

            SelectedAdminRole ??= AdminRoles.FirstOrDefault();
            SelectedAdminUser ??= AdminUsers.FirstOrDefault();
            await LoadSelectedAdminUserAddressAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    partial void OnSelectedAdminUserChanged(UserAccountDto? value) =>
        _ = LoadSelectedAdminUserAddressAsync();

    private async Task LoadSelectedAdminUserAddressAsync()
    {
        SelectedAdminUserAddressEditor.Reset();
        if (SelectedAdminUser?.AddressId is not Guid addressId)
        {
            return;
        }

        try
        {
            var address = await _geographyApiService.GetAddressAsync(addressId);
            await SelectedAdminUserAddressEditor.LoadFromDtoAsync(address);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SaveSelectedAdminUserAsync()
    {
        if (SelectedAdminUser is null)
        {
            return;
        }

        var parts = SelectedAdminUser.FullName.Split(' ', 2);
        IsBusy = true;
        try
        {
            await _adminApiService.UpdateUserAsync(SelectedAdminUser.Id, new UpdateUserRequest(
                SelectedAdminUser.Email,
                parts.Length > 1 ? parts[1] : parts[0],
                parts[0],
                SelectedAdminUser.IsActive,
                SelectedAdminUserAddressEditor.ToInputDto(),
                UpdateAddress: true));

            StatusMessage = "Utilisateur mis à jour.";
            await LoadUsersAsync();
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
    private async Task LoadTeachersAsync()
    {
        try
        {
            var teachers = await _adminApiService.GetTeachersAsync();
            AdminTeachers.Clear();
            foreach (var teacher in teachers)
            {
                AdminTeachers.Add(teacher);
            }

            SelectedAdminTeacher ??= AdminTeachers.FirstOrDefault();
            await LoadSelectedTeacherFormAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    partial void OnSelectedAdminTeacherChanged(TeacherAdminDto? value) =>
        _ = LoadSelectedTeacherFormAsync();

    private async Task LoadSelectedTeacherFormAsync()
    {
        SelectedTeacherAddressEditor.Reset();
        if (SelectedAdminTeacher is null)
        {
            EditTeacherEmployeeNumber = string.Empty;
            EditTeacherFirstName = string.Empty;
            EditTeacherLastName = string.Empty;
            EditTeacherPhone = string.Empty;
            EditTeacherEmail = string.Empty;
            EditTeacherSpecialization = string.Empty;
            EditTeacherHireDate = null;
            EditTeacherIsActive = true;
            return;
        }

        EditTeacherEmployeeNumber = SelectedAdminTeacher.EmployeeNumber;
        EditTeacherFirstName = SelectedAdminTeacher.FirstName;
        EditTeacherLastName = SelectedAdminTeacher.LastName;
        EditTeacherPhone = SelectedAdminTeacher.Phone ?? string.Empty;
        EditTeacherEmail = SelectedAdminTeacher.Email ?? string.Empty;
        EditTeacherSpecialization = SelectedAdminTeacher.Specialization ?? string.Empty;
        EditTeacherHireDate = SelectedAdminTeacher.HireDate?.ToDateTime(TimeOnly.MinValue);
        EditTeacherIsActive = SelectedAdminTeacher.IsActive;

        if (SelectedAdminTeacher.AddressId is Guid addressId)
        {
            try
            {
                var address = await _geographyApiService.GetAddressAsync(addressId);
                await SelectedTeacherAddressEditor.LoadFromDtoAsync(address);
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
            }
        }
    }

    [RelayCommand]
    private async Task CreateAdminTeacherAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTeacherEmployeeNumber)
            || string.IsNullOrWhiteSpace(NewTeacherFirstName)
            || string.IsNullOrWhiteSpace(NewTeacherLastName))
        {
            StatusMessage = "Complétez le matricule, le nom et le postnom de l'enseignant.";
            return;
        }

        IsBusy = true;
        try
        {
            await _adminApiService.CreateTeacherAsync(new CreateTeacherAdminRequest(
                NewTeacherEmployeeNumber.Trim(),
                NewTeacherFirstName.Trim(),
                NewTeacherLastName.Trim(),
                string.IsNullOrWhiteSpace(NewTeacherPhone) ? null : NewTeacherPhone.Trim(),
                string.IsNullOrWhiteSpace(NewTeacherEmail) ? null : NewTeacherEmail.Trim(),
                string.IsNullOrWhiteSpace(NewTeacherSpecialization) ? null : NewTeacherSpecialization.Trim(),
                NewTeacherHireDate.HasValue ? DateOnly.FromDateTime(NewTeacherHireDate.Value) : null,
                NewTeacherAddressEditor.HasContent() ? NewTeacherAddressEditor.ToInputDto() : null));

            NewTeacherEmployeeNumber = string.Empty;
            NewTeacherFirstName = string.Empty;
            NewTeacherLastName = string.Empty;
            NewTeacherPhone = string.Empty;
            NewTeacherEmail = string.Empty;
            NewTeacherSpecialization = string.Empty;
            NewTeacherHireDate = DateTime.Today;
            NewTeacherAddressEditor.Reset();
            StatusMessage = "Enseignant créé.";
            await LoadTeachersAsync();
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
    private async Task SaveSelectedAdminTeacherAsync()
    {
        if (SelectedAdminTeacher is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditTeacherEmployeeNumber)
            || string.IsNullOrWhiteSpace(EditTeacherFirstName)
            || string.IsNullOrWhiteSpace(EditTeacherLastName))
        {
            StatusMessage = "Complétez le matricule, le nom et le postnom de l'enseignant.";
            return;
        }

        IsBusy = true;
        try
        {
            await _adminApiService.UpdateTeacherAsync(SelectedAdminTeacher.Id, new UpdateTeacherAdminRequest(
                EditTeacherEmployeeNumber.Trim(),
                EditTeacherFirstName.Trim(),
                EditTeacherLastName.Trim(),
                string.IsNullOrWhiteSpace(EditTeacherPhone) ? null : EditTeacherPhone.Trim(),
                string.IsNullOrWhiteSpace(EditTeacherEmail) ? null : EditTeacherEmail.Trim(),
                string.IsNullOrWhiteSpace(EditTeacherSpecialization) ? null : EditTeacherSpecialization.Trim(),
                EditTeacherHireDate.HasValue ? DateOnly.FromDateTime(EditTeacherHireDate.Value) : null,
                EditTeacherIsActive,
                SelectedTeacherAddressEditor.ToInputDto(),
                UpdateAddress: true));

            StatusMessage = "Enseignant mis à jour.";
            await LoadTeachersAsync();
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
    private async Task ToggleAdminTeacherActiveAsync()
    {
        if (SelectedAdminTeacher is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _adminApiService.UpdateTeacherAsync(SelectedAdminTeacher.Id, new UpdateTeacherAdminRequest(
                SelectedAdminTeacher.EmployeeNumber,
                SelectedAdminTeacher.FirstName,
                SelectedAdminTeacher.LastName,
                SelectedAdminTeacher.Phone,
                SelectedAdminTeacher.Email,
                SelectedAdminTeacher.Specialization,
                SelectedAdminTeacher.HireDate,
                !SelectedAdminTeacher.IsActive,
                null,
                UpdateAddress: false));

            StatusMessage = SelectedAdminTeacher.IsActive ? "Enseignant désactivé." : "Enseignant activé.";
            await LoadTeachersAsync();
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
    private async Task CreateAdminUserAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAdminUserName)
            || string.IsNullOrWhiteSpace(NewAdminPassword)
            || string.IsNullOrWhiteSpace(NewAdminFirstName)
            || string.IsNullOrWhiteSpace(NewAdminLastName))
        {
            StatusMessage = "Complétez les champs obligatoires de l'utilisateur.";
            return;
        }

        IsBusy = true;
        try
        {
            var roleIds = SelectedAdminRole is not null ? new List<Guid> { SelectedAdminRole.Id } : [];
            await _adminApiService.CreateUserAsync(new CreateUserRequest(
                NewAdminUserName.Trim(),
                NewAdminEmail.Trim(),
                NewAdminPassword,
                NewAdminFirstName.Trim(),
                NewAdminLastName.Trim(),
                roleIds,
                NewAdminUserAddressEditor.HasContent() ? NewAdminUserAddressEditor.ToInputDto() : null));

            NewAdminUserName = string.Empty;
            NewAdminEmail = string.Empty;
            NewAdminPassword = string.Empty;
            NewAdminFirstName = string.Empty;
            NewAdminLastName = string.Empty;
            NewAdminUserAddressEditor.Reset();
            StatusMessage = "Utilisateur créé.";
            await LoadUsersAsync();
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
    private async Task AssignAdminRoleAsync()
    {
        if (SelectedAdminUser is null || SelectedAdminRole is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _adminApiService.SetUserRolesAsync(SelectedAdminUser.Id, new SetUserRolesRequest([SelectedAdminRole.Id]));
            StatusMessage = $"Rôle {SelectedAdminRole.Name} assigné.";
            await LoadUsersAsync();
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
    private async Task ToggleAdminUserActiveAsync()
    {
        if (SelectedAdminUser is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var wasActive = SelectedAdminUser.IsActive;
            var parts = SelectedAdminUser.FullName.Split(' ', 2);
            await _adminApiService.UpdateUserAsync(SelectedAdminUser.Id, new UpdateUserRequest(
                SelectedAdminUser.Email,
                parts.Length > 1 ? parts[1] : parts[0],
                parts[0],
                !wasActive));

            StatusMessage = wasActive ? "Utilisateur désactivé." : "Utilisateur activé.";
            await LoadUsersAsync();
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

    private async Task EnsureCurrentAcademicYearForStructureAsync()
    {
        if (AcademicYears.Count == 0)
        {
            await LoadAcademicYearsAsync();
        }

        SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? SelectedAcademicYear;
    }

    private async Task LoadStructureAsync()
    {
        try
        {
            await EnsureCurrentAcademicYearForStructureAsync();

            var classes = await _schoolApiService.GetPedagogicalClassesAsync(
                string.IsNullOrWhiteSpace(ClassSearch) ? null : ClassSearch.Trim(),
                SelectedProgramFilter?.Program,
                academicYearId: SelectedAcademicYear?.Id);

            ApplyClasses(classes);
            await RefreshSummaryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task LoadLocalsAsync()
    {
        ClassLocals.Clear();
        if (SelectedPedagogicalClass is null)
        {
            return;
        }

        try
        {
            var locals = await _schoolApiService.GetClassLocalsAsync(
                SelectedPedagogicalClass.Id,
                SelectedAcademicYear?.Id);

            foreach (var local in locals)
            {
                ClassLocals.Add(local);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RefreshSummaryAsync()
    {
        var summary = await _schoolApiService.GetPedagogicalSummaryAsync(SelectedAcademicYear?.Id);
        var yearLabel = SelectedAcademicYear?.Label ?? "année courante";
        StructureSummary = $"{summary.EnabledClasses} / {summary.TotalClasses} classes actives — {summary.TotalLocals} locaux ({yearLabel})";
    }

    private void ApplyClasses(IReadOnlyList<PedagogicalClassDto> classes)
    {
        var selectedId = SelectedPedagogicalClass?.Id;
        PedagogicalClasses.Clear();

        foreach (var item in classes)
        {
            PedagogicalClasses.Add(new PedagogicalClassItemViewModel(item));
        }

        SelectedPedagogicalClass = selectedId.HasValue
            ? PedagogicalClasses.FirstOrDefault(c => c.Id == selectedId.Value)
            : PedagogicalClasses.FirstOrDefault(c => c.IsEnabled);
    }

    private void ClearLocalForm()
    {
        LocalName = string.Empty;
        LocalCapacityText = null;
        LocalObservations = null;
        LocalIsActive = true;
    }
}

public enum SettingsSection
{
    Etablissement = 1,
    Reglement = 2,
    StructurePedagogique = 3,
    AnneesScolaires = 4,
    FraisScolaires = 5,
    Matieres = 6,
    Geographie = 9,
    Utilisateurs = 7,
    Enseignants = 8
}

public sealed record ProgramFilterItem(SchoolProgram? Program, string Label);

public partial class SettingsNodeViewModel : ObservableObject
{
    public SettingsNodeViewModel(
        string title,
        string iconKind,
        SettingsSection? section,
        bool isExpanded = false,
        IReadOnlyList<SettingsNodeViewModel>? children = null)
    {
        Title = title;
        IconKind = iconKind;
        Section = section;
        _isExpanded = isExpanded;
        Children = new ObservableCollection<SettingsNodeViewModel>(children ?? []);
    }

    public string Title { get; }

    public string IconKind { get; }

    public SettingsSection? Section { get; }

    public ObservableCollection<SettingsNodeViewModel> Children { get; }

    [ObservableProperty]
    private bool _isExpanded;
}

public partial class PedagogicalClassItemViewModel : ObservableObject
{
    public PedagogicalClassItemViewModel(PedagogicalClassDto dto)
    {
        Id = dto.Id;
        TemplateCode = dto.TemplateCode;
        ProgramLabel = dto.ProgramLabel;
        DisplayName = dto.DisplayName;
        HumanitiesSection = dto.HumanitiesSection;
        StudyOption = dto.StudyOption;
        LocalCount = dto.LocalCount;
        _isEnabled = dto.IsEnabled;
        _minAge = dto.MinAge;
        _maxAge = dto.MaxAge;
    }

    public Guid Id { get; }

    public string TemplateCode { get; }

    public string ProgramLabel { get; }

    public string DisplayName { get; }

    public string? HumanitiesSection { get; }

    public string? StudyOption { get; }

    public int LocalCount { get; private set; }

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private int? _minAge;

    [ObservableProperty]
    private int? _maxAge;

    public string DetailLine => string.IsNullOrWhiteSpace(StudyOption)
        ? ProgramLabel
        : $"{HumanitiesSection} — {StudyOption}";
}
