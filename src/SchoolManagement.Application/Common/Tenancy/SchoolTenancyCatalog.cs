namespace SchoolManagement.Application.Common.Tenancy;

/// <summary>
/// Référentiel des chaînes d'appartenance à l'établissement pour les entités sans SchoolId direct.
/// Utilisé par la documentation, les tests et <see cref="Interfaces.ISchoolTenancyService"/>.
/// </summary>
public static class SchoolTenancyCatalog
{
    /// <summary>Entités globales (hors tenant scolaire).</summary>
    public static readonly IReadOnlySet<string> GlobalEntities = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(Domain.Entities.Geography.Country),
        nameof(Domain.Entities.Geography.Province),
        nameof(Domain.Entities.Geography.City),
        nameof(Domain.Entities.Geography.Commune),
        nameof(Domain.Entities.Geography.PostalAddress),
        nameof(Domain.Entities.Security.Permission),
        nameof(Domain.Entities.Finance.CurrencyDefinition),
        nameof(Domain.Entities.Finance.ExchangeRateType),
        nameof(Domain.Entities.Finance.ExchangeRate),
        nameof(Domain.Entities.Finance.ExchangeRateHistory),
        nameof(Domain.Entities.System.ApplicationVersion),
        nameof(Domain.Entities.Settings.School),
    };

    /// <summary>Chaîne de résolution SchoolId (entité → chemin).</summary>
    public static readonly IReadOnlyDictionary<string, string> IndirectOwnershipChains =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(Domain.Entities.Students.Enrollment)] = "Student.SchoolId | ClassRoom.SchoolId",
            [nameof(Domain.Entities.Students.StudentGuardian)] = "Student.SchoolId",
            [nameof(Domain.Entities.Students.StudentDocument)] = "Student.SchoolId",
            [nameof(Domain.Entities.Students.EnrollmentPricingCategoryHistory)] = "Enrollment.Student.SchoolId",
            [nameof(Domain.Entities.Students.StudentStatusHistory)] = "Student.SchoolId",
            [nameof(Domain.Entities.Grades.Evaluation)] = "ClassRoom.SchoolId",
            [nameof(Domain.Entities.Grades.GradeEntry)] = "Student.SchoolId",
            [nameof(Domain.Entities.Grades.ReportCard)] = "Student.SchoolId",
            [nameof(Domain.Entities.Grades.ReportCardDetail)] = "ReportCard.Student.SchoolId",
            [nameof(Domain.Entities.Academic.CourseAssignment)] = "ClassRoom.SchoolId",
            [nameof(Domain.Entities.Academic.ScheduleSlot)] = "CourseAssignment.ClassRoom.SchoolId",
            [nameof(Domain.Entities.Finance.PaymentLine)] = "Payment.SchoolId",
            [nameof(Domain.Entities.Finance.PaymentReversal)] = "Payment.SchoolId",
            [nameof(Domain.Entities.Finance.StudentFeeBalance)] = "Student.SchoolId",
            [nameof(Domain.Entities.Finance.RevenueAllocationKeyDetail)] = "AllocationKey.SchoolId",
            [nameof(Domain.Entities.Notifications.NotificationRecipient)] = "Notification.SchoolId",
            [nameof(Domain.Entities.Security.UserRoleAssignment)] = "User.SchoolId",
            [nameof(Domain.Entities.Security.RolePermission)] = "Role.SchoolId",
            [nameof(Domain.Entities.Security.RefreshToken)] = "User.SchoolId",
            [nameof(Domain.Entities.Sync.SyncOutboxItem)] = "Unit.SchoolId",
            [nameof(Domain.Entities.Deliberation.StudentRemedialCourse)] = "RemedialSession.SchoolId",
        };

    public static bool IsGlobalEntity(Type clrType) =>
        GlobalEntities.Contains(clrType.Name);

    public static bool HasIndirectChain(Type clrType) =>
        IndirectOwnershipChains.ContainsKey(clrType.Name);
}
