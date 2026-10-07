using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Seeding;
using Serilog;

namespace SchoolManagement.API.Hosting;

/// <summary>
/// Applique les SchemaInitializers + seed apres que le host ecoute
/// (evite le timeout Windows SCM 1053 / 30s sur le service ErpScolaireApi).
/// </summary>
internal static class SchemaAndSeedBootstrap
{
    public static async Task EnsureAsync(WebApplication app, string sqlConnectionString)
    {
        // Contrat schema (officiel) :
        // - 001_InitialCreate_EF.sql = baseline historique immuable ;
        // - SchemaInitializers = evolution idempotente (Setup + demarrage API) ;
        // - Migrations EF = artefacts de modele — Database.Migrate() est interdit ici.
        using var scope = app.Services.CreateScope();
        var brandingSchema = new DocumentBrandingSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<DocumentBrandingSchemaInitializer>>());
        await brandingSchema.EnsureCreatedAsync();

        var parentNoticeSchema = new ParentNoticeSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<ParentNoticeSchemaInitializer>>());
        await parentNoticeSchema.EnsureCreatedAsync();

        var enrollmentGuardianSchema = new EnrollmentGuardianSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<EnrollmentGuardianSchemaInitializer>>());
        await enrollmentGuardianSchema.EnsureCreatedAsync();

        var geographySchema = new GeographySchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<GeographySchemaInitializer>>());
        await geographySchema.EnsureCreatedAsync();

        var classRoomSchema = new ClassRoomSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<ClassRoomSchemaInitializer>>());
        await classRoomSchema.EnsureUpdatedAsync();

        var curriculumSchema = new CurriculumSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<CurriculumSchemaInitializer>>());
        await curriculumSchema.EnsureUpdatedAsync();

        var courseCodeSchema = new CourseCodeSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<CourseCodeSchemaInitializer>>());
        await courseCodeSchema.EnsureUpdatedAsync();

        var courseAssignmentSchema = new CourseAssignmentSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<CourseAssignmentSchemaInitializer>>());
        await courseAssignmentSchema.EnsureUpdatedAsync();

        var evaluationSchema = new EvaluationSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<EvaluationSchemaInitializer>>());
        await evaluationSchema.EnsureUpdatedAsync();

        var maximaParPeriodeSchema = new MaximaParPeriodeSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<MaximaParPeriodeSchemaInitializer>>());
        await maximaParPeriodeSchema.EnsureCreatedAsync();

        var attendanceSchema = new AttendanceSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<AttendanceSchemaInitializer>>());
        await attendanceSchema.EnsureUpdatedAsync();

        var disciplineMeritSchema = new DisciplineMeritSchoolIdSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<DisciplineMeritSchoolIdSchemaInitializer>>());
        await disciplineMeritSchema.EnsureUpdatedAsync();

        var schoolFeeSchema = new SchoolFeeSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<SchoolFeeSchemaInitializer>>());
        await schoolFeeSchema.EnsureCreatedAsync();

        // FinDevise requis avant rÃ©partition recettes / comptabilitÃ© / paiements (FK CurrencyId).
        var currencySchema = new CurrencySchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<CurrencySchemaInitializer>>());
        await currencySchema.EnsureCreatedAsync();

        var revenueAllocationSchema = new RevenueAllocationSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<RevenueAllocationSchemaInitializer>>());
        await revenueAllocationSchema.EnsureCreatedAsync();

        var accountingSchema = new AccountingSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<AccountingSchemaInitializer>>());
        await accountingSchema.EnsureCreatedAsync();

        var withholdingSchema = new WithholdingSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<WithholdingSchemaInitializer>>());
        await withholdingSchema.EnsureCreatedAsync();

        var enrollmentPricingSchema = new EnrollmentPricingSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<EnrollmentPricingSchemaInitializer>>());
        await enrollmentPricingSchema.EnsureCreatedAsync();

        var studentFeeBalanceSchema = new StudentFeeBalanceSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<StudentFeeBalanceSchemaInitializer>>());
        await studentFeeBalanceSchema.EnsureCreatedAsync();

        var paymentLineSchema = new PaymentLineSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<PaymentLineSchemaInitializer>>());
        await paymentLineSchema.EnsureCreatedAsync();

        var paymentCashRegisterSchema = new PaymentCashRegisterSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<PaymentCashRegisterSchemaInitializer>>());
        await paymentCashRegisterSchema.EnsureCreatedAsync();

        var studentCardSchema = new StudentCardSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<StudentCardSchemaInitializer>>());
        await studentCardSchema.EnsureCreatedAsync();

        var cloudSyncSchema = new CloudSyncSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<CloudSyncSchemaInitializer>>());
        await cloudSyncSchema.EnsureCreatedAsync();

        var schoolDefaultFeeSchema = new SchoolDefaultFeeSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<SchoolDefaultFeeSchemaInitializer>>());
        await schoolDefaultFeeSchema.EnsureCreatedAsync();

        var personnelSchema = new PersonnelSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<PersonnelSchemaInitializer>>());
        await personnelSchema.EnsureUpdatedAsync();

        var pedagogicalPeriodSchema = new PedagogicalPeriodSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<PedagogicalPeriodSchemaInitializer>>());
        await pedagogicalPeriodSchema.EnsureUpdatedAsync();

        var resultValidationSchema = new ResultValidationSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<ResultValidationSchemaInitializer>>());
        await resultValidationSchema.EnsureCreatedAsync();

        var deliberationMinutesSchema = new DeliberationMinutesSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<DeliberationMinutesSchemaInitializer>>());
        await deliberationMinutesSchema.EnsureCreatedAsync();

        var deliberationDecisionSchema = new DeliberationDecisionSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<DeliberationDecisionSchemaInitializer>>());
        await deliberationDecisionSchema.EnsureCreatedAsync();

        var deliberationCatalogSchema = new DeliberationCatalogSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<DeliberationCatalogSchemaInitializer>>());
        await deliberationCatalogSchema.EnsureCreatedAsync();

        var updateSchema = new ApplicationUpdateSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<ApplicationUpdateSchemaInitializer>>());
        await updateSchema.EnsureCreatedAsync();

        var notificationSchema = new NotificationSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<NotificationSchemaInitializer>>());
        await notificationSchema.EnsureCreatedAsync();

        var parentActivationSchema = new ParentActivationSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<ParentActivationSchemaInitializer>>());
        await parentActivationSchema.EnsureCreatedAsync();

        var schoolEstablishmentSchema = new SchoolEstablishmentSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<SchoolEstablishmentSchemaInitializer>>());
        await schoolEstablishmentSchema.EnsureCreatedAsync();

        var schoolSubscriptionSchema = new SchoolSubscriptionSchemaInitializer(
            sqlConnectionString,
            scope.ServiceProvider.GetRequiredService<ILogger<SchoolSubscriptionSchemaInitializer>>());
        await schoolSubscriptionSchema.EnsureCreatedAsync();

        // En dernier : dÃ©pend des tables crÃ©Ã©es par les initialiseurs prÃ©cÃ©dents (sync, caisseâ€¦).
        var schoolTenancySchema = new SchoolTenancySchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<SchoolTenancySchemaInitializer>>());
        await schoolTenancySchema.EnsureCreatedAsync();

        var securityPhase0Schema = new SecurityEnginePhase0SchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<SecurityEnginePhase0SchemaInitializer>>());
        await securityPhase0Schema.EnsureCreatedAsync();

        var registrationNumberCounterSchema = new RegistrationNumberCounterSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<RegistrationNumberCounterSchemaInitializer>>());
        await registrationNumberCounterSchema.EnsureCreatedAsync();

        var userRoleAssignmentSchema = new UserRoleAssignmentSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<UserRoleAssignmentSchemaInitializer>>());
        await userRoleAssignmentSchema.EnsureUpdatedAsync();

        // Seed systeme (permissions + admin) : Development toujours ; Production seulement si SEED_DATABASE=true|1
        // Seed demo : Development uniquement (jamais Production, sauf ALLOW_DEMO_SEED=true explicite).
        var seedFlag = Environment.GetEnvironmentVariable("SEED_DATABASE");
        var allowDemoFlag = Environment.GetEnvironmentVariable("ALLOW_DEMO_SEED");
        var shouldSeedSystem = app.Environment.IsDevelopment()
            || string.Equals(seedFlag, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(seedFlag, "1", StringComparison.OrdinalIgnoreCase);
        var includeDemoConfig = app.Configuration.GetValue("Seed:IncludeDemoData", app.Environment.IsDevelopment());
        var allowDemoExplicit =
            string.Equals(allowDemoFlag, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(allowDemoFlag, "1", StringComparison.OrdinalIgnoreCase);
        var shouldSeedDemo = shouldSeedSystem
            && includeDemoConfig
            && (app.Environment.IsDevelopment() || allowDemoExplicit)
            && !DatabaseEnvironmentGuard.IsProductionDatabase(sqlConnectionString);

        if (shouldSeedSystem)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedSystemAsync();
            if (shouldSeedDemo)
            {
                await seeder.SeedDemoAsync();
                Log.Information("Seed Development : donnees de demonstration chargees.");
            }
            else if (!app.Environment.IsDevelopment())
            {
                Log.Information("Seed Production : systeme uniquement (pas de donnees de demonstration).");
            }
        }
    }
}
