using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.OpenApi.Models;
using SchoolManagement.API.Extensions;
using SchoolManagement.API.Middleware;
using SchoolManagement.API.Options;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Application.Configuration.FileStorage;
using SchoolManagement.Application.DependencyInjection;
using SchoolManagement.Infrastructure.DependencyInjection;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Seeding;
using Serilog;

// Console immédiat (Coolify) — avant UseSerilog / Build, sinon les fatals sont invisibles.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

static async Task FatalExitAsync(string messageTemplate, params object?[] args)
{
    try
    {
        Log.Fatal(messageTemplate, args);
        await Log.CloseAndFlushAsync();
    }
    catch
    {
        // ignore
    }

    try
    {
        Console.Error.WriteLine("FATAL: " + string.Join(" | ", args.Select(a => a?.ToString() ?? string.Empty)));
        Console.Error.WriteLine(messageTemplate);
    }
    catch
    {
        Console.Error.WriteLine("FATAL: application startup failed. Check SQL_CONNECTION_STRING / Jwt__SecretKey / firewall SQL 1433.");
    }

    Environment.Exit(1);
}

try
{
await RunAsync();
}
catch (Exception ex)
{
    await FatalExitAsync("Démarrage API échoué : {Error}", ex.ToString());
}

async Task RunAsync()
{
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Service Windows : cwd = System32 — forcer la racine sur le dossier de l'exe.
    ContentRootPath = AppContext.BaseDirectory,
});

// Permet d'héberger l'API comme service Windows (setup serveur).
builder.Host.UseWindowsService();

var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logsDir);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(logsDir, "api-.log"), rollingInterval: RollingInterval.Day));

Console.WriteLine("Boot: reading SQL / JWT environment...");
Console.WriteLine("Boot: ContentRoot = " + builder.Environment.ContentRootPath);

try
{
    ProductionEncryptionKeyGuard.EnsureConfigured(builder.Environment, builder.Configuration);
}
catch (Exception ex)
{
    await FatalExitAsync("Clé de chiffrement configuration : {Error}", ex.Message);
}

var encryption = EncryptionServiceFactory.Create();
var databaseBootstrap = new DatabaseConnectionBootstrap(AppContext.BaseDirectory, encryption);

// Docker / cloud : priorité à la connection string d'environnement (sans DPAPI).
// Docker / Coolify : SQL_CONNECTION_STRING prime sur appsettings.Production (évite localhost\INSTANCE Windows).
var envConnectionString =
    Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

// Alias JWT issus de .env / Coolify (JWT_SECRET_KEY → Jwt__SecretKey)
if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:SecretKey"])
    && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JWT_SECRET_KEY")))
{
    builder.Configuration["Jwt:SecretKey"] = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
}
if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"])
    && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JWT_ISSUER")))
{
    builder.Configuration["Jwt:Issuer"] = Environment.GetEnvironmentVariable("JWT_ISSUER");
}
if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"])
    && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JWT_AUDIENCE")))
{
    builder.Configuration["Jwt:Audience"] = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
}

string sqlConnectionString;
DatabaseConnectionTestResult databaseTestResult;
if (!string.IsNullOrWhiteSpace(envConnectionString))
{
    sqlConnectionString = envConnectionString.Trim();
    Console.WriteLine("Boot: testing SQL_CONNECTION_STRING...");
    databaseTestResult = await DatabaseConnectionTester.TestConnectionStringAsync(sqlConnectionString);
    if (!databaseTestResult.IsSuccess)
    {
        await FatalExitAsync(
            "Connexion SQL Server impossible via SQL_CONNECTION_STRING. {Error} Vérifiez réseau Docker Coolify (même réseau que SQL) et port 1433.",
            databaseTestResult.Message);
    }

    Log.Information("Connexion SQL Server validée via variable d'environnement (Docker/cloud).");
}
else
{
    (_, sqlConnectionString, databaseTestResult) = await databaseBootstrap.LoadValidateAndTestAsync();
    if (!databaseTestResult.IsSuccess)
    {
        await FatalExitAsync(
            "Connexion SQL Server impossible. Corrigez {ConfigFile} ou définissez SQL_CONNECTION_STRING. {Error}",
            DatabaseConfigurationManager.FileName,
            databaseTestResult.Message);
    }

    Log.Information("Connexion SQL Server validée via {ConfigFile}.", DatabaseConfigurationManager.FileName);
}

