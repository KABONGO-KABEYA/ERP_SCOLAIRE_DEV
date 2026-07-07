using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.UI;

public static class SettingsNavigationBridge
{
    public static event Action<SettingsNavItem>? SectionSelected;

    public static void Select(SettingsNavItem item) => SectionSelected?.Invoke(item);
}
