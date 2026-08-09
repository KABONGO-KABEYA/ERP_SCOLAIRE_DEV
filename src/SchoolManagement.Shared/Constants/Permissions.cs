namespace SchoolManagement.Shared.Constants;

public static class Permissions
{
    public const string StudentsRead = "students.read";
    public const string StudentsCreate = "students.create";
    public const string StudentsUpdate = "students.update";
    public const string StudentsDelete = "students.delete";

    public const string SchoolsRead = "schools.read";
    public const string SchoolsUpdate = "schools.update";

    public const string PaymentsRead = "payments.read";
    public const string PaymentsCreate = "payments.create";
    public const string PaymentsValidate = "payments.validate";
    public const string PaymentsCancel = "payments.cancel";
    public const string PaymentsNotesUpdate = "payments.notes.update";
    public const string PaymentsPaidMutation = "payments.paid-mutation";
    public const string PricingCategoriesAssign = "pricing-categories.assign";

    public const string RevenueAllocationRead = "revenue-allocation.read";
    public const string RevenueAllocationManage = "revenue-allocation.update";

    public const string WithholdingsRead = "withholdings.read";
    public const string WithholdingsManage = "withholdings.update";

    public const string CurrenciesRead = "currencies.read";
    public const string CurrenciesCreate = "currencies.create";
    public const string CurrenciesUpdate = "currencies.update";
    public const string CurrenciesDelete = "currencies.delete";

    public const string ExchangeRatesRead = "exchange-rates.read";
    public const string ExchangeRatesCreate = "exchange-rates.create";
    public const string ExchangeRatesUpdate = "exchange-rates.update";
    public const string ExchangeRatesDelete = "exchange-rates.delete";
    public const string ExchangeRatesActivate = "exchange-rates.approve";

    public const string ExchangeRateHistoryRead = "exchange-rate-history.read";

    /// <summary>Autorise la modification du taux pendant un encaissement.</summary>
    public const string PaymentFxOverride = "payment-fx.update";

    public const string StudentCardsRead = "student-cards.read";
    public const string StudentCardsCreate = "student-cards.create";
    public const string StudentCardsUpdate = "student-cards.update";
    public const string StudentCardsDelete = "student-cards.delete";
    public const string StudentCardsPrint = "student-cards.print";
    public const string StudentCardsRenew = "student-cards.renew";
    public const string StudentCardsDeclareLost = "student-cards.declare-lost";
    public const string CardTemplatesRead = "card-templates.read";
    public const string CardTemplatesManage = "card-templates.update";

    public const string GradesRead = "grades.read";
    public const string GradesCreate = "grades.create";
    public const string GradesUpdate = "grades.update";
    public const string GradesDelete = "grades.delete";
    public const string GradesEvaluationDeleteWithGrades = "grades.evaluation.delete-with-grades";
    public const string GradesRecalculate = "grades.recalculate";
    public const string GradesPublish = "grades.publish";
    public const string GradesUnpublish = "grades.unpublish";
    public const string GradesCotationDelegate = "grades.cotation.delegate";
    public const string GradesCotationScopeClass = "grades.cotation.scope.class";

    public const string ResultsValidationRead = "results-validation.read";
    public const string ResultsValidationValidate = "results-validation.validate";
    public const string ResultsValidationLock = "results-validation.lock";
    public const string ResultsValidationUnlock = "results-validation.unlock";

    public const string DeliberationPvRead = "deliberation.pv.read";
    public const string DeliberationPvWrite = "deliberation.pv.update";
    public const string DeliberationDecisionRead = "deliberation.decision.read";
    public const string DeliberationDecisionWrite = "deliberation.decision.update";

    public const string ReportsRead = "reports.read";

    public const string AccountingRead = "accounting.read";
    public const string AccountingManage = "accounting.update";

    public const string PersonnelRead = "personnel.read";
    public const string PersonnelManage = "personnel.manage";
    public const string TeachersManage = "teachers.manage";

    public const string GeographyManage = "geography.manage";
    public const string CloudSyncManage = "cloud-sync.manage";
    public const string UpdatesManage = "updates.manage";
    public const string ParentActivationManage = "parent-activation.manage";
    public const string PedagogicalPeriodsManage = "pedagogical-periods.manage";

    public const string AdminFull = "admin.full";

    public const string SecurityUsersManage = "security.users.manage";
    public const string SecurityRolesManage = "security.roles.manage";
    public const string SecurityExceptionsManage = "security.exceptions.manage";
    public const string SecurityAuditRead = "security.audit.read";
    public const string PlatformCatalogManage = "platform.catalog.manage";
    public const string PlatformSuperAdmin = "platform.superadmin";

    public static IReadOnlyList<string> All { get; } =
    [
        StudentsRead, StudentsCreate, StudentsUpdate, StudentsDelete,
        SchoolsRead, SchoolsUpdate,
        PaymentsRead, PaymentsCreate, PaymentsValidate,
        PaymentsCancel, PaymentsNotesUpdate, PaymentsPaidMutation,
        PricingCategoriesAssign,
        RevenueAllocationRead, RevenueAllocationManage,
        WithholdingsRead, WithholdingsManage,
        CurrenciesRead, CurrenciesCreate, CurrenciesUpdate, CurrenciesDelete,
        ExchangeRatesRead, ExchangeRatesCreate, ExchangeRatesUpdate, ExchangeRatesDelete, ExchangeRatesActivate,
        ExchangeRateHistoryRead, PaymentFxOverride,
        StudentCardsRead, StudentCardsCreate, StudentCardsUpdate, StudentCardsDelete,
        StudentCardsPrint, StudentCardsRenew, StudentCardsDeclareLost,
        CardTemplatesRead, CardTemplatesManage,
        GradesRead, GradesCreate, GradesUpdate, GradesDelete, GradesEvaluationDeleteWithGrades,
        GradesRecalculate, GradesPublish, GradesUnpublish,
        GradesCotationDelegate, GradesCotationScopeClass,
        ResultsValidationRead, ResultsValidationValidate, ResultsValidationLock, ResultsValidationUnlock,
        DeliberationPvRead, DeliberationPvWrite,
        DeliberationDecisionRead, DeliberationDecisionWrite,
        ReportsRead, AccountingRead, AccountingManage,
        PersonnelRead, PersonnelManage, TeachersManage,
        GeographyManage, CloudSyncManage, UpdatesManage, ParentActivationManage,
        PedagogicalPeriodsManage,
        AdminFull,
        SecurityUsersManage, SecurityRolesManage, SecurityExceptionsManage, SecurityAuditRead,
        PlatformCatalogManage, PlatformSuperAdmin
    ];
}
