using SchoolManagement.Application.Deliberation.DTOs;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using AcademicPeriod = SchoolManagement.Domain.Entities.Settings.AcademicPeriod;

namespace SchoolManagement.Application.Deliberation.Services;

/// <summary>
/// Détermine automatiquement le mode du conseil selon la période pédagogique.
/// </summary>
internal static class DeliberationPeriodModeResolver
{
    public static DeliberationPeriodContextDto Resolve(
        AcademicPeriod period,
        ClassRoom classRoom,
        PedagogicalClass? pedagogicalClass,
        IReadOnlyList<AcademicMainPeriod> mainPeriods,
        IReadOnlyList<AcademicPeriod> yearPeriods,
        ResultValidationStatus validationStatus)
    {
        var cycle = ResolveCycleGroup(classRoom, pedagogicalClass);
        var mode = ResolveMode(period, cycle, mainPeriods, yearPeriods);
        var isLocked = validationStatus == ResultValidationStatus.Verrouille;
        var isValidated = validationStatus == ResultValidationStatus.Valide;
        var isYearEnd = mode is DeliberationPeriodMode.YearEndPrimary or DeliberationPeriodMode.YearEndSecondary;
        var canRepechage = mode == DeliberationPeriodMode.YearEndSecondary;
        var canEdit = !isLocked && !isValidated;
        var periodClosed = IsPeriodClosed(period);
        var canCancel = isValidated && !isLocked && !periodClosed;

        return new DeliberationPeriodContextDto(
            mode,
            ModeLabel(mode),
            isYearEnd,
            CanSetFinalDecision: isYearEnd && canEdit,
            CanOfferRepechage: canRepechage && canEdit,
            CanAddBonusPoints: canEdit && !period.IsRemedial,
            CanSetConduct: canEdit && !period.IsRemedial,
            CanValidateClass: canEdit,
            CanCancelValidation: canCancel,
            IsReadOnly: isLocked || isValidated,
            AvailableDecisions: BuildDecisions(mode));
    }

    public static bool IsPeriodClosed(AcademicPeriod period) =>
        period.IsClosed
        || period.Status is AcademicSubPeriodStatus.Cloturee or AcademicSubPeriodStatus.Verrouillee;

    public static DeliberationPeriodMode ResolveMode(
        AcademicPeriod period,
        PedagogicalCycleGroup cycle,
        IReadOnlyList<AcademicMainPeriod> mainPeriods,
        IReadOnlyList<AcademicPeriod> yearPeriods)
    {
        if (period.IsRemedial)
        {
            return DeliberationPeriodMode.Intermediate;
        }

        if (period.Kind != AcademicSubPeriodKind.Examen)
        {
            return DeliberationPeriodMode.Intermediate;
        }

        var cycleMains = mainPeriods
            .Where(m => m.CycleGroup == cycle)
            .OrderBy(m => m.OrderIndex)
            .ToList();
        if (cycleMains.Count == 0)
        {
            return DeliberationPeriodMode.Intermediate;
        }

        var lastMain = cycleMains[^1];
        if (period.MainPeriodId != lastMain.Id)
        {
            return DeliberationPeriodMode.Intermediate;
        }

        // Dernier examen du cycle = fin d'année.
        var examsOfLastMain = yearPeriods
            .Where(p => p.MainPeriodId == lastMain.Id && p.Kind == AcademicSubPeriodKind.Examen && !p.IsRemedial)
            .OrderBy(p => p.OrderIndex)
            .ToList();
        if (examsOfLastMain.Count == 0 || examsOfLastMain[^1].Id != period.Id)
        {
            // Si un seul examen dans le trimestre/semestre final, c'est celui-ci.
            if (period.MainPeriodId == lastMain.Id && period.Kind == AcademicSubPeriodKind.Examen)
            {
                return cycle == PedagogicalCycleGroup.Secondaire
                    ? DeliberationPeriodMode.YearEndSecondary
                    : DeliberationPeriodMode.YearEndPrimary;
            }

            return DeliberationPeriodMode.Intermediate;
        }

        return cycle == PedagogicalCycleGroup.Secondaire
            ? DeliberationPeriodMode.YearEndSecondary
            : DeliberationPeriodMode.YearEndPrimary;
    }

    public static PedagogicalCycleGroup ResolveCycleGroup(ClassRoom classRoom, PedagogicalClass? pedagogicalClass)
    {
        if (pedagogicalClass is not null)
        {
            return pedagogicalClass.Program is SchoolProgram.Maternelle or SchoolProgram.Primaire
                ? PedagogicalCycleGroup.MaternellePrimaire
                : PedagogicalCycleGroup.Secondaire;
        }

        return PedagogicalCycleGroup.MaternellePrimaire;
    }

    private static string ModeLabel(DeliberationPeriodMode mode) => mode switch
    {
        DeliberationPeriodMode.YearEndPrimary => "Fin d'année (primaire / maternelle)",
        DeliberationPeriodMode.YearEndSecondary => "Fin d'année (secondaire)",
        _ => "Période intermédiaire"
    };

    private static IReadOnlyList<FinalCouncilDecisionOptionDto> BuildDecisions(DeliberationPeriodMode mode)
    {
        if (mode == DeliberationPeriodMode.Intermediate)
        {
            return [];
        }

        var list = new List<FinalCouncilDecisionOptionDto>
        {
            new(FinalCouncilDecision.PasseDeClasse, "Passe de classe"),
            new(FinalCouncilDecision.Redouble, "Redouble"),
            new(FinalCouncilDecision.PasseAilleurs, "Passe ailleurs")
        };

        if (mode == DeliberationPeriodMode.YearEndSecondary)
        {
            list.Add(new(FinalCouncilDecision.Repechage, "Repêchage"));
        }

        return list;
    }
}
