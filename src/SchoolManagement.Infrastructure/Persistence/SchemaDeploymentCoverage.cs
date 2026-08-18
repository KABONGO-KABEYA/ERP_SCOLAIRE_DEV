namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Contrat de déploiement du schéma école.
/// Baseline historique + SchemaInitializers = mécanisme officiel.
/// Les migrations EF post-InitialCreate doivent être déclarées ici.
/// </summary>
public enum SchemaCoverageKind
{
    Complete,
    Partial,
    Excluded
}

public sealed record SchemaCoverageEntry(
    string MigrationId,
    string OfficialMechanism,
    SchemaCoverageKind Kind,
    string? Justification = null);

public static class SchemaDeploymentCoverage
{
    public const string InitialCreateMigrationId = "20260706114538_InitialCreate";

    /// <summary>
    /// Toute migration EF découverte hors InitialCreate doit figurer dans cette liste.
    /// Kind=Excluded exige une justification.
    /// </summary>
    public static IReadOnlyList<SchemaCoverageEntry> Entries { get; } =
    [
        new(
            "20260707084815_AddPedagogicalStructure",
            "CurriculumSchemaInitializer, ClassRoomSchemaInitializer, AttendanceSchemaInitializer, DisciplineMeritSchoolIdSchemaInitializer",
            SchemaCoverageKind.Partial,
            "Colonnes et tables pédagogiques couvertes. Les index baseline IX_TeacherAttendances_TeacherId_AttendanceDate (UNIQUE) et IX_StudentAttendances_StudentId_AttendanceDate (non UNIQUE) restent globaux volontairement : TeacherId/StudentId sont des PK GUID, pas une clé métier partagée entre écoles (contrairement à Courses.Code)."),
        new(
            "20260709093000_AddDocumentBranding",
            "DocumentBrandingSchemaInitializer",
            SchemaCoverageKind.Complete),
        new(
            "20260723140000_AddBranchAndPedagogicalClassCourse",
            "CurriculumSchemaInitializer",
            SchemaCoverageKind.Complete),
        new(
            "20260723153000_WidenCourseAndBranchCode",
            "CourseCodeSchemaInitializer",
            SchemaCoverageKind.Complete),
        new(
            "20260725100000_AddMaximaParPeriode",
            "MaximaParPeriodeSchemaInitializer",
            SchemaCoverageKind.Complete),
        new(
            "20260731140000_AddClassPeriodResultValidation",
            "ResultValidationSchemaInitializer",
            SchemaCoverageKind.Complete),
        new(
            "20260731160000_AddClassPeriodDeliberationMinutes",
            "DeliberationMinutesSchemaInitializer",
            SchemaCoverageKind.Complete),
        new(
            "20260731170000_AddDeliberationCouncilDecisions",
            "DeliberationDecisionSchemaInitializer",
            SchemaCoverageKind.Complete),
        new(
            "20260804120000_AddParentActivation",
            "ParentActivationSchemaInitializer",
            SchemaCoverageKind.Complete),
        new(
            "20260805103000_StrictSchoolTenantIsolation",
            "SchoolTenancySchemaInitializer",
            SchemaCoverageKind.Complete),
        new(
            "20260807081353_SecurityEnginePhase0Foundation",
            "SecurityEnginePhase0SchemaInitializer",
            SchemaCoverageKind.Partial,
            "Le Up() EF est volontairement plus étroit que le snapshot ; l'initializer pose le catalogue sécurité."),
        new(
            "20260812210000_FilterUserRoleAssignmentsUniqueIndex",
            "UserRoleAssignmentSchemaInitializer",
            SchemaCoverageKind.Complete),
        new(
            "20260812220000_AddRegistrationNumberCounters",
            "RegistrationNumberCounterSchemaInitializer",
            SchemaCoverageKind.Complete),
    ];
}
