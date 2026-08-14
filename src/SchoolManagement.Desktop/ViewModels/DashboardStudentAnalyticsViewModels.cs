using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Dashboard.DTOs;
using SchoolManagement.Application.EnrollmentWizard.DTOs;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class DashboardEnrolledStudentsDetailViewModel : ViewModelBase
{
    private readonly IPromoterDashboardApiService _dashboardApi;
    private readonly IStudentApiService _studentApi;
    private readonly INavigationService _navigation;

    public DashboardEnrolledStudentsDetailViewModel(
        IPromoterDashboardApiService dashboardApi,
        IStudentApiService studentApi,
        INavigationService navigation)
    {
        _dashboardApi = dashboardApi;
        _studentApi = studentApi;
        _navigation = navigation;
        AcademicYearRefreshBridge.CurrentYearChanged += OnAcademicYearChanged;
        _ = LoadAsync();
    }

    public ObservableCollection<DashboardEnrolledRegimeGroupRow> RegimeGroups { get; } = [];
    public ObservableCollection<DashboardEnrolledStudentRow> SearchResults { get; } = [];

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isSearchMode;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _searchResultsCount;
    [ObservableProperty] private string? _searchStatusMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _summaryLabel = "—";
    [ObservableProperty] private string _yearLabel = "—";
    [ObservableProperty] private int _totalStudents;
    [ObservableProperty] private int _totalBoys;
    [ObservableProperty] private int _totalGirls;
    [ObservableProperty] private int _activeClassesCount;
    [ObservableProperty] private string _boysPercentLabel = "—";
    [ObservableProperty] private string _girlsPercentLabel = "—";
    [ObservableProperty] private string _averagePerClassLabel = "—";
    [ObservableProperty] private string _lastUpdatedLabel = "—";

    private CancellationTokenSource? _searchCts;

    public string SearchResultsLabel => SearchResultsCount switch
    {
        0 => "Aucun élève trouvé",
        1 => "1 élève trouvé",
        _ => $"{SearchResultsCount:N0} élèves trouvés"
    };

    partial void OnSearchResultsCountChanged(int value) => OnPropertyChanged(nameof(SearchResultsLabel));

    partial void OnSearchTextChanged(string value)
    {
        var active = !string.IsNullOrWhiteSpace(value);
        IsSearchMode = active;
        if (!active)
        {
            _searchCts?.Cancel();
            SearchResults.Clear();
            SearchResultsCount = 0;
            SearchStatusMessage = null;
            return;
        }

        QueueSearch();
    }

    private void QueueSearch()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = DebouncedSearchAsync(token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            if (!token.IsCancellationRequested)
            {
                await ExecuteSearchAsync();
            }
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
    }

    private async Task ExecuteSearchAsync()
    {
        var term = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            return;
        }

        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null)
        {
            SearchStatusMessage = "Sélectionnez une année scolaire dans la barre supérieure.";
            SearchResults.Clear();
            SearchResultsCount = 0;
            return;
        }

        IsSearching = true;
        SearchStatusMessage = null;
        try
        {
            var result = await _studentApi.SearchAsync(new StudentSearchRequest(
                term,
                yearId,
                null,
                null,
                null,
                null,
                null,
                null,
                ApplyFilters: false,
                IncludeAll: false,
                IncludeInscrits: true,
                Page: 1,
                PageSize: 500));

            SearchResults.Clear();
            var rowNumber = 1;
            foreach (var student in result.Items
                         .Where(s => s.IsEnrolledCurrentYear || !string.IsNullOrWhiteSpace(s.CurrentYearClassName))
                         .OrderBy(s => s.LastName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(s => s.FirstName, StringComparer.OrdinalIgnoreCase))
            {
                SearchResults.Add(new DashboardEnrolledStudentRow(
                    student.Id,
                    StudentDisplayName.Format(student.LastName, student.MiddleName, student.FirstName),
                    student.Gender == Gender.Feminin ? "F" : "M",
                    student.RegistrationNumber,
                    student.CurrentYearClassName ?? "—",
                    student.DateOfBirth.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    rowNumber++,
                    OpenStudentConsultation));
            }

            SearchResultsCount = SearchResults.Count;
            SearchStatusMessage = SearchResultsCount == 0 ? "Aucun élève inscrit ne correspond à cette recherche." : null;
        }
        catch (Exception ex)
        {
            SearchResults.Clear();
            SearchResultsCount = 0;
            SearchStatusMessage = ex.Message;
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void OnAcademicYearChanged()
    {
        _ = LoadAsync();
        if (IsSearchMode)
        {
            QueueSearch();
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        RegimeGroups.Clear();

        try
        {
            var yearId = AcademicYearRefreshBridge.SelectedYearId;
            YearLabel = AcademicYearRefreshBridge.SelectedYear?.Label ?? "Année courante";
            if (yearId is null)
            {
                StatusMessage = "Sélectionnez une année scolaire dans la barre supérieure.";
                return;
            }

            var data = await _dashboardApi.GetEnrolledStudentsAsync();
            SummaryLabel = $"{data.TotalStudents:N0} élèves · ♂ {data.TotalBoys:N0} · ♀ {data.TotalGirls:N0}";
            TotalStudents = data.TotalStudents;
            TotalBoys = data.TotalBoys;
            TotalGirls = data.TotalGirls;

            var regimeMap = new Dictionary<string, DashboardEnrolledRegimeGroupRow>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in data.Sections)
            {
                var regimeName = StudentRegimeCatalog.ResolveRegime(section.SectionName);
                if (!regimeMap.TryGetValue(regimeName, out var regime))
                {
                    regime = new DashboardEnrolledRegimeGroupRow(regimeName);
                    regimeMap[regimeName] = regime;
                }

                regime.TotalStudents += section.TotalStudents;
                regime.TotalBoys += section.Boys;
                regime.TotalGirls += section.Girls;

                foreach (var cls in section.Classes)
                {
                    var existing = regime.Classes.FirstOrDefault(c => c.ClassRoomId == cls.ClassRoomId);
                    if (existing is null)
                    {
                        regime.Classes.Add(new DashboardEnrolledClassRow(
                            cls.ClassRoomId,
                            cls.ClassName,
                            section.SectionName,
                            cls.TotalStudents,
                            cls.Boys,
                            cls.Girls,
                            yearId.Value,
                            OpenStudentConsultation));
                    }
                    else
                    {
                        existing.TotalStudents += cls.TotalStudents;
                        existing.TotalBoys += cls.Boys;
                        existing.TotalGirls += cls.Girls;
                    }
                }
            }

            foreach (var regime in regimeMap.Values.OrderBy(r => StudentRegimeCatalog.SortKey(r.RegimeName)))
            {
                foreach (var cls in regime.Classes.OrderBy(c => c.ClassName, StringComparer.OrdinalIgnoreCase))
                {
                    regime.ClassesSorted.Add(cls);
                }

                RegimeGroups.Add(regime);
            }

            var fr = CultureInfo.GetCultureInfo("fr-FR");
            ActiveClassesCount = regimeMap.Values.SelectMany(r => r.Classes).Count();
            BoysPercentLabel = TotalStudents > 0
                ? $"{100.0 * TotalBoys / TotalStudents:N2} %"
                : "—";
            GirlsPercentLabel = TotalStudents > 0
                ? $"{100.0 * TotalGirls / TotalStudents:N2} %"
                : "—";
            AveragePerClassLabel = ActiveClassesCount > 0
                ? (TotalStudents / (double)ActiveClassesCount).ToString("N1", fr)
                : "—";
            LastUpdatedLabel = $"Dernière mise à jour : {DateTime.Now:dd/MM/yyyy HH:mm}";
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
    public async Task LoadClassStudents(DashboardEnrolledClassRow? row)
    {
        if (row is null)
        {
            return;
        }

        await LoadClassStudentsAsync(row);
    }

    private async Task LoadClassStudentsAsync(DashboardEnrolledClassRow row)
    {
        if (row.StudentsLoaded || row.IsLoadingStudents)
        {
            return;
        }

        row.IsLoadingStudents = true;
        row.Students.Clear();
        try
        {
            var result = await _studentApi.SearchAsync(new StudentSearchRequest(
                null,
                row.AcademicYearId,
                null,
                null,
                row.ClassRoomId,
                null,
                null,
                null,
                ApplyFilters: true,
                IncludeAll: false,
                IncludeInscrits: true,
                Page: 1,
                PageSize: 500));

            var rowNumber = 1;
            foreach (var student in result.Items.OrderBy(s => s.LastName).ThenBy(s => s.FirstName))
            {
                row.Students.Add(new DashboardEnrolledStudentRow(
                    student.Id,
                    StudentDisplayName.Format(student.LastName, student.MiddleName, student.FirstName),
                    student.Gender == Gender.Feminin ? "F" : "M",
                    student.RegistrationNumber,
                    student.CurrentYearClassName ?? row.ClassName,
                    student.DateOfBirth.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    rowNumber++,
                    OpenStudentConsultation));
            }

            row.StudentsLoaded = true;
        }
        catch (Exception ex)
        {
            row.LoadError = ex.Message;
        }
        finally
        {
            row.IsLoadingStudents = false;
        }
    }

    private void OpenStudentConsultation(Guid studentId)
    {
        DashboardStudentsNavigationBridge.RequestConsultation(studentId);
        _navigation.NavigateTo<DashboardStudentConsultationViewModel>(recordBack: true);
    }

    [RelayCommand]
    private void GoBack() => _navigation.NavigateBack();
}

public partial class DashboardEnrolledRegimeGroupRow : ObservableObject
{
    public DashboardEnrolledRegimeGroupRow(string regimeName)
    {
        RegimeName = regimeName;
    }

    public string RegimeName { get; }

    public int TotalStudents { get; set; }

    public int TotalBoys { get; set; }

    public int TotalGirls { get; set; }

    public ObservableCollection<DashboardEnrolledClassRow> Classes { get; } = [];

    public ObservableCollection<DashboardEnrolledClassRow> ClassesSorted { get; } = [];

    public string HeaderLabel => $"{RegimeName} — {TotalStudents:N0} élève(s) · ♂ {TotalBoys:N0} · ♀ {TotalGirls:N0}";

    public string RegimeTitle => RegimeName.ToUpperInvariant();

    public string StudentsBadge => $"{TotalStudents} élève(s)";

    public string BoysBadge => $"{TotalBoys} garçon(s)";

    public string GirlsBadge => $"{TotalGirls} fille(s)";

    public string ClassesBadge => $"{ClassesSorted.Count} classe(s)";

    public string AccentBackgroundHex => RegimeName switch
    {
        "Maternelle" => "#FDF2F8",
        "Primaire" => "#EFF6FF",
        _ => "#ECFDF5"
    };

    public string AccentBorderHex => RegimeName switch
    {
        "Maternelle" => "#FBCFE8",
        "Primaire" => "#BFDBFE",
        _ => "#A7F3D0"
    };

    public string AccentTitleHex => RegimeName switch
    {
        "Maternelle" => "#BE185D",
        "Primaire" => "#1E3A8A",
        _ => "#047857"
    };
}

public partial class DashboardEnrolledClassRow : ObservableObject
{
    private readonly Action<Guid> _openStudent;

    public DashboardEnrolledClassRow(
        Guid classRoomId,
        string className,
        string sectionName,
        int totalStudents,
        int boys,
        int girls,
        Guid academicYearId,
        Action<Guid> openStudent)
    {
        ClassRoomId = classRoomId;
        ClassName = className;
        SectionName = sectionName;
        TotalStudents = totalStudents;
        TotalBoys = boys;
        TotalGirls = girls;
        AcademicYearId = academicYearId;
        _openStudent = openStudent;
    }

    public Guid ClassRoomId { get; }
    public string ClassName { get; }
    public string SectionName { get; }
    public Guid AcademicYearId { get; }

    [ObservableProperty] private int _totalStudents;
    [ObservableProperty] private int _totalBoys;
    [ObservableProperty] private int _totalGirls;
    [ObservableProperty] private bool _isLoadingStudents;
    [ObservableProperty] private bool _studentsLoaded;
    [ObservableProperty] private string? _loadError;

    public ObservableCollection<DashboardEnrolledStudentRow> Students { get; } = [];

    public string HeaderLabel =>
        $"{ClassName} — {TotalStudents:N0} · ♂ {TotalBoys:N0} · ♀ {TotalGirls:N0}";

    public string ClassTitle => ClassName.ToUpperInvariant();

    public string StudentCountLabel => $"{TotalStudents} élève(s)";

    [RelayCommand]
    private void OpenStudent(DashboardEnrolledStudentRow? row)
    {
        if (row is null)
        {
            return;
        }

        _openStudent(row.StudentId);
    }
}

public partial class DashboardEnrolledStudentRow : ObservableObject
{
    private readonly Action<Guid> _openStudent;

    public DashboardEnrolledStudentRow(
        Guid studentId,
        string fullName,
        string genderLabel,
        string registrationNumber,
        string className,
        string dateOfBirthLabel,
        int rowNumber,
        Action<Guid> openStudent)
    {
        StudentId = studentId;
        FullName = fullName;
        GenderLabel = genderLabel;
        RegistrationNumber = registrationNumber;
        ClassName = className;
        DateOfBirthLabel = dateOfBirthLabel;
        RowNumber = rowNumber;
        _openStudent = openStudent;
    }

    public Guid StudentId { get; }
    public string FullName { get; }
    public string GenderLabel { get; }
    public string RegistrationNumber { get; }
    public string ClassName { get; }
    public string DateOfBirthLabel { get; }
    public int RowNumber { get; }

    [RelayCommand]
    private void Open() => _openStudent(StudentId);
}

public partial class DashboardStudentConsultationViewModel : ViewModelBase
{
    private readonly IEnrollmentWizardApiService _wizardApi;
    private readonly IStudentApiService _studentApi;
    private readonly IFinanceApiService _financeApi;
    private readonly IPaymentApiService _paymentApi;
    private readonly ISchoolFeeApiService _schoolFeeApi;
    private readonly IGeographyApiService _geographyApi;
    private readonly INavigationService _navigation;

    public DashboardStudentConsultationViewModel(
        IEnrollmentWizardApiService wizardApi,
        IStudentApiService studentApi,
        IFinanceApiService financeApi,
        IPaymentApiService paymentApi,
        ISchoolFeeApiService schoolFeeApi,
        IGeographyApiService geographyApi,
        INavigationService navigation)
    {
        _wizardApi = wizardApi;
        _studentApi = studentApi;
        _financeApi = financeApi;
        _paymentApi = paymentApi;
        _schoolFeeApi = schoolFeeApi;
        _geographyApi = geographyApi;
        _navigation = navigation;
        _ = LoadAsync();
    }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _identityTitle = "Consultation élève";
    [ObservableProperty] private string _registrationNumber = "—";
    [ObservableProperty] private string? _photoPath;
    [ObservableProperty] private string _financialTotalsLabel = "—";

    public ObservableCollection<StudentConsultationInfoItem> IdentificationItems { get; } = [];
    public ObservableCollection<StudentConsultationInfoItem> AddressItems { get; } = [];
    public ObservableCollection<StudentConsultationInfoItem> SchoolItems { get; } = [];
    public ObservableCollection<StudentConsultationGuardianRow> Guardians { get; } = [];
    public ObservableCollection<StudentConsultationInfoItem> MedicalItems { get; } = [];
    public ObservableCollection<StudentConsultationInfoItem> DocumentItems { get; } = [];
    public ObservableCollection<StudentConsultationFinancialGroupRow> FinancialGroups { get; } = [];

    [RelayCommand]
    private void GoBack() => _navigation.NavigateBack();

    [RelayCommand]
    private async Task LoadAsync()
    {
        var studentId = DashboardStudentsNavigationBridge.ConsumeConsultationStudentId();
        if (studentId is null)
        {
            StatusMessage = "Aucun élève sélectionné.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        ClearSections();

        try
        {
            var dossier = await _wizardApi.GetStudentDossierForEditAsync(studentId.Value);
            var profile = await _studentApi.GetProfileAsync(studentId.Value);
            var d = dossier.Dossier;
            var currentEnrollment = profile.Enrollments.FirstOrDefault(e => e.IsCurrentYear && e.IsActive)
                ?? profile.Enrollments.FirstOrDefault();
            IdentityTitle = StudentDisplayName.Format(d.LastName, d.MiddleName, d.FirstName);
            RegistrationNumber = dossier.RegistrationNumber;
            PhotoPath = d.PhotoPath;

            AddItem(IdentificationItems, "Matricule", dossier.RegistrationNumber);
            AddItem(IdentificationItems, "Nom", d.LastName);
            AddItem(IdentificationItems, "Postnom", d.MiddleName);
            AddItem(IdentificationItems, "Prénom", d.FirstName);
            AddItem(IdentificationItems, "Sexe", d.Gender == Gender.Feminin ? "Féminin" : "Masculin");
            AddItem(IdentificationItems, "Date de naissance", d.DateOfBirth.ToString("dd/MM/yyyy"));
            AddItem(IdentificationItems, "Lieu de naissance", d.PlaceOfBirth);
            AddItem(IdentificationItems, "Nationalité", d.Nationality);
            AddItem(IdentificationItems, "Téléphone", d.Phone);
            AddItem(IdentificationItems, "Email", d.Email);

            var addr = d.ResidenceAddress;
            if (addr is not null)
            {
                var resolved = await ResolveAddressLabelsAsync(addr);
                AddItem(AddressItems, "Province", resolved.Province);
                AddItem(AddressItems, "Ville / territoire", resolved.City);
                AddItem(AddressItems, "Commune", resolved.Commune);
                AddItem(AddressItems, "Quartier", addr.Neighborhood);
                AddItem(AddressItems, "Avenue", addr.Avenue);
                AddItem(AddressItems, "Numéro", addr.HouseNumber);
            }

            var sc = d.Scolarite;
            AddItem(SchoolItems, "Année scolaire", currentEnrollment?.AcademicYearLabel);
            AddItem(SchoolItems, "Section", currentEnrollment?.SectionName);
            AddItem(SchoolItems, "Classe", currentEnrollment?.ClassDisplayName);
            AddItem(SchoolItems, "Option", currentEnrollment?.StudyOption);
            AddItem(SchoolItems, "Local", currentEnrollment?.LocalName);
            AddItem(SchoolItems, "Date d'inscription", sc.EnrollmentDate.ToString("dd/MM/yyyy"));
            AddItem(SchoolItems, "École précédente", sc.PreviousSchool);
            AddItem(SchoolItems, "Code élève précédent", sc.PreviousStudentCode);

            foreach (var g in d.Guardians)
            {
                Guardians.Add(new StudentConsultationGuardianRow(
                    g.Relationship,
                    $"{g.LastName} {g.FirstName}".Trim(),
                    g.Phone,
                    g.Email,
                    g.Profession));
            }

            var med = d.Medical;
            AddItem(MedicalItems, "Groupe sanguin", med.BloodGroup);
            AddItem(MedicalItems, "Allergies", med.Allergies);
            AddItem(MedicalItems, "Maladies chroniques", med.ChronicDiseases);
            AddItem(MedicalItems, "Handicap", med.Disability);
            AddItem(MedicalItems, "Médecin", med.DoctorName);
            AddItem(MedicalItems, "Centre médical", med.MedicalCenter);

            foreach (var doc in d.Documents)
            {
                AddItem(DocumentItems, doc.DocumentType, doc.Status);
            }

            var academicYearId = currentEnrollment?.AcademicYearId
                ?? AcademicYearRefreshBridge.SelectedYearId
                ?? throw new InvalidOperationException("Année scolaire introuvable.");
            await LoadFinancialAsync(dossier.EnrollmentId, dossier.StudentId, academicYearId);
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

    private async Task LoadFinancialAsync(Guid enrollmentId, Guid studentId, Guid academicYearId)
    {
        var catalog = await _schoolFeeApi.GetCatalogAsync();
        var summary = await _paymentApi.GetStudentFinancialSummaryAsync(studentId, academicYearId);
        FinancialTotalsLabel =
            $"Total prévu : {summary.TotalDue:N0} {summary.Currency} · " +
            $"Total payé : {summary.TotalPaid:N0} {summary.Currency} · " +
            $"Reste : {summary.Balance:N0} {summary.Currency}";

        var situations = await _financeApi.SearchPaymentSituationsAsync(new StudentPaymentSituationSearchRequest(
            AcademicYearId: academicYearId,
            Page: 1,
            PageSize: 100,
            StudentId: studentId));

        var categoryName = situations.Items.FirstOrDefault()?.FeePricingCategoryName ?? "—";

        foreach (var feeType in catalog.FeeTypes.Where(f => f.IsActive).OrderBy(f => f.Name))
        {
            try
            {
                var plan = await _financeApi.GetInstallmentPaymentPlanAsync(enrollmentId, feeType.Id);
                var situation = situations.Items.FirstOrDefault(s => s.FeeTypeId == feeType.Id);
                var group = new StudentConsultationFinancialGroupRow(
                    feeType.Name,
                    situation?.FeePricingCategoryName ?? categoryName,
                    plan.Currency.ToString());

                foreach (var line in plan.Lines)
                {
                    group.Lines.Add(new StudentConsultationFinancialLineRow(
                        line.InstallmentName,
                        line.AmountExpected,
                        line.AmountPaid,
                        line.Remaining,
                        plan.Currency.ToString(),
                        line.DueDate?.ToString("dd/MM/yyyy")));
                }

                group.RefreshTotals();
                FinancialGroups.Add(group);
            }
            catch
            {
                // Type de frais sans tranches configurées pour cette inscription.
            }
        }

        if (FinancialGroups.Count == 0 && situations.Items.Count > 0)
        {
            foreach (var item in situations.Items)
            {
                var group = new StudentConsultationFinancialGroupRow(
                    item.FeeTypeName,
                    item.FeePricingCategoryName,
                    item.Currency.ToString());
                group.Lines.Add(new StudentConsultationFinancialLineRow(
                    "Global",
                    item.AmountExpected,
                    item.AmountPaid,
                    item.Balance,
                    item.Currency.ToString(),
                    null));
                group.RefreshTotals();
                FinancialGroups.Add(group);
            }
        }
    }

    private async Task<(string? Province, string? City, string? Commune)> ResolveAddressLabelsAsync(
        SchoolManagement.Application.Geography.DTOs.AddressInputDto addr)
    {
        string? province = null;
        string? city = null;
        string? commune = null;

        if (addr.ProvinceId is Guid provinceId && addr.CountryId is Guid countryId)
        {
            var provinces = await _geographyApi.GetProvincesAsync(countryId);
            province = provinces.FirstOrDefault(p => p.Id == provinceId)?.Name;
        }

        if (addr.CityId is Guid cityId && addr.ProvinceId is Guid provId)
        {
            var cities = await _geographyApi.GetCitiesAsync(provId);
            city = cities.FirstOrDefault(c => c.Id == cityId)?.Name;
        }

        if (addr.CommuneId is Guid communeId && addr.CityId is Guid cId)
        {
            var communes = await _geographyApi.GetCommunesAsync(cId);
            commune = communes.FirstOrDefault(c => c.Id == communeId)?.Name;
        }

        return (province, city, commune);
    }

    private void ClearSections()
    {
        IdentificationItems.Clear();
        AddressItems.Clear();
        SchoolItems.Clear();
        Guardians.Clear();
        MedicalItems.Clear();
        DocumentItems.Clear();
        FinancialGroups.Clear();
    }

    private static void AddItem(ObservableCollection<StudentConsultationInfoItem> target, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        target.Add(new StudentConsultationInfoItem(label, value));
    }
}

public sealed record StudentConsultationInfoItem(string Label, string Value);

public sealed record StudentConsultationGuardianRow(
    string Role,
    string FullName,
    string? Phone,
    string? Email,
    string? Profession);

public partial class StudentConsultationFinancialGroupRow : ObservableObject
{
    public StudentConsultationFinancialGroupRow(string feeTypeName, string categoryName, string currency)
    {
        FeeTypeName = feeTypeName;
        CategoryName = categoryName;
        Currency = currency;
    }

    public string FeeTypeName { get; }
    public string CategoryName { get; }
    public string Currency { get; }

    [ObservableProperty] private string _totalsLabel = "—";

    public ObservableCollection<StudentConsultationFinancialLineRow> Lines { get; } = [];

    public void RefreshTotals()
    {
        var expected = Lines.Sum(l => l.AmountExpected);
        var paid = Lines.Sum(l => l.AmountPaid);
        var balance = Lines.Sum(l => l.Balance);
        TotalsLabel = $"Prévu {expected:N0} · Payé {paid:N0} · Reste {balance:N0} {Currency}";
    }
}

public sealed record StudentConsultationFinancialLineRow(
    string InstallmentName,
    decimal AmountExpected,
    decimal AmountPaid,
    decimal Balance,
    string Currency,
    string? DueDateLabel);
