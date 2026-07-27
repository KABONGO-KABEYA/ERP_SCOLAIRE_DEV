using System.Collections.ObjectModel;
using SchoolManagement.Application.Schools.DTOs;

namespace SchoolManagement.Desktop.UI;

/// <summary>
/// Année scolaire de travail globale (barre du haut).
/// Les modules s'abonnent à <see cref="CurrentYearChanged"/> au lieu d'afficher leur propre sélecteur.
/// </summary>
public static class AcademicYearRefreshBridge
{
    public static event Action? CurrentYearChanged;

    public static ObservableCollection<AcademicYearDto> Years { get; } = [];

    public static AcademicYearDto? SelectedYear { get; private set; }

    public static Guid? SelectedYearId => SelectedYear?.Id;

    public static void NotifyCurrentYearChanged() => CurrentYearChanged?.Invoke();

    public static void ReplaceYears(IEnumerable<AcademicYearDto> years, Guid? preferSelectedId = null)
    {
        var list = years.ToList();
        Years.Clear();
        foreach (var year in list)
            Years.Add(year);

        var selected =
            (preferSelectedId.HasValue
                ? list.FirstOrDefault(y => y.Id == preferSelectedId.Value)
                : null)
            ?? list.FirstOrDefault(y => y.IsCurrent)
            ?? list.FirstOrDefault();

        SelectedYear = selected;
        CurrentYearChanged?.Invoke();
    }

    public static void SetSelectedYear(AcademicYearDto? year)
    {
        if (year is null)
            return;
        if (SelectedYear?.Id == year.Id)
            return;

        SelectedYear = Years.FirstOrDefault(y => y.Id == year.Id) ?? year;
        CurrentYearChanged?.Invoke();
    }
}
