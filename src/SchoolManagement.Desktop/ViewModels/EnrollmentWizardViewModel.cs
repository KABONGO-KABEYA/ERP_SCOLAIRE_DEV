using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Application.Parent.DTOs;
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

    private static readonly string[] NewEnrollmentStepSubtitles =
    [
        "État civil", "Classe & local", "Contacts", "Médical", "Pièces", "Finalisation"
    ];

    private static readonly string[] ReinscriptionStepSubtitles =
    [
        "Dossier existant", "Classe & local", "Contacts", "Médical", "Pièces", "Finalisation"
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
    private readonly IGeographyApiService _geographyApi;
    private readonly IEnrollmentFormPrintService _enrollmentFormPrintService;
    private readonly INavigationService _navigationService;
    private readonly List<EnrollmentClassOptionDto> _allClasses = [];
    private int? _reinscriptionMinClassLevel;
    private readonly EnrollmentWizardEntryMode _entryMode;
    private readonly Guid? _modificationStudentId;

    public EnrollmentWizardViewModel(
        IEnrollmentWizardApiService wizardApi,
        IGeographyApiService geographyApi,
        IEnrollmentFormPrintService enrollmentFormPrintService,
        INavigationService navigationService)
    {
        _wizardApi = wizardApi;
        _geographyApi = geographyApi;
        _enrollmentFormPrintService = enrollmentFormPrintService;
        _navigationService = navigationService;
        StudentAddressEditor = new AddressEditorViewModel(_geographyApi);
        FatherAddressEditor = new AddressEditorViewModel(_geographyApi);
        MotherAddressEditor = new AddressEditorViewModel(_geographyApi);
        Contact1AddressEditor = new AddressEditorViewModel(_geographyApi);
        Contact2AddressEditor = new AddressEditorViewModel(_geographyApi);
        _entryMode = EnrollmentWizardNavigationBridge.ConsumeMode();
        _modificationStudentId = EnrollmentWizardNavigationBridge.ConsumeModificationStudentId();
        InitializeDocuments();
        InitializeSteps();
        _ = InitializeAsync();
    }

    public EnrollmentWizardEntryMode EntryMode => _entryMode;
    public bool IsReinscriptionMode => EntryMode == EnrollmentWizardEntryMode.Reinscription;
    public bool IsModificationMode => EntryMode == EnrollmentWizardEntryMode.Modification;
    public bool CanEditClassAssignment => !IsModificationMode || CanChangeClass;
    public string PageTitle => IsModificationMode
        ? "Modifier le dossier élève"
        : IsReinscriptionMode ? "Réinscription" : "Nouvelle inscription";
    public string PageSubtitle => IsModificationMode
        ? "Modification du dossier de l'élève inscrit sur l'année scolaire courante"
        : IsReinscriptionMode
            ? "Recherche et réinscription d'un élève existant pour la nouvelle année scolaire"
            : "Enregistrement complet du dossier d'un nouvel élève — les frais scolaires sont traités séparément";
    public bool ShowRegistrationKindPicker => !IsReinscriptionMode && !IsModificationMode;

    public ObservableCollection<EnrollmentWizardStepItem> WizardSteps { get; } = [];
    public ObservableCollection<EnrollmentStudentSearchResultDto> SearchResults { get; } = [];
    public ObservableCollection<EnrollmentGuardianSearchResultDto> GuardianSearchResults { get; } = [];
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
    [ObservableProperty] private string _guardianSearchText = string.Empty;
    [ObservableProperty] private bool _guardianSearchHasNoResults;
    [ObservableProperty] private Guid? _existingStudentId;
    [ObservableProperty] private Guid? _dossierEnrollmentId;
    [ObservableProperty] private bool _canChangeClass = true;
    [ObservableProperty] private string? _classChangeBlockedReason;

    [ObservableProperty] private string _registrationNumber = string.Empty;
    /// <summary>Identifiant session fichiers temp/{draftId} (P3).</summary>
    private readonly Guid _draftId = Guid.NewGuid();
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _middleName = string.Empty;
    [ObservableProperty] private Gender? _gender;
    [ObservableProperty] private DateTime? _dateOfBirth;
    [ObservableProperty] private string _placeOfBirth = string.Empty;
    [ObservableProperty] private string _nationality = "Congolaise";
    [ObservableProperty] private string _language = string.Empty;
    [ObservableProperty] private string _religion = string.Empty;
    [ObservableProperty] private string? _photoPath;
    [ObservableProperty] private string? _pendingPhotoFilePath;
    [ObservableProperty] private string _permanentNumber = string.Empty;

    public string? PhotoDisplayPath => PendingPhotoFilePath ?? PhotoPath;

    [ObservableProperty] private Guid? _academicYearId;
    [ObservableProperty] private string _academicYearLabel = string.Empty;
    [ObservableProperty] private Guid? _selectedSectionId;
    [ObservableProperty] private string? _selectedStudyOption;
    [ObservableProperty] private Guid? _selectedPedagogicalClassId;
    [ObservableProperty] private EnrollmentClassOptionDto? _selectedClass;
    [ObservableProperty] private int? _orderNumber;
    [ObservableProperty] private DateTime? _enrollmentDate;
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

    public AddressEditorViewModel StudentAddressEditor { get; }

    public AddressEditorViewModel FatherAddressEditor { get; }

    public AddressEditorViewModel MotherAddressEditor { get; }

    public AddressEditorViewModel Contact1AddressEditor { get; }

    public AddressEditorViewModel Contact2AddressEditor { get; }

    [ObservableProperty] private string _fatherLastName = string.Empty;
    [ObservableProperty] private string _fatherFirstName = string.Empty;
    [ObservableProperty] private string _fatherPhone = string.Empty;
    [ObservableProperty] private string _fatherEmail = string.Empty;
    [ObservableProperty] private string _fatherProfession = string.Empty;
    [ObservableProperty] private bool _fatherSameAddressAsStudent = true;
    [ObservableProperty] private Guid? _fatherExistingGuardianId;

    [ObservableProperty] private string _motherLastName = string.Empty;
    [ObservableProperty] private string _motherFirstName = string.Empty;
    [ObservableProperty] private string _motherPhone = string.Empty;
    [ObservableProperty] private string _motherEmail = string.Empty;
    [ObservableProperty] private string _motherProfession = string.Empty;
    [ObservableProperty] private bool _motherSameAddressAsStudent = true;
    [ObservableProperty] private Guid? _motherExistingGuardianId;

    [ObservableProperty] private string _contact1LastName = string.Empty;
    [ObservableProperty] private string _contact1FirstName = string.Empty;
    [ObservableProperty] private string _contact1Phone = string.Empty;
    [ObservableProperty] private string _contact1Email = string.Empty;
    [ObservableProperty] private string _contact1Relationship = string.Empty;
    [ObservableProperty] private bool _contact1SameAddressAsStudent = true;
    [ObservableProperty] private Gender? _contact1Gender;
    [ObservableProperty] private Guid? _contact1ExistingGuardianId;

    [ObservableProperty] private string _contact2LastName = string.Empty;
    [ObservableProperty] private string _contact2FirstName = string.Empty;
    [ObservableProperty] private string _contact2Phone = string.Empty;
    [ObservableProperty] private string _contact2Email = string.Empty;
    [ObservableProperty] private string _contact2Relationship = string.Empty;
    [ObservableProperty] private bool _contact2SameAddressAsStudent = true;
    [ObservableProperty] private Gender? _contact2Gender;
    [ObservableProperty] private Guid? _contact2ExistingGuardianId;
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
    public int Age
    {
        get
        {
            if (!DateOfBirth.HasValue)
            {
                return 0;
            }

            var reference = EnrollmentDate?.Date ?? DateTime.Today;
            return CalculateAge(DateOnly.FromDateTime(DateOfBirth.Value), DateOnly.FromDateTime(reference));
        }
    }
    public string AgeCategory => Age < 18 ? "Mineur" : "Majeur";
    public string DisplayName => string.IsNullOrWhiteSpace(LastName)
        ? "—"
        : StudentDisplayName.Format(LastName, MiddleName, FirstName);
    public string FullDisplayName => DisplayName;
    public string SummarySection => Sections.FirstOrDefault(s => s.Id == SelectedSectionId)?.Name ?? "—";
    public string SummaryOption => SelectedStudyOption ?? "—";
    public string SummaryPedagogicalClass => SelectedClass?.PedagogicalDisplayName ?? PedagogicalClassOptions
        .FirstOrDefault(p => p.Id == SelectedPedagogicalClassId)?.DisplayName ?? "—";
    public string SummaryLocal => SelectedClass?.LocalName ?? "—";
    public string SummaryClass => SelectedClass?.FullDisplayName ?? "—";
    public string SummaryGuardian => string.IsNullOrWhiteSpace(FatherLastName)
        ? string.IsNullOrWhiteSpace(MotherLastName) ? "—" : $"{MotherLastName} {MotherFirstName}".Trim()
        : string.IsNullOrWhiteSpace(MotherLastName)
            ? $"{FatherLastName} {FatherFirstName}".Trim()
            : $"{FatherLastName} {FatherFirstName} / {MotherLastName} {MotherFirstName}".Trim();
    public string SummaryPhone => string.IsNullOrWhiteSpace(FatherPhone)
        ? string.IsNullOrWhiteSpace(MotherPhone) ? "—" : MotherPhone
        : FatherPhone;
    public string ProgressStepLabel => $"Étape {CurrentStep} sur {TotalSteps}";
    public string CurrentStepDetailName => GetStepDetailNames()[Math.Clamp(CurrentStep - 1, 0, TotalSteps - 1)];
    public string CurrentStepGuidance => GetStepGuidances()[Math.Clamp(CurrentStep - 1, 0, TotalSteps - 1)];
    public string SummaryFeesStatus => "À traiter ultérieurement (module Paiements)";
    public string DocumentsProgressLabel => $"{CompletedDocumentsCount}/{Documents.Count}";
    public bool ShowSectionPicker => true;
    public bool ShowOptionPicker => false;
    public bool ShowClassPicker => SelectedSectionId.HasValue;
    public bool ShowEmptyClassHint => SelectedSectionId.HasValue && AvailableLocals.Count == 0 && !IsBusy;
    public string EmptyClassHint =>
        "Aucune classe active pour cette section. Vérifiez la structure pédagogique et les locaux activés pour l'année en cours.";
    public bool ShowLocalPicker => false;

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

    public Domain.Enums.Gender FatherGenderValue => Domain.Enums.Gender.Masculin;

    public Domain.Enums.Gender MotherGenderValue => Domain.Enums.Gender.Feminin;

    public string FatherGenderLabel => "Masculin";

    public string MotherGenderLabel => "Féminin";

    public bool IsContact1Filled =>
        !string.IsNullOrWhiteSpace(Contact1LastName)
        || !string.IsNullOrWhiteSpace(Contact1FirstName)
        || !string.IsNullOrWhiteSpace(Contact1Phone)
        || !string.IsNullOrWhiteSpace(Contact1Email);

    public bool IsContact2Filled =>
        !string.IsNullOrWhiteSpace(Contact2LastName)
        || !string.IsNullOrWhiteSpace(Contact2FirstName)
        || !string.IsNullOrWhiteSpace(Contact2Phone)
        || !string.IsNullOrWhiteSpace(Contact2Email);

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
        RefreshClassOptions();
        NotifySummaryProperties();
        OnPropertyChanged(nameof(ShowAgeCompatibilityWarning));
        NotifyNavigationState();
    }

    partial void OnSelectedStudyOptionChanged(string? value)
    {
        NotifySummaryProperties();
        NotifyNavigationState();
    }

    partial void OnSelectedPedagogicalClassIdChanged(Guid? value)
    {
        NotifySummaryProperties();
        NotifyNavigationState();
    }

    partial void OnSelectedClassChanged(EnrollmentClassOptionDto? value)
    {
        if (value is not null)
        {
            SelectedPedagogicalClassId = value.PedagogicalClassId;
            SelectedStudyOption = value.StudyOption ?? value.HumanitiesSection ?? "Général";
        }
        else
        {
            SelectedPedagogicalClassId = null;
            SelectedStudyOption = null;
        }

        NotifySummaryProperties();
        _ = RefreshCapacityAsync();
        UpdateAgeCompatibility();
        OnPropertyChanged(nameof(ShowAgeCompatibilityWarning));
        NotifyNavigationState();
    }

    partial void OnEnrollmentDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(Age));
        OnPropertyChanged(nameof(AgeCategory));
        UpdateAgeCompatibility();
        OnPropertyChanged(nameof(ShowAgeCompatibilityWarning));
        NotifyNavigationState();
    }

    partial void OnAgeCompatibilityOkChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowAgeCompatibilityWarning));
        NotifyNavigationState();
    }

    partial void OnAgeCompatibilityMessageChanged(string? value) =>
        OnPropertyChanged(nameof(ShowAgeCompatibilityWarning));

    public bool ShowAgeCompatibilityWarning =>
        GetCurrentContentStep() == WizardContentStep.Scolarite
        && SelectedClass is not null
        && !AgeCompatibilityOk
        && !string.IsNullOrWhiteSpace(AgeCompatibilityMessage);

    partial void OnDateOfBirthChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(Age));
        OnPropertyChanged(nameof(AgeCategory));
        UpdateAgeCompatibility();
        OnPropertyChanged(nameof(ShowAgeCompatibilityWarning));
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
    partial void OnMiddleNameChanged(string value)
    {
        OnPropertyChanged(nameof(FullDisplayName));
        UpdateStepValidationHint();
    }
    partial void OnGenderChanged(Gender? value) => NotifyNavigationState();
    partial void OnPrimaryLastNameChanged(string value) => NotifyResponsablesChanged();
    partial void OnPrimaryFirstNameChanged(string value) => NotifyResponsablesChanged();
    partial void OnPrimaryPhoneChanged(string value) => NotifyResponsablesChanged();
    partial void OnPrimaryEmailChanged(string value) => NotifyResponsablesChanged();
    partial void OnFatherLastNameChanged(string value) => NotifyResponsablesChanged();
    partial void OnFatherFirstNameChanged(string value) => NotifyResponsablesChanged();
    partial void OnFatherPhoneChanged(string value) => NotifyResponsablesChanged();
    partial void OnFatherEmailChanged(string value) => NotifyResponsablesChanged();
    partial void OnMotherLastNameChanged(string value) => NotifyResponsablesChanged();
    partial void OnMotherFirstNameChanged(string value) => NotifyResponsablesChanged();
    partial void OnMotherPhoneChanged(string value) => NotifyResponsablesChanged();
    partial void OnMotherEmailChanged(string value) => NotifyResponsablesChanged();
    partial void OnContact1LastNameChanged(string value) => NotifyResponsablesChanged();
    partial void OnContact1FirstNameChanged(string value) => NotifyResponsablesChanged();
    partial void OnContact1PhoneChanged(string value) => NotifyResponsablesChanged();
    partial void OnContact1EmailChanged(string value) => NotifyResponsablesChanged();
    partial void OnContact2LastNameChanged(string value) => NotifyResponsablesChanged();
    partial void OnContact2FirstNameChanged(string value) => NotifyResponsablesChanged();
    partial void OnContact2PhoneChanged(string value) => NotifyResponsablesChanged();
    partial void OnContact2EmailChanged(string value) => NotifyResponsablesChanged();
    partial void OnFatherSameAddressAsStudentChanged(bool value) => UpdateStepValidationHint();
    partial void OnMotherSameAddressAsStudentChanged(bool value) => UpdateStepValidationHint();
    partial void OnContact1SameAddressAsStudentChanged(bool value) => UpdateStepValidationHint();
    partial void OnContact2SameAddressAsStudentChanged(bool value) => UpdateStepValidationHint();
    partial void OnContact1GenderChanged(Gender? value) => UpdateStepValidationHint();
    partial void OnContact2GenderChanged(Gender? value) => UpdateStepValidationHint();

    private void NotifyResponsablesChanged()
    {
        OnPropertyChanged(nameof(SummaryGuardian));
        OnPropertyChanged(nameof(SummaryPhone));
        OnPropertyChanged(nameof(IsContact1Filled));
        OnPropertyChanged(nameof(IsContact2Filled));
        UpdateProgress();
        UpdateStepValidationHint();
        NotifyNavigationState();
    }
    partial void OnCanChangeClassChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditClassAssignment));
    }

    partial void OnStep1CompletedChanged(bool value) => NotifyNavigationState();
    partial void OnPhotoPathChanged(string? value)
    {
        OnPropertyChanged(nameof(PhotoDisplayPath));
    }

    partial void OnPendingPhotoFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(PhotoDisplayPath));
    }

    private void InitializeSteps()
    {
        WizardSteps.Clear();
        var titles = GetStepTitles();
        var subtitles = GetStepSubtitles();
        for (var i = 0; i < titles.Length; i++)
        {
            WizardSteps.Add(new EnrollmentWizardStepItem(i + 1, titles[i], subtitles[i], i == titles.Length - 1));
        }

        UpdateWizardSteps();
    }

    private string[] GetStepSubtitles() =>
        IsReinscriptionMode ? ReinscriptionStepSubtitles : NewEnrollmentStepSubtitles;

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
            AcademicYearLabel = prerequisites.CurrentAcademicYearLabel ?? string.Empty;

            ResetAllWizardFields();
            await InitializeAddressEditorsAsync();

            if (IsModificationMode && _modificationStudentId.HasValue)
            {
                await LoadDossierForEditAsync(_modificationStudentId.Value);
            }
            else if (IsReinscriptionMode)
            {
                StatusMessage = PrerequisitesReady
                    ? "Réinscription — recherchez l'élève à réinscrire."
                    : "Configurez les prérequis avant de lancer une réinscription.";
            }
            else
            {
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
            var results = await _wizardApi.SearchStudentsAsync(SearchText, IsReinscriptionMode);
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
    private async Task SearchGuardiansAsync()
    {
        if (string.IsNullOrWhiteSpace(GuardianSearchText))
        {
            ValidationMessage = "Saisissez un nom, un téléphone ou un e-mail pour rechercher un responsable.";
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            var results = await _wizardApi.SearchGuardiansAsync(GuardianSearchText);
            GuardianSearchResults.Clear();
            foreach (var guardian in results)
            {
                GuardianSearchResults.Add(guardian);
            }

            GuardianSearchHasNoResults = GuardianSearchResults.Count == 0;
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
    private void ApplyGuardianAsFather(EnrollmentGuardianSearchResultDto? guardian)
    {
        if (!TryApplyGuardianToRole(guardian, GuardianRole.Father, out var message))
        {
            ValidationMessage = message;
        }
    }

    [RelayCommand]
    private void ApplyGuardianAsMother(EnrollmentGuardianSearchResultDto? guardian)
    {
        if (!TryApplyGuardianToRole(guardian, GuardianRole.Mother, out var message))
        {
            ValidationMessage = message;
        }
    }

    [RelayCommand]
    private void ApplyGuardianAsContact1(EnrollmentGuardianSearchResultDto? guardian)
    {
        if (!TryApplyGuardianToRole(guardian, GuardianRole.Contact1, out var message))
        {
            ValidationMessage = message;
        }
    }

    [RelayCommand]
    private void ApplyGuardianAsContact2(EnrollmentGuardianSearchResultDto? guardian)
    {
        if (!TryApplyGuardianToRole(guardian, GuardianRole.Contact2, out var message))
        {
            ValidationMessage = message;
        }
    }

    private enum GuardianRole
    {
        Father,
        Mother,
        Contact1,
        Contact2
    }

    private bool TryApplyGuardianToRole(
        EnrollmentGuardianSearchResultDto? guardian,
        GuardianRole role,
        out string message)
    {
        message = string.Empty;
        if (guardian is null)
        {
            return false;
        }

        if (role == GuardianRole.Father && guardian.Gender == Domain.Enums.Gender.Feminin)
        {
            message = $"Impossible d'appliquer « {guardian.FullName} » comme père : le sexe enregistré est féminin.";
            return false;
        }

        if (role == GuardianRole.Mother && guardian.Gender == Domain.Enums.Gender.Masculin)
        {
            message = $"Impossible d'appliquer « {guardian.FullName} » comme mère : le sexe enregistré est masculin.";
            return false;
        }

        ApplyGuardianToRole(guardian, role);
        return true;
    }

    private void ApplyGuardianToRole(EnrollmentGuardianSearchResultDto guardian, GuardianRole role)
    {
        switch (role)
        {
            case GuardianRole.Father:
                FatherExistingGuardianId = guardian.Id;
                FatherLastName = guardian.LastName;
                FatherFirstName = guardian.FirstName;
                FatherPhone = guardian.Phone ?? string.Empty;
                FatherEmail = guardian.Email ?? string.Empty;
                FatherProfession = guardian.Profession ?? string.Empty;
                FatherSameAddressAsStudent = true;
                FatherAddressEditor.Reset();
                break;
            case GuardianRole.Mother:
                MotherExistingGuardianId = guardian.Id;
                MotherLastName = guardian.LastName;
                MotherFirstName = guardian.FirstName;
                MotherPhone = guardian.Phone ?? string.Empty;
                MotherEmail = guardian.Email ?? string.Empty;
                MotherProfession = guardian.Profession ?? string.Empty;
                MotherSameAddressAsStudent = true;
                MotherAddressEditor.Reset();
                break;
            case GuardianRole.Contact1:
                Contact1ExistingGuardianId = guardian.Id;
                Contact1LastName = guardian.LastName;
                Contact1FirstName = guardian.FirstName;
                Contact1Phone = guardian.Phone ?? string.Empty;
                Contact1Email = guardian.Email ?? string.Empty;
                Contact1Gender = guardian.Gender;
                Contact1SameAddressAsStudent = true;
                Contact1AddressEditor.Reset();
                break;
            case GuardianRole.Contact2:
                Contact2ExistingGuardianId = guardian.Id;
                Contact2LastName = guardian.LastName;
                Contact2FirstName = guardian.FirstName;
                Contact2Phone = guardian.Phone ?? string.Empty;
                Contact2Email = guardian.Email ?? string.Empty;
                Contact2Gender = guardian.Gender;
                Contact2SameAddressAsStudent = true;
                Contact2AddressEditor.Reset();
                break;
        }

        ValidationMessage = null;
        StatusMessage = $"Responsable « {guardian.FullName} » appliqué.";
    }

    private AddressInputDto? ResolveGuardianAddress(bool usesStudentAddress, AddressEditorViewModel editor) =>
        usesStudentAddress ? StudentAddressEditor.ToInputDto() : editor.ToInputDto();

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
        if (CurrentStep == 1 && GetCurrentContentStep() == WizardContentStep.Identity)
        {
            await EnsureRegistrationNumberAsync();
        }

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
            if (!IsModificationMode)
            {
                await EnsureRegistrationNumberAsync();
            }

            await UploadPendingFilesAsync();
            var feeSummary = await TryLoadEnrollmentFeeSummaryAsync();
            var request = BuildRequest(feeSummary);

            if (IsModificationMode)
            {
                if (!DossierEnrollmentId.HasValue)
                {
                    ValidationMessage = "Inscription introuvable pour la modification.";
                    return;
                }

                var updateValidation = await _wizardApi.ValidateStudentDossierUpdateAsync(
                    DossierEnrollmentId.Value,
                    request);
                if (!updateValidation.IsValid)
                {
                    ValidationMessage = updateValidation.Issues.FirstOrDefault()?.Message ?? "Validation échouée.";
                    return;
                }

                var updateResult = await _wizardApi.UpdateStudentDossierAsync(DossierEnrollmentId.Value, request);
                var updateMessage =
                    $"Dossier mis à jour — matricule {updateResult.RegistrationNumber}. {updateResult.Message}";

                StatusMessage = updateMessage;
                MessageBox.Show(
                    $"{updateMessage}\n\nLa fiche d'inscription PDF a été régénérée automatiquement dans le dossier élève.{FormatParentAccessDetails(updateResult.ParentAccessAccounts)}",
                    "Modification enregistrée",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                _navigationService.NavigateTo<StudentsViewModel>();
                return;
            }

            var validation = await _wizardApi.ValidateAsync(request);
            if (!validation.IsValid)
            {
                ValidationMessage = validation.Issues.FirstOrDefault()?.Message ?? "Validation échouée.";
                return;
            }

            var result = await _wizardApi.CompleteAsync(request);
            var successMessage =
                $"Inscription enregistrée — matricule {result.RegistrationNumber}. {result.Message}";

            StatusMessage = successMessage;
            MessageBox.Show(
                $"{successMessage}\n\nLa fiche d'inscription PDF a été enregistrée automatiquement dans le dossier élève.{FormatParentAccessDetails(result.ParentAccessAccounts)}",
                "Inscription réussie",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await ResetWizardAfterSuccessAsync(successMessage);
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
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images JPEG|*.jpg;*.jpeg|Images PNG|*.png|Tous les fichiers|*.*",
                Title = "Importer une photo"
            };

            if (ErpFileDialog.ShowOpen(dialog, ErpFileDialog.ResolveOwnerWindow()) != true
                || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return;
            }

            if (!File.Exists(dialog.FileName))
            {
                MessageBox.Show(
                    "Le fichier sélectionné est introuvable.",
                    "Import photo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            PhotoPath = null;
            PendingPhotoFilePath = dialog.FileName;
            ApplySelectedPhotoFile(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible d'importer la photo :\n{ex.Message}",
                "Import photo",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void TakePhoto()
    {
        var window = new Views.WebcamCaptureWindow
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.CapturedFilePath))
        {
            return;
        }

        PhotoPath = null;
        PendingPhotoFilePath = window.CapturedFilePath;
        ApplySelectedPhotoFile(window.CapturedFilePath);
    }

    private void ApplySelectedPhotoFile(string filePath)
    {
        var photoDoc = Documents.FirstOrDefault(d => d.DocumentType.Equals("Photo", StringComparison.OrdinalIgnoreCase));
        if (photoDoc is not null)
        {
            photoDoc.PendingFilePath = filePath;
            photoDoc.FileName = System.IO.Path.GetFileName(filePath);
            photoDoc.Status = "En attente";
            photoDoc.LocalPath = null;
        }

        ValidationMessage = null;
        StatusMessage = "Photo sélectionnée — envoi au serveur lors de l'enregistrement.";
        UpdateDocumentProgress();
        OnPropertyChanged(nameof(PhotoDisplayPath));
    }

    [RelayCommand]
    private void RemovePhoto()
    {
        PhotoPath = null;
        PendingPhotoFilePath = null;
        var photoDoc = Documents.FirstOrDefault(d => d.DocumentType.Equals("Photo", StringComparison.OrdinalIgnoreCase));
        if (photoDoc is not null)
        {
            photoDoc.PendingFilePath = null;
            photoDoc.LocalPath = null;
            photoDoc.FileName = null;
            photoDoc.Status = "Incomplet";
        }

        UpdateDocumentProgress();
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
            Filter = "Documents|*.pdf;*.jpg;*.jpeg;*.png|Tous les fichiers|*.*",
            Title = $"Importer — {doc.DocumentType}"
        };

        if (ErpFileDialog.ShowOpen(dialog, ErpFileDialog.ResolveOwnerWindow()) != true)
        {
            return;
        }

        doc.PendingFilePath = dialog.FileName;
        doc.FileName = System.IO.Path.GetFileName(dialog.FileName);
        doc.LocalPath = null;
        doc.Status = "En attente";

        if (doc.DocumentType.Equals("Photo", StringComparison.OrdinalIgnoreCase))
        {
            ApplySelectedPhotoFile(dialog.FileName);
            return;
        }

        ValidationMessage = null;
        UpdateDocumentProgress();
        StatusMessage = $"Document « {doc.DocumentType} » sélectionné — envoi au serveur lors de l'enregistrement.";
    }

    private async Task UploadPendingFilesAsync()
    {
        foreach (var doc in Documents.Where(d => !string.IsNullOrWhiteSpace(d.PendingFilePath)))
        {
            var stored = await _wizardApi.StoreEnrollmentFileAsync(
                _draftId,
                doc.DocumentType,
                doc.PendingFilePath!);

            doc.LocalPath = stored.StoragePath;
            doc.FileName = stored.FileName;
            doc.FileSizeBytes = stored.FileSizeBytes;
            doc.Status = "Complet";
            doc.PendingFilePath = null;

            if (doc.DocumentType.Equals("Photo", StringComparison.OrdinalIgnoreCase))
            {
                PhotoPath = stored.StoragePath;
                PendingPhotoFilePath = null;
            }
        }

        UpdateDocumentProgress();
    }

    private async Task LoadDossierForEditAsync(Guid studentId)
    {
        var dossier = await _wizardApi.GetStudentDossierForEditAsync(studentId);
        var data = dossier.Dossier;

        DossierEnrollmentId = dossier.EnrollmentId;
        RegistrationNumber = dossier.RegistrationNumber;
        CanChangeClass = dossier.CanChangeClass;
        ClassChangeBlockedReason = dossier.ClassChangeBlockedReason;
        ExistingStudentId = data.ExistingStudentId;
        LastName = data.LastName;
        FirstName = data.FirstName;
        MiddleName = data.MiddleName ?? string.Empty;
        Gender = data.Gender;
        DateOfBirth = data.DateOfBirth.ToDateTime(TimeOnly.MinValue);
        PlaceOfBirth = data.PlaceOfBirth ?? string.Empty;
        Nationality = data.Nationality ?? "Congolaise";
        Language = data.Language ?? string.Empty;
        Religion = data.Religion ?? string.Empty;
        PhotoPath = data.PhotoPath;
        PendingPhotoFilePath = null;

        if (data.ResidenceAddress is not null)
        {
            await StudentAddressEditor.LoadFromInputAsync(data.ResidenceAddress);
        }

        RegistrationKind = data.Scolarite.RegistrationKind;
        OrderNumber = data.Scolarite.OrderNumber;
        EnrollmentDate = data.Scolarite.EnrollmentDate.ToDateTime(TimeOnly.MinValue);
        PreviousSchool = data.Scolarite.PreviousSchool ?? string.Empty;
        PreviousStudentCode = data.Scolarite.PreviousStudentCode ?? string.Empty;
        PermanentNumber = data.Scolarite.PermanentNumber ?? string.Empty;

        BloodGroup = data.Medical.BloodGroup ?? string.Empty;
        Allergies = data.Medical.Allergies ?? string.Empty;
        ChronicDiseases = data.Medical.ChronicDiseases ?? string.Empty;
        Treatment = data.Medical.Treatment ?? string.Empty;
        DoctorName = data.Medical.DoctorName ?? string.Empty;
        MedicalCenter = data.Medical.MedicalCenter ?? string.Empty;
        Disability = data.Medical.Disability ?? string.Empty;
        MedicalObservations = data.Medical.Observations ?? string.Empty;
        MedicalEmergency = data.Medical.MedicalEmergency;

        ResetGuardianFields();
        await ApplyGuardiansFromDossierAsync(data.Guardians, data.ResidenceAddress);
        ApplyDocumentsFromDossier(data.Documents);

        await LoadStructureAsync();
        SelectedSectionId = data.Scolarite.SectionId;
        RefreshClassOptions();
        SelectedClass = _allClasses.FirstOrDefault(c => c.ClassRoomId == data.Scolarite.ClassRoomId)
            ?? AvailableLocals.FirstOrDefault(c => c.ClassRoomId == data.Scolarite.ClassRoomId);

        Step1Completed = true;
        CurrentStep = 1;
        StatusMessage = CanChangeClass
            ? "Modifiez le dossier de l'élève puis validez."
            : $"Modifiez le dossier de l'élève. Classe verrouillée : {ClassChangeBlockedReason}";
        UpdateProgress();
        NotifyNavigationState();
        OnPropertyChanged(nameof(CanEditClassAssignment));
    }

    private async Task ApplyGuardiansFromDossierAsync(
        IReadOnlyList<GuardianInputDto> guardians,
        AddressInputDto? studentAddress)
    {
        foreach (var guardian in guardians)
        {
            if (MatchesRelationship(guardian.Relationship, "Père", "Pere", "Father"))
            {
                ApplyGuardianToFather(guardian, studentAddress);
                continue;
            }

            if (MatchesRelationship(guardian.Relationship, "Mère", "Mere", "Mother"))
            {
                ApplyGuardianToMother(guardian, studentAddress);
                continue;
            }

            if (MatchesRelationship(guardian.Relationship, "Personne à contacter 1", "Contact 1"))
            {
                await ApplyGuardianToContact1Async(guardian, studentAddress);
                continue;
            }

            if (MatchesRelationship(guardian.Relationship, "Personne à contacter 2", "Contact 2"))
            {
                await ApplyGuardianToContact2Async(guardian, studentAddress);
                continue;
            }

            if (MatchesRelationship(guardian.Relationship, "Second responsable"))
            {
                SecondaryLastName = guardian.LastName;
                SecondaryFirstName = guardian.FirstName;
                SecondaryPhone = guardian.Phone ?? string.Empty;
                SecondaryEmail = guardian.Email ?? string.Empty;
                continue;
            }

            if (MatchesRelationship(guardian.Relationship, "Urgence"))
            {
                EmergencyName = $"{guardian.FirstName} {guardian.LastName}".Trim();
                EmergencyPhone = guardian.Phone ?? string.Empty;
                EmergencyRelationship = guardian.Relationship;
                continue;
            }

            if (guardian.CanPickup || MatchesRelationship(guardian.Relationship, "Autorisé récupération", "Récupération"))
            {
                PickupName = $"{guardian.FirstName} {guardian.LastName}".Trim();
                PickupPhone = guardian.Phone ?? string.Empty;
                PickupRelationship = guardian.Relationship;
            }
        }
    }

    private void ApplyGuardianToFather(GuardianInputDto guardian, AddressInputDto? studentAddress)
    {
        FatherLastName = guardian.LastName;
        FatherFirstName = guardian.FirstName;
        FatherPhone = guardian.Phone ?? string.Empty;
        FatherEmail = guardian.Email ?? string.Empty;
        FatherProfession = guardian.Profession ?? string.Empty;
        FatherExistingGuardianId = guardian.ExistingGuardianId;
        FatherSameAddressAsStudent = guardian.UsesStudentAddress;
        if (!guardian.UsesStudentAddress && guardian.ResidenceAddress is not null)
        {
            _ = FatherAddressEditor.LoadFromInputAsync(guardian.ResidenceAddress);
        }
    }

    private void ApplyGuardianToMother(GuardianInputDto guardian, AddressInputDto? studentAddress)
    {
        MotherLastName = guardian.LastName;
        MotherFirstName = guardian.FirstName;
        MotherPhone = guardian.Phone ?? string.Empty;
        MotherEmail = guardian.Email ?? string.Empty;
        MotherProfession = guardian.Profession ?? string.Empty;
        MotherExistingGuardianId = guardian.ExistingGuardianId;
        MotherSameAddressAsStudent = guardian.UsesStudentAddress;
        if (!guardian.UsesStudentAddress && guardian.ResidenceAddress is not null)
        {
            _ = MotherAddressEditor.LoadFromInputAsync(guardian.ResidenceAddress);
        }
    }

    private async Task ApplyGuardianToContact1Async(GuardianInputDto guardian, AddressInputDto? studentAddress)
    {
        Contact1LastName = guardian.LastName;
        Contact1FirstName = guardian.FirstName;
        Contact1Phone = guardian.Phone ?? string.Empty;
        Contact1Email = guardian.Email ?? string.Empty;
        Contact1Relationship = guardian.Relationship;
        Contact1ExistingGuardianId = guardian.ExistingGuardianId;
        Contact1SameAddressAsStudent = guardian.UsesStudentAddress;
        Contact1Gender = guardian.Gender;
        if (!guardian.UsesStudentAddress && guardian.ResidenceAddress is not null)
        {
            await Contact1AddressEditor.LoadFromInputAsync(guardian.ResidenceAddress);
        }
    }

    private async Task ApplyGuardianToContact2Async(GuardianInputDto guardian, AddressInputDto? studentAddress)
    {
        Contact2LastName = guardian.LastName;
        Contact2FirstName = guardian.FirstName;
        Contact2Phone = guardian.Phone ?? string.Empty;
        Contact2Email = guardian.Email ?? string.Empty;
        Contact2Relationship = guardian.Relationship;
        Contact2ExistingGuardianId = guardian.ExistingGuardianId;
        Contact2SameAddressAsStudent = guardian.UsesStudentAddress;
        Contact2Gender = guardian.Gender;
        if (!guardian.UsesStudentAddress && guardian.ResidenceAddress is not null)
        {
            await Contact2AddressEditor.LoadFromInputAsync(guardian.ResidenceAddress);
        }
    }

    private void ApplyDocumentsFromDossier(IReadOnlyList<EnrollmentDocumentStatusDto> documents)
    {
        foreach (var doc in documents)
        {
            var item = Documents.FirstOrDefault(d =>
                d.DocumentType.Equals(doc.DocumentType, StringComparison.OrdinalIgnoreCase)
                || NormalizeDocumentType(d.DocumentType).Equals(
                    NormalizeDocumentType(doc.DocumentType),
                    StringComparison.OrdinalIgnoreCase));

            if (item is null)
            {
                item = new EnrollmentDocumentItemViewModel(doc.DocumentType, isMandatory: false);
                Documents.Add(item);
            }

            item.LocalPath = doc.StoragePath;
            item.FileName = doc.FileName;
            item.FileSizeBytes = doc.FileSizeBytes;
            item.Status = "Complet";
            item.PendingFilePath = null;
        }

        UpdateDocumentProgress();
    }

    private static string NormalizeDocumentType(string value) =>
        value.Replace('_', ' ').Trim();

    private static bool MatchesRelationship(string relationship, params string[] keywords) =>
        keywords.Any(keyword => relationship.Contains(keyword, StringComparison.OrdinalIgnoreCase));

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
        PendingPhotoFilePath = null;
        _reinscriptionMinClassLevel = kind == RegistrationKind.Reinscription ? student.LastClassLevel : null;
        SelectedClass = null;
        RegistrationKind = kind;
        Step1Completed = true;
        CurrentStep = targetStep;
        StatusMessage = message;
        RefreshClassOptions();
        UpdateProgress();
        NotifyNavigationState();
        await Task.CompletedTask;
    }

    private async Task InitializeAddressEditorsAsync()
    {
        await StudentAddressEditor.InitializeAsync();
        await FatherAddressEditor.InitializeAsync();
        await MotherAddressEditor.InitializeAsync();
        await Contact1AddressEditor.InitializeAsync();
        await Contact2AddressEditor.InitializeAsync();

        var defaultCountry = StudentAddressEditor.Countries.FirstOrDefault(c =>
            c.Code.Equals("RDC", StringComparison.OrdinalIgnoreCase))
            ?? StudentAddressEditor.Countries.FirstOrDefault();
        if (defaultCountry is null)
        {
            return;
        }

        await StudentAddressEditor.SetCountryAsync(defaultCountry);

        var defaultProvince = StudentAddressEditor.Provinces.FirstOrDefault(p =>
            p.Code.Equals("KIN", StringComparison.OrdinalIgnoreCase))
            ?? StudentAddressEditor.Provinces.FirstOrDefault();
        if (defaultProvince is null)
        {
            return;
        }

        await StudentAddressEditor.SetProvinceAsync(defaultProvince);

        var defaultCity = StudentAddressEditor.Cities.FirstOrDefault(c =>
            c.Code.Equals("KIN", StringComparison.OrdinalIgnoreCase))
            ?? StudentAddressEditor.Cities.FirstOrDefault();
        if (defaultCity is not null)
        {
            await StudentAddressEditor.SetCityAsync(defaultCity);
        }
    }

    private void ResetStudentFields()
    {
        LastName = FirstName = MiddleName = string.Empty;
        Gender = null;
        DateOfBirth = null;
        PlaceOfBirth = Language = Religion = string.Empty;
        Nationality = "Congolaise";
        PhotoPath = null;
        PendingPhotoFilePath = null;
        PermanentNumber = string.Empty;
        StudentAddressEditor.Reset();
    }

    private void ResetScolariteFields()
    {
        SelectedSectionId = null;
        SelectedStudyOption = null;
        SelectedPedagogicalClassId = null;
        SelectedClass = null;
        OrderNumber = null;
        EnrollmentDate = DateTime.Today;
        PreviousSchool = string.Empty;
        PreviousStudentCode = string.Empty;
        ClassCapacityInfo = string.Empty;
        AgeCompatibilityMessage = null;
        AgeCompatibilityOk = true;
        SmartAlertMessage = null;
        SmartAlertIsWarning = false;
        AvailableLocals.Clear();
        TotalDue = 0;
        FeeLines.Clear();
    }

    private void ResetGuardianFields()
    {
        PrimaryLastName = PrimaryFirstName = PrimaryPhone = PrimaryEmail = string.Empty;
        PrimaryAddress = PrimaryProfession = PrimaryEmployer = string.Empty;
        FatherLastName = FatherFirstName = FatherPhone = FatherEmail = string.Empty;
        FatherExistingGuardianId = null;
        FatherProfession = string.Empty;
        FatherSameAddressAsStudent = true;
        FatherAddressEditor.Reset();
        MotherLastName = MotherFirstName = MotherPhone = MotherEmail = string.Empty;
        MotherExistingGuardianId = null;
        MotherProfession = string.Empty;
        MotherSameAddressAsStudent = true;
        MotherAddressEditor.Reset();
        Contact1LastName = Contact1FirstName = Contact1Phone = Contact1Email = Contact1Relationship = string.Empty;
        Contact1ExistingGuardianId = null;
        Contact1SameAddressAsStudent = true;
        Contact1Gender = null;
        Contact1AddressEditor.Reset();
        Contact2LastName = Contact2FirstName = Contact2Phone = Contact2Email = Contact2Relationship = string.Empty;
        Contact2ExistingGuardianId = null;
        Contact2SameAddressAsStudent = true;
        Contact2Gender = null;
        Contact2AddressEditor.Reset();
        SecondaryLastName = SecondaryFirstName = SecondaryPhone = SecondaryEmail = string.Empty;
        EmergencyName = EmergencyPhone = EmergencyRelationship = string.Empty;
        PickupName = PickupPhone = PickupRelationship = string.Empty;
    }

    private void ResetMedicalFields()
    {
        BloodGroup = Allergies = ChronicDiseases = Treatment = string.Empty;
        DoctorName = MedicalCenter = Disability = MedicalObservations = string.Empty;
        MedicalEmergency = false;
    }

    private void ResetDocuments()
    {
        Documents.Clear();
        InitializeDocuments();
        CompletedDocumentsCount = 0;
        OnPropertyChanged(nameof(DocumentsProgressLabel));
    }

    private void ResetAllWizardFields()
    {
        SearchText = string.Empty;
        GuardianSearchText = string.Empty;
        SearchResults.Clear();
        GuardianSearchResults.Clear();
        HasSearched = false;
        SearchHasNoResults = false;
        GuardianSearchHasNoResults = false;
        SelectedSearchResult = null;
        ExistingStudentId = null;
        RegistrationNumber = string.Empty;
        ConfirmAccuracy = false;
        ValidationMessage = null;
        WizardStatus = "Brouillon";
        Step1Completed = false;
        CurrentStep = 1;

        ResetStudentFields();
        _reinscriptionMinClassLevel = null;
        ResetScolariteFields();
        ResetGuardianFields();
        ResetMedicalFields();
        ResetDocuments();

        RegistrationKind = IsReinscriptionMode
            ? RegistrationKind.Reinscription
            : RegistrationKind.NouvelleInscription;

        UpdateWizardSteps();
        UpdateProgress();
        NotifySummaryProperties();
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FullDisplayName));
        OnPropertyChanged(nameof(SummaryGuardian));
        OnPropertyChanged(nameof(SummaryPhone));
        OnPropertyChanged(nameof(ShowAgeCompatibilityWarning));
        NotifyStepFlags();
    }

    private async Task ResetWizardAfterSuccessAsync(string successMessage)
    {
        ResetAllWizardFields();
        await InitializeAddressEditorsAsync();
        StatusMessage = IsReinscriptionMode
            ? $"{successMessage} Recherchez un autre élève à réinscrire."
            : $"{successMessage} Saisissez un nouvel élève.";
        await Task.CompletedTask;
    }

    private static string FormatParentAccessDetails(IReadOnlyList<ParentAppAccessCredentialDto>? accounts)
    {
        if (accounts is null || accounts.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>
        {
            string.Empty,
            "Accès application mobile parent :"
        };

        foreach (var account in accounts)
        {
            if (account.WasCreated && !string.IsNullOrWhiteSpace(account.TemporaryPassword))
            {
                lines.Add(
                    $"• {account.GuardianFullName} — identifiant : {account.UserName} / mot de passe temporaire : {account.TemporaryPassword} (à changer à la 1ère connexion)");
            }
            else
            {
                lines.Add($"• {account.GuardianFullName} — compte déjà existant : {account.UserName}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task EnsureRegistrationNumberAsync()
    {
        if (!string.IsNullOrWhiteSpace(RegistrationNumber))
        {
            return;
        }

        RegistrationNumber = await _wizardApi.GenerateRegistrationNumberAsync();
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
            AcademicYearId = options.AcademicYearId;
            AcademicYearLabel = options.AcademicYearLabel;
            Sections.Clear();
            foreach (var section in options.Sections.OrderBy(s => s.Name))
            {
                Sections.Add(section);
            }

            _allClasses.Clear();
            _allClasses.AddRange(options.Classes);

            if (SelectedSectionId.HasValue && !Sections.Any(s => s.Id == SelectedSectionId))
            {
                SelectedSectionId = null;
                SelectedClass = null;
            }

            RefreshClassOptions();
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

    private void RefreshClassOptions()
    {
        AvailableLocals.Clear();
        if (!SelectedSectionId.HasValue)
        {
            OnPropertyChanged(nameof(ShowEmptyClassHint));
            OnPropertyChanged(nameof(EmptyClassHint));
            return;
        }

        foreach (var option in FilteredBySection()
                     .Where(c => c.IsSelectable)
                     .Where(c => !IsReinscriptionMode || !_reinscriptionMinClassLevel.HasValue || c.Level >= _reinscriptionMinClassLevel.Value)
                     .Where(IsClassAgeCompatible)
                     .OrderBy(c => c.FullDisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            AvailableLocals.Add(option);
        }

        OnPropertyChanged(nameof(ShowEmptyClassHint));
        OnPropertyChanged(nameof(EmptyClassHint));
    }

    private IEnumerable<EnrollmentClassOptionDto> FilteredBySection()
    {
        if (!SelectedSectionId.HasValue)
        {
            return _allClasses;
        }

        var selectedSection = Sections.FirstOrDefault(s => s.Id == SelectedSectionId);
        return _allClasses.Where(c =>
            c.SectionId == SelectedSectionId
            || (selectedSection is not null
                && c.SectionName.Equals(selectedSection.Name, StringComparison.OrdinalIgnoreCase)));
    }

    private bool IsClassAgeCompatible(EnrollmentClassOptionDto option)
    {
        if (!DateOfBirth.HasValue)
        {
            return true;
        }

        if (option.MinAge is null && option.MaxAge is null)
        {
            return true;
        }

        var reference = EnrollmentDate?.Date ?? DateTime.Today;
        var age = CalculateAge(DateOnly.FromDateTime(DateOfBirth.Value), DateOnly.FromDateTime(reference));

        if (option.MinAge.HasValue && age < option.MinAge.Value)
        {
            return false;
        }

        if (option.MaxAge.HasValue && age > option.MaxAge.Value)
        {
            return false;
        }

        return true;
    }

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
                if (string.IsNullOrWhiteSpace(MiddleName)) { message = "Le postnom est obligatoire."; return false; }
                if (!Gender.HasValue) { message = "Le sexe est obligatoire."; return false; }
                if (!DateOfBirth.HasValue) { message = "La date de naissance est obligatoire."; return false; }
                if (DateOfBirth.Value.Date >= DateTime.Today) { message = "La date de naissance doit être dans le passé."; return false; }
                return true;
            case WizardContentStep.Scolarite:
                if (!SelectedSectionId.HasValue) { message = "Sélectionnez une section."; return false; }
                if (SelectedClass is null) { message = "Sélectionnez une classe."; return false; }
                if (!EnrollmentDate.HasValue) { message = "La date d'inscription est obligatoire."; return false; }
                if (EnrollmentDate.Value.Date > DateTime.Today) { message = "La date d'inscription ne peut pas être dans le futur."; return false; }
                if (SelectedClass.MaxCapacity.HasValue && SelectedClass.CurrentCount >= SelectedClass.MaxCapacity)
                {
                    message = "Ce local est saturé — choisissez un autre local.";
                    return false;
                }

                return true;
            case WizardContentStep.Responsables:
                if (!ValidateResponsiblePerson(FatherLastName, FatherFirstName, FatherPhone, FatherEmail, "père", out message))
                {
                    return false;
                }

                if (!FatherSameAddressAsStudent && !FatherAddressEditor.HasContent())
                {
                    message = "Renseignez l'adresse du père ou cochez « habite à la même adresse que l'élève ».";
                    return false;
                }

                if (!ValidateResponsiblePerson(MotherLastName, MotherFirstName, MotherPhone, MotherEmail, "mère", out message))
                {
                    return false;
                }

                if (!MotherSameAddressAsStudent && !MotherAddressEditor.HasContent())
                {
                    message = "Renseignez l'adresse de la mère ou cochez « habite à la même adresse que l'élève ».";
                    return false;
                }

                if (!ValidateOptionalContact(
                        Contact1LastName,
                        Contact1FirstName,
                        Contact1Phone,
                        Contact1Email,
                        Contact1Gender,
                        Contact1SameAddressAsStudent,
                        Contact1AddressEditor,
                        "1ère personne à contacter",
                        out message))
                {
                    return false;
                }

                if (!ValidateOptionalContact(
                        Contact2LastName,
                        Contact2FirstName,
                        Contact2Phone,
                        Contact2Email,
                        Contact2Gender,
                        Contact2SameAddressAsStudent,
                        Contact2AddressEditor,
                        "2ème personne à contacter",
                        out message))
                {
                    return false;
                }

                return true;
            case WizardContentStep.Sante:
                return true;
            case WizardContentStep.Documents:
                return true;
            case WizardContentStep.Validation:
                return true;
            default:
                return true;
        }
    }

    private async Task<EnrollmentFeeSummaryDto?> TryLoadEnrollmentFeeSummaryAsync()
    {
        if (SelectedClass?.PedagogicalClassId is not Guid pedagogicalClassId)
        {
            return null;
        }

        try
        {
            return await _wizardApi.CalculateFeesAsync(pedagogicalClassId, AcademicYearId);
        }
        catch
        {
            // Le serveur recalcule aussi à la validation — ne pas bloquer l'inscription.
            return null;
        }
    }

    private CompleteEnrollmentRequest BuildRequest(EnrollmentFeeSummaryDto? feeSummary = null)
    {
        var guardians = BuildGuardians();
        var docs = Documents.Select(d => new EnrollmentDocumentStatusDto(
            d.DocumentType,
            string.IsNullOrWhiteSpace(d.LocalPath) ? "Manquant" : "Complet",
            d.FileName,
            d.LocalPath,
            d.FileSizeBytes)).ToList();
        return new CompleteEnrollmentRequest(
            ExistingStudentId,
            FirstName.Trim(),
            LastName.Trim(),
            string.IsNullOrWhiteSpace(MiddleName) ? null : MiddleName.Trim(),
            Gender!.Value,
            DateOnly.FromDateTime(DateOfBirth!.Value),
            string.IsNullOrWhiteSpace(PlaceOfBirth) ? null : PlaceOfBirth.Trim(),
            string.IsNullOrWhiteSpace(Nationality) ? "Congolaise" : Nationality.Trim(),
            StudentAddressEditor.ToInputDto(),
            string.IsNullOrWhiteSpace(Language) ? null : Language.Trim(),
            string.IsNullOrWhiteSpace(Religion) ? null : Religion.Trim(),
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
                DateOnly.FromDateTime(EnrollmentDate!.Value),
                RegistrationKind,
                string.IsNullOrWhiteSpace(PreviousSchool) ? null : PreviousSchool.Trim(),
                string.IsNullOrWhiteSpace(PreviousStudentCode) ? null : PreviousStudentCode.Trim(),
                string.IsNullOrWhiteSpace(PermanentNumber) ? null : PermanentNumber.Trim()),
            guardians,
            docs,
            feeSummary,
            ConfirmAccuracy,
            _draftId);
    }

    private List<GuardianInputDto> BuildGuardians()
    {
        var guardians = new List<GuardianInputDto>();
        AddGuardianIfFilled(
            guardians,
            FatherFirstName,
            FatherLastName,
            FatherPhone,
            FatherEmail,
            ResolveGuardianAddress(FatherSameAddressAsStudent, FatherAddressEditor),
            FatherProfession,
            null,
            "Père",
            true,
            false,
            FatherGenderValue,
            FatherSameAddressAsStudent,
            FatherExistingGuardianId);
        AddGuardianIfFilled(
            guardians,
            MotherFirstName,
            MotherLastName,
            MotherPhone,
            MotherEmail,
            ResolveGuardianAddress(MotherSameAddressAsStudent, MotherAddressEditor),
            MotherProfession,
            null,
            "Mère",
            false,
            false,
            MotherGenderValue,
            MotherSameAddressAsStudent,
            MotherExistingGuardianId);
        AddGuardianIfFilled(
            guardians,
            Contact1FirstName,
            Contact1LastName,
            Contact1Phone,
            Contact1Email,
            ResolveGuardianAddress(Contact1SameAddressAsStudent, Contact1AddressEditor),
            null,
            null,
            string.IsNullOrWhiteSpace(Contact1Relationship) ? "Personne à contacter 1" : Contact1Relationship,
            false,
            false,
            Contact1Gender,
            Contact1SameAddressAsStudent,
            Contact1ExistingGuardianId);
        AddGuardianIfFilled(
            guardians,
            Contact2FirstName,
            Contact2LastName,
            Contact2Phone,
            Contact2Email,
            ResolveGuardianAddress(Contact2SameAddressAsStudent, Contact2AddressEditor),
            null,
            null,
            string.IsNullOrWhiteSpace(Contact2Relationship) ? "Personne à contacter 2" : Contact2Relationship,
            false,
            true,
            Contact2Gender,
            Contact2SameAddressAsStudent,
            Contact2ExistingGuardianId);

        if (!string.IsNullOrWhiteSpace(SecondaryLastName) || !string.IsNullOrWhiteSpace(SecondaryFirstName))
        {
            AddGuardianIfFilled(
                guardians,
                SecondaryFirstName,
                SecondaryLastName,
                SecondaryPhone,
                SecondaryEmail,
                null,
                null,
                null,
                "Second responsable",
                false,
                false);
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

    private static void AddGuardianIfFilled(
        List<GuardianInputDto> guardians,
        string firstName,
        string lastName,
        string phone,
        string? email,
        AddressInputDto? residenceAddress,
        string? profession,
        string? employer,
        string relationship,
        bool isPrimary,
        bool canPickup,
        Gender? gender = null,
        bool usesStudentAddress = false,
        Guid? existingGuardianId = null)
    {
        if (string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(firstName))
        {
            return;
        }

        guardians.Add(new GuardianInputDto(
            firstName.Trim(),
            lastName.Trim(),
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            residenceAddress,
            string.IsNullOrWhiteSpace(profession) ? null : profession.Trim(),
            string.IsNullOrWhiteSpace(employer) ? null : employer.Trim(),
            relationship,
            isPrimary,
            canPickup,
            gender,
            usesStudentAddress,
            existingGuardianId));
    }

    private string GetDossierFirstName() =>
        string.IsNullOrWhiteSpace(FirstName) ? LastName.Trim() : FirstName.Trim();

    private static bool ValidateResponsiblePerson(
        string lastName,
        string firstName,
        string phone,
        string? email,
        string roleLabel,
        out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
        {
            message = $"Renseignez le nom et le prénom du/de la {roleLabel}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            message = $"Le téléphone du/de la {roleLabel} est obligatoire.";
            return false;
        }

        if (!IsValidPhone(phone))
        {
            message = $"Le téléphone du/de la {roleLabel} est invalide (9 à 15 chiffres).";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
        {
            message = $"L'e-mail du/de la {roleLabel} est invalide.";
            return false;
        }

        return true;
    }

    private bool ValidateOptionalContact(
        string lastName,
        string firstName,
        string phone,
        string? email,
        Gender? gender,
        bool sameAddressAsStudent,
        AddressEditorViewModel addressEditor,
        string roleLabel,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(lastName)
            && string.IsNullOrWhiteSpace(firstName)
            && string.IsNullOrWhiteSpace(phone)
            && string.IsNullOrWhiteSpace(email))
        {
            message = string.Empty;
            return true;
        }

        if (!ValidateResponsiblePerson(lastName, firstName, phone, email, roleLabel, out message))
        {
            return false;
        }

        if (!gender.HasValue)
        {
            message = $"Le sexe est obligatoire pour la {roleLabel}.";
            return false;
        }

        if (!sameAddressAsStudent && !addressEditor.HasContent())
        {
            message = $"Renseignez l'adresse de la {roleLabel} ou cochez « habite à la même adresse que l'élève ».";
            return false;
        }

        return true;
    }

    private void UpdateStepValidationHint()
    {
        if (IsBusy || !PrerequisitesReady || CurrentStep >= TotalSteps)
        {
            return;
        }

        if (ValidateCurrentStep(out var message))
        {
            if (GetCurrentContentStep() is WizardContentStep.Responsables or WizardContentStep.Scolarite)
            {
                ValidationMessage = null;
            }

            return;
        }

        ValidationMessage = message;
    }

    private void InitializeDocuments()
    {
        var types = new[]
        {
            "Acte de naissance",
            "Photo",
            "Bulletin précédent",
            "Certificat médical",
            "Attestation de réussite",
            "Transfert",
            "Autres"
        };

        foreach (var type in types)
        {
            Documents.Add(new EnrollmentDocumentItemViewModel(type, isMandatory: false));
        }
    }

    private void UpdateDocumentProgress()
    {
        CompletedDocumentsCount = Documents.Count(d =>
            d.Status.Equals("Complet", StringComparison.OrdinalIgnoreCase)
            || d.Status.Equals("En attente", StringComparison.OrdinalIgnoreCase));
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
        UpdateStepValidationHint();
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
    [ObservableProperty] private string? _pendingFilePath;
    [ObservableProperty] private long _fileSizeBytes;
}
