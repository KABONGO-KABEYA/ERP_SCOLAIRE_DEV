using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.StudentCards.DTOs;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Printing.CardLayout;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.Views;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public sealed record CardStatusFilterItem(StudentCardStatus? Status, string Label);

public sealed record CardCreateScopeOption(string Key, string Label);

/// <summary>Module Cartes élèves — tableau de bord, liste, fiche et actions métier.</summary>
public partial class StudentCardsViewModel : ViewModelBase
{
    private readonly IStudentCardApiService _cardApi;
    private readonly IStudentApiService _studentApi;
    private readonly IAcademicApiService _academicApi;
    private readonly IStudentCardPrintService _printService;
    private CancellationTokenSource? _searchCts;
    private bool _suppressReload;

    public StudentCardsViewModel(
        IStudentCardApiService cardApi,
        IStudentApiService studentApi,
        IAcademicApiService academicApi,
        IStudentCardPrintService printService)
    {
        _cardApi = cardApi;
        _studentApi = studentApi;
        _academicApi = academicApi;
        _printService = printService;
        StatusFilters =
        [
            new CardStatusFilterItem(null, "Tous les statuts"),
            new CardStatusFilterItem(StudentCardStatus.Brouillon, "Brouillon"),
            new CardStatusFilterItem(StudentCardStatus.Active, "Active"),
            new CardStatusFilterItem(StudentCardStatus.Suspendue, "Suspendue"),
            new CardStatusFilterItem(StudentCardStatus.Expiree, "Expirée"),
            new CardStatusFilterItem(StudentCardStatus.Perdue, "Perdue"),
            new CardStatusFilterItem(StudentCardStatus.Volee, "Volée"),
            new CardStatusFilterItem(StudentCardStatus.Remplacee, "Remplacée"),
            new CardStatusFilterItem(StudentCardStatus.Desactivee, "Désactivée")
        ];
        CreateScopes =
        [
            new CardCreateScopeOption("student", "Un élève"),
            new CardCreateScopeOption("class", "Toute une classe"),
            new CardCreateScopeOption("section", "Toute une section"),
            new CardCreateScopeOption("school", "Toute l'école")
        ];
        SelectedStatusFilter = StatusFilters[0];
        SelectedCreateScope = CreateScopes[0];
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalYearChanged;
        _ = InitializeAsync();
    }

    private void OnGlobalYearChanged() => _ = RefreshAllAsync();

    public ObservableCollection<StudentCardListItemDto> Cards { get; } = [];
    public ObservableCollection<CardTemplateDto> Templates { get; } = [];
    public ObservableCollection<StudentDto> StudentSuggestions { get; } = [];
    public ObservableCollection<StudentCardHistoryDto> Histories { get; } = [];
    public ObservableCollection<StudentCardPrintLogDto> PrintLogs { get; } = [];
    public ObservableCollection<SectionDto> CreateSections { get; } = [];
    public ObservableCollection<ClassRoomDto> CreateClassRooms { get; } = [];
    public ObservableCollection<ClassRoomDto> FilterClassRooms { get; } = [];
    public IReadOnlyList<CardStatusFilterItem> StatusFilters { get; }
    public IReadOnlyList<CardCreateScopeOption> CreateScopes { get; }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isFiltersExpanded = true;
    [ObservableProperty] private bool _isCreatePanelOpen;
    [ObservableProperty] private CardStatusFilterItem? _selectedStatusFilter;
    [ObservableProperty] private StudentCardListItemDto? _selectedCard;
    [ObservableProperty] private StudentCardDetailDto? _selectedDetail;
    [ObservableProperty] private StudentCardDashboardDto? _dashboard;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _pageSize = 50;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private string _createStudentSearch = string.Empty;
    [ObservableProperty] private StudentDto? _selectedCreateStudent;
    [ObservableProperty] private CardTemplateDto? _selectedCreateTemplate;
    [ObservableProperty] private bool _createActivateImmediately = true;
    [ObservableProperty] private bool _createSkipExisting = true;
    [ObservableProperty] private bool _renewKeepQr;
    [ObservableProperty] private CardCreateScopeOption? _selectedCreateScope;
    [ObservableProperty] private ClassRoomDto? _selectedCreateClassRoom;
    [ObservableProperty] private SectionDto? _selectedCreateSection;
    [ObservableProperty] private SectionDto? _filterSection;
    [ObservableProperty] private ClassRoomDto? _filterClassRoom;

