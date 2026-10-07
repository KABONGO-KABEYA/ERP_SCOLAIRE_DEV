namespace SchoolManagement.Application.Schools.DTOs;

public static class SchoolSubscriptionStatuses
{
    public const string Valid = "Valid";
    public const string Expired = "Expired";
    public const string NotConfigured = "NotConfigured";
    public const string Unavailable = "Unavailable";
}

public sealed record SchoolSubscriptionDto(
    bool IsConfigured,
    Guid? Id,
    Guid? SchoolId,
    DateTime? InstallationDate,
    int? DurationMonths,
    DateTime? ExpirationDate,
    bool IsActive,
    bool IsExpired,
    bool IsValid,
    string Status);
