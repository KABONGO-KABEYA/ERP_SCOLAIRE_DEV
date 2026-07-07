using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class EnrollmentWizardViewModel : ViewModelBase
{
    private const int TotalSteps = 6;

    private static readonly string[] NewEnrollmentStepTitles =
    [
        "Identité", "Scolarité", "Responsables", "Santé", "Documents", "Validation"
    ];

    private static readonly string[] ReinscriptionStepTitles =
    [
        "Recherche", "Scolarité", "Responsables", "Santé", "Documents", "Validation"
    ];

    private static readonly string[] NewEnrollmentStepDetailNames =
    [
        "Informations personnelles",
        "Affectation scolaire",
        "Responsables légaux",
        "Informations médicales",
        "Pièces justificatives",
        "Validation du dossier"
    ];

    private static readonly string[] ReinscriptionStepDetailNames =
    [
        "Recherche d'élève",
        "Affectation scolaire",
        "Responsables légaux",
        "Informations médicales",
        "Pièces justificatives",
        "Validation du dossier"
    ];

    private static readonly string[] NewEnrollmentStepGuidances =
    [
        "Renseignez les informations d'état civil du nouvel élève.",
        "Sélectionnez la structure pédagogique déjà configurée dans votre établissement.",
        "Indiquez les personnes légalement responsables de l'élève (principal, secondaire, urgence, récupération).",
        "Complétez les informations médicales utiles en cas d'urgence.",
        "Déposez les pièces justificatives nécessaires.",
        "Vérifiez le dossier complet. Les frais scolaires seront traités séparément."
    ];

    private static readonly string[] ReinscriptionStepGuidances =
    [
        "Recherchez l'élève à réinscrire pour la nouvelle année scolaire.",
        "Sélectionnez la nouvelle affectation scolaire de l'élève.",
        "Vérifiez ou mettez à jour les responsables légaux.",
        "Vérifiez ou mettez à jour les informations médicales.",
        "Vérifiez les pièces justificatives du dossier.",
        "Vérifiez le dossier complet. Les frais scolaires seront traités séparément."
    ];

    private enum WizardContentStep
    {
        Search,
        Identity,
        Scolarite,
        Responsables,
        Sante,
        Documents,
        Validation
    }

    private readonly IEnrollmentWizardApiService _wizardApi;
    private readonly INavigationService _navigationService;
    private readonly List<EnrollmentClassOptionDto> _allClasses = [];
    private readonly EnrollmentWizardEntryMode _entryMode;

    public EnrollmentWizardViewModel(
        IEnrollmentWizardApiService wizardApi,
        INavigationService navigationService)
    {
        _wizardApi = wizardApi;
        _navigationService = navigationService;
        _entryMode = EnrollmentWizardNavigationBridge.ConsumeMode();
        InitializeDocuments();
        InitializeSteps();
        _ = InitializeAsync();
    }

    public EnrollmentWizardEntryMode EntryMode => _entryMode;
    public bool IsReinscriptionMode => EntryMode == EnrollmentWizardEntryMode.Reinscription;
    public string PageTitle => IsReinscriptionMode ? "Réinscription" : "Nouvelle inscription";
    public string PageSubtitle => IsReinscriptionMode
        ? "Recherche et réinscription d'un élève existant pour la nouvelle année scolaire"
        : "Enregistrement complet du dossier d'un nouvel élève — les frais scolaires sont traités séparément";
    public bool ShowRegistrationKindPicker => !IsReinscriptionMode;

    public ObservableCollection<EnrollmentWizardStepItem> WizardSteps { get; } = [];
    public ObservableCollection<EnrollmentStudentSearchResultDto> SearchResults { get; } = [];
    public ObservableCollection<SectionDto> Sections { get; } = [];
    public ObservableCollection<string> StudyOptions { get; } = [];
    public ObservableCollection<PedagogicalClassPickerItem> PedagogicalClassOptions { get; } = [];
    public ObservableCollection<EnrollmentClassOptionDto> AvailableLocals { get; } = [];
    public ObservableCollection<EnrollmentDocumentItemViewModel> Documents { get; } = [];
    public ObservableCollection<EnrollmentFeeLineDto> FeeLines { get; } = [];
    public ObservableCollection<EnrollmentPrerequisiteIssueDto> PrerequisiteIssues { get; } = [];

    [ObservableProperty] private int _currentStep = 1;
    [ObservableProperty] private bool _prerequisitesReady;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _validationMessage;
    [ObservableProperty] private bool _confirmAccuracy;
    [ObservableProperty] private bool _hasSearched;
    [ObservableProperty] private bool _searchHasNoResults;
    [ObservableProperty] private bool _step1Completed;
    [ObservableProperty] private EnrollmentStudentSearchResultDto? _selectedSearchResult;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private Guid? _existingStudentId;

    [ObservableProperty] private string _registrationNumber = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _middleName = string.Empty;
    [ObservableProperty] private Gender _gender = Gender.Masculin;
    [ObservableProperty] private DateTime _dateOfBirth = DateTime.Today.AddYears(-8);
    [ObservableProperty] private string _placeOfBirth = string.Empty;
    [ObservableProperty] private string _nationality = "Congolaise";
    [ObservableProperty] private string _country = "RDC";
    [ObservableProperty] private string _province = string.Empty;
    [ObservableProperty] private string _territory = string.Empty;
    [ObservableProperty] private string _city = string.Empty;
    [ObservableProperty] private string _language = string.Empty;
    [ObservableProperty] private string _religion = string.Empty;
    [ObservableProperty] private string? _photoPath;
    [ObservableProperty] private string _permanentNumber = string.Empty;

    [ObservableProperty] private Guid? _academicYearId;
    [ObservableProperty] private string _academicYearLabel = string.Empty;
    [ObservableProperty] private Guid? _selectedSectionId;
    [ObservableProperty] private string? _selectedStudyOption;
    [ObservableProperty] private Guid? _selectedPedagogicalClassId;
    [ObservableProperty] private EnrollmentClassOptionDto? _selectedClass;
    [ObservableProperty] private int? _orderNumber;
    [ObservableProperty] private DateTime _enrollmentDate = DateTime.Today;
    [ObservableProperty] private RegistrationKind _registrationKind = RegistrationKind.NouvelleInscription;
    [ObservableProperty] private string _previousSchool = string.Empty;
    [ObservableProperty] private string _previousStudentCode = string.Empty;
    [ObservableProperty] private string _classCapacityInfo = string.Empty;
    [ObservableProperty] private string? _ageCompatibilityMessage;
    [ObservableProperty] private bool _ageCompatibilityOk = true;
    [ObservableProperty] private string? _smartAlertMessage;
    [ObservableProperty] private bool _smartAlertIsWarning;

    [ObservableProperty] private string _primaryLastName = string.Empty;
    [ObservableProperty] private string _primaryFirstName = string.Empty;
    [ObservableProperty] private string _primaryPhone = string.Empty;
    [ObservableProperty] private string _primaryEmail = string.Empty;
    [ObservableProperty] private string _primaryAddress = string.Empty;
    [ObservableProperty] private string _primaryProfession = string.Empty;
    [ObservableProperty] private string _primaryEmployer = string.Empty;
    [ObservableProperty] private string _secondaryLastName = string.Empty;
    [ObservableProperty] private string _secondaryFirstName = string.Empty;
    [ObservableProperty] private string _secondaryPhone = string.Empty;
    [ObservableProperty] private string _secondaryEmail = string.Empty;
    [ObservableProperty] private string _emergencyName = string.Empty;
    [ObservableProperty] private string _emergencyPhone = string.Empty;
    [ObservableProperty] private string _emergencyRelationship = string.Empty;
    [ObservableProperty] private string _pickupName = string.Empty;
    [ObservableProperty] private string _pickupPhone = string.Empty;
    [ObservableProperty] private string _pickupRelationship = string.Empty;

    [ObservableProperty] private string _bloodGroup = string.Empty;
    [ObservableProperty] private string _allergies = string.Empty;
    [ObservableProperty] private string _chronicDiseases = string.Empty;
    [ObservableProperty] private string _treatment = string.Empty;
    [ObservableProperty] private string _doctorName = string.Empty;
    [ObservableProperty] private string _medicalCenter = string.Empty;
    [ObservableProperty] private string _disability = string.Empty;
    [ObservableProperty] private string _medicalObservations = string.Empty;
    [ObservableProperty] private bool _medicalEmergency;

    [ObservableProperty] private decimal _totalDue;
    [ObservableProperty] private string _wizardStatus = "Brouillon";
    [ObservableProperty] private int _completedDocumentsCount;
    [ObservableProperty] private int _progressPercent;

    public bool ShowPrerequisiteBlock => !PrerequisitesReady;
    public int Age => CalculateAge(DateOnly.FromDateTime(DateOfBirth), DateOnly.FromDateTime(EnrollmentDate));
    public string AgeCategory => Age < 18 ? "Mineur" : "Majeur";
    public string DisplayName => string.IsNullOrWhiteSpace(LastName) ? "—" : $"{LastName} {FirstName}".Trim();
    public string FullDisplayName => string.IsNullOrWhiteSpace(MiddleName)
        ? DisplayName
        : $"{LastName} {MiddleName} {FirstName}".Trim();
    public string SummarySection => Sections.FirstOrDefault(s => s.Id == SelectedSectionId)?.Name ?? "—";
    public string SummaryOption => SelectedStudyOption ?? "—";
    public string SummaryPedagogicalClass => SelectedClass?.PedagogicalDisplayName ?? PedagogicalClassOptions
        .FirstOrDefault(p => p.Id == SelectedPedagogicalClassId)?.DisplayName ?? "—";
    public string SummaryLocal => SelectedClass?.LocalName ?? "—";
    public string SummaryClass => SelectedClass?.FullDisplayName ?? "—";
    public string SummaryGuardian => string.IsNullOrWhiteSpace(PrimaryLastName) ? "—" : $"{PrimaryLastName} {PrimaryFirstName}".Trim();
    public string SummaryPhone => string.IsNullOrWhiteSpace(PrimaryPhone) ? "—" : PrimaryPhone;
    public string ProgressStepLabel => $"Étape {CurrentStep} sur {TotalSteps}";
    public string CurrentStepDetailName => GetStepDetailNames()[Math.Clamp(CurrentStep - 1, 0, TotalSteps - 1)];
    public string CurrentStepGuidance => GetStepGuidances()[Math.Clamp(CurrentStep - 1, 0, TotalSteps - 1)];
    public string SummaryFeesStatus => "À traiter ultérieurement (module Paiements)";
    public string DocumentsProgressLabel => $"{CompletedDocumentsCount}/{Documents.Count}";
    public bool ShowSectionPicker => true;
    public bool ShowOptionPicker => SelectedSectionId.HasValue;
    public bool ShowClassPicker => !string.IsNullOrWhiteSpace(SelectedStudyOption);
    public bool ShowLocalPicker => SelectedPedagogicalClassId.HasValue;

    public bool ShowSearchStep => GetCurrentContentStep() == WizardContentStep.Search;
    public bool ShowIdentityStep => GetCurrentContentStep() == WizardContentStep.Identity;
    public bool ShowScolariteStep => GetCurrentContentStep() == WizardContentStep.Scolarite;
    public bool ShowResponsablesStep => GetCurrentContentStep() == WizardContentStep.Responsables;
    public bool ShowSanteStep => GetCurrentContentStep() == WizardContentStep.Sante;
    public bool ShowDocumentsStep => GetCurrentContentStep() == WizardContentStep.Documents;
    public bool ShowValidationStep => GetCurrentContentStep() == WizardContentStep.Validation;

    public bool CanGoPrevious => CurrentStep > 1 && !IsBusy;
    public bool CanGoNext => PrerequisitesReady && CurrentStep < TotalSteps && !IsBusy && IsCurrentStepValid();
    public bool ShowNextStep => CurrentStep < TotalSteps;
    public bool ShowFinalize => CurrentStep == TotalSteps;

    public IReadOnlyList<RegistrationKind> RegistrationKinds { get; } = Enum.GetValues<RegistrationKind>();
    public IReadOnlyList<Gender> Genders { get; } = Enum.GetValues<Gender>();

    partial void OnPrerequisitesReadyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowPrerequisiteBlock));
        NotifyNavigationState();
    }

    partial void OnIsBusyChanged(bool value) => NotifyNavigationState();

    partial void OnCurrentStepChanged(int value)
    {
        NotifyStepFlags();
        UpdateWizardSteps();
        UpdateProgress();
        ValidationMessage = null;
        if (GetCurrentContentStep() == WizardContentStep.Scolarite)
        {
            _ = LoadStructureAsync();
        }
    }

    partial void OnSelectedSectionIdChanged(Guid? value)
    {
        SelectedStudyOption = null;
        SelectedPedagogicalClassId = null;
        SelectedClass = null;
        RefreshStudyOptions();
        NotifySummaryProperties();
        NotifyNavigationState();
    }

    partial void OnSelectedStudyOptionChanged(string? value)
    {
        SelectedPedagogicalClassId = null;
        SelectedClass = null;
        RefreshPedagogicalClasses();
        NotifySummaryProperties();
        NotifyNavigationState();
    }

    partial void OnSelectedPedagogicalClassIdChanged(Guid? value)
    {
        SelectedClass = null;
        RefreshAvailableLocals();
        NotifySummaryProperties();
        NotifyNavigationState();
    }

    partial void OnSelectedClassChanged(EnrollmentClassOptionDto? value)
    {
        NotifySummaryProperties();
        _ = RefreshCapacityAsync();
        UpdateAgeCompatibility();
        NotifyNavigationState();
    }

    partial void OnDateOfBirthChanged(DateTime value)
    {
        OnPropertyChanged(nameof(Age));
        OnPropertyChanged(nameof(AgeCategory));
        UpdateAgeCompatibility();
        NotifyNavigationState();
    }

    partial void OnLastNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FullDisplayName));
        UpdateProgress();
        NotifyNavigationState();
    }

    partial void OnFirstNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FullDisplayName));
        UpdateProgress();
        NotifyNavigationState();
    }
    partial void OnMiddleNameChanged(string value) => OnPropertyChanged(nameof(FullDisplayName));
    partial void OnPrimaryLastNameChanged(string value) { OnPropertyChanged(nameof(SummaryGuardian)); UpdateProgress(); NotifyNavigationState(); }
    partial void OnPrimaryFirstNameChanged(string value) { OnPropertyChanged(nameof(SummaryGuardian)); UpdateProgress(); NotifyNavigationState(); }
    partial void OnPrimaryPhoneChanged(string value) { OnPropertyChanged(nameof(SummaryPhone)); UpdateProgress(); NotifyNavigationState(); }
    partial void OnPrimaryEmailChanged(string value) => NotifyNavigationState();
    partial void OnStep1CompletedChanged(bool value) => NotifyNavigationState();
    partial void OnPhotoPathChanged(string? value) => OnPropertyChanged(nameof(PhotoPath));

    private void InitializeSteps()
    {
        WizardSteps.Clear();
        var titles = GetStepTitles();
        for (var i = 0; i < titles.Length; i++)
        {
            WizardSteps.Add(new EnrollmentWizardStepItem(i + 1, titles[i], i == titles.Length - 1));
        }

        UpdateWizardSteps();
    }

    private string[] GetStepTitles() =>
        IsReinscriptionMode ? ReinscriptionStepTitles : NewEnrollmentStepTitles;

    private string[] GetStepDetailNames() =>
        IsReinscriptionMode ? ReinscriptionStepDetailNames : NewEnrollmentStepDetailNames;

    private string[] GetStepGuidances() =>
        IsReinscriptionMode ? ReinscriptionStepGuidances : NewEnrollmentStepGuidances;

    private WizardContentStep GetCurrentContentStep()
    {
        if (IsReinscriptionMode)
        {
            return CurrentStep switch
            {
                1 => WizardContentStep.Search,
                2 => WizardContentStep.Scolarite,
                3 => WizardContentStep.Responsables,
                4 => WizardContentStep.Sante,
                5 => WizardContentStep.Documents,
                6 => WizardContentStep.Validation,
                _ => WizardContentStep.Validation
            };
        }

        return CurrentStep switch
        {
            1 => WizardContentStep.Identity,
            2 => WizardContentStep.Scolarite,
            3 => WizardContentStep.Responsables,
            4 => WizardContentStep.Sante,
            5 => WizardContentStep.Documents,
            6 => WizardContentStep.Validation,
            _ => WizardContentStep.Validation
        };
    }

    private void UpdateWizardSteps()
    {
        for (var i = 0; i < WizardSteps.Count; i++)
        {
            WizardSteps[i].State = i + 1 < CurrentStep
                ? WizardStepVisualState.Completed
                : i + 1 == CurrentStep
                    ? WizardStepVisualState.Active
                    : WizardStepVisualState.Pending;
        }
    }

    private void NotifySummaryProperties()
    {
        OnPropertyChanged(nameof(SummarySection));
        OnPropertyChanged(nameof(SummaryOption));
        OnPropertyChanged(nameof(SummaryPedagogicalClass));
        OnPropertyChanged(nameof(SummaryLocal));
        OnPropertyChanged(nameof(SummaryClass));
        OnPropertyChanged(nameof(ShowOptionPicker));
        OnPropertyChanged(nameof(ShowClassPicker));
        OnPropertyChanged(nameof(ShowLocalPicker));
    }

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var prerequisites = await _wizardApi.GetPrerequisitesAsync();
            PrerequisitesReady = prerequisites.IsReady;
            PrerequisiteIssues.Clear();
            foreach (var issue in prerequisites.Issues)
            {
                PrerequisiteIssues.Add(issue);
            }

            AcademicYearId = prerequisites.CurrentAcademicYearId;
            AcademicYearLabel = prerequisites.CurrentAcademicYearLabel ?? "—";

            if (IsReinscriptionMode)
            {
                RegistrationKind = RegistrationKind.Reinscription;
                StatusMessage = PrerequisitesReady
                    ? "Réinscription — recherchez l'élève à réinscrire."
                    : "Configurez les prérequis avant de lancer une réinscription.";
            }
            else
            {
                ResetStudentFields();
                ExistingStudentId = null;
                RegistrationKind = RegistrationKind.NouvelleInscription;
                RegistrationNumber = await _wizardApi.GenerateRegistrationNumberAsync();
                Step1Completed = true;
                StatusMessage = PrerequisitesReady
                    ? "Nouvelle inscription — renseignez l'identité de l'élève."
                    : "Configurez les prérequis avant de lancer une inscription.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyNavigationState();
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ValidationMessage = "Saisissez au moins un critère de recherche.";
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            var results = await _wizardApi.SearchStudentsAsync(SearchText);
            SearchResults.Clear();
            foreach (var student in results)
            {
                SearchResults.Add(student);
            }

            HasSearched = true;
            SearchHasNoResults = SearchResults.Count == 0;
            SelectedSearchResult = null;
            Step1Completed = false;
            NotifyNavigationState();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ReinscribeStudentAsync(EnrollmentStudentSearchResultDto? student) =>
        await LoadExistingStudentAsync(student, RegistrationKind.Reinscription, 2, "Réinscription — sélectionnez la nouvelle classe.");

    [RelayCommand]
    private void ConfigurePrerequisite(EnrollmentPrerequisiteIssueDto? issue)
    {
        if (issue is null)
        {
            return;
        }

        var key = issue.SettingsRoute switch
        {
            "academic-years" => "annees-scolaires",
            "pedagogical-structure" => "structure-pedagogique",
            "class_locals" => "structure-pedagogique",
            "fee-types" => "frais-scolaires",
            _ => issue.SettingsRoute
        };

        var item = SettingsNavCatalog.FindByKey(key);
        if (item is not null)
        {
            _navigationService.NavigateTo<SettingsViewModel>();
            SettingsNavigationBridge.Select(item);
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            if (IsReinscriptionMode && GetCurrentContentStep() == WizardContentStep.Search)
            {
                Step1Completed = false;
            }
        }
    }

    [RelayCommand]
    private async Task NextStepAsync()
    {
        if (!ValidateCurrentStep(out var message))
        {
            ValidationMessage = message;
            return;
        }

        ValidationMessage = null;
        if (CurrentStep < TotalSteps)
        {
            CurrentStep++;
        }

        UpdateProgress();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void SaveDraft()
    {
        WizardStatus = "Brouillon enregistré";
        StatusMessage = "Brouillon enregistré localement.";
    }

    [RelayCommand]
    private void CancelWizard() => _navigationService.NavigateTo<StudentsViewModel>();

    [RelayCommand]
    private async Task FinalizeAsync()
    {
        if (!ConfirmAccuracy)
        {
            ValidationMessage = "Cochez la confirmation d'exactitude des informations.";
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            var request = BuildRequest();
            var validation = await _wizardApi.ValidateAsync(request);
            if (!validation.IsValid)
            {
                ValidationMessage = validation.Issues.FirstOrDefault()?.Message ?? "Validation échouée.";
                return;
            }

            var result = await _wizardApi.CompleteAsync(request);
            RegistrationNumber = result.RegistrationNumber;
            WizardStatus = "Inscrit";
            StatusMessage = result.Message;
            ProgressPercent = 100;
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ImportPhoto()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png|All|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            PhotoPath = dialog.FileName;
            StatusMessage = "Photo importée.";
        }
    }

    [RelayCommand]
    private void TakePhoto() =>
        StatusMessage = "Capture photo : branchez une webcam ou importez une image depuis l'appareil.";

    [RelayCommand]
    private void RemovePhoto()
    {
        PhotoPath = null;
        StatusMessage = "Photo supprimée.";
    }

    [RelayCommand]
    private void CropPhoto() =>
        StatusMessage = "Recadrage disponible prochainement — utilisez une image déjà recadrée (400×400 px recommandé).";

    [RelayCommand]
    private void ImportDocument(EnrollmentDocumentItemViewModel? doc)
    {
        if (doc is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Documents|*.pdf;*.jpg;*.jpeg;*.png|Tous les fichiers|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            doc.FileName = System.IO.Path.GetFileName(dialog.FileName);
            doc.LocalPath = dialog.FileName;
            doc.Status = "Complet";

            if (doc.DocumentType.Equals("Photo", StringComparison.OrdinalIgnoreCase))
            {
                PhotoPath = dialog.FileName;
            }

            UpdateDocumentProgress();
        }
    }

    private async Task LoadExistingStudentAsync(
        EnrollmentStudentSearchResultDto? student,
        RegistrationKind kind,
        int targetStep,
        string message)
    {
        if (student is null)
        {
            return;
        }

        SelectedSearchResult = student;
        ExistingStudentId = student.Id;
        RegistrationNumber = student.RegistrationNumber;
        LastName = student.LastName;
        FirstName = student.FirstName;
        MiddleName = student.MiddleName ?? string.Empty;
        Gender = student.Gender;
        DateOfBirth = student.DateOfBirth.ToDateTime(TimeOnly.MinValue);
        PhotoPath = student.PhotoPath;
        RegistrationKind = kind;
        Step1Completed = true;
        CurrentStep = targetStep;
        StatusMessage = message;
        UpdateProgress();
        NotifyNavigationState();
        await Task.CompletedTask;
    }

    private void ResetStudentFields()
    {
        LastName = FirstName = MiddleName = string.Empty;
        Gender = Gender.Masculin;
        DateOfBirth = DateTime.Today.AddYears(-8);
        PlaceOfBirth = Province = Territory = City = Language = Religion = string.Empty;
        Nationality = "Congolaise";
        Country = "RDC";
        PhotoPath = null;
        PermanentNumber = string.Empty;
    }

    private async Task LoadStructureAsync()
    {
        if (!AcademicYearId.HasValue)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var options = await _wizardApi.GetStructureOptionsAsync();
            Sections.Clear();
            foreach (var section in options.Sections)
            {
                Sections.Add(section);
            }

            _allClasses.Clear();
            _allClasses.AddRange(options.Classes);
            RefreshStudyOptions();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshCapacityAsync()
    {
        if (SelectedClass is null || !AcademicYearId.HasValue)
        {
            ClassCapacityInfo = string.Empty;
            SmartAlertMessage = null;
            return;
        }

        try
        {
            var capacity = await _wizardApi.GetClassCapacityAsync(SelectedClass.ClassRoomId, AcademicYearId.Value);
            ClassCapacityInfo = capacity.MaxCapacity.HasValue
                ? $"Places : {capacity.CurrentCount}/{capacity.MaxCapacity} — reste {capacity.Remaining}"
                : "Capacité non limitée";

            if (capacity.IsFull)
            {
                SmartAlertIsWarning = true;
                SmartAlertMessage = "Attention — cette classe est complète.";
            }
            else if (capacity.MaxCapacity.HasValue && capacity.Remaining <= 3)
            {
                SmartAlertIsWarning = true;
                SmartAlertMessage = $"Attention — il ne reste que {capacity.Remaining} place(s).";
            }
            else
            {
                SmartAlertIsWarning = false;
                SmartAlertMessage = "Local disponible.";
            }
        }
        catch (Exception ex)
        {
            ClassCapacityInfo = ex.Message;
            SmartAlertIsWarning = true;
            SmartAlertMessage = ex.Message;
        }
    }

    private void RefreshStudyOptions()
    {
        StudyOptions.Clear();
        foreach (var option in FilteredBySection()
                     .Select(c => c.StudyOption ?? c.HumanitiesSection ?? "Général")
                     .Distinct()
                     .OrderBy(o => o))
        {
            StudyOptions.Add(option);
        }
    }

    private void RefreshPedagogicalClasses()
    {
        PedagogicalClassOptions.Clear();
        if (string.IsNullOrWhiteSpace(SelectedStudyOption))
        {
            return;
        }

        foreach (var group in FilteredBySection()
                     .Where(c => (c.StudyOption ?? c.HumanitiesSection ?? "Général") == SelectedStudyOption)
                     .GroupBy(c => c.PedagogicalClassId))
        {
            var first = group.First();
            PedagogicalClassOptions.Add(new PedagogicalClassPickerItem(
                group.Key,
                first.PedagogicalDisplayName ?? first.FullDisplayName));
        }
    }

    private void RefreshAvailableLocals()
    {
        AvailableLocals.Clear();
        foreach (var local in FilteredBySection().Where(c => c.PedagogicalClassId == SelectedPedagogicalClassId))
        {
            AvailableLocals.Add(local);
        }
    }

    private IEnumerable<EnrollmentClassOptionDto> FilteredBySection() =>
        _allClasses.Where(c => !SelectedSectionId.HasValue || c.SectionId == SelectedSectionId);

    private void UpdateAgeCompatibility()
    {
        if (SelectedClass?.MinAge is null && SelectedClass?.MaxAge is null)
        {
            AgeCompatibilityOk = true;
            AgeCompatibilityMessage = null;
            return;
        }

        var min = SelectedClass.MinAge;
        var max = SelectedClass.MaxAge;

        if (min.HasValue && Age < min.Value)
        {
            AgeCompatibilityOk = false;
            AgeCompatibilityMessage = $"Attention — l'âge ({Age} ans) est inférieur au minimum ({min} ans) pour {SelectedClass.PedagogicalDisplayName}.";
            return;
        }

        if (max.HasValue && Age > max.Value)
        {
            AgeCompatibilityOk = false;
            AgeCompatibilityMessage = $"Attention — l'âge ({Age} ans) dépasse le maximum ({max} ans) pour {SelectedClass.PedagogicalDisplayName}.";
            return;
        }

        AgeCompatibilityOk = true;
        AgeCompatibilityMessage = SelectedClass.PedagogicalDisplayName is not null
            ? $"Compatible avec {SelectedClass.PedagogicalDisplayName}."
            : "Âge compatible avec la classe sélectionnée.";
    }

    private bool IsCurrentStepValid() => ValidateCurrentStep(out _);

    private bool ValidateCurrentStep(out string message)
    {
        message = string.Empty;
        switch (GetCurrentContentStep())
        {
            case WizardContentStep.Search:
                if (!Step1Completed)
                {
                    message = "Recherchez l'élève puis cliquez sur « Réinscrire ».";
                    return false;
                }

                return true;
            case WizardContentStep.Identity:
                if (string.IsNullOrWhiteSpace(LastName)) { message = "Le nom est obligatoire."; return false; }
                if (string.IsNullOrWhiteSpace(FirstName)) { message = "Le prénom est obligatoire."; return false; }
                if (DateOfBirth >= DateTime.Today) { message = "La date de naissance doit être dans le passé."; return false; }
                return true;
            case WizardContentStep.Scolarite:
                if (!SelectedSectionId.HasValue) { message = "Sélectionnez une section."; return false; }
                if (string.IsNullOrWhiteSpace(SelectedStudyOption)) { message = "Sélectionnez une option."; return false; }
                if (!SelectedPedagogicalClassId.HasValue) { message = "Sélectionnez une classe pédagogique."; return false; }
                if (SelectedClass is null) { message = "Sélectionnez un local."; return false; }
                if (SelectedClass.MaxCapacity.HasValue && SelectedClass.CurrentCount >= SelectedClass.MaxCapacity)
                {
                    message = "Ce local est saturé — choisissez un autre local.";
                    return false;
                }

                if (!AgeCompatibilityOk)
                {
                    message = AgeCompatibilityMessage ?? "L'âge n'est pas compatible avec la classe.";
                    return false;
                }

                return true;
            case WizardContentStep.Responsables:
                if (string.IsNullOrWhiteSpace(PrimaryLastName) || string.IsNullOrWhiteSpace(PrimaryFirstName))
                {
                    message = "Le responsable principal est obligatoire.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(PrimaryPhone))
                {
                    message = "Le téléphone du responsable principal est obligatoire.";
                    return false;
                }

                if (!IsValidPhone(PrimaryPhone))
                {
                    message = "Le téléphone du responsable principal est invalide.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(PrimaryEmail) && !IsValidEmail(PrimaryEmail))
                {
                    message = "L'adresse e-mail du responsable principal est invalide.";
                    return false;
                }

                return true;
            case WizardContentStep.Sante:
                return true;
            case WizardContentStep.Documents:
                foreach (var doc in Documents.Where(d => d.IsMandatory))
                {
                    if (!doc.Status.Equals("Complet", StringComparison.OrdinalIgnoreCase))
                    {
                        message = $"Le document « {doc.DocumentType} » est obligatoire.";
                        return false;
                    }
                }

                return true;
            case WizardContentStep.Validation:
                return true;
            default:
                return true;
        }
    }

    private CompleteEnrollmentRequest BuildRequest()
    {
        var guardians = BuildGuardians();
        var docs = Documents.Select(d => new EnrollmentDocumentStatusDto(
            d.DocumentType, d.Status, d.FileName, d.LocalPath)).ToList();
        return new CompleteEnrollmentRequest(
            ExistingStudentId,
            FirstName.Trim(),
            LastName.Trim(),
            string.IsNullOrWhiteSpace(MiddleName) ? null : MiddleName.Trim(),
            Gender,
            DateOnly.FromDateTime(DateOfBirth),
            string.IsNullOrWhiteSpace(PlaceOfBirth) ? null : PlaceOfBirth.Trim(),
            string.IsNullOrWhiteSpace(Nationality) ? "Congolaise" : Nationality.Trim(),
            string.IsNullOrWhiteSpace(Province) ? null : Province.Trim(),
            string.IsNullOrWhiteSpace(Territory) ? null : Territory.Trim(),
            string.IsNullOrWhiteSpace(City) ? null : City.Trim(),
            string.IsNullOrWhiteSpace(Country) ? null : Country.Trim(),
            string.IsNullOrWhiteSpace(Language) ? null : Language.Trim(),
            string.IsNullOrWhiteSpace(Religion) ? null : Religion.Trim(),
            null,
            null,
            null,
            PhotoPath,
            new EnrollmentMedicalDto(
                BloodGroup, Allergies, ChronicDiseases, Treatment, DoctorName, MedicalCenter,
                Disability, MedicalObservations, MedicalEmergency),
            new EnrollmentScolariteDto(
                SelectedClass?.SectionId ?? Guid.Empty,
                SelectedClass?.ClassRoomId ?? Guid.Empty,
                SelectedClass?.PedagogicalClassId,
                OrderNumber,
                DateOnly.FromDateTime(EnrollmentDate),
                RegistrationKind,
                string.IsNullOrWhiteSpace(PreviousSchool) ? null : PreviousSchool.Trim(),
                string.IsNullOrWhiteSpace(PreviousStudentCode) ? null : PreviousStudentCode.Trim(),
                string.IsNullOrWhiteSpace(PermanentNumber) ? null : PermanentNumber.Trim()),
            guardians,
            docs,
            null,
            ConfirmAccuracy);
    }

    private List<GuardianInputDto> BuildGuardians()
    {
        var guardians = new List<GuardianInputDto>();
        if (!string.IsNullOrWhiteSpace(PrimaryLastName) || !string.IsNullOrWhiteSpace(PrimaryFirstName))
        {
            guardians.Add(new GuardianInputDto(
                PrimaryFirstName, PrimaryLastName, PrimaryPhone, PrimaryEmail, PrimaryAddress,
                PrimaryProfession, PrimaryEmployer, "Responsable principal", true, false));
        }

        if (!string.IsNullOrWhiteSpace(SecondaryLastName) || !string.IsNullOrWhiteSpace(SecondaryFirstName))
        {
            guardians.Add(new GuardianInputDto(
                SecondaryFirstName, SecondaryLastName, SecondaryPhone, SecondaryEmail, null,
                null, null, "Second responsable", false, false));
        }

        if (!string.IsNullOrWhiteSpace(EmergencyName))
        {
            guardians.Add(new GuardianInputDto(
                EmergencyName, string.Empty, EmergencyPhone, null, null,
                null, null, string.IsNullOrWhiteSpace(EmergencyRelationship) ? "Urgence" : EmergencyRelationship,
                false, false));
        }

        if (!string.IsNullOrWhiteSpace(PickupName))
        {
            guardians.Add(new GuardianInputDto(
                PickupName, string.Empty, PickupPhone, null, null,
                null, null, string.IsNullOrWhiteSpace(PickupRelationship) ? "Autorisé récupération" : PickupRelationship,
                false, true));
        }

        return guardians;
    }

    private void InitializeDocuments()
    {
        var types = new (string Type, bool Mandatory)[]
        {
            ("Acte de naissance", true),
            ("Photo", true),
            ("Bulletin précédent", false),
            ("Certificat médical", false),
            ("Attestation de réussite", false),
            ("Transfert", false),
            ("Autres", false)
        };

        foreach (var (type, mandatory) in types)
        {
            Documents.Add(new EnrollmentDocumentItemViewModel(type, mandatory));
        }
    }

    private void UpdateDocumentProgress()
    {
        CompletedDocumentsCount = Documents.Count(d => d.Status.Equals("Complet", StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(DocumentsProgressLabel));
        UpdateProgress();
        NotifyNavigationState();
    }

    private void UpdateProgress()
    {
        ProgressPercent = (int)Math.Round(CurrentStep / (double)TotalSteps * 100);
        OnPropertyChanged(nameof(ProgressStepLabel));
        OnPropertyChanged(nameof(CurrentStepDetailName));
        OnPropertyChanged(nameof(CurrentStepGuidance));
        OnPropertyChanged(nameof(Age));
        OnPropertyChanged(nameof(AgeCategory));
    }

    private void NotifyStepFlags()
    {
        OnPropertyChanged(nameof(ShowSearchStep));
        OnPropertyChanged(nameof(ShowIdentityStep));
        OnPropertyChanged(nameof(ShowScolariteStep));
        OnPropertyChanged(nameof(ShowResponsablesStep));
        OnPropertyChanged(nameof(ShowSanteStep));
        OnPropertyChanged(nameof(ShowDocumentsStep));
        OnPropertyChanged(nameof(ShowValidationStep));
        NotifyNavigationState();
    }

    private void NotifyNavigationState()
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(ShowNextStep));
        OnPropertyChanged(nameof(ShowFinalize));
    }

    private static bool IsValidEmail(string email) =>
        Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);

    private static bool IsValidPhone(string phone)
    {
        var digits = Regex.Replace(phone, @"\D", string.Empty);
        return digits.Length is >= 9 and <= 15;
    }

    private static int CalculateAge(DateOnly birth, DateOnly reference)
    {
        var age = reference.Year - birth.Year;
        if (birth > reference.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}

public partial class EnrollmentDocumentItemViewModel : ObservableObject
{
    public EnrollmentDocumentItemViewModel(string documentType, bool isMandatory)
    {
        DocumentType = documentType;
        IsMandatory = isMandatory;
        Status = isMandatory ? "Manquant" : "Incomplet";
    }

    public string DocumentType { get; }
    public bool IsMandatory { get; }

    [ObservableProperty] private string _status;
    [ObservableProperty] private string? _fileName;
    [ObservableProperty] private string? _localPath;
}
