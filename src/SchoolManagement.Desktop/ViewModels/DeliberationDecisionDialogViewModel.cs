using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Deliberation.DTOs;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public sealed class FinalDecisionOption
{
    public FinalCouncilDecision Value { get; init; }
    public string Label { get; init; } = string.Empty;
}

public partial class DeliberationCourseCheckItem : ObservableObject
{
    public Guid CourseId { get; init; }
    public Guid? CourseAssignmentId { get; init; }
    public string CourseName { get; init; } = string.Empty;

    [ObservableProperty] private bool _isSelected;
}

public partial class DeliberationDecisionDialogViewModel : ObservableObject
{
    public DeliberationDecisionDialogViewModel(DeliberationDecisionDialogDto source)
    {
        Source = source;
        FullName = source.FullName;
        RegistrationNumber = source.RegistrationNumber;
        ClassDisplayName = source.ClassDisplayName;
        PeriodLabel = source.PeriodLabel;
        AverageDisplay = source.AverageDisplay;
        PercentageDisplay = source.PercentageDisplay;
        Mention = source.Mention ?? "—";
        ProposedDecisionLabel = source.ProposedDecisionLabel;
        Observation = source.Observation;
        ExemptionMotive = source.ExemptionMotive;
        ExemptionObservation = source.ExemptionObservation;
        DecidedByDisplay = string.IsNullOrWhiteSpace(source.DecidedByUserName) ? "—" : source.DecidedByUserName!;
        DecidedAtDisplay = source.DecidedAtDisplay;

        FinalDecisionOptions = source.AvailableDecisions.Count > 0
            ? source.AvailableDecisions
                .Select(d => new FinalDecisionOption { Value = d.Value, Label = d.Label })
                .ToList()
            : [];

        SelectedFinalDecision = source.FinalDecision is FinalCouncilDecision existing
            ? FinalDecisionOptions.FirstOrDefault(o => o.Value == existing)
              ?? FinalDecisionOptions.FirstOrDefault()
            : FinalDecisionOptions.FirstOrDefault();

        foreach (var course in source.Courses)
        {
            Courses.Add(new DeliberationCourseCheckItem
            {
                CourseId = course.CourseId,
                CourseAssignmentId = course.CourseAssignmentId,
                CourseName = course.CourseName,
                IsSelected = course.IsSelected
            });
        }
    }

    public DeliberationDecisionDialogDto Source { get; }
    public SaveDeliberationDecisionRequest? PendingSave { get; private set; }

    public string FullName { get; }
    public string RegistrationNumber { get; }
    public string ClassDisplayName { get; }
    public string PeriodLabel { get; }
    public string SubtitleDisplay => $"{RegistrationNumber} · {ClassDisplayName} · {PeriodLabel}";
    public string AverageDisplay { get; }
    public string PercentageDisplay { get; }
    public string Mention { get; }
    public string ProposedDecisionLabel { get; }
    public string DecidedByDisplay { get; }
    public string DecidedAtDisplay { get; }
    public string AuditDisplay => $"Dernière saisie : {DecidedByDisplay} — {DecidedAtDisplay}";
    public IReadOnlyList<FinalDecisionOption> FinalDecisionOptions { get; }
    public ObservableCollection<DeliberationCourseCheckItem> Courses { get; } = [];

    [ObservableProperty] private FinalDecisionOption? _selectedFinalDecision;
    [ObservableProperty] private string? _observation;
    [ObservableProperty] private string? _exemptionMotive;
    [ObservableProperty] private string? _exemptionObservation;
    [ObservableProperty] private string? _validationMessage;
    [ObservableProperty] private bool _showRemedialCourses;
    [ObservableProperty] private bool _showExemptionFields;

    partial void OnSelectedFinalDecisionChanged(FinalDecisionOption? value)
    {
        ShowRemedialCourses = value?.Value == FinalCouncilDecision.Repechage;
        ShowExemptionFields = value?.Value == FinalCouncilDecision.Dispense;
    }

    public bool TryBuildSaveRequest()
    {
        ValidationMessage = null;
        if (SelectedFinalDecision is null)
        {
            ValidationMessage = "Choisissez une décision finale.";
            return false;
        }

        var selectedCourseIds = Courses.Where(c => c.IsSelected).Select(c => c.CourseId).ToList();

        if (SelectedFinalDecision.Value == FinalCouncilDecision.Repechage && selectedCourseIds.Count == 0)
        {
            ValidationMessage = "Sélectionnez au moins un cours à repêcher.";
            return false;
        }

        if (SelectedFinalDecision.Value == FinalCouncilDecision.Dispense)
        {
            if (selectedCourseIds.Count == 0)
            {
                ValidationMessage = "Sélectionnez au moins un cours concerné par la dispense.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ExemptionMotive))
            {
                ValidationMessage = "Indiquez le motif de la dispense.";
                return false;
            }
        }

        PendingSave = new SaveDeliberationDecisionRequest(
            Source.AcademicYearId,
            Source.ClassRoomId,
            Source.AcademicPeriodId,
            Source.StudentId,
            SelectedFinalDecision.Value,
            Observation,
            SelectedFinalDecision.Value == FinalCouncilDecision.Repechage ? selectedCourseIds : null,
            SelectedFinalDecision.Value == FinalCouncilDecision.Dispense ? selectedCourseIds : null,
            SelectedFinalDecision.Value == FinalCouncilDecision.Dispense ? ExemptionMotive : null,
            SelectedFinalDecision.Value == FinalCouncilDecision.Dispense ? ExemptionObservation : null);

        return true;
    }
}