    public string FiltersHeaderText => $"Filtres ({TotalCount})";
    public string FiltersToggleLabel => IsFiltersExpanded ? "Masquer les filtres" : "Afficher les filtres";
    public string PaginationLabel => $"Page {CurrentPage} / {TotalPages}";
    public bool CanGoPreviousPage => CurrentPage > 1;
    public bool CanGoNextPage => CurrentPage < TotalPages;
    public bool HasDetail => SelectedDetail is not null;
    public string QrDisplay => SelectedDetail?.QrPayload ?? "—";
    public string StatusDisplay => SelectedDetail is null
        ? "—"
        : StudentCardStatusLabels.From(SelectedDetail.Status);

    public bool CanActivate => SelectedDetail?.Status
        is StudentCardStatus.Brouillon or StudentCardStatus.Suspendue;

    public bool CanSuspend => SelectedDetail?.Status == StudentCardStatus.Active;

    public bool CanPrint => SelectedDetail?.Status
        is StudentCardStatus.Active or StudentCardStatus.Brouillon;

    public bool CanRenew => SelectedDetail is not null
        && SelectedDetail.Status is not (StudentCardStatus.Remplacee or StudentCardStatus.Desactivee);

    public bool CanDeactivate => SelectedDetail is not null
        && SelectedDetail.Status is not (StudentCardStatus.Remplacee or StudentCardStatus.Desactivee);

    public bool IsCreateStudentScope => SelectedCreateScope?.Key == "student";
    public bool IsCreateClassScope => SelectedCreateScope?.Key == "class";
    public bool IsCreateSectionScope => SelectedCreateScope?.Key == "section";
    public bool IsCreateSchoolScope => SelectedCreateScope?.Key == "school";
    public bool IsCreateBulkScope => !IsCreateStudentScope;
    public string CreateActionLabel => IsCreateStudentScope ? "Créer la carte" : "Créer en lot";

    public int ActiveCount => Dashboard?.ActiveCount ?? 0;
    public int ExpiredCount => Dashboard?.ExpiredCount ?? 0;
    public int LostCount => Dashboard?.LostCount ?? 0;
    public int StolenCount => Dashboard?.StolenCount ?? 0;
    public int ToRenewCount => Dashboard?.ToRenewCount ?? 0;

