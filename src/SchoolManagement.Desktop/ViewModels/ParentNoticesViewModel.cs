using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.ParentNotices.DTOs;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Printing;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Entities.Documents;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public sealed record ParentNoticeField(string Group, string Label, string Token);
public sealed record ParentNoticeLayoutOption(ParentNoticePageLayout Value, string Label);
public sealed record ParentNoticeOrientationOption(ParentNoticePageOrientation Value, string Label);

public partial class ParentNoticeInstallmentOption : ObservableObject
{
    public ParentNoticeInstallmentOption(FeeTypeInstallmentDto installment, bool isSelected)
    {
        Installment = installment;
        _isSelected = isSelected;
    }

    public FeeTypeInstallmentDto Installment { get; }
    public Guid FeeInstallmentId => Installment.FeeInstallmentId;
    public string InstallmentName => Installment.InstallmentName;
    public int SortOrder => Installment.SortOrder;
    [ObservableProperty] private bool _isSelected;
}

public partial class ParentNoticeRecipientRow : ObservableObject
{
    public required StudentPaymentSituationDto Situation { get; init; }
    public StudentDto? Student { get; init; }
    [ObservableProperty] private bool _isSelected;
    public Guid StudentId => Situation.StudentId;
    public string RegistrationNumber => Situation.RegistrationNumber;
    public string FullName => Situation.FullName;
    public string ClassName => Situation.ClassName;
    public string SectionName => Situation.SectionName ?? "—";
    public string BalanceDisplay => $"{Math.Max(0, Situation.Balance):N0} {Situation.Currency}";
}

public partial class ParentNoticesViewModel : ViewModelBase
{
    private readonly IParentNoticeApiService _noticeApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly IAcademicApiService _academicApi;
    private readonly ISchoolFeeApiService _feeApi;
    private readonly IFinanceApiService _financeApi;
    private readonly IStudentApiService _studentApi;
    private readonly IDocumentBrandingApiService _brandingApi;
    private readonly IDocumentBrandingPathResolver _brandingPathResolver;
    private IReadOnlyList<ClassRoomDto> _allClasses = [];
    private HashSet<Guid>? _pendingRecipientIds;
    private bool _suppressFilterReload;
    private SchoolDto? _school;
    private DocumentBrandingConfigurationDto? _branding;

    public ParentNoticesViewModel(
        IParentNoticeApiService noticeApi,
        ISchoolApiService schoolApi,
        IAcademicApiService academicApi,
        ISchoolFeeApiService feeApi,
        IFinanceApiService financeApi,
        IStudentApiService studentApi,
        IDocumentBrandingApiService brandingApi,
        IDocumentBrandingPathResolver brandingPathResolver)
    {
        _noticeApi = noticeApi;
        _schoolApi = schoolApi;
        _academicApi = academicApi;
        _feeApi = feeApi;
        _financeApi = financeApi;
        _studentApi = studentApi;
        _brandingApi = brandingApi;
        _brandingPathResolver = brandingPathResolver;
        ContentRtfBase64 = ParentNoticeDocumentBuilder.CreateDefaultTemplate();
        foreach (var field in AvailableFields) Fields.Add(field);
        Layouts.Add(new ParentNoticeLayoutOption(ParentNoticePageLayout.FullPage, "Une page A4 par élève"));
        Layouts.Add(new ParentNoticeLayoutOption(ParentNoticePageLayout.TwoPerPage, "Deux avis côte à côte sur A4 paysage"));
        Orientations.Add(new ParentNoticeOrientationOption(ParentNoticePageOrientation.Portrait, "Portrait"));
        Orientations.Add(new ParentNoticeOrientationOption(ParentNoticePageOrientation.Landscape, "Paysage"));
        SelectedOrientation = Orientations[0];
        SelectedLayout = Layouts[0];
        _ = InitializeAsync();
    }

