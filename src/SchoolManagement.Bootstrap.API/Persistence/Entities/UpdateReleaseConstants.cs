namespace SchoolManagement.Bootstrap.API.Persistence.Entities;

public static class UpdateReleaseStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Blocked = "Blocked";

    public static readonly string[] All = [Draft, Published, Blocked];

    public static bool IsKnown(string? value) =>
        All.Any(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string value) =>
        All.First(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));
}

public static class UpdateReleaseChannels
{
    public const string Dev = "DEV";
    public const string Prod = "PROD";

    public static readonly string[] All = [Dev, Prod];

    public static bool IsKnown(string? value) =>
        All.Any(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string value) =>
        All.First(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));
}

public static class UpdateReleaseArtifactTypes
{
    public const string Desktop = "Desktop";
    public const string Api = "Api";
    public const string Migration = "Migration";
    public const string Mobile = "Mobile";

    public static readonly string[] All = [Desktop, Api, Migration, Mobile];

    public static bool IsKnown(string? value) =>
        All.Any(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string value) =>
        All.First(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));
}