    partial void OnIsFiltersExpandedChanged(bool value) => OnPropertyChanged(nameof(FiltersToggleLabel));
    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(FiltersHeaderText));
    partial void OnCurrentPageChanged(int value) => NotifyPagination();
    partial void OnTotalPagesChanged(int value) => NotifyPagination();
    partial void OnDashboardChanged(StudentCardDashboardDto? value) => NotifyDashboard();
    partial void OnSelectedDetailChanged(StudentCardDetailDto? value)
    {
        OnPropertyChanged(nameof(HasDetail));
        OnPropertyChanged(nameof(QrDisplay));
        OnPropertyChanged(nameof(StatusDisplay));
        OnPropertyChanged(nameof(CanActivate));
        OnPropertyChanged(nameof(CanSuspend));
        OnPropertyChanged(nameof(CanPrint));
        OnPropertyChanged(nameof(CanRenew));
        OnPropertyChanged(nameof(CanDeactivate));
        Histories.Clear();
        PrintLogs.Clear();
        if (value is null) return;
        foreach (var h in value.Histories) Histories.Add(h);
        foreach (var p in value.PrintLogs) PrintLogs.Add(p);
    }

    partial void OnSearchTextChanged(string value) => QueueSearch();
    partial void OnSelectedStatusFilterChanged(CardStatusFilterItem? value)
    {
        if (_suppressReload) return;
        CurrentPage = 1;
        QueueSearch();
    }

    partial void OnSelectedCardChanged(StudentCardListItemDto? value) => _ = LoadDetailAsync(value?.Id);
    partial void OnCreateStudentSearchChanged(string value) => _ = SearchStudentsForCreateAsync();

    partial void OnSelectedCreateScopeChanged(CardCreateScopeOption? value)
    {
        OnPropertyChanged(nameof(IsCreateStudentScope));
        OnPropertyChanged(nameof(IsCreateClassScope));
        OnPropertyChanged(nameof(IsCreateSectionScope));
        OnPropertyChanged(nameof(IsCreateSchoolScope));
        OnPropertyChanged(nameof(IsCreateBulkScope));
        OnPropertyChanged(nameof(CreateActionLabel));
        _ = EnsureCreateLookupsAsync();
    }

    partial void OnFilterSectionChanged(SectionDto? value)
    {
        RefreshFilterClassRooms();
        if (_suppressReload) return;
        CurrentPage = 1;
        QueueSearch();
    }

    partial void OnFilterClassRoomChanged(ClassRoomDto? value)
    {
        if (_suppressReload) return;
        CurrentPage = 1;
        QueueSearch();
    }

    partial void OnIsCreatePanelOpenChanged(bool value)
    {
        if (value)
            _ = EnsureCreateLookupsAsync();
    }

    [RelayCommand]
    private void ToggleFilters() => IsFiltersExpanded = !IsFiltersExpanded;

    [RelayCommand]
    private void ToggleCreatePanel() => IsCreatePanelOpen = !IsCreatePanelOpen;

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        await EnsureCreateLookupsAsync();
        await LoadDashboardAsync();
        await SearchAsync();
        await LoadTemplatesAsync();
    }

    [RelayCommand]
    private async Task ApplyFiltersAsync() => await SearchAsync();

    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        _suppressReload = true;
        try
        {
            SearchText = string.Empty;
            SelectedStatusFilter = StatusFilters[0];
            FilterSection = null;
            FilterClassRoom = null;
            CurrentPage = 1;
        }
        finally
        {
            _suppressReload = false;
        }

        await SearchAsync();
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (!CanGoPreviousPage) return;
        CurrentPage--;
        await SearchAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!CanGoNextPage) return;
        CurrentPage++;
        await SearchAsync();
    }

    [RelayCommand]
    private async Task CreateCardAsync()
    {
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null)
        {
            StatusMessage = "Aucune année scolaire sélectionnée (barre du haut).";
            return;
        }

        IsBusy = true;
        try
        {
            var template = SelectedCreateTemplate ?? await EnsureDefaultTemplateAsync();
            var scope = SelectedCreateScope?.Key ?? "student";

            if (scope == "student")
            {
                if (SelectedCreateStudent is null)
                {
                    StatusMessage = "Sélectionnez un élève pour créer la carte.";
                    return;
                }

                var detail = await _cardApi.CreateAsync(new CreateStudentCardRequest(
                    SelectedCreateStudent.Id,
                    yearId.Value,
                    template.Id,
                    ActivateImmediately: CreateActivateImmediately));

                StatusMessage = $"Carte {detail.CardNumber} créée.";
                IsCreatePanelOpen = false;
                CreateStudentSearch = string.Empty;
                SelectedCreateStudent = null;
                await RefreshAllAsync();
                SelectedCard = Cards.FirstOrDefault(c => c.Id == detail.Id);
                return;
            }

            BulkCreateStudentCardsRequest request = scope switch
            {
                "class" when SelectedCreateClassRoom is not null => new(
                    yearId.Value,
                    template.Id,
                    ClassRoomId: SelectedCreateClassRoom.Id,
                    ActivateImmediately: CreateActivateImmediately,
                    SkipExistingActive: CreateSkipExisting),
                "section" when SelectedCreateSection is not null => new(
                    yearId.Value,
                    template.Id,
                    SectionId: SelectedCreateSection.Id,
                    ActivateImmediately: CreateActivateImmediately,
                    SkipExistingActive: CreateSkipExisting),
                "school" => new(
                    yearId.Value,
                    template.Id,
                    EntireSchool: true,
                    ActivateImmediately: CreateActivateImmediately,
                    SkipExistingActive: CreateSkipExisting),
                _ => throw new InvalidOperationException(scope switch
                {
                    "class" => "Sélectionnez une classe.",
                    "section" => "Sélectionnez une section.",
                    _ => "Périmètre de création invalide."
                })
            };

            var scopeLabel = scope switch
            {
                "class" => $"la classe « {SelectedCreateClassRoom!.FullDisplayName} »",
                "section" => $"la section « {SelectedCreateSection!.Name} »",
                _ => "toute l'école"
            };

            var confirm = MessageBox.Show(
                $"Créer des cartes pour {scopeLabel} ?\n\n"
                + $"Modèle : {template.Name}\n"
                + $"Activer immédiatement : {(CreateActivateImmediately ? "oui" : "non")}\n"
                + $"Ignorer déjà équipés : {(CreateSkipExisting ? "oui" : "non")}",
                "Création en lot",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var result = await _cardApi.BulkCreateAsync(request);
            StatusMessage = result.Summary;
            IsCreatePanelOpen = false;
            await RefreshAllAsync();
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
    private async Task PrintSelectedAsync()
    {
        if (SelectedDetail is null) return;
        var options = AskPrintOptions(1);
        if (options is null) return;

        IsBusy = true;
        try
        {
            await _printService.PrintCardAsync(SelectedDetail.Id, options.Value.Layout);
            StatusMessage = "Impression lancée (1 job).";
            await LoadDetailAsync(SelectedDetail.Id);
            await SearchAsync();
            await LoadDashboardAsync();
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
    private async Task PreviewSelectedAsync()
    {
        if (SelectedDetail is null) return;
        try
        {
            await _printService.PreviewCardAsync(SelectedDetail.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PrintFilteredAsync()
    {
        var printable = Cards
            .Where(c => c.Status is StudentCardStatus.Active or StudentCardStatus.Brouillon)
            .ToList();

        if (printable.Count == 0)
        {
            StatusMessage = Cards.Count == 0
                ? "Aucune carte dans la liste à imprimer."
                : "Aucune carte imprimable sur cette page (statuts expirés, perdus, volés ou désactivés).";
            return;
        }

        if (printable.Count < Cards.Count)
        {
            var skipped = Cards.Count - printable.Count;
            var confirmSkip = MessageBox.Show(
                $"{skipped} carte(s) de cette page ne sont pas imprimables et seront ignorées.\n\n"
                + $"Continuer avec {printable.Count} carte(s) ?",
                "Impression",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmSkip != MessageBoxResult.Yes) return;
        }

        var options = AskPrintOptions(printable.Count);
        if (options is null) return;

        IsBusy = true;
        try
        {
            await _printService.PrintCardsAsync(
                printable.Select(c => c.Id).ToList(),
                options.Value.Layout,
                options.Value.Rows);
            StatusMessage = options.Value.Layout == CardPrintLayoutKind.A4Sheet
                ? $"Impression A4 terminée — page {CurrentPage}/{TotalPages} ({printable.Count} carte(s), 2×{options.Value.Rows})."
                : $"Impression unitaire terminée — page {CurrentPage}/{TotalPages} ({printable.Count} carte(s)).";
            await RefreshAllAsync();
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

    private static (CardPrintLayoutKind Layout, int Rows)? AskPrintOptions(int cardCount)
    {
        var dialog = new CardPrintOptionsWindow(cardCount)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        var ok = dialog.ShowDialog() == true && dialog.Confirmed;
        return ok ? (dialog.SelectedLayout, dialog.A4Rows) : null;
    }

    [RelayCommand]
    private void OpenTemplateDesigner()
    {
        var designerVm = App.Services!.GetRequiredService<CardTemplateDesignerViewModel>();
        if (SelectedCreateTemplate is not null)
            designerVm.LoadTemplate(SelectedCreateTemplate);
        else if (Templates.Count > 0)
            designerVm.LoadTemplate(Templates[0]);
        else
            designerVm.LoadNew();

        var window = new CardTemplateDesignerWindow(designerVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
        _ = LoadTemplatesAsync();
    }

    [RelayCommand]
    private void OpenNewTemplateDesigner()
    {
        var designerVm = App.Services!.GetRequiredService<CardTemplateDesignerViewModel>();
        designerVm.LoadNew();
        var window = new CardTemplateDesignerWindow(designerVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
        _ = LoadTemplatesAsync();
    }

    [RelayCommand]
    private async Task ReprintSelectedAsync()
    {
        if (SelectedDetail is null) return;
        IsBusy = true;
        try
        {
            await _cardApi.ReprintAsync(SelectedDetail.Id, new ReprintStudentCardRequest("Réimpression"));
            StatusMessage = "Réimpression enregistrée.";
            await LoadDetailAsync(SelectedDetail.Id);
            await SearchAsync();
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
    private async Task RenewSelectedAsync()
    {
        if (SelectedDetail is null) return;
        var confirm = MessageBox.Show(
            $"Renouveler la carte {SelectedDetail.CardNumber} ?\nConserve QR : {(RenewKeepQr ? "oui" : "non")}",
            "Renouvellement",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var detail = await _cardApi.RenewAsync(
                SelectedDetail.Id,
                new RenewStudentCardRequest(KeepQrToken: RenewKeepQr));
            StatusMessage = $"Nouvelle carte {detail.CardNumber} créée (v{detail.Version}).";
            await RefreshAllAsync();
            SelectedCard = Cards.FirstOrDefault(c => c.Id == detail.Id);
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
    private async Task DeclareLostAsync()
    {
        if (SelectedDetail is null) return;
        if (!ConfirmIncident("perdue")) return;
        await RunIncidentAsync(() => _cardApi.DeclareLostAsync(
            SelectedDetail.Id,
            new DeclareCardIncidentRequest("Déclarée perdue depuis le module Cartes")));
    }

    [RelayCommand]
    private async Task DeclareStolenAsync()
    {
        if (SelectedDetail is null) return;
        if (!ConfirmIncident("volée")) return;
        await RunIncidentAsync(() => _cardApi.DeclareStolenAsync(
            SelectedDetail.Id,
            new DeclareCardIncidentRequest("Déclarée volée depuis le module Cartes")));
    }

    [RelayCommand]
    private async Task ActivateSelectedAsync()
    {
        if (SelectedDetail is null) return;

        IsBusy = true;
        try
        {
            var detail = await _cardApi.ActivateAsync(
                SelectedDetail.Id,
                new ActivateStudentCardRequest("Activation depuis le module Cartes"));
            StatusMessage = $"Carte {detail.CardNumber} activée.";
            await RefreshAllAsync();
            SelectedCard = Cards.FirstOrDefault(c => c.Id == detail.Id);
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
    private async Task SuspendSelectedAsync()
    {
        if (SelectedDetail is null) return;
        var confirm = MessageBox.Show(
            $"Suspendre la carte {SelectedDetail.CardNumber} ?\n\n"
            + "La carte ne sera plus valide ni imprimable, mais pourra être réactivée.",
            "Suspension",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _cardApi.SuspendAsync(
                SelectedDetail.Id,
                new SuspendStudentCardRequest("Suspension manuelle depuis le module Cartes"));
            StatusMessage = "Carte suspendue.";
            await RefreshAllAsync();
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
    private async Task DeactivateSelectedAsync()
    {
        if (SelectedDetail is null) return;
        var confirm = MessageBox.Show(
            $"Désactiver définitivement la carte {SelectedDetail.CardNumber} ?\n\n"
            + "Cette opération est irréversible : seule une nouvelle carte pourra la remplacer.",
            "Désactivation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            await _cardApi.DeactivateAsync(
                SelectedDetail.Id,
                new DeactivateStudentCardRequest("Désactivation manuelle"));
            StatusMessage = "Carte désactivée.";
            await RefreshAllAsync();
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

    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await LoadTemplatesAsync();
            await EnsureCreateLookupsAsync();
            await LoadDashboardAsync();
            await SearchAsync();
            StatusMessage = "Module cartes prêt.";
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

    private async Task LoadDashboardAsync()
    {
        Dashboard = await _cardApi.GetDashboardAsync(AcademicYearRefreshBridge.SelectedYearId);
    }

    private async Task LoadTemplatesAsync()
    {
        var items = await _cardApi.ListTemplatesAsync(activeOnly: false);
        Templates.Clear();
        foreach (var t in items.OrderBy(t => t.Name))
            Templates.Add(t);
        SelectedCreateTemplate ??= Templates.FirstOrDefault(t => t.IsActive) ?? Templates.FirstOrDefault();
    }

    private async Task EnsureCreateLookupsAsync()
    {
        try
        {
            var yearId = AcademicYearRefreshBridge.SelectedYearId;
            var sections = await _academicApi.GetSectionsAsync();
            CreateSections.Clear();
            foreach (var s in sections.OrderBy(s => s.Name))
                CreateSections.Add(s);

            var rooms = await _academicApi.GetClassRoomsAsync(yearId);
            CreateClassRooms.Clear();
            foreach (var r in rooms.Where(r => r.IsActive).OrderBy(r => r.FullDisplayName))
                CreateClassRooms.Add(r);

            RefreshFilterClassRooms();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chargement classes/sections : {ex.Message}";
        }
    }

    private void RefreshFilterClassRooms()
    {
        FilterClassRooms.Clear();
        var rooms = CreateClassRooms.AsEnumerable();
        if (FilterSection is not null)
            rooms = rooms.Where(r => r.SectionId == FilterSection.Id);

        foreach (var r in rooms.OrderBy(r => r.FullDisplayName))
            FilterClassRooms.Add(r);

        if (FilterClassRoom is not null && FilterClassRooms.All(r => r.Id != FilterClassRoom.Id))
            FilterClassRoom = null;
    }

    private async Task SearchAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _cardApi.SearchAsync(new StudentCardSearchRequest(
                AcademicYearRefreshBridge.SelectedYearId,
                ClassRoomId: FilterClassRoom?.Id,
                SectionId: FilterClassRoom is null ? FilterSection?.Id : null,
                Status: SelectedStatusFilter?.Status,
                Search: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                Page: CurrentPage,
                PageSize: PageSize));

            Cards.Clear();
            foreach (var item in result.Items)
                Cards.Add(item);

            TotalCount = result.TotalCount;
            TotalPages = Math.Max(1, result.TotalPages);
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            StatusMessage = $"{result.TotalCount} carte(s).";
            NotifyPagination();
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

    private async Task LoadDetailAsync(Guid? cardId)
    {
        if (cardId is null)
        {
            SelectedDetail = null;
            return;
        }

        try
        {
            SelectedDetail = await _cardApi.GetByIdAsync(cardId.Value);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            SelectedDetail = null;
        }
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
                CurrentPage = 1;
                await SearchAsync();
            }
        }
        catch (TaskCanceledException)
        {
            // ignore
        }
    }

    private async Task SearchStudentsForCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(CreateStudentSearch) || CreateStudentSearch.Trim().Length < 2)
        {
            StudentSuggestions.Clear();
            return;
        }

        try
        {
            var result = await _studentApi.SearchAsync(new StudentSearchRequest(
                CreateStudentSearch.Trim(),
                AcademicYearRefreshBridge.SelectedYearId,
                null,
                null,
                null,
                null,
                ApplyFilters: true,
                IncludeInscrits: true,
                Page: 1,
                PageSize: 20));
            StudentSuggestions.Clear();
            foreach (var s in result.Items)
                StudentSuggestions.Add(s);
        }
        catch
        {
            // silent for typeahead
        }
    }

    private async Task<CardTemplateDto> EnsureDefaultTemplateAsync()
    {
        if (Templates.Count > 0)
            return Templates.FirstOrDefault(t => t.IsActive) ?? Templates[0];

            var pair = CardLayoutDefaults.CreateProfessionalPair();
            var created = await _cardApi.CreateTemplateAsync(new SaveCardTemplateRequest(
                "Carte Élève CR80",
                "Modèle professionnel CR80 — personnalisable",
                85.6m,
                53.98m,
                CardTemplateOrientation.Landscape,
                CardTemplateKind.Eleve,
                LayoutJsonFront: CardLayoutSerializer.Serialize(pair.Front),
                LayoutJsonBack: CardLayoutSerializer.Serialize(pair.Back),
                IsActive: true));
        Templates.Add(created);
        SelectedCreateTemplate = created;
        return created;
    }

    private async Task RunIncidentAsync(Func<Task<StudentCardDetailDto>> action)
    {
        IsBusy = true;
        try
        {
            await action();
            StatusMessage = "Incident enregistré — carte désactivée.";
            await RefreshAllAsync();
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

    private bool ConfirmIncident(string label) =>
        MessageBox.Show(
            $"Déclarer la carte {SelectedDetail?.CardNumber} comme {label} ?\nElle sera immédiatement désactivée.",
            "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void NotifyPagination()
    {
        OnPropertyChanged(nameof(PaginationLabel));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
    }

    private void NotifyDashboard()
    {
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(ExpiredCount));
        OnPropertyChanged(nameof(LostCount));
        OnPropertyChanged(nameof(StolenCount));
        OnPropertyChanged(nameof(ToRenewCount));
    }
}