    public ObservableCollection<AcademicYearDto> AcademicYears { get; } = [];
    public ObservableCollection<SectionDto> Sections { get; } = [];
    public ObservableCollection<ClassRoomDto> Classes { get; } = [];
    public ObservableCollection<FeeTypeDto> FeeTypes { get; } = [];
    public ObservableCollection<ParentNoticeInstallmentOption> Installments { get; } = [];
    public ObservableCollection<ParentNoticeRecipientRow> Recipients { get; } = [];
    public ObservableCollection<ParentNoticeDto> Notices { get; } = [];
    public ObservableCollection<ParentNoticeField> Fields { get; } = [];
    public ObservableCollection<ParentNoticeLayoutOption> Layouts { get; } = [];
    public ObservableCollection<ParentNoticeOrientationOption> Orientations { get; } = [];

    [ObservableProperty] private Guid? _currentNoticeId;
    [ObservableProperty] private string _title = "Rappel de paiement";
    [ObservableProperty] private string? _subject = "Rappel concernant les frais scolaires";
    [ObservableProperty] private string _contentRtfBase64;
    [ObservableProperty] private AcademicYearDto? _selectedAcademicYear;
    [ObservableProperty] private SectionDto? _selectedSection;
    [ObservableProperty] private ClassRoomDto? _selectedClass;
    [ObservableProperty] private FeeTypeDto? _selectedFeeType;
    [ObservableProperty] private ParentNoticeLayoutOption? _selectedLayout;
    [ObservableProperty] private ParentNoticeOrientationOption? _selectedOrientation;
    [ObservableProperty] private bool _includeSchoolHeader = true;
    [ObservableProperty] private ParentNoticeDto? _selectedNotice;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private int _selectedRecipientCount;
    [ObservableProperty] private int _selectedInstallmentCount;
    [ObservableProperty] private bool _isLeftPanelVisible = true;
    [ObservableProperty] private bool _isRightPanelVisible = true;

    public string SelectionSummary => $"{SelectedRecipientCount} élève(s) sélectionné(s)";
    public string InstallmentSelectionSummary => $"{SelectedInstallmentCount} tranche(s) sélectionnée(s)";
    public string LeftPanelToggleLabel => IsLeftPanelVisible ? "◀ Ciblage" : "Ciblage ▶";
    public string RightPanelToggleLabel => IsRightPanelVisible ? "Champs ▶" : "◀ Champs";
    public bool IsPageOrientationEnabled => SelectedLayout?.Value != ParentNoticePageLayout.TwoPerPage;
    public double EditorPageWidth => SelectedLayout?.Value == ParentNoticePageLayout.TwoPerPage
        ? 561.26
        : SelectedOrientation?.Value == ParentNoticePageOrientation.Landscape ? 1122.52 : 793.7;
    public double EditorPageHeight => SelectedLayout?.Value == ParentNoticePageLayout.TwoPerPage
        ? 793.7
        : SelectedOrientation?.Value == ParentNoticePageOrientation.Landscape ? 793.7 : 1122.52;
    public string EditorPageLabel => SelectedLayout?.Value == ParentNoticePageLayout.TwoPerPage
        ? "Modèle A5 portrait — deux avis côte à côte sur A4 paysage"
        : $"Modèle A4 {(SelectedOrientation?.Value == ParentNoticePageOrientation.Landscape ? "paysage" : "portrait")}";
    public Thickness EditorPagePadding => SelectedLayout?.Value == ParentNoticePageLayout.TwoPerPage
        ? new Thickness(30, 24, 30, 24)
        : new Thickness(65, 55, 65, 55);

    partial void OnSelectedAcademicYearChanged(AcademicYearDto? value)
    { if (!_suppressFilterReload) _ = ReloadStructureAndRecipientsAsync(); }

    partial void OnSelectedSectionChanged(SectionDto? value)
    {
        FilterClasses();
        if (SelectedClass is not null && value is not null && SelectedClass.SectionId != value.Id) SelectedClass = null;
        if (!_suppressFilterReload) _ = LoadRecipientsAsync();
    }

    partial void OnSelectedClassChanged(ClassRoomDto? value)
    { if (!_suppressFilterReload) _ = LoadRecipientsAsync(); }

    partial void OnSelectedFeeTypeChanged(FeeTypeDto? value)
    { if (!_suppressFilterReload) _ = ReloadInstallmentsAndRecipientsAsync(); }

