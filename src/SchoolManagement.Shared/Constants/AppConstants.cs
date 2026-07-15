namespace SchoolManagement.Shared.Constants;

public static class ApiRoutes
{
    public const string Base = "api/v1";
    public const string Auth = $"{Base}/auth";
    public const string Schools = $"{Base}/schools";
    public const string Students = $"{Base}/students";
    public const string Grades = $"{Base}/grades";
    public const string Payments = $"{Base}/payments";
    public const string Reports = $"{Base}/reports";
    public const string Academic = $"{Base}/academic";
    public const string Teacher = $"{Base}/teacher";
    public const string Parent = $"{Base}/parent";
    public const string Documents = $"{Base}/documents";
    public const string DocumentBranding = $"{Base}/document-branding";
    public const string Admin = $"{Base}/admin";
}

public static class AppConstants
{
    public const string DefaultCulture = "fr-FR";
    public const string ApplicationName = "ERP Administration Scolaire RDC";
    public const string DefaultCurrency = "CDF";
}

public static class ClaimTypesCustom
{
    public const string SchoolId = "school_id";
    public const string Permissions = "permissions";
    public const string FullName = "full_name";
}
