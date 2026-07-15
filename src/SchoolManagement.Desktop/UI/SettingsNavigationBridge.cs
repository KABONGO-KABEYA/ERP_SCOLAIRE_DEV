using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.UI;

public static class SettingsNavigationBridge
{
    public static event Action<SettingsNavItem>? SectionSelected;

    public static SettingsNavItem? CurrentSelection { get; private set; }

    public static void Select(SettingsNavItem item)
    {
        CurrentSelection = item;
        SectionSelected?.Invoke(item);
    }

    public static void ApplyToViewModel(SettingsViewModel viewModel, SettingsNavItem item) =>
        viewModel.ApplyNavigation(item);
}
