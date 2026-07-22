using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Geography;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Infrastructure.CloudSync;

/// <summary>
/// Catalogue des entités synchronisables : ordre FK, priorité, agrégats.
/// </summary>
internal static class CloudSyncCatalog
{
    public static readonly HashSet<string> CriticalTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Payments",
        "PaymentLines",
        "PaymentReversals",
        "CashMovements",
        "FinRepartitionRecette",
        "FinRetenueApplication",
        "StudentFeeBalances"
    };

    public static readonly HashSet<string> SyncMetaTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SyncOutboxUnit",
        "SyncOutboxItem",
        "SyncJournal",
        "SyncWatermark"
    };

    /// <summary>Ordre d'application (parents avant enfants).</summary>
    public static readonly IReadOnlyList<(string Table, Type ClrType)> SyncOrder =
    [
        ("Schools", typeof(School)),
        ("Permissions", typeof(Permission)),
        ("Roles", typeof(Role)),
        ("RolePermissions", typeof(RolePermission)),
        ("UserAccounts", typeof(UserAccount)),
        ("UserRoleAssignments", typeof(UserRoleAssignment)),
        ("AcademicYears", typeof(AcademicYear)),
        ("Sections", typeof(Section)),
        ("StudyOptions", typeof(StudyOption)),
        ("PedagogicalClasses", typeof(PedagogicalClass)),
        ("ClassRooms", typeof(ClassRoom)),
        ("Courses", typeof(Course)),
        ("AcademicPeriods", typeof(AcademicPeriod)),
        ("FeeTypes", typeof(FeeType)),
        ("FeeInstallments", typeof(FeeInstallment)),
        ("FeePricingCategories", typeof(FeePricingCategory)),
        ("FeeTypeInstallments", typeof(FeeTypeInstallment)),
        ("ClassFeeAmounts", typeof(ClassFeeAmount)),
        ("Banks", typeof(Bank)),
        ("CashRegisters", typeof(CashRegister)),
        ("AppConfigurations", typeof(AppConfiguration)),
        ("Pays", typeof(Country)),
        ("Province", typeof(Province)),
        ("Ville", typeof(City)),
        ("Commune", typeof(Commune)),
        ("Adresse", typeof(PostalAddress)),
        ("Students", typeof(Student)),
        ("Guardians", typeof(Guardian)),
        ("StudentGuardians", typeof(StudentGuardian)),
        ("StudentDocuments", typeof(StudentDocument)),
        ("Enrollments", typeof(Enrollment)),
        ("EnrollmentPricingCategoryHistory", typeof(EnrollmentPricingCategoryHistory)),
        ("StudentStatusHistory", typeof(StudentStatusHistory)),
        ("StudentFeeBalances", typeof(StudentFeeBalance)),
        ("Teachers", typeof(Teacher)),
        ("CourseAssignments", typeof(CourseAssignment)),
        ("ScheduleSlots", typeof(ScheduleSlot)),
        ("StudentAttendances", typeof(StudentAttendance)),
        ("TeacherAttendances", typeof(TeacherAttendance)),
        ("CalendarEvents", typeof(CalendarEvent)),
        ("DisciplineRecords", typeof(DisciplineRecord)),
        ("MeritRecords", typeof(MeritRecord)),
        ("Announcements", typeof(Announcement)),
        ("Evaluations", typeof(Evaluation)),
        ("GradeEntries", typeof(GradeEntry)),
        ("PeriodResults", typeof(PeriodResult)),
        ("ReportCards", typeof(ReportCard)),
        ("ReportCardDetails", typeof(ReportCardDetail)),
        ("FinRetenue", typeof(WithholdingType)),
        ("FinRetenueConfiguration", typeof(WithholdingConfiguration)),
        ("FinDestinationRepartition", typeof(RevenueAllocationDestination)),
        ("FinCleRepartition", typeof(RevenueAllocationKey)),
        ("FinCleRepartitionDetail", typeof(RevenueAllocationKeyDetail)),
        ("Payments", typeof(Payment)),
        ("PaymentLines", typeof(PaymentLine)),
        ("PaymentReversals", typeof(PaymentReversal)),
        ("CashMovements", typeof(CashMovement)),
        ("FinRepartitionRecette", typeof(RevenueAllocationEntry)),
        ("FinRetenueApplication", typeof(WithholdingApplication)),
        ("FinDemandePaiement", typeof(ExpenseRequest)),
        ("FinDepense", typeof(ExpensePayment)),
        ("EcoleLogo", typeof(SchoolLogo)),
        ("EcoleEntete", typeof(SchoolDocumentHeader)),
        ("EcoleSignature", typeof(SchoolSignature)),
        ("EcoleCachet", typeof(SchoolStamp)),
        ("EcolePiedPage", typeof(SchoolDocumentFooter)),
        ("AuditEntries", typeof(AuditEntry))
    ];

    private static readonly Dictionary<string, Type> ByTable =
        SyncOrder.ToDictionary(x => x.Table, x => x.ClrType, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<Type, string> ByClr =
        SyncOrder.ToDictionary(x => x.ClrType, x => x.Table);

    private static readonly Dictionary<string, int> SequenceByTable =
        SyncOrder.Select((x, i) => (x.Table, i)).ToDictionary(x => x.Table, x => x.i, StringComparer.OrdinalIgnoreCase);

    public static bool TryGetClrType(string tableName, out Type type) =>
        ByTable.TryGetValue(tableName, out type!);

    public static bool TryGetTableName(Type clrType, out string tableName) =>
        ByClr.TryGetValue(clrType, out tableName!);

    public static int GetSequence(string tableName) =>
        SequenceByTable.TryGetValue(tableName, out var seq) ? seq : 1000;

    public static bool IsCriticalTable(string tableName) =>
        CriticalTables.Contains(tableName);

    public static SyncPriority ResolvePriority(string tableName) =>
        IsCriticalTable(tableName) ? SyncPriority.Critical
        : tableName is "Schools" or "Permissions" or "Roles" or "AppConfigurations"
            or "FeeTypes" or "FeeInstallments" or "FeePricingCategories"
            ? SyncPriority.Low
            : SyncPriority.Normal;

    public static (string AggregateType, Guid? AggregateId) ResolveAggregate(
        string tableName,
        Guid entityId,
        object? entity)
    {
        if (entity is Payment payment)
        {
            return ("Payment", payment.Id);
        }

        if (entity is PaymentLine line)
        {
            return ("Payment", line.PaymentId);
        }

        if (entity is PaymentReversal reversal)
        {
            return ("Payment", reversal.PaymentId);
        }

        if (entity is RevenueAllocationEntry allocation)
        {
            return ("Payment", allocation.PaymentId);
        }

        if (entity is CashMovement cash && cash.PaymentId.HasValue)
        {
            return ("Payment", cash.PaymentId);
        }

        if (entity is WithholdingApplication withholding)
        {
            return ("Payment", withholding.PaymentId);
        }

        if (IsCriticalTable(tableName))
        {
            return ("FinanceBatch", entityId);
        }

        return ("Entity", entityId);
    }
}
