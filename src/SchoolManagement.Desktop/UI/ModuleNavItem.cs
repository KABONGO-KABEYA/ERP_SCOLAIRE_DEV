using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Desktop.Navigation;

namespace SchoolManagement.Desktop.UI;

/// <summary>Module du rail Desktop, construit depuis le catalogue API (pas de liste hardcodée).</summary>
public sealed record ModuleNavItem(
    string Code,
    string Title,
    string IconKind,
    Type? ViewModelType,
    bool IsHub,
    IReadOnlyList<ModuleNavPageItem> Pages);

public sealed record ModuleNavPageItem(
    string FunctionCode,
    string FunctionName,
    string PageCode,
    string Title,
    string DesktopViewKey,
    int SortOrder);

public static class DesktopNavigationMenuBuilder
{
    private static readonly HashSet<string> HubModuleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "SETTINGS", "FINANCE", "PERSONNEL", "RESULTS", "SECURITY", "DOCUMENTS"
    };

    public static IReadOnlyList<ModuleNavItem> Build(
        NavigationTreeDto tree,
        IDesktopViewRegistry registry,
        Action<string>? onUnresolvedKey = null)
    {
        var modules = new List<ModuleNavItem>();

        foreach (var module in tree.Modules.OrderBy(m => m.SortOrder).ThenBy(m => m.Name))
        {
            var resolvedPages = new List<ModuleNavPageItem>();
            foreach (var function in module.Functions.OrderBy(f => f.SortOrder))
            {
                foreach (var page in function.Pages.OrderBy(p => p.SortOrder))
                {
                    if (string.IsNullOrWhiteSpace(page.DesktopViewKey))
                    {
                        continue;
                    }

                    if (!registry.TryResolve(page.DesktopViewKey, out _))
                    {
                        onUnresolvedKey?.Invoke(page.DesktopViewKey);
                        continue;
                    }

                    resolvedPages.Add(new ModuleNavPageItem(
                        function.Code,
                        function.Name,
                        page.Code,
                        page.Name,
                        page.DesktopViewKey,
                        page.SortOrder));
                }
            }

            if (resolvedPages.Count == 0)
            {
                continue;
            }

            var isHub = HubModuleCodes.Contains(module.Code)
                        || resolvedPages.Count > 1 && registry.ResolveHubViewModelType(module.Code) is not null;

            Type? viewModelType;
            if (isHub)
            {
                viewModelType = registry.ResolveHubViewModelType(module.Code);
                // SECURITY pages mapped to Settings hub
                if (viewModelType is null && module.Code.Equals("SECURITY", StringComparison.OrdinalIgnoreCase))
                {
                    viewModelType = typeof(ViewModels.SettingsViewModel);
                    isHub = true;
                }
            }
            else
            {
                registry.TryResolve(resolvedPages[0].DesktopViewKey, out var target);
                viewModelType = target switch
                {
                    DirectDesktopViewTarget d => d.ViewModelType,
                    SettingsDesktopViewTarget => typeof(ViewModels.SettingsViewModel),
                    FinanceDesktopViewTarget => typeof(ViewModels.FinanceHubViewModel),
                    PersonnelDesktopViewTarget => typeof(ViewModels.PersonnelHubViewModel),
                    ResultsDesktopViewTarget => typeof(ViewModels.ResultsHubViewModel),
                    _ => null
                };
                isHub = false;
            }

            if (viewModelType is null)
            {
                continue;
            }

            // Si hub déclarée mais une seule page directe (ex. ValidationResultats seule), rester hub si code hub.
            if (HubModuleCodes.Contains(module.Code))
            {
                isHub = true;
                viewModelType = registry.ResolveHubViewModelType(module.Code)
                    ?? (module.Code.Equals("SECURITY", StringComparison.OrdinalIgnoreCase)
                        ? typeof(ViewModels.SettingsViewModel)
                        : viewModelType);
            }

            modules.Add(new ModuleNavItem(
                module.Code,
                module.Name,
                string.IsNullOrWhiteSpace(module.Icon) ? "CircleOutline" : module.Icon!,
                viewModelType,
                isHub,
                resolvedPages));
        }

        return MergeSecurityIntoSettings(modules);
    }

    private static IReadOnlyList<ModuleNavItem> MergeSecurityIntoSettings(List<ModuleNavItem> modules)
    {
        var settings = modules.FirstOrDefault(m => m.Code.Equals("SETTINGS", StringComparison.OrdinalIgnoreCase));
        var security = modules.FirstOrDefault(m => m.Code.Equals("SECURITY", StringComparison.OrdinalIgnoreCase));
        if (settings is null || security is null)
        {
            return modules;
        }

        var mergedPages = settings.Pages.Concat(security.Pages).ToList();
        var merged = settings with { Pages = mergedPages };
        return modules
            .Where(m => !m.Code.Equals("SECURITY", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Code.Equals("SETTINGS", StringComparison.OrdinalIgnoreCase) ? merged : m)
            .ToList();
    }
}
