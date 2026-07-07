using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolManagement.Desktop.ViewModels;

public enum WizardStepVisualState
{
    Pending,
    Active,
    Completed
}

public partial class EnrollmentWizardStepItem : ObservableObject
{
    public EnrollmentWizardStepItem(int number, string title, bool isLast = false)
    {
        Number = number;
        Title = title;
        IsLast = isLast;
    }

    public int Number { get; }

    public string Title { get; }

    public bool IsLast { get; }

    [ObservableProperty] private WizardStepVisualState _state = WizardStepVisualState.Pending;
}

public sealed record PedagogicalClassPickerItem(Guid? Id, string DisplayName);
