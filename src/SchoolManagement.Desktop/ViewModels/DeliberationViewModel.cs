using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SchoolManagement.Application.Deliberation.DTOs;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Délibération — consultation officielle des PeriodResult validés.
/// Aucun calcul, aucune modification.
/// </summary>
public partial class DeliberationViewModel : ViewModelBase
{
    private readonly ISchoolApiService _schoolApi;
    private readonly IGradeApiService _gradeApi;
    private readonly IDeliberationApiService _deliberationApi;
    private readonly IStudentApiService _studentApi;

    private SchoolLookupsDto? _lookups;
    private PedagogicalSheetContextDto? _periodContext;
    private bool _filtersReady;
    private bool _suppressReload;

    public DeliberationViewModel(
        ISchoolApiService schoolApi,
        IGradeApiService gradeApi,
        IDeliberationApiService deliberationApi,
        IStudentApiService studentApi)
    {
        _schoolApi = schoolApi;
        _gradeApi = gradeApi;
        _deliberationApi = deliberationApi;
        _studentApi = studentApi;
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
    }

    public ObservableCollection<ClassRoomLookupDto> ClassRooms { get; } = [];
    public ObservableCollection<PedagogicalSheetPeriodOptionDto> PeriodOptions { get; } = [];
    public ObservableCollection<DeliberationRowVm> Rows { get; } = [];
    public ObservableCollection<DeliberationSpecialCaseSectionVm> SpecialCaseSections { get; } = [];
    public ObservableCollection<ConductOptionDto> ConductOptions { get; } = [];
    public ObservableCollection<DeliberationCourseOptionDto> CourseOptions { get; } = [];
    public ObservableCollection<FinalCouncilDecisionOptionDto> AvailableDecisions { get; } = [];

    [ObservableProperty] private AcademicYearDto? _selectedYear;
    [ObservableProperty] private ClassRoomLookupDto? _selectedClassRoom;
    [ObservableProperty] private PedagogicalSheetPeriodOptionDto? _selectedPeriod;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _accessBlocked;
    [ObservableProperty] private string _classDisplayName = "—";
    [ObservableProperty] private string _periodLabel = "—";
    [ObservableProperty] private string _periodModeLabel = "—";
    [ObservableProperty] private string _validationStatusLabel = "—";
    [ObservableProperty] private string _validatedAtDisplay = "—";
    [ObservableProperty] private string _validatedByDisplay = "—";
    [ObservableProperty] private string _summaryStudentCount = "0";
    [ObservableProperty] private string _summaryAdmitted = "0";
    [ObservableProperty] private string _summaryDeferred = "0";
    [ObservableProperty] private string _summaryExcluded = "0";
    [ObservableProperty] private string _summaryClassAverage = "—";
    [ObservableProperty] private string _summarySuccessRate = "—";
    [ObservableProperty] private int _specialCaseTotalCount;
    [ObservableProperty] private bool _hasSpecialCases;
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string? _pvGeneralObservations;
    [ObservableProperty] private string? _pvCouncilDecisions;
    [ObservableProperty] private string? _pvPedagogicalRecommendations;
    [ObservableProperty] private string _pvRecordedByDisplay = "—";
    [ObservableProperty] private string _pvRecordedAtDisplay = "—";
    [ObservableProperty] private bool _pvExists;
    [ObservableProperty] private string _pvStatusDisplay = "Non enregistré";
    [ObservableProperty] private bool _showLocalFilters = true;
    [ObservableProperty] private DeliberationRowVm? _selectedRow;
    [ObservableProperty] private bool _canAddBonusPoints;
    [ObservableProperty] private bool _canSetConduct;
    [ObservableProperty] private bool _canSetFinalDecision;
    [ObservableProperty] private bool _canOfferRepechage;
    [ObservableProperty] private bool _canValidateClass;
    [ObservableProperty] private bool _canCancelValidation;
    [ObservableProperty] private bool _isSessionReadOnly;
    [ObservableProperty] private bool _isYearEnd;
    [ObservableProperty] private bool _showDecisionColumn;
    [ObservableProperty] private ConductOptionDto? _selectedConductOption;

    public bool HasSelectedRow => SelectedRow is not null;

    public event Action? SheetChanged;

    partial void OnSelectedRowChanged(DeliberationRowVm? value)
    {
        OnPropertyChanged(nameof(HasSelectedRow));
        SelectedConductOption = value?.ConductDefinitionId is Guid id
            ? ConductOptions.FirstOrDefault(c => c.Id == id)
            : null;
    }

