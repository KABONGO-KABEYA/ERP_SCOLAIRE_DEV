using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.Geography.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class StudentDossierEditViewModel : ViewModelBase
{
    private readonly IEnrollmentWizardApiService _wizardApi;
    private readonly IGeographyApiService _geographyApi;
    private readonly List<EnrollmentClassOptionDto> _allClasses = [];
    private Guid _studentId;
    private bool _loadingDossier;

    public StudentDossierEditViewModel(
        IEnrollmentWizardApiService wizardApi,
        IGeographyApiService geographyApi)
    {
        _wizardApi = wizardApi;
        _geographyApi = geographyApi;
        StudentAddressEditor = new AddressEditorViewModel(_geographyApi);
        FatherAddressEditor = new AddressEditorViewModel(_geographyApi);
        MotherAddressEditor = new AddressEditorViewModel(_geographyApi);
        Contact1AddressEditor = new AddressEditorViewModel(_geographyApi);
        Contact2AddressEditor = new AddressEditorViewModel(_geographyApi);
        InitializeDocuments();
    }

    public event EventHandler<bool>? CloseRequested;

    public bool CanEditClassAssignment => CanChangeClass;

    public bool IsDossierLoaded => DossierEnrollmentId.HasValue;

    public AddressEditorViewModel StudentAddressEditor { get; }

    public AddressEditorViewModel FatherAddressEditor { get; }

    public AddressEditorViewModel MotherAddressEditor { get; }

    public AddressEditorViewModel Contact1AddressEditor { get; }

    public AddressEditorViewModel Contact2AddressEditor { get; }

    public ObservableCollection<EnrollmentGuardianSearchResultDto> GuardianSearchResults { get; } = [];

    public ObservableCollection<SectionDto> Sections { get; } = [];

    public ObservableCollection<EnrollmentClassOptionDto> AvailableLocals { get; } = [];

    public ObservableCollection<EnrollmentDocumentItemViewModel> Documents { get; } = [];

    public IReadOnlyList<Domain.Enums.Gender> Genders { get; } = Enum.GetValues<Domain.Enums.Gender>();

    public Domain.Enums.Gender FatherGenderValue => Domain.Enums.Gender.Masculin;

    public Domain.Enums.Gender MotherGenderValue => Domain.Enums.Gender.Feminin;

    public string FatherGenderLabel => "Masculin";

    public string MotherGenderLabel => "Féminin";

    public string? PhotoDisplayPath => PendingPhotoFilePath ?? PhotoPath;

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

    public bool ShowClassPicker => SelectedSectionId.HasValue;

    public bool ShowEmptyClassHint => SelectedSectionId.HasValue && AvailableLocals.Count == 0 && !IsBusy;

    public string EmptyClassHint =>
        "Aucune classe active pour cette section. Vérifiez la structure pédagogique et les locaux activés pour l'année en cours.";

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

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _validationMessage;
    [ObservableProperty] private bool _confirmAccuracy;

    [ObservableProperty] private Guid? _dossierEnrollmentId;
    [ObservableProperty] private bool _canChangeClass = true;
    [ObservableProperty] private string? _classChangeBlockedReason;

    [ObservableProperty] private string _registrationNumber = string.Empty;
    [ObservableProperty] private string _academicYearLabel = string.Empty;

    [ObservableProperty] private Guid? _existingStudentId;
    [ObservableProperty] private Guid? _academicYearId;

    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _middleName = string.Empty;
    [ObservableProperty] private Domain.Enums.Gender? _gender;
    [ObservableProperty] private DateTime? _dateOfBirth;
    [ObservableProperty] private string _placeOfBirth = string.Empty;
    [ObservableProperty] private string _nationality = "Congolaise";
    [ObservableProperty] private string _language = string.Empty;
    [ObservableProperty] private string _religion = string.Empty;
    [ObservableProperty] private string? _photoPath;
    [ObservableProperty] private string? _pendingPhotoFilePath;

    [ObservableProperty] private Guid? _selectedSectionId;
    [ObservableProperty] private EnrollmentClassOptionDto? _selectedClass;
    [ObservableProperty] private int? _orderNumber;
    [ObservableProperty] private DateTime? _enrollmentDate;
    [ObservableProperty] private RegistrationKind _registrationKind = RegistrationKind.NouvelleInscription;
    [ObservableProperty] private string _previousSchool = string.Empty;
    [ObservableProperty] private string _previousStudentCode = string.Empty;
    [ObservableProperty] private string _permanentNumber = string.Empty;

    [ObservableProperty] private string _guardianSearchText = string.Empty;
    [ObservableProperty] private bool _guardianSearchHasNoResults;

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
    [ObservableProperty] private Domain.Enums.Gender? _contact1Gender;
    [ObservableProperty] private Guid? _contact1ExistingGuardianId;

    [ObservableProperty] private string _contact2LastName = string.Empty;
    [ObservableProperty] private string _contact2FirstName = string.Empty;
    [ObservableProperty] private string _contact2Phone = string.Empty;
    [ObservableProperty] private string _contact2Email = string.Empty;
    [ObservableProperty] private string _contact2Relationship = string.Empty;
    [ObservableProperty] private bool _contact2SameAddressAsStudent = true;
    [ObservableProperty] private Domain.Enums.Gender? _contact2Gender;
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

    [ObservableProperty] private int _completedDocumentsCount;

    partial void OnCanChangeClassChanged(bool value) => OnPropertyChanged(nameof(CanEditClassAssignment));

    partial void OnDossierEnrollmentIdChanged(Guid? value) => OnPropertyChanged(nameof(IsDossierLoaded));

    partial void OnPhotoPathChanged(string? value) => OnPropertyChanged(nameof(PhotoDisplayPath));

    partial void OnPendingPhotoFilePathChanged(string? value) => OnPropertyChanged(nameof(PhotoDisplayPath));

    partial void OnSelectedSectionIdChanged(Guid? value)
    {
        if (_loadingDossier || !CanChangeClass)
        {
            return;
        }

        SelectedClass = null;
        RefreshClassOptions();
    }

    partial void OnSelectedClassChanged(EnrollmentClassOptionDto? value) => _ = RefreshCapacityAsync();

    partial void OnDateOfBirthChanged(DateTime? value) => OnPropertyChanged(nameof(Age));

    partial void OnEnrollmentDateChanged(DateTime? value) => OnPropertyChanged(nameof(Age));

    partial void OnContact1LastNameChanged(string value) => OnPropertyChanged(nameof(IsContact1Filled));
    partial void OnContact1FirstNameChanged(string value) => OnPropertyChanged(nameof(IsContact1Filled));
    partial void OnContact1PhoneChanged(string value) => OnPropertyChanged(nameof(IsContact1Filled));
    partial void OnContact1EmailChanged(string value) => OnPropertyChanged(nameof(IsContact1Filled));
    partial void OnContact2LastNameChanged(string value) => OnPropertyChanged(nameof(IsContact2Filled));
    partial void OnContact2FirstNameChanged(string value) => OnPropertyChanged(nameof(IsContact2Filled));
    partial void OnContact2PhoneChanged(string value) => OnPropertyChanged(nameof(IsContact2Filled));
    partial void OnContact2EmailChanged(string value) => OnPropertyChanged(nameof(IsContact2Filled));

    public async Task LoadAsync(Guid studentId)
    {
        _studentId = studentId;
        _loadingDossier = true;
        IsBusy = true;
        ValidationMessage = null;
        DossierEnrollmentId = null;
        try
        {
            await InitializeAddressEditorsAsync();

            var dossier = await _wizardApi.GetStudentDossierForEditAsync(studentId);
            if (dossier.Dossier is null)
            {
                throw new InvalidOperationException("Le dossier renvoyé par le serveur est incomplet.");
            }

            var data = dossier.Dossier;

            DossierEnrollmentId = dossier.EnrollmentId;
            RegistrationNumber = dossier.RegistrationNumber;
            CanChangeClass = dossier.CanChangeClass;
            ClassChangeBlockedReason = dossier.ClassChangeBlockedReason;
            ExistingStudentId = data.ExistingStudentId ?? dossier.StudentId;

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

            ConfirmAccuracy = false;
            StatusMessage = CanChangeClass
                ? "Modifiez le dossier de l'élève puis enregistrez."
                : $"Modifiez le dossier de l'élève. Classe verrouillée : {ClassChangeBlockedReason}";
            OnPropertyChanged(nameof(CanEditClassAssignment));
            OnPropertyChanged(nameof(PhotoDisplayPath));
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.InnerException?.Message ?? ex.Message;
            StatusMessage = "Impossible de charger le dossier.";
        }
        finally
        {
            _loadingDossier = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ConfirmAccuracy)
        {
            ValidationMessage = "Cochez la confirmation d'exactitude des informations.";
            return;
        }

        if (!ValidateForSave(out var message))
        {
            ValidationMessage = message;
            return;
        }

        if (!DossierEnrollmentId.HasValue)
        {
            ValidationMessage = "Inscription introuvable pour la modification.";
            return;
        }

        IsBusy = true;
        ValidationMessage = null;
        try
        {
            await UploadPendingFilesAsync();
            var request = BuildRequest();

            var updateValidation = await _wizardApi.ValidateStudentDossierUpdateAsync(
                DossierEnrollmentId.Value,
                request);
            if (!updateValidation.IsValid)
            {
                ValidationMessage = updateValidation.Issues.FirstOrDefault()?.Message ?? "Validation échouée.";
                return;
            }

            var updateResult = await _wizardApi.UpdateStudentDossierAsync(DossierEnrollmentId.Value, request);
            StatusMessage =
                $"Dossier mis à jour — matricule {updateResult.RegistrationNumber}. {updateResult.Message}";
            CloseRequested?.Invoke(this, true);
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
    private void Cancel() => CloseRequested?.Invoke(this, false);

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
        doc.FileName = Path.GetFileName(dialog.FileName);
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

    private async Task ApplyGuardiansFromDossierAsync(
        IReadOnlyList<GuardianInputDto> guardians,
        AddressInputDto? studentAddress)
    {
        foreach (var guardian in guardians)
        {
            if (MatchesRelationship(guardian.Relationship, "Père", "Pere", "Father"))
            {
                ApplyGuardianToFather(guardian);
                continue;
            }

            if (MatchesRelationship(guardian.Relationship, "Mère", "Mere", "Mother"))
            {
                ApplyGuardianToMother(guardian);
                continue;
            }

            if (MatchesRelationship(guardian.Relationship, "Personne à contacter 1", "Contact 1"))
            {
                await ApplyGuardianToContact1Async(guardian);
                continue;
            }

            if (MatchesRelationship(guardian.Relationship, "Personne à contacter 2", "Contact 2"))
            {
                await ApplyGuardianToContact2Async(guardian);
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

    private void ApplyGuardianToFather(GuardianInputDto guardian)
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

    private void ApplyGuardianToMother(GuardianInputDto guardian)
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

    private async Task ApplyGuardianToContact1Async(GuardianInputDto guardian)
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

    private async Task ApplyGuardianToContact2Async(GuardianInputDto guardian)
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

    private void ResetGuardianFields()
    {
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

    private async Task LoadStructureAsync()
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

        if (!_loadingDossier
            && SelectedSectionId.HasValue
            && !Sections.Any(s => s.Id == SelectedSectionId))
        {
            SelectedSectionId = null;
            SelectedClass = null;
        }

        RefreshClassOptions();
    }

    private async Task RefreshCapacityAsync()
    {
        if (SelectedClass is null || !AcademicYearId.HasValue)
        {
            return;
        }

        try
        {
            await _wizardApi.GetClassCapacityAsync(SelectedClass.ClassRoomId, AcademicYearId.Value);
        }
        catch
        {
            // Capacity info is optional for dossier edit.
        }
    }

    private void RefreshClassOptions()
    {
        AvailableLocals.Clear();
        if (!SelectedSectionId.HasValue)
        {
            OnPropertyChanged(nameof(ShowEmptyClassHint));
            return;
        }

        foreach (var option in FilteredBySection()
                     .Where(c => c.IsSelectable)
                     .Where(IsClassAgeCompatible)
                     .OrderBy(c => c.FullDisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            AvailableLocals.Add(option);
        }

        OnPropertyChanged(nameof(ShowEmptyClassHint));
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

    private void ApplySelectedPhotoFile(string filePath)
    {
        var photoDoc = Documents.FirstOrDefault(d => d.DocumentType.Equals("Photo", StringComparison.OrdinalIgnoreCase));
        if (photoDoc is not null)
        {
            photoDoc.PendingFilePath = filePath;
            photoDoc.FileName = Path.GetFileName(filePath);
            photoDoc.Status = "En attente";
            photoDoc.LocalPath = null;
        }

        ValidationMessage = null;
        StatusMessage = "Photo sélectionnée — envoi au serveur lors de l'enregistrement.";
        UpdateDocumentProgress();
        OnPropertyChanged(nameof(PhotoDisplayPath));
    }

    private async Task UploadPendingFilesAsync()
    {
        if (string.IsNullOrWhiteSpace(LastName))
        {
            throw new InvalidOperationException("Le nom de l'élève est requis pour enregistrer les fichiers.");
        }

        if (string.IsNullOrWhiteSpace(AcademicYearLabel))
        {
            throw new InvalidOperationException("Année scolaire indisponible.");
        }

        foreach (var doc in Documents.Where(d => !string.IsNullOrWhiteSpace(d.PendingFilePath)))
        {
            var stored = await _wizardApi.StoreEnrollmentFileAsync(
                LastName,
                GetDossierFirstName(),
                RegistrationNumber,
                AcademicYearLabel,
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

    private CompleteEnrollmentRequest BuildRequest()
    {
        var guardians = BuildGuardians();
        var docs = Documents.Select(d => new EnrollmentDocumentStatusDto(
            d.DocumentType,
            string.IsNullOrWhiteSpace(d.LocalPath) ? "Manquant" : "Complet",
            d.FileName,
            d.LocalPath,
            d.FileSizeBytes)).ToList();

        return new CompleteEnrollmentRequest(
            ExistingStudentId ?? _studentId,
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
                SelectedClass?.SectionId ?? SelectedSectionId ?? Guid.Empty,
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
            null,
            ConfirmAccuracy);
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

    private bool ValidateForSave(out string message)
    {
        if (!ValidateIdentity(out message))
        {
            return false;
        }

        if (!ValidateScolarite(out message))
        {
            return false;
        }

        return ValidateResponsables(out message);
    }

    private bool ValidateIdentity(out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(LastName))
        {
            message = "Le nom est obligatoire.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(MiddleName))
        {
            message = "Le postnom est obligatoire.";
            return false;
        }

        if (!Gender.HasValue)
        {
            message = "Le sexe est obligatoire.";
            return false;
        }

        if (!DateOfBirth.HasValue)
        {
            message = "La date de naissance est obligatoire.";
            return false;
        }

        if (DateOfBirth.Value.Date >= DateTime.Today)
        {
            message = "La date de naissance doit être dans le passé.";
            return false;
        }

        return true;
    }

    private bool ValidateScolarite(out string message)
    {
        message = string.Empty;
        if (!SelectedSectionId.HasValue)
        {
            message = "Sélectionnez une section.";
            return false;
        }

        if (SelectedClass is null)
        {
            message = "Sélectionnez une classe.";
            return false;
        }

        if (!EnrollmentDate.HasValue)
        {
            message = "La date d'inscription est obligatoire.";
            return false;
        }

        if (EnrollmentDate.Value.Date > DateTime.Today)
        {
            message = "La date d'inscription ne peut pas être dans le futur.";
            return false;
        }

        if (CanChangeClass
            && SelectedClass.MaxCapacity.HasValue
            && SelectedClass.CurrentCount >= SelectedClass.MaxCapacity)
        {
            message = "Ce local est saturé — choisissez un autre local.";
            return false;
        }

        return true;
    }

    private bool ValidateResponsables(out string message)
    {
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
    }

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
        Domain.Enums.Gender? gender,
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

    private void UpdateDocumentProgress()
    {
        CompletedDocumentsCount = Documents.Count(d =>
            d.Status.Equals("Complet", StringComparison.OrdinalIgnoreCase)
            || d.Status.Equals("En attente", StringComparison.OrdinalIgnoreCase));
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
