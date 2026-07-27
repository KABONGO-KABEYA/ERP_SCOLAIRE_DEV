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

    public const string ReportsRead = "reports.read";

    public const string AccountingRead = "accounting.read";
    public const string AccountingManage = "accounting.update";

    public const string AdminFull = "admin.full";

    public static IReadOnlyList<string> All { get; } =
    [
        StudentsRead, StudentsCreate, StudentsUpdate, StudentsDelete,
        SchoolsRead, SchoolsUpdate,
        PaymentsRead, PaymentsCreate, PaymentsValidate,
        RevenueAllocationRead, RevenueAllocationManage,
        WithholdingsRead, WithholdingsManage,
        CurrenciesRead, CurrenciesCreate, CurrenciesUpdate, CurrenciesDelete,
        ExchangeRatesRead, ExchangeRatesCreate, ExchangeRatesUpdate, ExchangeRatesDelete, ExchangeRatesActivate,
        ExchangeRateHistoryRead, PaymentFxOverride,
        StudentCardsRead, StudentCardsCreate, StudentCardsUpdate, StudentCardsDelete,
        StudentCardsPrint, StudentCardsRenew, StudentCardsDeclareLost,
        CardTemplatesRead, CardTemplatesManage,
        GradesRead, GradesCreate, GradesUpdate,
        ReportsRead, AccountingRead, AccountingManage,
        AdminFull
    ];
}
