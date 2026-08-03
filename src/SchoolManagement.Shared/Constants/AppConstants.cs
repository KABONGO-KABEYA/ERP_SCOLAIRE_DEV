namespace SchoolManagement.Shared.Constants;

public static class ApiRoutes
{
    public const string Base = "api/v1";
    public const string Auth = $"{Base}/auth";
    public const string Schools = $"{Base}/schools";
    public const string Students = $"{Base}/students";
    public const string Grades = $"{Base}/grades";
    public const string Bulletins = $"{Base}/bulletins";
    public const string ResultValidation = $"{Base}/result-validation";
    public const string Deliberation = $"{Base}/deliberation";
    public const string Mentions = $"{Base}/mentions";
    public const string Payments = $"{Base}/payments";
    public const string RevenueAllocation = $"{Base}/revenue-allocation";
    public const string Withholdings = $"{Base}/withholdings";
    public const string Currencies = $"{Base}/currencies";
    public const string ExchangeRates = $"{Base}/exchange-rates";
    public const string ExchangeRateTypes = $"{Base}/exchange-rate-types";
    public const string SchoolCurrencies = $"{Base}/school-currencies";
    public const string StudentCards = $"{Base}/cards";
    public const string CardTemplates = $"{Base}/card-templates";
    public const string Accounting = $"{Base}/accounting";
    public const string Finance = $"{Base}/finance";
    public const string Reports = $"{Base}/reports";
    public const string Dashboard = $"{Base}/dashboard";
    public const string Academic = $"{Base}/academic";
    public const string Teacher = $"{Base}/teacher";
    public const string Parent = $"{Base}/parent";
    public const string Documents = $"{Base}/documents";
    public const string DocumentBranding = $"{Base}/document-branding";
    public const string Admin = $"{Base}/admin";
    public const string Personnel = $"{Base}/personnel";
    public const string CloudSync = $"{Base}/cloud-sync";
    public const string Update = $"{Base}/update";
    public const string Setup = $"{Base}/setup";
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
