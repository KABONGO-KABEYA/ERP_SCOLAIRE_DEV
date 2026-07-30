using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.PedagogicalPeriods.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class PedagogicalPeriodsViewModel : ViewModelBase
{
    private readonly IPedagogicalPeriodApiService _api;

    public PedagogicalPeriodsViewModel(IPedagogicalPeriodApiService api)
    {
        _api = api;
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
        _ = LoadStructureAsync();
    }

    public ObservableCollection<CycleGroupItem> CycleGroups { get; } = [];

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _hasStructure;
    [ObservableProperty] private bool _isOpenDialogOpen;
    [ObservableProperty] private SubPeriodItem? _openingItem;
    [ObservableProperty] private DateTime _openStartDate = DateTime.Today;
    [ObservableProperty] private DateTime _openEndDate = DateTime.Today;
    [ObservableProperty] private string? _openError;

    private void OnGlobalAcademicYearChanged() => _ = LoadStructureAsync();

    [RelayCommand]
    private async Task LoadStructureAsync()
    {
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null)
        {
            CycleGroups.Clear();
            HasStructure = false;
            StatusMessage = "Aucune année scolaire sélectionnée.";
            return;
        }

        IsBusy = true;
        try
        {
            var structure = await _api.GetStructureAsync(yearId.Value);
            ApplyStructure(structure);
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            CycleGroups.Clear();
            HasStructure = false;
            StatusMessage = ex.Message.Contains("introuvable", StringComparison.OrdinalIgnoreCase)
                ? null
                : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateStructureAsync()
    {
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null)
        {
            StatusMessage = "Aucune année scolaire sélectionnée.";
            return;
        }

        IsBusy = true;
        try
        {
            var structure = await _api.CreateStructureAsync(
                new CreatePedagogicalStructureRequest(yearId.Value, ReplaceExisting: false));
            ApplyStructure(structure);
            StatusMessage = null;
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
    private void BeginOpen(SubPeriodItem? item)
    {
        if (item is null || !item.CanOpen)
        {
            return;
        }

        OpeningItem = item;
        var suggestedStart = item.SuggestedStartDate ?? DateOnly.FromDateTime(DateTime.Today);
        OpenStartDate = suggestedStart.ToDateTime(TimeOnly.MinValue);
        OpenEndDate = suggestedStart.AddDays(29).ToDateTime(TimeOnly.MinValue);
        OpenError = null;
        IsOpenDialogOpen = true;
    }

    [RelayCommand]
    private void CancelOpen()
    {
        IsOpenDialogOpen = false;
        OpeningItem = null;
        OpenError = null;
    }

    [RelayCommand]
    private async Task ConfirmOpenAsync()
    {
        if (OpeningItem is null)
        {
            return;
        }

        var start = DateOnly.FromDateTime(OpenStartDate);
        var end = DateOnly.FromDateTime(OpenEndDate);
        if (end < start)
        {
            OpenError = "La date de fin doit être postérieure ou égale à la date de début.";
            return;
        }

        IsBusy = true;
        try
        {
            await _api.OpenSubPeriodAsync(
                OpeningItem.Id,
                new OpenSubPeriodRequest(start, end));
            IsOpenDialogOpen = false;
            OpeningItem = null;
            await LoadStructureAsync();
        }
        catch (Exception ex)
        {
            OpenError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CloseAsync(SubPeriodItem? item)
    {
        if (item is null) return;
        await RunActionAsync(() => _api.CloseSubPeriodAsync(item.Id));
    }

    [RelayCommand]
    private async Task LockAsync(SubPeriodItem? item)
    {
        if (item is null) return;
        await RunActionAsync(() => _api.LockSubPeriodAsync(item.Id));
    }

    [RelayCommand]
    private async Task UnlockAsync(SubPeriodItem? item)
    {
        if (item is null) return;
        await RunActionAsync(() => _api.UnlockSubPeriodAsync(item.Id));
    }

    private async Task RunActionAsync(Func<Task<PedagogicalSubPeriodDto>> action)
    {
        IsBusy = true;
        try
        {
            await action();
            await LoadStructureAsync();
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

    private void ApplyStructure(PedagogicalPeriodStructureDto structure)
    {
        CycleGroups.Clear();
        var totalSubs = 0;
        foreach (var cycle in structure.Cycles)
        {
            var mains = new ObservableCollection<MainPeriodItem>();
            DateOnly? previousEnd = null;
            foreach (var main in cycle.MainPeriods.OrderBy(m => m.OrderIndex))
            {
                var subs = new ObservableCollection<SubPeriodItem>();
                foreach (var sub in main.SubPeriods.OrderBy(s => s.SequenceIndex))
                {
                    totalSubs++;
                    DateOnly? suggested = null;
                    if (sub.Status == AcademicSubPeriodStatus.AVenir && previousEnd is DateOnly pe)
                    {
                        suggested = pe.AddDays(1);
                    }

                    subs.Add(new SubPeriodItem(sub, suggested));
                    if (sub.EndDate is DateOnly end)
                    {
                        previousEnd = end;
                    }
                }

                mains.Add(new MainPeriodItem(main.Id, main.Name, main.OrderIndex, subs));
            }

            CycleGroups.Add(new CycleGroupItem(cycle.CycleGroup, cycle.CycleGroupLabel, mains));
        }

        HasStructure = totalSubs > 0;
    }
}

public sealed class CycleGroupItem
{
    public CycleGroupItem(
        PedagogicalCycleGroup cycleGroup,
        string label,
        ObservableCollection<MainPeriodItem> mainPeriods)
    {
        CycleGroup = cycleGroup;
        Label = label;
        MainPeriods = mainPeriods;
    }

    public PedagogicalCycleGroup CycleGroup { get; }
    public string Label { get; }
    public ObservableCollection<MainPeriodItem> MainPeriods { get; }
}

public sealed class MainPeriodItem
{
    public MainPeriodItem(
        Guid id,
        string name,
        int orderIndex,
        ObservableCollection<SubPeriodItem> subPeriods)
    {
        Id = id;
        Name = name;
        OrderIndex = orderIndex;
        SubPeriods = subPeriods;
    }

    public Guid Id { get; }
    public string Name { get; }
    public int OrderIndex { get; }
    public ObservableCollection<SubPeriodItem> SubPeriods { get; }
}

public sealed partial class SubPeriodItem : ObservableObject
{
    public SubPeriodItem(PedagogicalSubPeriodDto dto, DateOnly? suggestedStartDate = null)
    {
        Id = dto.Id;
        Name = dto.Name;
        Status = dto.Status;
        StatusLabel = dto.StatusLabel;
        IsActive = dto.IsActive;
        StartDate = dto.StartDate;
        EndDate = dto.EndDate;
        SuggestedStartDate = suggestedStartDate;
        HasDates = dto.StartDate is not null && dto.EndDate is not null;
        DateRangeText = HasDates
            ? $"{dto.StartDate:dd/MM/yyyy} → {dto.EndDate:dd/MM/yyyy}"
            : "—";
        StatusGlyph = dto.Status switch
        {
            AcademicSubPeriodStatus.Ouverte => "●",
            AcademicSubPeriodStatus.Cloturee => "●",
            AcademicSubPeriodStatus.Verrouillee => "●",
            _ => "○"
        };
        StatusBrush = dto.Status switch
        {
            AcademicSubPeriodStatus.Ouverte => "#16A34A",
            AcademicSubPeriodStatus.Cloturee => "#2563EB",
            AcademicSubPeriodStatus.Verrouillee => "#DC2626",
            _ => "#94A3B8"
        };
        CanOpen = dto.Status == AcademicSubPeriodStatus.AVenir;
        CanClose = dto.Status == AcademicSubPeriodStatus.Ouverte;
        CanLock = dto.Status == AcademicSubPeriodStatus.Cloturee;
        CanUnlock = dto.Status is AcademicSubPeriodStatus.Cloturee or AcademicSubPeriodStatus.Verrouillee;
    }

    public Guid Id { get; }
    public string Name { get; }
    public AcademicSubPeriodStatus Status { get; }
    public string StatusLabel { get; }
    public bool IsActive { get; }
    public DateOnly? StartDate { get; }
    public DateOnly? EndDate { get; }
    public DateOnly? SuggestedStartDate { get; }
    public bool HasDates { get; }
    public string DateRangeText { get; }
    public string StatusGlyph { get; }
    public string StatusBrush { get; }
    public bool CanOpen { get; }
    public bool CanClose { get; }
    public bool CanLock { get; }
    public bool CanUnlock { get; }
}