try
{
    DatabaseEnvironmentGuard.EnsureSafe(builder.Environment, sqlConnectionString);
    Log.Information(
        "Garde BD OK — Env={Env}, Database={Database}",
        builder.Environment.EnvironmentName,
        DatabaseEnvironmentGuard.ExtractDatabaseName(sqlConnectionString));
}
catch (Exception ex)
{
    await FatalExitAsync("Garde environnement / base de données : {Error}", ex.Message);
}

var fileStorageManager = new FileStorageConfigurationManager(AppContext.BaseDirectory);
var fileStorageRoot =
    builder.Configuration["FileStorage:Root"]
    ?? Environment.GetEnvironmentVariable("FILE_STORAGE_ROOT");
if (!string.IsNullOrWhiteSpace(fileStorageRoot))
{
    Directory.CreateDirectory(fileStorageRoot);
    fileStorageManager.SaveConfiguration(new FileStorageConfiguration { Racine = fileStorageRoot.Trim() });
    Log.Information("Dossier fichiers configuré via FILE_STORAGE_ROOT={Root}.", fileStorageRoot);
}
else
{
    fileStorageManager.EnsureDefaultFileExists();
}

var fileStorageConfiguration = fileStorageManager.LoadConfiguration();
var fileStorageValidation = fileStorageManager.Validate(fileStorageConfiguration);
if (!fileStorageValidation.IsValid)
{
    await FatalExitAsync(
        "Configuration fichiers invalide. Définissez FILE_STORAGE_ROOT ou corrigez {ConfigFile}.{NewLine}{Error}",
        FileStorageConfigurationManager.FileName,
        Environment.NewLine,
        string.Join(Environment.NewLine, fileStorageValidation.FieldErrors.Values));
}

var fileStorageTestResult = new FileStoragePathTester().TestConfiguration(
    fileStorageConfiguration,
    AppContext.BaseDirectory,
    requireWriteAccess: true);
if (!fileStorageTestResult.IsSuccess)
{
    await FatalExitAsync(
        "Dossier partagé inaccessible. Corrigez FILE_STORAGE_ROOT / {ConfigFile}.{NewLine}{Error}",
        FileStorageConfigurationManager.FileName,
        Environment.NewLine,
        fileStorageTestResult.Message);
}

Log.Information("Dossier partagé validé.");

var cloudConfigManager = new CloudDatabaseConfigurationManager(AppContext.BaseDirectory, encryption);
builder.Services.AddSingleton(cloudConfigManager);
builder.Services.AddSingleton(databaseBootstrap.ConfigurationManager);
builder.Services.AddSingleton(fileStorageManager);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, sqlConnectionString);
builder.Services.AddPermissionPolicies();
builder.Services.AddSignalR();
builder.Services.AddSingleton<
    SchoolManagement.Application.Notifications.Interfaces.INotificationRealtimePublisher,
    SchoolManagement.API.Notifications.SignalRNotificationRealtimePublisher>();

if (cloudConfigManager.FileExists)
{
    var cloudPreview = cloudConfigManager.LoadConfigurationWithoutPassword();
    Log.Information(
        "Sync cloud : fichier {File} présent — ACTIF={Actif}, SERVEUR={Serveur}, INTERVALLE={Interval} min.",
        CloudDatabaseConfigurationManager.FileName,
        cloudPreview.Actif ? 1 : 0,
        cloudPreview.Serveur,
        cloudPreview.IntervalleMinutes);
}
else
{
    Log.Information(
        "Sync cloud inactive — créez {File} (voir scripts/configure-cloud-sync.ps1).",
        CloudDatabaseConfigurationManager.FileName);
}

