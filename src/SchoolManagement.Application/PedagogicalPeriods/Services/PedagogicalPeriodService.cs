namespace SchoolManagement.Application.PedagogicalPeriods.Services;

using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.PedagogicalPeriods.DTOs;
using SchoolManagement.Application.PedagogicalPeriods.Interfaces;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Shared.Constants;

public sealed class PedagogicalPeriodService : IPedagogicalPeriodService
{
    private readonly IRepository<AcademicYear> _yearRepository;
    private readonly IRepository<AcademicMainPeriod> _mainPeriodRepository;
    private readonly IRepository<AcademicPeriod> _subPeriodRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public PedagogicalPeriodService(
        IRepository<AcademicYear> yearRepository,
        IRepository<AcademicMainPeriod> mainPeriodRepository,
        IRepository<AcademicPeriod> subPeriodRepository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _yearRepository = yearRepository;
        _mainPeriodRepository = mainPeriodRepository;
        _subPeriodRepository = subPeriodRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<PedagogicalPeriodStructureDto> GetStructureAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        var year = await GetYearOrThrowAsync(schoolId, academicYearId, cancellationToken);
        return await BuildStructureDtoAsync(year, cancellationToken);
    }

    public async Task<PedagogicalPeriodStructureDto> CreateDefaultStructureAsync(
        Guid schoolId,
        CreatePedagogicalStructureRequest request,
        CancellationToken cancellationToken = default)
    {
        var year = await GetYearOrThrowAsync(schoolId, request.AcademicYearId, cancellationToken);

        EnsureCanManagePedagogicalPeriods();

        var existingMains = await _mainPeriodRepository.FindAsync(
            m => m.SchoolId == schoolId && m.AcademicYearId == year.Id,
            cancellationToken);

        if (existingMains.Count > 0)
        {
            if (!request.ReplaceExisting)
            {
                throw new DomainException(
                    "Une structure pédagogique existe déjà pour cette année. " +
                    "Utilisez le remplacement explicite pour la recréer.");
            }

            var existingSubs = await _subPeriodRepository.FindAsync(
                s => s.AcademicYearId == year.Id && s.MainPeriodId != null,
                cancellationToken);

            if (existingSubs.Any(s => s.Status is AcademicSubPeriodStatus.Ouverte
                    or AcademicSubPeriodStatus.Cloturee
                    or AcademicSubPeriodStatus.Verrouillee))
            {
                throw new DomainException(
                    "Impossible de remplacer : des sous-périodes ont déjà été ouvertes ou clôturées.");
            }

            foreach (var sub in existingSubs)
            {
                await _subPeriodRepository.DeleteAsync(sub, cancellationToken);
            }

            foreach (var main in existingMains)
            {
                await _mainPeriodRepository.DeleteAsync(main, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var templates = BuildDefaultTemplates(year);
        foreach (var (main, subs) in templates)
        {
            await _mainPeriodRepository.AddAsync(main, cancellationToken);
            foreach (var sub in subs)
            {
                await _subPeriodRepository.AddAsync(sub, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildStructureDtoAsync(year, cancellationToken);
    }

    public async Task<PedagogicalSubPeriodDto> OpenSubPeriodAsync(
        Guid schoolId,
        Guid subPeriodId,
        OpenSubPeriodRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManagePedagogicalPeriods();
        var sub = await GetSubPeriodOrThrowAsync(schoolId, subPeriodId, cancellationToken);

        if (sub.Status is AcademicSubPeriodStatus.Cloturee or AcademicSubPeriodStatus.Verrouillee)
        {
            throw new DomainException(
                "Une sous-période clôturée ou verrouillée ne peut pas être rouverte via « Ouvrir ». " +
                "Utilisez le déverrouillage exceptionnel.");
        }

        if (sub.Status == AcademicSubPeriodStatus.Ouverte)
        {
            return await MapSubAsync(sub, cancellationToken);
        }

        if (request is null)
        {
            throw new DomainException(
                "Les dates de début et de fin sont obligatoires pour ouvrir une période.");
        }

        EnsureValidDateRange(request.StartDate, request.EndDate, sub.Name);

        var main = await GetMainOrThrowAsync(sub.MainPeriodId!.Value, cancellationToken);
        var ordered = await GetOrderedCycleSubsAsync(schoolId, sub.AcademicYearId, main.CycleGroup, cancellationToken);
        EnsureCanOpen(sub, ordered);

        sub.StartDate = request.StartDate;
        sub.EndDate = request.EndDate;
        sub.PlannedCloseDate = request.EndDate;

        EnsureChronologicalDates(ordered, sub);
        EnsureNoOverlap(ordered.Select(x => x.Sub).ToList(), sub);

        sub.Status = AcademicSubPeriodStatus.Ouverte;
        sub.IsClosed = false;
        sub.OpenedAt = DateTime.UtcNow;
        sub.ClosedAt = null;

        await _subPeriodRepository.UpdateAsync(sub, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapSubAsync(sub, cancellationToken);
    }

    public async Task<PedagogicalSubPeriodDto> CloseSubPeriodAsync(
        Guid schoolId,
        Guid subPeriodId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManagePedagogicalPeriods();
        var sub = await GetSubPeriodOrThrowAsync(schoolId, subPeriodId, cancellationToken);

        if (sub.Status != AcademicSubPeriodStatus.Ouverte)
        {
            throw new DomainException("Seule une sous-période ouverte peut être clôturée.");
        }

        var closeDate = DateOnly.FromDateTime(DateTime.Now);
        var plannedEnd = sub.PlannedCloseDate ?? sub.EndDate
            ?? throw new DomainException("La sous-période ouverte n'a pas de date de fin.");

        sub.Status = AcademicSubPeriodStatus.Cloturee;
        sub.IsClosed = true;
        sub.ClosedAt = DateTime.UtcNow;
        // Conserve la date de fin réelle de clôture pour l'historique.
        if (sub.EndDate is null || closeDate < sub.EndDate.Value)
        {
            sub.EndDate = closeDate;
            sub.PlannedCloseDate = plannedEnd;
        }

        await _subPeriodRepository.UpdateAsync(sub, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapSubAsync(sub, cancellationToken);
    }

    public async Task<PedagogicalSubPeriodDto> LockSubPeriodAsync(
        Guid schoolId,
        Guid subPeriodId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManagePedagogicalPeriods();
        var sub = await GetSubPeriodOrThrowAsync(schoolId, subPeriodId, cancellationToken);

        if (sub.Status is not (AcademicSubPeriodStatus.Cloturee or AcademicSubPeriodStatus.Verrouillee))
        {
            throw new DomainException("Verrouillez uniquement une sous-période déjà clôturée.");
        }

        sub.Status = AcademicSubPeriodStatus.Verrouillee;
        sub.IsClosed = true;
        await _subPeriodRepository.UpdateAsync(sub, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapSubAsync(sub, cancellationToken);
    }

    public async Task<PedagogicalSubPeriodDto> UnlockSubPeriodAsync(
        Guid schoolId,
        Guid subPeriodId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManagePedagogicalPeriods();
        var sub = await GetSubPeriodOrThrowAsync(schoolId, subPeriodId, cancellationToken);

        if (sub.Status != AcademicSubPeriodStatus.Verrouillee
            && sub.Status != AcademicSubPeriodStatus.Cloturee)
        {
            throw new DomainException(
                "Seul un déverrouillage exceptionnel d'une période clôturée/verrouillée est autorisé.");
        }

        var main = await GetMainOrThrowAsync(sub.MainPeriodId!.Value, cancellationToken);
        var ordered = await GetOrderedCycleSubsAsync(schoolId, sub.AcademicYearId, main.CycleGroup, cancellationToken);
        var openOther = ordered.Select(x => x.Sub)
            .FirstOrDefault(s => s.Status == AcademicSubPeriodStatus.Ouverte && s.Id != sub.Id);
        if (openOther is not null)
        {
            throw new DomainException(
                $"La {openOther.Name} est actuellement ouverte.\n" +
                "Veuillez d'abord la clôturer avant de déverrouiller une autre période.");
        }

        sub.Status = AcademicSubPeriodStatus.Ouverte;
        sub.IsClosed = false;
        sub.OpenedAt ??= DateTime.UtcNow;
        sub.ClosedAt = null;
        await _subPeriodRepository.UpdateAsync(sub, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapSubAsync(sub, cancellationToken);
    }

    public async Task<PedagogicalSubPeriodDto> UpdateSubPeriodSettingsAsync(
        Guid schoolId,
        Guid subPeriodId,
        UpdateSubPeriodSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManagePedagogicalPeriods();
        var sub = await GetSubPeriodOrThrowAsync(schoolId, subPeriodId, cancellationToken);

        if (sub.Status != AcademicSubPeriodStatus.AVenir)
        {
            throw new DomainException(
                "Seules les sous-périodes « À venir » peuvent avoir leurs dates modifiées.");
        }

        EnsureValidDateRange(request.StartDate, request.EndDate, sub.Name);

        var main = await GetMainOrThrowAsync(sub.MainPeriodId!.Value, cancellationToken);
        var ordered = await GetOrderedCycleSubsAsync(schoolId, sub.AcademicYearId, main.CycleGroup, cancellationToken);

        sub.StartDate = request.StartDate;
        sub.EndDate = request.EndDate;
        sub.PlannedCloseDate = request.EndDate;

        EnsureChronologicalDates(ordered, sub);
        EnsureNoOverlap(ordered.Select(x => x.Sub).ToList(), sub);

        await _subPeriodRepository.UpdateAsync(sub, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await MapSubAsync(sub, cancellationToken);
    }

    public async Task<PedagogicalPeriodStructureDto> ProposeSequentialDatesAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManagePedagogicalPeriods();
        var year = await GetYearOrThrowAsync(schoolId, academicYearId, cancellationToken);

        foreach (var cycle in Enum.GetValues<PedagogicalCycleGroup>())
        {
            await ProposeDatesForCycleAsync(schoolId, year, cycle, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await BuildStructureDtoAsync(year, cancellationToken);
    }

    private async Task ProposeDatesForCycleAsync(
        Guid schoolId,
        AcademicYear year,
        PedagogicalCycleGroup cycleGroup,
        CancellationToken cancellationToken)
    {
        var ordered = await GetOrderedCycleSubsAsync(schoolId, year.Id, cycleGroup, cancellationToken);
        if (ordered.Count == 0)
        {
            return;
        }

        var cursor = year.StartDate;
        var lastCommitted = ordered.LastOrDefault(x => x.Sub.Status != AcademicSubPeriodStatus.AVenir);
        if (lastCommitted.Sub is not null && lastCommitted.Sub.EndDate is DateOnly committedEnd)
        {
            cursor = committedEnd.AddDays(1);
        }

        var upcoming = ordered.Where(x => x.Sub.Status == AcademicSubPeriodStatus.AVenir).ToList();
        if (upcoming.Count == 0)
        {
            return;
        }

        var remainingDays = Math.Max(upcoming.Count, year.EndDate.DayNumber - cursor.DayNumber + 1);
        var slice = Math.Max(5, remainingDays / upcoming.Count);

        for (var i = 0; i < upcoming.Count; i++)
        {
            var sub = upcoming[i].Sub;
            var start = cursor;
            if (start > year.EndDate)
            {
                start = year.EndDate;
            }

            var end = DateOnly.FromDayNumber(Math.Min(year.EndDate.DayNumber, start.DayNumber + slice - 1));
            if (end < start)
            {
                end = start;
            }

            if (i == upcoming.Count - 1 && year.EndDate >= start)
            {
                end = year.EndDate;
            }

            sub.StartDate = start;
            sub.EndDate = end;
            sub.PlannedCloseDate = end;
            await _subPeriodRepository.UpdateAsync(sub, cancellationToken);
            cursor = end.AddDays(1);
        }
    }

    public async Task<ActiveSubPeriodDto?> GetActiveSubPeriodAsync(
        Guid schoolId,
        Guid academicYearId,
        PedagogicalCycleGroup cycleGroup,
        CancellationToken cancellationToken = default)
    {
        await GetYearOrThrowAsync(schoolId, academicYearId, cancellationToken);
        var ordered = await GetOrderedCycleSubsAsync(schoolId, academicYearId, cycleGroup, cancellationToken);
        var open = ordered.FirstOrDefault(x => x.Sub.Status == AcademicSubPeriodStatus.Ouverte);
        if (open.Sub is null || open.Sub.StartDate is null || open.Sub.EndDate is null)
        {
            return null;
        }

        return new ActiveSubPeriodDto(
            open.Sub.Id,
            open.Sub.Name,
            open.Main.Name,
            open.Main.CycleGroup,
            open.Sub.Kind,
            open.Sub.Status,
            GetStatusLabel(open.Sub.Status),
            open.Sub.MaxScore,
            open.Sub.MaxEvaluationCount,
            open.Sub.StartDate.Value,
            open.Sub.EndDate.Value,
            open.Sub.OpenedAt,
            open.Sub.PlannedCloseDate ?? open.Sub.EndDate);
    }

    private static void EnsureCanOpen(
        AcademicPeriod sub,
        IReadOnlyList<(AcademicPeriod Sub, AcademicMainPeriod Main, int SequenceIndex)> ordered)
    {
        var openOther = ordered.Select(x => x.Sub)
            .FirstOrDefault(s => s.Status == AcademicSubPeriodStatus.Ouverte && s.Id != sub.Id);
        if (openOther is not null)
        {
            throw new DomainException(
                $"La {openOther.Name} est actuellement ouverte.\n" +
                "Veuillez d'abord la clôturer avant d'ouvrir une nouvelle période.");
        }

        var index = IndexOfSub(ordered, sub.Id);
        if (index < 0)
        {
            throw new DomainException("Sous-période introuvable dans la chronologie du cycle.");
        }

        if (index > 0)
        {
            var previous = ordered[index - 1].Sub;
            if (previous.Status is AcademicSubPeriodStatus.AVenir or AcademicSubPeriodStatus.Ouverte)
            {
                throw new DomainException(
                    $"Impossible d'ouvrir « {sub.Name} » : la période précédente « {previous.Name} » " +
                    $"doit d'abord être clôturée ou verrouillée (état actuel : {GetStatusLabel(previous.Status)}).");
            }
        }
    }

    private static void EnsureValidDateRange(DateOnly start, DateOnly end, string name)
    {
        if (end < start)
        {
            throw new DomainException(
                $"Dates invalides pour « {name} » : la date de fin doit être postérieure ou égale à la date de début.");
        }
    }

    private static void EnsureChronologicalDates(
        IReadOnlyList<(AcademicPeriod Sub, AcademicMainPeriod Main, int SequenceIndex)> ordered,
        AcademicPeriod edited)
    {
        var index = IndexOfSub(ordered, edited.Id);
        if (index < 0 || edited.StartDate is null || edited.EndDate is null)
        {
            return;
        }

        if (index > 0)
        {
            var previous = ordered[index - 1].Sub;
            if (previous.EndDate is DateOnly prevEnd && edited.StartDate.Value <= prevEnd)
            {
                throw new DomainException(
                    $"La date de début de « {edited.Name} » ({edited.StartDate:dd/MM/yyyy}) " +
                    $"doit être strictement postérieure à la fin de « {previous.Name} » ({prevEnd:dd/MM/yyyy}).");
            }
        }

        if (index < ordered.Count - 1)
        {
            var next = ordered[index + 1].Sub;
            if (next.Id != edited.Id
                && next.StartDate is DateOnly nextStart
                && nextStart <= edited.EndDate.Value
                && next.Status == AcademicSubPeriodStatus.AVenir)
            {
                throw new DomainException(
                    $"Chevauchement détecté avec « {next.Name} » " +
                    $"({next.StartDate:dd/MM/yyyy} → {next.EndDate:dd/MM/yyyy}).");
            }
        }
    }

    private static int IndexOfSub(
        IReadOnlyList<(AcademicPeriod Sub, AcademicMainPeriod Main, int SequenceIndex)> ordered,
        Guid subId)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Sub.Id == subId)
            {
                return i;
            }
        }

        return -1;
    }

    private static void EnsureNoOverlap(IReadOnlyList<AcademicPeriod> all, AcademicPeriod candidate)
    {
        if (candidate.StartDate is null || candidate.EndDate is null)
        {
            return;
        }

        foreach (var other in all.Where(s => s.Id != candidate.Id))
        {
            if (other.StartDate is null || other.EndDate is null)
            {
                continue;
            }

            var overlaps = candidate.StartDate <= other.EndDate && other.StartDate <= candidate.EndDate;
            if (overlaps)
            {
                throw new DomainException(
                    $"Chevauchement interdit entre « {candidate.Name} » " +
                    $"({candidate.StartDate:dd/MM/yyyy} → {candidate.EndDate:dd/MM/yyyy}) et « {other.Name} » " +
                    $"({other.StartDate:dd/MM/yyyy} → {other.EndDate:dd/MM/yyyy}).");
            }
        }
    }

    private async Task<List<(AcademicPeriod Sub, AcademicMainPeriod Main, int SequenceIndex)>> GetOrderedCycleSubsAsync(
        Guid schoolId,
        Guid academicYearId,
        PedagogicalCycleGroup cycleGroup,
        CancellationToken cancellationToken)
    {
        var mains = (await _mainPeriodRepository.FindAsync(
            m => m.SchoolId == schoolId
                 && m.AcademicYearId == academicYearId
                 && m.CycleGroup == cycleGroup,
            cancellationToken))
            .OrderBy(m => m.OrderIndex)
            .ToList();

        var mainIds = mains.Select(m => m.Id).ToHashSet();
        var subs = (await _subPeriodRepository.FindAsync(
            s => s.AcademicYearId == academicYearId && s.MainPeriodId != null,
            cancellationToken))
            .Where(s => s.MainPeriodId.HasValue && mainIds.Contains(s.MainPeriodId.Value))
            .ToList();

        var result = new List<(AcademicPeriod, AcademicMainPeriod, int)>();
        var sequence = 1;
        foreach (var main in mains)
        {
            foreach (var sub in subs.Where(s => s.MainPeriodId == main.Id).OrderBy(s => s.OrderIndex))
            {
                result.Add((sub, main, sequence++));
            }
        }

        return result;
    }

    private void EnsureCanManagePedagogicalPeriods()
    {
        if (!_currentUser.HasPermission(Permissions.PedagogicalPeriodsManage))
        {
            throw new DomainException("Réservé à l'administration pédagogique.");
        }
    }

    private async Task<AcademicYear> GetYearOrThrowAsync(
        Guid schoolId,
        Guid academicYearId,
        CancellationToken cancellationToken)
    {
        return (await _yearRepository.FindAsync(
            y => y.Id == academicYearId && y.SchoolId == schoolId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Année scolaire introuvable.");
    }

    private async Task<AcademicPeriod> GetSubPeriodOrThrowAsync(
        Guid schoolId,
        Guid subPeriodId,
        CancellationToken cancellationToken)
    {
        var sub = (await _subPeriodRepository.FindAsync(
            s => s.Id == subPeriodId,
            cancellationToken)).FirstOrDefault()
            ?? throw new DomainException("Sous-période introuvable.");

        await GetYearOrThrowAsync(schoolId, sub.AcademicYearId, cancellationToken);
        if (sub.MainPeriodId is null)
        {
            throw new DomainException(
                "Cette période n'appartient pas au moteur pédagogique. Créez d'abord la structure.");
        }

        return sub;
    }

    private async Task<AcademicMainPeriod> GetMainOrThrowAsync(Guid mainPeriodId, CancellationToken cancellationToken)
    {
        return (await _mainPeriodRepository.FindAsync(m => m.Id == mainPeriodId, cancellationToken))
            .FirstOrDefault()
            ?? throw new DomainException("Période pédagogique introuvable.");
    }

    private async Task<PedagogicalPeriodStructureDto> BuildStructureDtoAsync(
        AcademicYear year,
        CancellationToken cancellationToken)
    {
        var cycles = new List<PedagogicalCycleStructureDto>();
        foreach (var cycle in Enum.GetValues<PedagogicalCycleGroup>())
        {
            var ordered = await GetOrderedCycleSubsAsync(year.SchoolId, year.Id, cycle, cancellationToken);
            if (ordered.Count == 0)
            {
                continue;
            }

            var mains = ordered
                .GroupBy(x => x.Main.Id)
                .OrderBy(g => g.First().Main.OrderIndex)
                .Select(g =>
                {
                    var main = g.First().Main;
                    return new PedagogicalMainPeriodDto(
                        main.Id,
                        main.Name,
                        main.PeriodType,
                        main.OrderIndex,
                        g.OrderBy(x => x.SequenceIndex)
                            .Select(x => MapSub(x.Sub, x.Main, x.SequenceIndex))
                            .ToList());
                })
                .ToList();

            cycles.Add(new PedagogicalCycleStructureDto(cycle, GetCycleLabel(cycle), mains));
        }

        return new PedagogicalPeriodStructureDto(year.Id, year.Label, cycles);
    }

    private async Task<PedagogicalSubPeriodDto> MapSubAsync(AcademicPeriod sub, CancellationToken cancellationToken)
    {
        var main = await GetMainOrThrowAsync(sub.MainPeriodId!.Value, cancellationToken);
        var ordered = await GetOrderedCycleSubsAsync(main.SchoolId, main.AcademicYearId, main.CycleGroup, cancellationToken);
        var seq = ordered.FirstOrDefault(x => x.Sub.Id == sub.Id).SequenceIndex;
        return MapSub(sub, main, seq == 0 ? sub.OrderIndex : seq);
    }

    private static PedagogicalSubPeriodDto MapSub(AcademicPeriod sub, AcademicMainPeriod main, int sequenceIndex) =>
        new(
            sub.Id,
            main.Id,
            sub.Name,
            main.Name,
            main.CycleGroup,
            main.PeriodType,
            sub.Kind,
            GetKindLabel(sub.Kind),
            sub.Status,
            GetStatusLabel(sub.Status),
            sub.OrderIndex,
            sequenceIndex,
            sub.MaxScore,
            sub.MaxEvaluationCount,
            sub.StartDate,
            sub.EndDate,
            sub.OpenedAt,
            sub.PlannedCloseDate ?? sub.EndDate,
            sub.ClosedAt,
            sub.Status == AcademicSubPeriodStatus.Ouverte);

    private static List<(AcademicMainPeriod Main, List<AcademicPeriod> Subs)> BuildDefaultTemplates(AcademicYear year)
    {
        var result = new List<(AcademicMainPeriod, List<AcademicPeriod>)>();

        result.AddRange(BuildCycleTemplates(
            year,
            PedagogicalCycleGroup.MaternellePrimaire,
            AcademicPeriodType.Trimestre,
            [
                ("1er Trimestre", [
                    ("1ère Période", AcademicSubPeriodKind.Travail, 20, null),
                    ("2ème Période", AcademicSubPeriodKind.Travail, 20, null),
                    ("Examen", AcademicSubPeriodKind.Examen, 40, 1)
                ]),
                ("2ème Trimestre", [
                    ("3ème Période", AcademicSubPeriodKind.Travail, 20, null),
                    ("4ème Période", AcademicSubPeriodKind.Travail, 20, null),
                    ("Examen", AcademicSubPeriodKind.Examen, 40, 1)
                ]),
                ("3ème Trimestre", [
                    ("5ème Période", AcademicSubPeriodKind.Travail, 20, null),
                    ("6ème Période", AcademicSubPeriodKind.Travail, 20, null),
                    ("Examen", AcademicSubPeriodKind.Examen, 40, 1)
                ])
            ]));

        result.AddRange(BuildCycleTemplates(
            year,
            PedagogicalCycleGroup.Secondaire,
            AcademicPeriodType.Semestre,
            [
                ("1er Semestre", [
                    ("1ère Période", AcademicSubPeriodKind.Travail, 20, null),
                    ("2ème Période", AcademicSubPeriodKind.Travail, 20, null),
                    ("Examen", AcademicSubPeriodKind.Examen, 40, 1)
                ]),
                ("2ème Semestre", [
                    ("3ème Période", AcademicSubPeriodKind.Travail, 20, null),
                    ("4ème Période", AcademicSubPeriodKind.Travail, 20, null),
                    ("Examen", AcademicSubPeriodKind.Examen, 40, 1)
                ])
            ]));

        return result;
    }

    private static List<(AcademicMainPeriod Main, List<AcademicPeriod> Subs)> BuildCycleTemplates(
        AcademicYear year,
        PedagogicalCycleGroup cycleGroup,
        AcademicPeriodType periodType,
        (string MainName, (string Name, AcademicSubPeriodKind Kind, int Max, int? MaxEval)[] Subs)[] mains)
    {
        var result = new List<(AcademicMainPeriod, List<AcademicPeriod>)>();
        var mainOrder = 1;

        foreach (var (mainName, subDefs) in mains)
        {
            var main = new AcademicMainPeriod
            {
                SchoolId = year.SchoolId,
                AcademicYearId = year.Id,
                CycleGroup = cycleGroup,
                Name = mainName,
                PeriodType = periodType,
                OrderIndex = mainOrder++
            };

            var subs = new List<AcademicPeriod>();
            var subOrder = 1;
            foreach (var (subName, kind, max, maxEval) in subDefs)
            {
                // Étape 1 : structure seule — dates null jusqu'à saisie admin (étape 2).
                subs.Add(new AcademicPeriod
                {
                    SchoolId = year.SchoolId,
                    AcademicYearId = year.Id,
                    MainPeriodId = main.Id,
                    Name = subName,
                    PeriodType = periodType,
                    OrderIndex = subOrder++,
                    StartDate = null,
                    EndDate = null,
                    PlannedCloseDate = null,
                    IsClosed = false,
                    Kind = kind,
                    Status = AcademicSubPeriodStatus.AVenir,
                    MaxScore = max,
                    MaxEvaluationCount = maxEval
                });
            }

            result.Add((main, subs));
        }

        return result;
    }

    private static string GetCycleLabel(PedagogicalCycleGroup group) => group switch
    {
        PedagogicalCycleGroup.MaternellePrimaire => "Maternelle / Primaire",
        PedagogicalCycleGroup.Secondaire => "Secondaire",
        _ => group.ToString()
    };

    private static string GetKindLabel(AcademicSubPeriodKind kind) => kind switch
    {
        AcademicSubPeriodKind.Examen => "Examen",
        _ => "Travail"
    };

    private static string GetStatusLabel(AcademicSubPeriodStatus status) => status switch
    {
        AcademicSubPeriodStatus.Ouverte => "Ouverte",
        AcademicSubPeriodStatus.Cloturee => "Clôturée",
        AcademicSubPeriodStatus.Verrouillee => "Verrouillée",
        _ => "À venir"
    };
}