    partial void OnSelectedRecipientCountChanged(int value) => OnPropertyChanged(nameof(SelectionSummary));
    partial void OnSelectedInstallmentCountChanged(int value) => OnPropertyChanged(nameof(InstallmentSelectionSummary));
    partial void OnIsLeftPanelVisibleChanged(bool value) => OnPropertyChanged(nameof(LeftPanelToggleLabel));
    partial void OnIsRightPanelVisibleChanged(bool value) => OnPropertyChanged(nameof(RightPanelToggleLabel));

    partial void OnSelectedLayoutChanged(ParentNoticeLayoutOption? value)
    {
        if (value?.Value == ParentNoticePageLayout.TwoPerPage)
            SelectedOrientation = Orientations.FirstOrDefault(x => x.Value == ParentNoticePageOrientation.Portrait);
        NotifyPagePresentationChanged();
    }

    partial void OnSelectedOrientationChanged(ParentNoticeOrientationOption? value) => NotifyPagePresentationChanged();

    [RelayCommand] private void ToggleLeftPanel() => IsLeftPanelVisible = !IsLeftPanelVisible;
    [RelayCommand] private void ToggleRightPanel() => IsRightPanelVisible = !IsRightPanelVisible;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var yearsTask = _schoolApi.GetAcademicYearsAsync();
            var sectionsTask = _academicApi.GetSectionsAsync();
            var catalogTask = _feeApi.GetCatalogAsync();
            var noticesTask = _noticeApi.ListAsync();
            var schoolTask = _schoolApi.GetCurrentSchoolAsync();
            var brandingTask = _brandingApi.GetConfigurationAsync();
            await Task.WhenAll(yearsTask, sectionsTask, catalogTask, noticesTask, schoolTask, brandingTask);
            _school = schoolTask.Result;
            _branding = brandingTask.Result;
            Replace(AcademicYears, yearsTask.Result.OrderByDescending(y => y.StartDate));
            Replace(Sections, sectionsTask.Result.OrderBy(s => s.Name));
            Replace(FeeTypes, catalogTask.Result.FeeTypes.Where(f => f.IsActive).OrderBy(f => f.Name));
            Replace(Notices, noticesTask.Result);
            SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.IsCurrent) ?? AcademicYears.FirstOrDefault();
            SelectedFeeType = FeeTypes.FirstOrDefault();
            await ReloadStructureAndRecipientsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ReloadStructureAndRecipientsAsync()
    {
        if (SelectedAcademicYear is null) return;
        try
        {
            _allClasses = await _academicApi.GetClassRoomsAsync(SelectedAcademicYear.Id);
            FilterClasses();
            await LoadRecipientsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task ReloadInstallmentsAndRecipientsAsync()
    {
        await ReloadInstallmentsAsync(null);
        await LoadRecipientsAsync();
    }

    private async Task ReloadInstallmentsAsync(IReadOnlyCollection<Guid>? selectedIds)
    {
        foreach (var old in Installments) old.PropertyChanged -= InstallmentOnPropertyChanged;
        Installments.Clear();
        if (SelectedFeeType is null) { RefreshSelectedInstallmentCount(); return; }

        var items = await _feeApi.GetFeeTypeInstallmentsAsync(SelectedFeeType.Id);
        var useSavedSelection = selectedIds is { Count: > 0 };
        foreach (var item in items.OrderBy(i => i.SortOrder))
        {
            var option = new ParentNoticeInstallmentOption(item,
                useSavedSelection ? selectedIds!.Contains(item.FeeInstallmentId) : true);
            option.PropertyChanged += InstallmentOnPropertyChanged;
            Installments.Add(option);
        }
        RefreshSelectedInstallmentCount();
    }

    [RelayCommand]
    private async Task LoadRecipientsAsync()
    {
        if (SelectedAcademicYear is null || SelectedFeeType is null) return;
        IsBusy = true;
        try
        {
            var request = new StudentPaymentSituationSearchRequest(
                SelectedAcademicYear.Id, SelectedSection?.Id, SelectedClass?.PedagogicalClassId,
                SelectedClass?.Id, null, SelectedFeeType.Id, null, null, 1, 5000);
            var studentsRequest = new StudentSearchRequest(null, SelectedAcademicYear.Id, SelectedSection?.Id,
                SelectedClass?.PedagogicalClassId, SelectedClass?.Id, null,
                ApplyFilters: true, IncludeAll: true, Page: 1, PageSize: 5000);
            var financeTask = _financeApi.SearchPaymentSituationsAsync(request);
            var studentsTask = _studentApi.SearchAsync(studentsRequest);
            await Task.WhenAll(financeTask, studentsTask);
            var students = studentsTask.Result.Items.ToDictionary(s => s.Id);
            foreach (var old in Recipients) old.PropertyChanged -= RecipientOnPropertyChanged;
            Recipients.Clear();
            foreach (var situation in financeTask.Result.Items.OrderBy(i => i.FullName))
            {
                students.TryGetValue(situation.StudentId, out var student);
                var row = new ParentNoticeRecipientRow
                {
                    Situation = situation,
                    Student = student,
                    IsSelected = _pendingRecipientIds?.Contains(situation.StudentId) ?? true
                };
                row.PropertyChanged += RecipientOnPropertyChanged;
                Recipients.Add(row);
            }
            _pendingRecipientIds = null;
            RefreshSelectedCount();
            StatusMessage = Recipients.Count == 0 ? "Aucun élève ne correspond aux critères." : null;
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand] private void SelectAll() { foreach (var row in Recipients) row.IsSelected = true; }
    [RelayCommand] private void ClearSelection() { foreach (var row in Recipients) row.IsSelected = false; }
    [RelayCommand] private void SelectAllInstallments() { foreach (var item in Installments) item.IsSelected = true; }
    [RelayCommand] private void ClearInstallments() { foreach (var item in Installments) item.IsSelected = false; }

    [RelayCommand]
    private void ClearFilters()
    {
        _suppressFilterReload = true;
        try { SelectedSection = null; SelectedClass = null; FilterClasses(); }
        finally { _suppressFilterReload = false; }
        _ = LoadRecipientsAsync();
    }

    [RelayCommand]
    private void NewNotice()
    {
        CurrentNoticeId = null;
        SelectedNotice = null;
        Title = "Rappel de paiement";
        Subject = "Rappel concernant les frais scolaires";
        ContentRtfBase64 = ParentNoticeDocumentBuilder.CreateDefaultTemplate();
        IncludeSchoolHeader = true;
        SelectedLayout = Layouts.FirstOrDefault(x => x.Value == ParentNoticePageLayout.FullPage);
        SelectedOrientation = Orientations.FirstOrDefault(x => x.Value == ParentNoticePageOrientation.Portrait);
        SelectAllInstallments();
        SelectAll();
        StatusMessage = "Nouveau brouillon.";
    }

    [RelayCommand]
    private async Task OpenSelectedAsync()
    {
        if (SelectedNotice is null) return;
        SelectedNotice = await _noticeApi.GetAsync(SelectedNotice.Id);
        CurrentNoticeId = SelectedNotice.Status == ParentNoticeStatus.Draft ? SelectedNotice.Id : null;
        Title = SelectedNotice.Status == ParentNoticeStatus.Draft ? SelectedNotice.Title : $"Copie de {SelectedNotice.Title}";
        Subject = SelectedNotice.Subject;
        ContentRtfBase64 = SelectedNotice.ContentRtfBase64;
        IncludeSchoolHeader = SelectedNotice.IncludeSchoolHeader;
        SelectedLayout = Layouts.FirstOrDefault(x => x.Value == SelectedNotice.PageLayout) ?? Layouts[0];
        SelectedOrientation = Orientations.FirstOrDefault(x => x.Value == SelectedNotice.PageOrientation) ?? Orientations[0];
        _suppressFilterReload = true;
        try
        {
            SelectedAcademicYear = AcademicYears.FirstOrDefault(y => y.Id == SelectedNotice.AcademicYearId);
            SelectedFeeType = FeeTypes.FirstOrDefault(f => f.Id == SelectedNotice.FeeTypeId);
            SelectedSection = Sections.FirstOrDefault(s => s.Id == SelectedNotice.SectionId);
            _allClasses = SelectedAcademicYear is null ? [] : await _academicApi.GetClassRoomsAsync(SelectedAcademicYear.Id);
            FilterClasses();
            SelectedClass = Classes.FirstOrDefault(c => c.Id == SelectedNotice.ClassRoomId);
            await ReloadInstallmentsAsync(SelectedNotice.SelectedInstallmentIds);
        }
        finally { _suppressFilterReload = false; }
        _pendingRecipientIds = SelectedNotice.RecipientStudentIds.ToHashSet();
        await LoadRecipientsAsync();
        StatusMessage = SelectedNotice.Status == ParentNoticeStatus.Generated
            ? "Une copie modifiable de l'avis généré a été ouverte."
            : "Brouillon chargé.";
    }

    [RelayCommand] private async Task SaveDraftAsync() => await SaveInternalAsync();

    [RelayCommand]
    private async Task PreviewAsync()
    {
        var row = Recipients.FirstOrDefault(r => r.IsSelected);
        if (row is null) { StatusMessage = "Sélectionnez au moins un élève."; return; }
        var preview = await BuildDocumentsAsync([row]);
        DocumentPreview.Show(preview.Document, $"{Title} — {row.FullName}");
    }

    [RelayCommand]
    private async Task PreviewArchivedAsync()
    {
        if (SelectedNotice is null) return;
        SelectedNotice = await _noticeApi.GetAsync(SelectedNotice.Id);
        if (SelectedNotice.GeneratedSnapshots.Count == 0)
        {
            StatusMessage = "Cet avis ne contient pas encore de version générée.";
            return;
        }
        _branding = await _brandingApi.GetConfigurationAsync();
        var options = CreatePrintOptions(
            SelectedNotice.PageLayout,
            SelectedNotice.PageOrientation,
            SelectedNotice.IncludeSchoolHeader);
        var documents = SelectedNotice.GeneratedSnapshots
            .Select(s => ParentNoticeDocumentBuilder.Build(SelectedNotice.ContentRtfBase64, s.Values, options))
            .ToList();
        var document = SelectedNotice.PageLayout == ParentNoticePageLayout.TwoPerPage
            ? ParentNoticeDocumentBuilder.Combine(documents, SelectedNotice.PageLayout, SelectedNotice.PageOrientation)
            : documents.Count == 1
                ? documents[0]
                : ParentNoticeDocumentBuilder.Combine(documents, SelectedNotice.PageLayout, SelectedNotice.PageOrientation);
        DocumentPreview.Show(document, $"{SelectedNotice.Title} — archive");
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        var rows = Recipients.Where(r => r.IsSelected).ToList();
        if (rows.Count == 0) { StatusMessage = "Sélectionnez au moins un élève."; return; }
        var saved = await SaveInternalAsync();
        if (saved is null) return;
        IsBusy = true;
        try
        {
            var preview = await BuildDocumentsAsync(rows);
            DocumentPreview.Show(preview.Document, $"{Title} — {rows.Count} élèves");
            var generated = await _noticeApi.MarkGeneratedAsync(saved.Id,
                new GenerateParentNoticeRequest(rows.Select(r => r.StudentId).ToArray(), preview.Snapshots));
            ReplaceOrInsertNotice(generated);
            CurrentNoticeId = null;
            StatusMessage = $"{rows.Count} avis personnalisé(s) généré(s).";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedNotice is null) return;
        await _noticeApi.DeleteAsync(SelectedNotice.Id);
        Notices.Remove(SelectedNotice);
        SelectedNotice = null;
        StatusMessage = "Avis supprimé.";
    }

    private async Task<ParentNoticeDto?> SaveInternalAsync()
    {
        if (SelectedAcademicYear is null || SelectedFeeType is null)
        {
            StatusMessage = "Sélectionnez l'année scolaire et le type de frais.";
            return null;
        }
        var ids = Recipients.Where(r => r.IsSelected).Select(r => r.StudentId).ToArray();
        var installmentIds = SelectedInstallmentIds();
        var layout = SelectedLayout?.Value ?? ParentNoticePageLayout.FullPage;
        var orientation = layout == ParentNoticePageLayout.TwoPerPage
            ? ParentNoticePageOrientation.Portrait
            : SelectedOrientation?.Value ?? ParentNoticePageOrientation.Portrait;
        var request = new SaveParentNoticeRequest(Title, Subject, SelectedAcademicYear.Id, SelectedFeeType.Id,
            SelectedSection?.Id, SelectedClass?.Id, BuildTargetDescription(), ContentRtfBase64,
            installmentIds, IncludeSchoolHeader, layout, orientation, ids);
        IsBusy = true;
        try
        {
            var result = CurrentNoticeId.HasValue
                ? await _noticeApi.UpdateAsync(CurrentNoticeId.Value, request)
                : await _noticeApi.CreateAsync(request);
            CurrentNoticeId = result.Id;
            ReplaceOrInsertNotice(result);
            StatusMessage = "Brouillon enregistré.";
            return result;
        }
        catch (Exception ex) { StatusMessage = ex.Message; return null; }
        finally { IsBusy = false; }
    }

    private async Task<(System.Windows.Documents.FlowDocument Document,
        IReadOnlyList<ParentNoticeRecipientSnapshotDto> Snapshots)> BuildDocumentsAsync(
        IReadOnlyList<ParentNoticeRecipientRow> rows)
    {
        _branding = await _brandingApi.GetConfigurationAsync();
        var documents = new List<System.Windows.Documents.FlowDocument>();
        var snapshots = new List<ParentNoticeRecipientSnapshotDto>();
        var layout = SelectedLayout?.Value ?? ParentNoticePageLayout.FullPage;
        var orientation = layout == ParentNoticePageLayout.TwoPerPage
            ? ParentNoticePageOrientation.Portrait
            : SelectedOrientation?.Value ?? ParentNoticePageOrientation.Portrait;
        var options = CreatePrintOptions(layout, orientation, IncludeSchoolHeader);
        var current = 0;
        foreach (var row in rows)
        {
            StatusMessage = $"Préparation de l'avis {++current}/{rows.Count}…";
            StudentInstallmentPaymentPlanDto? plan = null;
            try { plan = await _financeApi.GetInstallmentPaymentPlanAsync(row.Situation.EnrollmentId, SelectedFeeType!.Id); }
            catch { /* L'avis annuel reste disponible même sans échéancier configuré. */ }
            var values = BuildValues(row, plan);
            documents.Add(ParentNoticeDocumentBuilder.Build(ContentRtfBase64, values, options));
            snapshots.Add(new ParentNoticeRecipientSnapshotDto(row.StudentId, row.FullName, values));
        }
        var document = layout == ParentNoticePageLayout.TwoPerPage
            ? ParentNoticeDocumentBuilder.Combine(documents, layout, orientation)
            : documents.Count == 1
                ? documents[0]
                : ParentNoticeDocumentBuilder.Combine(documents, layout, orientation);
        return (document, snapshots);
    }

    private IReadOnlyDictionary<string, string> BuildValues(ParentNoticeRecipientRow row, StudentInstallmentPaymentPlanDto? plan)
    {
        var currency = row.Situation.Currency.ToString();
        var lines = plan?.Lines.OrderBy(l => l.SortOrder).ToArray() ?? [];
        var selectedIds = SelectedInstallmentIds().ToHashSet();
        var selectedLines = selectedIds.Count == 0
            ? lines
            : lines.Where(l => selectedIds.Contains(l.FeeInstallmentId)).ToArray();
        var chosen = selectedLines.FirstOrDefault();
        var selectedNames = selectedLines.Length == 0 ? "—" : string.Join(", ", selectedLines.Select(l => l.InstallmentName));
        var selectedDueDates = selectedLines.Length == 0 ? "—" : string.Join(", ", selectedLines
            .Where(l => l.DueDate.HasValue).Select(l => $"{l.InstallmentName}: {l.DueDate:dd/MM/yyyy}"));
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{{Eleve.NomComplet}}"] = row.FullName,
            ["{{Eleve.Nom}}"] = row.Student?.LastName ?? row.FullName,
            ["{{Eleve.Postnom}}"] = row.Student?.MiddleName ?? "",
            ["{{Eleve.Prenom}}"] = row.Student?.FirstName ?? "",
            ["{{Eleve.Matricule}}"] = row.RegistrationNumber,
            ["{{Eleve.Classe}}"] = row.ClassName,
            ["{{Eleve.Section}}"] = row.SectionName,
            ["{{Scolarite.Annee}}"] = row.Situation.AcademicYearLabel,
            ["{{Avis.Objet}}"] = Subject ?? "",
            ["{{Avis.Date}}"] = DateTime.Today.ToString("dd/MM/yyyy"),
            ["{{Ecole.Nom}}"] = _school?.Name ?? "",
            ["{{Ecole.Adresse}}"] = _school?.Address ?? "",
            ["{{Ecole.Telephone}}"] = _school?.Phone ?? "",
            ["{{Frais.Type}}"] = row.Situation.FeeTypeName,
            ["{{Frais.Devise}}"] = currency,
            ["{{Frais.TotalAnnuel}}"] = Money(row.Situation.AmountExpected, currency),
            ["{{Frais.TotalPaye}}"] = Money(row.Situation.AmountPaid, currency),
            ["{{Frais.ResteAnnuel}}"] = Money(Math.Max(0, row.Situation.Balance), currency),
            ["{{Frais.TranchesPrevues}}"] = string.Join(", ", lines.Select(l => l.InstallmentName)),
            ["{{Frais.TableauTranches}}"] = lines.Length == 0 ? "Échéancier non configuré" : string.Join("\n", lines.Select(l =>
                $"{l.InstallmentName} : prévu {Money(l.AmountExpected, currency)} ; payé {Money(l.AmountPaid, currency)} ; reste {Money(l.Remaining, currency)}")),
            ["{{Tranches.Selectionnees}}"] = selectedNames,
            ["{{Tranches.TotalPrevu}}"] = Money(selectedLines.Sum(l => l.AmountExpected), currency),
            ["{{Tranches.TotalPaye}}"] = Money(selectedLines.Sum(l => l.AmountPaid), currency),
            ["{{Tranches.Reste}}"] = Money(selectedLines.Sum(l => Math.Max(0, l.Remaining)), currency),
            ["{{Tranches.Echeances}}"] = selectedDueDates,
            ["{{Tranche.Nom}}"] = chosen?.InstallmentName ?? "—",
            ["{{Tranche.MontantPrevu}}"] = chosen is null ? "—" : Money(chosen.AmountExpected, currency),
            ["{{Tranche.MontantPaye}}"] = chosen is null ? "—" : Money(chosen.AmountPaid, currency),
            ["{{Tranche.Reste}}"] = chosen is null ? "—" : Money(chosen.Remaining, currency),
            ["{{Tranche.Echeance}}"] = chosen?.DueDate?.ToString("dd/MM/yyyy") ?? "—"
        };
    }

    private ParentNoticePrintOptions CreatePrintOptions(
        ParentNoticePageLayout layout,
        ParentNoticePageOrientation orientation,
        bool includeHeader)
    {
        var header = _branding?.Headers.FirstOrDefault(h => h.IsActive && h.ApplicableDocumentTypes.Contains(DocumentBrandingType.AvisParents));
        var logo = _branding?.Logos.FirstOrDefault(l => l.IsActive && l.IsPrimary)
            ?? _branding?.Logos.FirstOrDefault(l => l.IsActive);
        var relativePath = header?.PrintMode == HeaderPrintMode.FullImage ? header.ImagePath : logo?.ImagePath;
        return new ParentNoticePrintOptions(
            layout,
            orientation,
            header is not null,
            _brandingPathResolver.ResolveAbsolutePath(relativePath),
            header?.MarginLeftMm ?? 0,
            header?.MarginRightMm ?? 0,
            header?.MaxHeightMm);
    }

    private void NotifyPagePresentationChanged()
    {
        OnPropertyChanged(nameof(IsPageOrientationEnabled));
        OnPropertyChanged(nameof(EditorPageWidth));
        OnPropertyChanged(nameof(EditorPageHeight));
        OnPropertyChanged(nameof(EditorPageLabel));
        OnPropertyChanged(nameof(EditorPagePadding));
    }

    private IReadOnlyList<Guid> SelectedInstallmentIds() => Installments
        .Where(i => i.IsSelected).OrderBy(i => i.SortOrder).Select(i => i.FeeInstallmentId).ToArray();

    private static string Money(decimal amount, string currency) => $"{amount:N0} {currency}";
    private string BuildTargetDescription() => SelectedClass is not null ? $"Classe : {SelectedClass.FullDisplayName}"
        : SelectedSection is not null ? $"Section : {SelectedSection.Name}" : "Élèves sélectionnés";
    private void FilterClasses() => Replace(Classes, _allClasses
        .Where(c => SelectedSection is null || c.SectionId == SelectedSection.Id).OrderBy(c => c.FullDisplayName));
    private void RecipientOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(ParentNoticeRecipientRow.IsSelected)) RefreshSelectedCount(); }
    private void InstallmentOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(ParentNoticeInstallmentOption.IsSelected)) RefreshSelectedInstallmentCount(); }
    private void RefreshSelectedCount() => SelectedRecipientCount = Recipients.Count(r => r.IsSelected);
    private void RefreshSelectedInstallmentCount() => SelectedInstallmentCount = Installments.Count(i => i.IsSelected);
    private void ReplaceOrInsertNotice(ParentNoticeDto item)
    {
        var old = Notices.FirstOrDefault(n => n.Id == item.Id);
        if (old is not null) Notices.Remove(old);
        Notices.Insert(0, item);
    }
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    { target.Clear(); foreach (var value in values) target.Add(value); }

    private static readonly ParentNoticeField[] AvailableFields =
    [
        new("Élève", "Nom complet", "{{Eleve.NomComplet}}"), new("Élève", "Nom", "{{Eleve.Nom}}"),
        new("Élève", "Postnom", "{{Eleve.Postnom}}"), new("Élève", "Prénom", "{{Eleve.Prenom}}"),
        new("Élève", "Matricule", "{{Eleve.Matricule}}"), new("Scolarité", "Classe", "{{Eleve.Classe}}"),
        new("Scolarité", "Section", "{{Eleve.Section}}"), new("Scolarité", "Année scolaire", "{{Scolarite.Annee}}"),
        new("Avis", "Objet", "{{Avis.Objet}}"), new("Avis", "Date du jour", "{{Avis.Date}}"),
        new("Établissement", "Nom de l'école", "{{Ecole.Nom}}"), new("Établissement", "Adresse", "{{Ecole.Adresse}}"),
        new("Établissement", "Téléphone", "{{Ecole.Telephone}}"),
        new("Frais", "Type de frais", "{{Frais.Type}}"), new("Frais", "Total annuel prévu", "{{Frais.TotalAnnuel}}"),
        new("Frais", "Montant total payé", "{{Frais.TotalPaye}}"), new("Frais", "Reste annuel", "{{Frais.ResteAnnuel}}"),
        new("Frais", "Tranches prévues", "{{Frais.TranchesPrevues}}"), new("Frais", "Situation des tranches", "{{Frais.TableauTranches}}"),
        new("Tranches sélectionnées", "Noms des tranches", "{{Tranches.Selectionnees}}"),
        new("Tranches sélectionnées", "Somme prévue", "{{Tranches.TotalPrevu}}"),
        new("Tranches sélectionnées", "Somme déjà payée", "{{Tranches.TotalPaye}}"),
        new("Tranches sélectionnées", "Somme restant à payer", "{{Tranches.Reste}}"),
        new("Tranches sélectionnées", "Dates d'échéance", "{{Tranches.Echeances}}"),
        new("Première tranche sélectionnée", "Nom de la tranche", "{{Tranche.Nom}}"),
        new("Première tranche sélectionnée", "Montant prévu", "{{Tranche.MontantPrevu}}"),
        new("Première tranche sélectionnée", "Montant payé", "{{Tranche.MontantPaye}}"),
        new("Première tranche sélectionnée", "Reste", "{{Tranche.Reste}}"),
        new("Première tranche sélectionnée", "Échéance", "{{Tranche.Echeance}}")
    ];
}
