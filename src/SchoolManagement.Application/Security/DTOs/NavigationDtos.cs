namespace SchoolManagement.Application.Security.DTOs;

public enum NavigationChannel
{
    Desktop = 1,
    Web = 2,
    Mobile = 3
}

public sealed record NavigationTreeDto(
    NavigationChannel Channel,
    IReadOnlyList<NavigationModuleDto> Modules);

public sealed record NavigationModuleDto(
    string Code,
    string Name,
    string? Icon,
    int SortOrder,
    IReadOnlyList<NavigationFunctionDto> Functions);

public sealed record NavigationFunctionDto(
    string Code,
    string Name,
    string? Icon,
    int SortOrder,
    IReadOnlyList<NavigationPageDto> Pages);

public sealed record NavigationPageDto(
    string Code,
    string Name,
    int SortOrder,
    string? RequiredPermissionCode,
    string? DesktopViewKey,
    string? WebRoute,
    string? MobileScreenKey,
    string? DeepLink,
    IReadOnlyList<NavigationActionDto> Actions);

public sealed record NavigationActionDto(
    string Code,
    string Name,
    int SortOrder);