builder.Services.Configure<DeploymentOptions>(
    builder.Configuration.GetSection(DeploymentOptions.SectionName));
builder.Services.Configure<SchoolManagement.Application.ParentActivation.BootstrapRelay.BootstrapRelaySchoolOptions>(
    builder.Configuration.GetSection(
        SchoolManagement.Application.ParentActivation.BootstrapRelay.BootstrapRelaySchoolOptions.SectionName));
builder.Services.AddSingleton<
    SchoolManagement.Application.ParentActivation.BootstrapRelay.IBootstrapRelayRequestValidator,
    SchoolManagement.API.Services.BootstrapRelay.StaticSharedKeyBootstrapRelayRequestValidator>();
var deploymentOptions = builder.Configuration
    .GetSection(DeploymentOptions.SectionName)
    .Get<DeploymentOptions>() ?? new DeploymentOptions();
Log.Information(
    "Déploiement API : Role={Role}, ReadOnly={ReadOnly}",
    deploymentOptions.Role,
    deploymentOptions.IsCloudReadOnly);

builder.Services.AddControllers();
var deploymentRolePreview = builder.Configuration["Deployment:Role"]
    ?? Environment.GetEnvironmentVariable("Deployment__Role")
    ?? "Local";
if (!deploymentRolePreview.Equals("Cloud", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHostedService<SchoolManagement.LocalServerDiscovery.MdnsServiceAdvertiser>();
}
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ERP Administration Scolaire RDC",
        Version = "v1",
        Description = "API REST pour la gestion scolaire (Desktop + Mobile). Authentification JWT Bearer. En Mode Cloud (ReadOnly), seules les écritures auth/health/notes sont autorisées."
    });

    // Évite les collisions de schémas (plusieurs DTO avec le même nom court).
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
    options.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT : Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy
                .SetIsOriginAllowed(static origin =>
                {
                    if (string.IsNullOrWhiteSpace(origin))
                    {
                        return false;
                    }

                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        return false;
                    }

                    return uri.Host is "localhost" or "127.0.0.1";
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            return;
        }

        policy
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
                ["http://localhost", "http://localhost:5041", "https://localhost:7060"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Contrat schéma (officiel) :
// - 001_InitialCreate_EF.sql = baseline historique immuable ;
// - SchemaInitializers = mécanisme officiel d'évolution (idempotent, Setup + démarrage API) ;
// - Migrations EF = artefacts de modèle — Database.Migrate() est interdit ici.
{
    using var scope = app.Services.CreateScope();
    var brandingSchema = new DocumentBrandingSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<DocumentBrandingSchemaInitializer>>());
    await brandingSchema.EnsureCreatedAsync();

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

    // FinDevise requis avant répartition recettes / comptabilité / paiements (FK CurrencyId).
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

    // En dernier : dépend des tables créées par les initialiseurs précédents (sync, caisse…).
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

    // Seed système (permissions + admin) : Development toujours ; Production seulement si SEED_DATABASE=true|1
    // Seed démo : Development uniquement (jamais Production, sauf ALLOW_DEMO_SEED=true explicite).
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
            Log.Information("Seed Development : données de démonstration chargées.");
        }
        else if (!app.Environment.IsDevelopment())
        {
            Log.Information("Seed Production : système uniquement (pas de données de démonstration).");
        }
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CloudReadOnlyMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP Scolaire API v1"));
app.UseSerilogRequestLogging();
// Pas de redirection HTTPS en Docker/Coolify (HTTP :1804)
if (!string.Equals(
        Environment.GetEnvironmentVariable("Deployment__Role"),
        "Cloud",
        StringComparison.OrdinalIgnoreCase)
    && !app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SchoolManagement.API.Hubs.ParentNotificationsHub>("/hubs/parent-notifications");

Console.WriteLine($"Boot: listening ready ({builder.Configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "urls-from-host"})");
app.Run();
} // RunAsync

public partial class Program;