    [RelayCommand]
    private async Task SaveSelectedConductAsync()
    {
        if (!CanSetConduct || SelectedRow is null || SelectedConductOption is null
            || SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            StatusMessage = "Sélectionnez un élève et une conduite.";
            return;
        }

        IsBusy = true;
        try
        {
            var sheet = await _deliberationApi.SaveConductAsync(new SaveStudentConductRequest(
                SelectedYear.Id,
                SelectedClassRoom.Id,
                SelectedPeriod.Id,
                SelectedRow.StudentId,
                SelectedConductOption.Id,
                null));
            ApplySheet(sheet);
            SheetChanged?.Invoke();
            StatusMessage = $"Conduite enregistrée pour {SelectedRow.FullName}.";
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
    private async Task ValidateClassAsync()
    {
        if (!CanValidateClass || SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            StatusMessage = "Validation impossible pour cette période.";
            return;
        }

        if (MessageBox.Show(
                "Valider officiellement les résultats de la classe ?\n\nCette action enregistre la conduite, les décisions et rend les résultats officiels.",
                "Valider la classe",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _deliberationApi.ValidateClassAsync(new ValidateDeliberationClassRequest(
                SelectedYear.Id,
                SelectedClassRoom.Id,
                SelectedPeriod.Id,
                string.IsNullOrWhiteSpace(PvGeneralObservations) ? null : PvGeneralObservations));

            // Procès-verbal généré après validation (plus d'onglet dédié).
            try
            {
                var stamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
                var councilNote = $"Validation officielle de la classe — {stamp}.";
                await _deliberationApi.SaveMinutesAsync(new SaveDeliberationMinutesRequest(
                    SelectedYear.Id,
                    SelectedClassRoom.Id,
                    SelectedPeriod.Id,
                    PvGeneralObservations,
                    councilNote,
                    null));
            }
            catch
            {
                // La validation a réussi ; le PV reste optionnel si l'enregistrement échoue.
            }

            await LoadSheetAsync();
            StatusMessage = result.Message;
            MessageBox.Show(
                result.Message + "\n\nLe procès-verbal a été généré automatiquement.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Validation impossible", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelClassValidationAsync()
    {
        if (!CanCancelValidation || SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            StatusMessage = "Annulation impossible : la période est clôturée ou la classe n'est pas validée.";
            return;
        }

        if (MessageBox.Show(
                "Désactiver la validation de cette classe ?\n\n" +
                "Le conseil redeviendra modifiable (conduite, décisions, bonus).\n" +
                "Impossible une fois la période clôturée.",
                "Désactiver la validation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _deliberationApi.CancelClassValidationAsync(new ValidateDeliberationClassRequest(
                SelectedYear.Id,
                SelectedClassRoom.Id,
                SelectedPeriod.Id,
                string.IsNullOrWhiteSpace(PvGeneralObservations) ? null : PvGeneralObservations));

            await LoadSheetAsync();
            StatusMessage = result.Message;
            MessageBox.Show(
                result.Message,
                "Validation désactivée",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Annulation impossible", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task EnsureLoadedAsync()
    {
        if (_lookups is not null)
        {
            return;
        }

        await LoadFiltersAsync();
    }

    public async Task SyncSelectionFromParentAsync(
        AcademicYearDto? year,
        ClassRoomLookupDto? classRoom,
        PedagogicalSheetPeriodOptionDto? period)
    {
        await EnsureLoadedAsync();
        _suppressReload = true;
        try
        {
            if (year is not null && SelectedYear?.Id != year.Id)
            {
                SelectedYear = year;
                RefreshClassRooms();
            }

            if (classRoom is not null)
            {
                SelectedClassRoom = ClassRooms.FirstOrDefault(c => c.Id == classRoom.Id) ?? classRoom;
            }

            if (period is not null && SelectedClassRoom is not null)
            {
                if (_periodContext is null || PeriodOptions.All(p => p.Id != period.Id))
                {
                    _suppressReload = false;
                    await LoadPeriodContextAndSheetAsync();
                    _suppressReload = true;
                    SelectedPeriod = PeriodOptions.FirstOrDefault(p => p.Id == period.Id)
                        ?? PeriodOptions.FirstOrDefault();
                }
                else
                {
                    SelectedPeriod = PeriodOptions.FirstOrDefault(p => p.Id == period.Id) ?? period;
                }
            }
        }
        finally
        {
            _suppressReload = false;
        }

        await LoadSheetAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadSheetAsync();

    [RelayCommand]
    private async Task SaveMinutesAsync()
    {
        if (AccessBlocked || SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            StatusMessage = "Sélectionnez une classe validée avant d'enregistrer le procès-verbal.";
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var saved = await _deliberationApi.SaveMinutesAsync(
                new SaveDeliberationMinutesRequest(
                    SelectedYear.Id,
                    SelectedClassRoom.Id,
                    SelectedPeriod.Id,
                    PvGeneralObservations,
                    PvCouncilDecisions,
                    PvPedagogicalRecommendations));
            ApplyMinutes(saved);
            StatusMessage = "Procès-verbal enregistré (utilisateur, date et heure consignés).";
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
    private void InsertPvExample(string? example)
    {
        if (string.IsNullOrWhiteSpace(example) || AccessBlocked)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PvPedagogicalRecommendations))
        {
            PvPedagogicalRecommendations = example;
            return;
        }

        PvPedagogicalRecommendations = $"{PvPedagogicalRecommendations.TrimEnd()}\n• {example}";
    }

    [RelayCommand]
    private void Consult(DeliberationRowVm? row)
    {
        if (row is null)
        {
            StatusMessage = "Sélectionnez une ligne.";
            return;
        }

        OpenIndividualResult(row.StudentId);
    }

    [RelayCommand]
    private async Task OpenBonusAsync()
    {
        if (!CanAddBonusPoints || SelectedRow is null
            || SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            StatusMessage = "Sélectionnez un élève pour ajouter des points.";
            return;
        }

        // Capturer l'élève : ApplySheet recrée les lignes après chaque save.
        var studentId = SelectedRow.StudentId;
        var yearId = SelectedYear.Id;
        var classRoomId = SelectedClassRoom.Id;
        var periodId = SelectedPeriod.Id;
        var savedCount = 0;

        try
        {
            IsBusy = true;
            var bonusDialog = await _deliberationApi.GetBonusDialogAsync(
                yearId, classRoomId, periodId, studentId);
            IsBusy = false;

            if (bonusDialog.Courses.Count == 0)
            {
                StatusMessage = "Aucun cours affecté à cette classe.";
                return;
            }

            var dialog = new Window
            {
                Title = $"Bonus pédagogique — {bonusDialog.StudentName}",
                Width = 500,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            var courseBox = new ComboBox
            {
                Height = 36,
                Margin = new Thickness(0, 0, 0, 10),
                DisplayMemberPath = nameof(PedagogicalBonusCourseContextDto.CourseName)
            };
            var infoBefore = new TextBlock
            {
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x33, 0x41, 0x55)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var infoBonus = new TextBlock
            {
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x33, 0x41, 0x55)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var infoRemaining = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x06, 0x5F, 0x46)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            var statusLabel = new TextBlock
            {
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x1D, 0x4E, 0xD8)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            var pointsBox = new TextBox { Height = 36, Margin = new Thickness(0, 0, 0, 10), Text = "1" };
            var motiveBox = new TextBox { Height = 36, Margin = new Thickness(0, 0, 0, 10) };

            void BindCourses(PedagogicalBonusDialogDto data, Guid? preferCourseId = null)
            {
                bonusDialog = data;
                var previousId = preferCourseId
                    ?? (courseBox.SelectedItem as PedagogicalBonusCourseContextDto)?.CourseId;
                courseBox.ItemsSource = data.Courses.ToList();
                courseBox.SelectedItem = data.Courses.FirstOrDefault(c => c.CourseId == previousId)
                    ?? data.Courses.FirstOrDefault(c => c.RemainingAddable > 0)
                    ?? data.Courses.FirstOrDefault();
                RefreshCourseInfo();
            }

            void RefreshCourseInfo()
            {
                if (courseBox.SelectedItem is not PedagogicalBonusCourseContextDto ctx)
                {
                    infoBefore.Text = infoBonus.Text = infoRemaining.Text = string.Empty;
                    return;
                }

                infoBefore.Text =
                    $"Note actuelle : {ctx.CurrentScoreDisplay} / {ctx.MaximumDisplay}" +
                    (ctx.BaseScore is not null
                        ? $"  (avant bonus de période : {ctx.BaseScoreDisplay})"
                        : string.Empty);
                infoBonus.Text =
                    $"Bonus déjà accordés sur ce cours : {ctx.ExistingBonusDisplay} pt(s)" +
                    $"  ·  Total élève période : {bonusDialog.StudentBonusTotalDisplay} pt(s)";
                var maxOp = bonusDialog.MaxPointsPerOperation;
                infoRemaining.Text =
                    ctx.RemainingAddable <= 0
                        ? "Plus aucun point ajoutable sur ce cours (plafond atteint)."
                        : $"Points encore ajoutables : {ctx.RemainingAddableDisplay} pt(s)" +
                          $" (max {maxOp.ToString("0.##", CultureInfo.CurrentCulture)} / opération)";
            }

            courseBox.SelectionChanged += (_, _) => RefreshCourseInfo();
            BindCourses(bonusDialog);

            var saveContinueBtn = new Button
            {
                Content = "Enregistrer et continuer",
                Height = 36,
                Margin = new Thickness(0, 8, 8, 0),
                MinWidth = 160
            };
            var saveCloseBtn = new Button
            {
                Content = "Enregistrer et fermer",
                Height = 36,
                Margin = new Thickness(0, 8, 8, 0),
                MinWidth = 150
            };
            var closeBtn = new Button
            {
                Content = "Fermer",
                Height = 36,
                Margin = new Thickness(0, 8, 0, 0),
                MinWidth = 90
            };

            async Task<bool> TrySaveAsync(bool closeAfter)
            {
                if (courseBox.SelectedItem is not PedagogicalBonusCourseContextDto course)
                {
                    MessageBox.Show("Sélectionnez un cours.", "Bonus", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (!decimal.TryParse(pointsBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var points)
                    || points <= 0)
                {
                    MessageBox.Show("Indiquez un nombre de points valide.", "Bonus", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                var maxAllowed = Math.Min(course.RemainingAddable, bonusDialog.MaxPointsPerOperation);
                if (points > maxAllowed)
                {
                    MessageBox.Show(
                        $"Vous ne pouvez ajouter que {maxAllowed.ToString("0.##", CultureInfo.CurrentCulture)} pt(s) sur ce cours.\n" +
                        $"Note actuelle : {course.CurrentScoreDisplay} / {course.MaximumDisplay}.",
                        "Plafond atteint",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                var motive = motiveBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(motive))
                {
                    MessageBox.Show("Indiquez le motif.", "Bonus", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                try
                {
                    saveContinueBtn.IsEnabled = saveCloseBtn.IsEnabled = false;
                    statusLabel.Text = "Enregistrement…";
                    var sheet = await _deliberationApi.SaveBonusAsync(new SavePedagogicalBonusRequest(
                        yearId,
                        classRoomId,
                        periodId,
                        studentId,
                        course.CourseId,
                        course.CourseAssignmentId,
                        points,
                        motive));
                    ApplySheet(sheet);
                    SelectedRow = Rows.FirstOrDefault(r => r.StudentId == studentId);
                    SheetChanged?.Invoke();
                    savedCount++;

                    if (closeAfter)
                    {
                        dialog.DialogResult = true;
                        dialog.Close();
                        return true;
                    }

                    // Rafraîchir le contexte pour enchaîner sur un autre cours.
                    var refreshed = await _deliberationApi.GetBonusDialogAsync(
                        yearId, classRoomId, periodId, studentId);
                    BindCourses(refreshed, preferCourseId: null);
                    // Préférer le prochain cours encore ajoutable (différent du cours courant).
                    var next = refreshed.Courses.FirstOrDefault(c =>
                        c.CourseId != course.CourseId && c.RemainingAddable > 0);
                    if (next is not null)
                    {
                        courseBox.SelectedItem = next;
                    }

                    pointsBox.Text = "1";
                    statusLabel.Text =
                        $"+{points.ToString("0.##", CultureInfo.CurrentCulture)} pt(s) sur {course.CourseName}. " +
                        "Choisissez un autre cours ou fermez.";
                    return true;
                }
                catch (Exception ex)
                {
                    statusLabel.Text = string.Empty;
                    MessageBox.Show(ex.Message, "Bonus pédagogique", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                finally
                {
                    saveContinueBtn.IsEnabled = saveCloseBtn.IsEnabled = true;
                }
            }

            saveContinueBtn.Click += async (_, _) => await TrySaveAsync(closeAfter: false);
            saveCloseBtn.Click += async (_, _) => await TrySaveAsync(closeAfter: true);
            closeBtn.Click += (_, _) =>
            {
                dialog.DialogResult = savedCount > 0;
                dialog.Close();
            };

            var infoPanel = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xF0, 0xFD, 0xF4)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xA7, 0xF3, 0xD0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 12),
                Child = new StackPanel { Children = { infoBefore, infoBonus, infoRemaining } }
            };

            dialog.Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Ajoutez des points sur un ou plusieurs cours, sans refermer la fenêtre.",
                            FontSize = 12,
                            Foreground = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(0x64, 0x74, 0x8B)),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 10)
                        },
                        new TextBlock
                        {
                            Text = "Cours",
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        courseBox,
                        infoPanel,
                        new TextBlock
                        {
                            Text = "Points à ajouter",
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        pointsBox,
                        new TextBlock
                        {
                            Text = "Motif",
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        motiveBox,
                        statusLabel,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children = { saveContinueBtn, saveCloseBtn, closeBtn }
                        }
                    }
                }
            };

            dialog.ShowDialog();
            StatusMessage = savedCount > 0
                ? $"{savedCount} bonus enregistré(s) — résultats recalculés."
                : StatusMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Bonus pédagogique", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenDecisionAsync(DeliberationRowVm? row)
    {
        if (row is null || SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null
            || !CanSetFinalDecision)
        {
            StatusMessage = "Les décisions finales ne sont disponibles qu'en fin d'année.";
            return;
        }

        try
        {
            var dialogDto = await _deliberationApi.GetDecisionAsync(
                SelectedYear.Id, SelectedClassRoom.Id, SelectedPeriod.Id, row.StudentId);
            var dialogVm = new DeliberationDecisionDialogViewModel(dialogDto);
            var window = new Views.DeliberationDecisionDialog(dialogVm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (window.ShowDialog() != true || dialogVm.PendingSave is null)
            {
                return;
            }

            IsBusy = true;
            await _deliberationApi.SaveDecisionAsync(dialogVm.PendingSave);
            await LoadSheetAsync();
            StatusMessage = $"Décision enregistrée pour {row.FullName}.";
            SheetChanged?.Invoke();
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
    private void ViewSpecialCaseResults(DeliberationSpecialCaseItemVm? item)
    {
        if (item is null)
        {
            return;
        }

        OpenIndividualResult(item.StudentId);
    }

    [RelayCommand]
    private async Task OpenSpecialCaseProfileAsync(DeliberationSpecialCaseItemVm? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            var profile = await _studentApi.GetProfileAsync(item.StudentId);
            var window = new Views.StudentProfileWindow(profile)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void OpenIndividualResult(Guid studentId)
    {
        if (SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            StatusMessage = "Sélectionnez une classe et une sous-période.";
            return;
        }

        ResultsNavigationBridge.RequestIndividual(new IndividualResultNavRequest(
            studentId,
            SelectedYear.Id,
            SelectedClassRoom.Id,
            PedagogicalSheetPeriodMode.SubPeriod,
            SelectedPeriod.Id));
    }

    [RelayCommand]
    private void ExportExcel()
    {
        if (Rows.Count == 0)
        {
            StatusMessage = "Aucune donnée à exporter.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV Excel (*.csv)|*.csv",
            FileName = $"Deliberation_{ClassDisplayName}_{PeriodLabel}.csv".Replace(' ', '_')
        };
        ErpFileDialog.PrepareSave(dialog);
        if (ErpFileDialog.ShowSave(dialog) != true)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Rang;Matricule;Nom;Moyenne;%;Mention;Décision;Décision après délibération;Statut");
        foreach (var row in Rows)
        {
            sb.Append(EscapeCsv(row.RankDisplay)).Append(';')
                .Append(EscapeCsv(row.RegistrationNumber)).Append(';')
                .Append(EscapeCsv(row.FullName)).Append(';')
                .Append(EscapeCsv(row.AverageDisplay)).Append(';')
                .Append(EscapeCsv(row.PercentageDisplay)).Append(';')
                .Append(EscapeCsv(row.Mention ?? string.Empty)).Append(';')
                .Append(EscapeCsv(row.ProposedDecisionLabel)).Append(';')
                .Append(EscapeCsv(row.FinalDecisionLabel)).Append(';')
                .Append(EscapeCsv(row.ValidationStatusLabel))
                .AppendLine();
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        StatusMessage = "Export Excel (CSV) terminé.";
    }

    [RelayCommand]
    private void ExportPdf()
    {
        if (Rows.Count == 0)
        {
            StatusMessage = "Aucune donnée à exporter.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "HTML (*.html)|*.html",
            FileName = $"Deliberation_{ClassDisplayName}_{PeriodLabel}.html".Replace(' ', '_')
        };
        ErpFileDialog.PrepareSave(dialog);
        if (ErpFileDialog.ShowSave(dialog) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, BuildHtmlDocument(), Encoding.UTF8);
        StatusMessage = "Export PDF (HTML) terminé.";
    }

    [RelayCommand]
    private void Print()
    {
        if (Rows.Count == 0)
        {
            StatusMessage = "Aucune donnée à imprimer.";
            return;
        }

        try
        {
            var document = BuildPrintDocument();
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true)
            {
                return;
            }

            printDialog.PrintDocument(
                ((IDocumentPaginatorSource)document).DocumentPaginator,
                "Délibération");
            StatusMessage = "Impression envoyée.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    partial void OnSelectedClassRoomChanged(ClassRoomLookupDto? value)
    {
        if (!_filtersReady || _suppressReload)
        {
            return;
        }

        _ = LoadPeriodContextAndSheetAsync();
    }

    partial void OnSelectedPeriodChanged(PedagogicalSheetPeriodOptionDto? value)
    {
        if (!_filtersReady || _suppressReload)
        {
            return;
        }

        _ = LoadSheetAsync();
    }

    private void OnGlobalAcademicYearChanged()
    {
        if (_lookups is null)
        {
            return;
        }

        _suppressReload = true;
        SyncYearFromTitleBar();
        RefreshClassRooms();
        _suppressReload = false;
        _ = LoadPeriodContextAndSheetAsync();
    }

    private async Task LoadFiltersAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            _lookups = await _schoolApi.GetLookupsAsync();
            _filtersReady = false;
            SyncYearFromTitleBar();
            RefreshClassRooms();
            _filtersReady = true;
            await LoadPeriodContextAndSheetAsync();
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

    private void SyncYearFromTitleBar()
    {
        var bridgeYear = AcademicYearRefreshBridge.SelectedYear;
        if (bridgeYear is not null)
        {
            SelectedYear = bridgeYear;
            return;
        }

        SelectedYear = _lookups?.AcademicYears.FirstOrDefault(y => y.IsCurrent)
            ?? _lookups?.AcademicYears.OrderByDescending(y => y.Label).FirstOrDefault();
    }

    private void RefreshClassRooms()
    {
        ClassRooms.Clear();
        if (_lookups is null || SelectedYear is null)
        {
            SelectedClassRoom = null;
            return;
        }

        foreach (var room in _lookups.ClassRooms
                     .Where(c => c.AcademicYearId == SelectedYear.Id)
                     .OrderBy(c => c.Name))
        {
            ClassRooms.Add(room);
        }

        SelectedClassRoom = ClassRooms.FirstOrDefault(c => c.Id == SelectedClassRoom?.Id)
            ?? ClassRooms.FirstOrDefault();
    }

    private async Task LoadPeriodContextAndSheetAsync()
    {
        if (SelectedYear is null || SelectedClassRoom is null)
        {
            ClearSheet();
            StatusMessage = SelectedYear is null
                ? "Aucune année scolaire sélectionnée dans la barre de titre."
                : "Sélectionnez une classe.";
            return;
        }

        IsBusy = true;
        try
        {
            _periodContext = await _gradeApi.GetPedagogicalSheetContextAsync(
                SelectedYear.Id, SelectedClassRoom.Id);
            ClassDisplayName = _periodContext.ClassDisplayName;
            RebuildPeriodOptions();
            await LoadSheetAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ClearSheet();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildPeriodOptions()
    {
        _suppressReload = true;
        try
        {
            PeriodOptions.Clear();
            if (_periodContext is null)
            {
                SelectedPeriod = null;
                return;
            }

            foreach (var option in _periodContext.SubPeriods
                         .OrderBy(o => o.OrderIndex).ThenBy(o => o.Name))
            {
                PeriodOptions.Add(option);
            }

            SelectedPeriod = PeriodOptions.FirstOrDefault(p => p.Id == _periodContext.DefaultSubPeriodId)
                ?? PeriodOptions.FirstOrDefault();
        }
        finally
        {
            _suppressReload = false;
        }
    }

    private async Task LoadSheetAsync()
    {
        if (SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            ClearSheet();
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        AccessBlocked = false;
        try
        {
            var sheet = await _deliberationApi.GetSheetAsync(
                SelectedYear.Id, SelectedClassRoom.Id, SelectedPeriod.Id);
            ApplySheet(sheet);
            await LoadMinutesInternalAsync();
            SheetChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ClearSheet();
            AccessBlocked = ex.Message.Contains("validés avant la délibération", StringComparison.OrdinalIgnoreCase);
            StatusMessage = AccessBlocked
                ? "Les résultats de cette classe doivent être validés avant la délibération."
                : ex.Message;
            SheetChanged?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadMinutesInternalAsync()
    {
        if (SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null || AccessBlocked)
        {
            ClearMinutes();
            return;
        }

        try
        {
            var minutes = await _deliberationApi.GetMinutesAsync(
                SelectedYear.Id, SelectedClassRoom.Id, SelectedPeriod.Id);
            ApplyMinutes(minutes);
        }
        catch (Exception ex)
        {
            // Lecture PV non bloquante pour la grille (ex. permission absente).
            ClearMinutes();
            if (StatusMessage is null || !StatusMessage.Contains("élève", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = ex.Message;
            }
        }
    }

    private void ApplyMinutes(DeliberationMinutesDto minutes)
    {
        PvGeneralObservations = minutes.GeneralObservations;
        PvCouncilDecisions = minutes.CouncilDecisions;
        PvPedagogicalRecommendations = minutes.PedagogicalRecommendations;
        PvRecordedByDisplay = string.IsNullOrWhiteSpace(minutes.RecordedByUserName)
            ? "—"
            : minutes.RecordedByUserName!;
        PvRecordedAtDisplay = minutes.RecordedAtDisplay;
        PvExists = minutes.Exists;
        PvStatusDisplay = minutes.Exists ? "Enregistré" : "Non enregistré";
    }

    private void ClearMinutes()
    {
        PvGeneralObservations = PvCouncilDecisions = PvPedagogicalRecommendations = null;
        PvRecordedByDisplay = PvRecordedAtDisplay = "—";
        PvExists = false;
        PvStatusDisplay = "Non enregistré";
    }

    private void ApplySheet(DeliberationSheetDto sheet)
    {
        AccessBlocked = false;
        ClassDisplayName = sheet.ClassDisplayName;
        PeriodLabel = sheet.PeriodLabel;
        ValidationStatusLabel = sheet.ValidationStatusLabel;
        ValidatedAtDisplay = sheet.ValidatedAtUtc is null
            ? "—"
            : sheet.ValidatedAtUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        ValidatedByDisplay = string.IsNullOrWhiteSpace(sheet.ValidatedByUserName)
            ? "—"
            : sheet.ValidatedByUserName!;
        SummaryStudentCount = sheet.Summary.StudentCount.ToString(CultureInfo.InvariantCulture);
        SummaryAdmitted = sheet.Summary.AdmittedCount.ToString(CultureInfo.InvariantCulture);
        SummaryDeferred = sheet.Summary.DeferredCount.ToString(CultureInfo.InvariantCulture);
        SummaryExcluded = sheet.Summary.ExcludedCount.ToString(CultureInfo.InvariantCulture);
        SummaryClassAverage = sheet.Summary.ClassAverageDisplay;
        SummarySuccessRate = sheet.Summary.SuccessRateDisplay;

        Rows.Clear();
        var previousStudentId = SelectedRow?.StudentId;
        foreach (var row in sheet.Students)
        {
            Rows.Add(new DeliberationRowVm(
                row.StudentId,
                row.Rank <= 0 ? "—" : row.Rank.ToString(CultureInfo.InvariantCulture),
                row.RegistrationNumber,
                row.FullName,
                row.AverageDisplay,
                row.PercentageDisplay,
                row.Mention ?? "—",
                row.ConductLabel ?? "—",
                row.ProposedDecisionLabel,
                row.FinalDecisionLabel,
                row.ValidationStatusLabel,
                row.Observation ?? string.Empty,
                row.ConductDefinitionId,
                row.FinalDecision));
        }

        // Conserver la sélection après recalcul (sinon le 2e bonus échoue silencieusement).
        SelectedRow = previousStudentId is Guid sid
            ? Rows.FirstOrDefault(r => r.StudentId == sid)
            : null;

        ApplySpecialCases(sheet.SpecialCases);

        PeriodModeLabel = sheet.PeriodContext.ModeLabel;
        CanAddBonusPoints = sheet.PeriodContext.CanAddBonusPoints;
        CanSetConduct = sheet.PeriodContext.CanSetConduct;
        CanSetFinalDecision = sheet.PeriodContext.CanSetFinalDecision;
        CanOfferRepechage = sheet.PeriodContext.CanOfferRepechage;
        CanValidateClass = sheet.PeriodContext.CanValidateClass;
        CanCancelValidation = sheet.PeriodContext.CanCancelValidation;
        IsSessionReadOnly = sheet.PeriodContext.IsReadOnly;
        IsYearEnd = sheet.PeriodContext.IsYearEnd;
        ShowDecisionColumn = sheet.PeriodContext.IsYearEnd;

        AvailableDecisions.Clear();
        foreach (var d in sheet.PeriodContext.AvailableDecisions)
        {
            AvailableDecisions.Add(d);
        }

        ConductOptions.Clear();
        foreach (var opt in sheet.ConductOptions)
        {
            ConductOptions.Add(opt);
        }

        CourseOptions.Clear();
        foreach (var course in sheet.CourseOptions)
        {
            CourseOptions.Add(course);
        }

        // Lier les sélections ComboBox après remplissage des options (sans déclencher de save).
        foreach (var rowVm in Rows)
        {
            rowVm.ConductChanged -= OnRowConductChanged;
            rowVm.DecisionChanged -= OnRowDecisionChanged;
            rowVm.SetSelectionsWithoutEvents(
                rowVm.ConductDefinitionId is Guid cid
                    ? ConductOptions.FirstOrDefault(c => c.Id == cid)
                    : null,
                rowVm.FinalDecision is FinalCouncilDecision fd
                    ? AvailableDecisions.FirstOrDefault(d => d.Value == fd)
                    : null);
            rowVm.ConductChanged += OnRowConductChanged;
            rowVm.DecisionChanged += OnRowDecisionChanged;
        }

        StatusMessage = $"{sheet.Students.Count} élève(s) — {sheet.PeriodContext.ModeLabel} — {sheet.ValidationStatusLabel}";
    }

    private async void OnRowConductChanged(DeliberationRowVm row)
    {
        if (!CanSetConduct || row.SelectedConduct is null
            || SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            return;
        }

        try
        {
            var sheet = await _deliberationApi.SaveConductAsync(new SaveStudentConductRequest(
                SelectedYear.Id,
                SelectedClassRoom.Id,
                SelectedPeriod.Id,
                row.StudentId,
                row.SelectedConduct.Id,
                string.IsNullOrWhiteSpace(row.Observation) ? null : row.Observation));
            ApplySheet(sheet);
            SheetChanged?.Invoke();
            StatusMessage = $"Conduite enregistrée pour {row.FullName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async void OnRowDecisionChanged(DeliberationRowVm row)
    {
        if (!CanSetFinalDecision || row.SelectedDecision is null
            || SelectedYear is null || SelectedClassRoom is null || SelectedPeriod is null)
        {
            return;
        }

        if (row.SelectedDecision.Value == FinalCouncilDecision.Repechage)
        {
            await OpenDecisionAsync(row);
            return;
        }

        try
        {
            await _deliberationApi.SaveDecisionAsync(new SaveDeliberationDecisionRequest(
                SelectedYear.Id,
                SelectedClassRoom.Id,
                SelectedPeriod.Id,
                row.StudentId,
                row.SelectedDecision.Value,
                string.IsNullOrWhiteSpace(row.Observation) ? null : row.Observation,
                null,
                null,
                null,
                null));
            await LoadSheetAsync();
            StatusMessage = $"Décision enregistrée pour {row.FullName}.";
            SheetChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void ApplySpecialCases(DeliberationSpecialCasesDto? specialCases)
    {
        SpecialCaseSections.Clear();
        if (specialCases is null)
        {
            SpecialCaseTotalCount = 0;
            HasSpecialCases = false;
            return;
        }

        AddSpecialCaseSection("Élèves ajournés", "#EA580C", specialCases.Deferred);
        AddSpecialCaseSection("Élèves exclus", "#DC2626", specialCases.Excluded);
        AddSpecialCaseSection("Absences justifiées", "#2563EB", specialCases.JustifiedAbsence);
        AddSpecialCaseSection("Absences injustifiées", "#7C3AED", specialCases.UnjustifiedAbsence);
        AddSpecialCaseSection("Décisions particulières", "#B45309", specialCases.ParticularDecision);

        SpecialCaseTotalCount = SpecialCaseSections.Sum(s => s.Items.Count);
        HasSpecialCases = SpecialCaseTotalCount > 0;
    }

    private void AddSpecialCaseSection(
        string title,
        string accentHex,
        IReadOnlyList<DeliberationSpecialCaseItemDto> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var section = new DeliberationSpecialCaseSectionVm(title, accentHex);
        foreach (var item in items)
        {
            section.Items.Add(new DeliberationSpecialCaseItemVm(
                item.StudentId,
                item.RegistrationNumber,
                item.FullName,
                item.CategoryLabel,
                item.Detail));
        }

        SpecialCaseSections.Add(section);
    }

    private void ClearSheet()
    {
        Rows.Clear();
        SpecialCaseSections.Clear();
        SpecialCaseTotalCount = 0;
        HasSpecialCases = false;
        ClearMinutes();
        ValidationStatusLabel = ValidatedAtDisplay = ValidatedByDisplay = "—";
        SummaryStudentCount = SummaryAdmitted = SummaryDeferred = SummaryExcluded = "0";
        SummaryClassAverage = SummarySuccessRate = "—";
        CanValidateClass = false;
        CanCancelValidation = false;
        IsSessionReadOnly = false;
    }

    private string BuildHtmlDocument()
    {
        var sb = new StringBuilder();
        sb.Append("<html><head><meta charset='utf-8'><title>Délibération</title>")
            .Append("<style>body{font-family:Segoe UI,sans-serif}table{border-collapse:collapse;width:100%}")
            .Append("th,td{border:1px solid #cbd5e1;padding:6px;font-size:12px}th{background:#0B1F47;color:#fff}</style></head><body>");
        sb.Append("<h2>Délibération — ").Append(System.Net.WebUtility.HtmlEncode(ClassDisplayName))
            .Append(" / ").Append(System.Net.WebUtility.HtmlEncode(PeriodLabel)).Append("</h2>");
        sb.Append("<p>Statut : ").Append(System.Net.WebUtility.HtmlEncode(ValidationStatusLabel))
            .Append(" — Validé le ").Append(System.Net.WebUtility.HtmlEncode(ValidatedAtDisplay))
            .Append(" par ").Append(System.Net.WebUtility.HtmlEncode(ValidatedByDisplay)).Append("</p>");
        sb.Append("<table><tr><th>Rang</th><th>Matricule</th><th>Nom</th><th>Moyenne</th><th>%</th><th>Mention</th><th>Décision</th><th>Décision après délibération</th><th>Statut</th></tr>");
        foreach (var row in Rows)
        {
            sb.Append("<tr><td>").Append(System.Net.WebUtility.HtmlEncode(row.RankDisplay))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.RegistrationNumber))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.FullName))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.AverageDisplay))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.PercentageDisplay))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.Mention ?? ""))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.ProposedDecisionLabel))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.FinalDecisionLabel))
                .Append("</td><td>").Append(System.Net.WebUtility.HtmlEncode(row.ValidationStatusLabel))
                .Append("</td></tr>");
        }

        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    private FlowDocument BuildPrintDocument()
    {
        var doc = new FlowDocument
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 12,
            PagePadding = new Thickness(40)
        };
        doc.Blocks.Add(new Paragraph(new Run($"Délibération — {ClassDisplayName} / {PeriodLabel}"))
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold
        });
        doc.Blocks.Add(new Paragraph(new Run(
            $"Statut : {ValidationStatusLabel} — Validé le {ValidatedAtDisplay} par {ValidatedByDisplay}")));

        var table = new Table();
        for (var i = 0; i < 9; i++)
        {
            table.Columns.Add(new TableColumn());
        }

        var header = new TableRowGroup();
        var headerRow = new TableRow();
        foreach (var h in new[] { "Rang", "Matricule", "Nom", "Moyenne", "%", "Mention", "Décision", "Décision après délibération", "Statut" })
        {
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(h))) { FontWeight = FontWeights.Bold });
        }

        header.Rows.Add(headerRow);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var row in Rows)
        {
            var tr = new TableRow();
            foreach (var cell in new[]
                     {
                         row.RankDisplay, row.RegistrationNumber, row.FullName, row.AverageDisplay,
                         row.PercentageDisplay, row.Mention ?? "", row.ProposedDecisionLabel,
                         row.FinalDecisionLabel, row.ValidationStatusLabel
                     })
            {
                tr.Cells.Add(new TableCell(new Paragraph(new Run(cell))));
            }

            body.Rows.Add(tr);
        }

        table.RowGroups.Add(body);
        doc.Blocks.Add(table);
        return doc;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(';') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

public partial class DeliberationRowVm : ObservableObject
{
    public DeliberationRowVm(
        Guid studentId,
        string rankDisplay,
        string registrationNumber,
        string fullName,
        string averageDisplay,
        string percentageDisplay,
        string? mention,
        string conductLabel,
        string proposedDecisionLabel,
        string finalDecisionLabel,
        string validationStatusLabel,
        string observation,
        Guid? conductDefinitionId,
        FinalCouncilDecision? finalDecision)
    {
        StudentId = studentId;
        RankDisplay = rankDisplay;
        RegistrationNumber = registrationNumber;
        FullName = fullName;
        AverageDisplay = averageDisplay;
        PercentageDisplay = percentageDisplay;
        Mention = mention;
        ConductLabel = conductLabel;
        ProposedDecisionLabel = proposedDecisionLabel;
        FinalDecisionLabel = finalDecisionLabel;
        ValidationStatusLabel = validationStatusLabel;
        Observation = observation;
        ConductDefinitionId = conductDefinitionId;
        FinalDecision = finalDecision;
    }

    public Guid StudentId { get; }
    public string RankDisplay { get; }
    public string RegistrationNumber { get; }
    public string FullName { get; }
    public string AverageDisplay { get; }
    public string PercentageDisplay { get; }
    public string? Mention { get; }
    public string ConductLabel { get; }
    public string ProposedDecisionLabel { get; }
    public string FinalDecisionLabel { get; }
    public string ValidationStatusLabel { get; }
    public Guid? ConductDefinitionId { get; }
    public FinalCouncilDecision? FinalDecision { get; }

    [ObservableProperty] private string _observation = string.Empty;
    [ObservableProperty] private ConductOptionDto? _selectedConduct;
    [ObservableProperty] private FinalCouncilDecisionOptionDto? _selectedDecision;

    public event Action<DeliberationRowVm>? ConductChanged;
    public event Action<DeliberationRowVm>? DecisionChanged;

    private bool _suppressEvents;

    public void SetSelectionsWithoutEvents(
        ConductOptionDto? conduct,
        FinalCouncilDecisionOptionDto? decision)
    {
        _suppressEvents = true;
        SelectedConduct = conduct;
        SelectedDecision = decision;
        _suppressEvents = false;
    }

    partial void OnSelectedConductChanged(ConductOptionDto? value)
    {
        if (!_suppressEvents)
        {
            ConductChanged?.Invoke(this);
        }
    }

    partial void OnSelectedDecisionChanged(FinalCouncilDecisionOptionDto? value)
    {
        if (!_suppressEvents)
        {
            DecisionChanged?.Invoke(this);
        }
    }
}

public sealed class DeliberationSpecialCaseSectionVm
{
    public DeliberationSpecialCaseSectionVm(string title, string accentHex)
    {
        Title = title;
        AccentHex = accentHex;
    }

    public string Title { get; }
    public string AccentHex { get; }
    public ObservableCollection<DeliberationSpecialCaseItemVm> Items { get; } = [];
    public string CountDisplay => Items.Count.ToString(CultureInfo.InvariantCulture);
}

public sealed record DeliberationSpecialCaseItemVm(
    Guid StudentId,
    string RegistrationNumber,
    string FullName,
    string CategoryLabel,
    string Detail);
