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

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: RollingInterval.Day));

var encryption = EncryptionServiceFactory.Create();
var databaseBootstrap = new DatabaseConnectionBootstrap(AppContext.BaseDirectory, encryption);

// Docker / cloud : priorité à la connection string d'environnement (sans DPAPI).
var envConnectionString =
    builder.Configuration.GetConnectionString("Default")
    ?? Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default");

string sqlConnectionString;
DatabaseConnectionTestResult databaseTestResult;
if (!string.IsNullOrWhiteSpace(envConnectionString))
{
    sqlConnectionString = envConnectionString.Trim();
    databaseTestResult = await DatabaseConnectionTester.TestConnectionStringAsync(sqlConnectionString);
    if (!databaseTestResult.IsSuccess)
    {
        Log.Fatal(
            "Connexion SQL Server impossible via SQL_CONNECTION_STRING / ConnectionStrings:Default.{NewLine}{Error}",
            Environment.NewLine,
            databaseTestResult.Message);
        return;
    }

    Log.Information("Connexion SQL Server validée via variable d'environnement (Docker/cloud).");
}
else
{
    (_, sqlConnectionString, databaseTestResult) = await databaseBootstrap.LoadValidateAndTestAsync();
    if (!databaseTestResult.IsSuccess)
    {
        Log.Fatal(
            "Connexion SQL Server impossible. Corrigez {ConfigFile} ou définissez SQL_CONNECTION_STRING.{NewLine}{Error}",
            DatabaseConfigurationManager.FileName,
            Environment.NewLine,
            databaseTestResult.Message);
        return;
    }

    Log.Information("Connexion SQL Server validée via {ConfigFile}.", DatabaseConfigurationManager.FileName);
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
    Log.Fatal(
        "Configuration fichiers invalide. Définissez FILE_STORAGE_ROOT ou corrigez {ConfigFile}.{NewLine}{Error}",
        FileStorageConfigurationManager.FileName,
        Environment.NewLine,
        string.Join(Environment.NewLine, fileStorageValidation.FieldErrors.Values));
    return;
}

var fileStorageTestResult = new FileStoragePathTester().TestConfiguration(
    fileStorageConfiguration,
    AppContext.BaseDirectory,
    requireWriteAccess: true);
if (!fileStorageTestResult.IsSuccess)
{
    Log.Fatal(
        "Dossier partagé inaccessible. Corrigez FILE_STORAGE_ROOT / {ConfigFile}.{NewLine}{Error}",
        FileStorageConfigurationManager.FileName,
        Environment.NewLine,
        fileStorageTestResult.Message);
    return;
}

Log.Information("Dossier partagé validé.");

var cloudConfigManager = new CloudDatabaseConfigurationManager(AppContext.BaseDirectory, encryption);
builder.Services.AddSingleton(cloudConfigManager);
builder.Services.AddSingleton(databaseBootstrap.ConfigurationManager);
builder.Services.AddSingleton(fileStorageManager);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, sqlConnectionString);
builder.Services.AddPermissionPolicies();

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
var deploymentOptions = builder.Configuration
    .GetSection(DeploymentOptions.SectionName)
    .Get<DeploymentOptions>() ?? new DeploymentOptions();
Log.Information(
    "Déploiement API : Role={Role}, ReadOnly={ReadOnly}",
    deploymentOptions.Role,
    deploymentOptions.IsCloudReadOnly);

builder.Services.AddControllers();
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
        Description = "API REST pour la gestion scolaire (Desktop + Mobile)"
    });

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

// Patches de schéma idempotents : aussi en Production (Docker Cloud),
// sinon la BD cloud reste en retard (ex. DefaultFeeTypeId) → HTTP 500.
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

    var attendanceSchema = new AttendanceSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<AttendanceSchemaInitializer>>());
    await attendanceSchema.EnsureUpdatedAsync();

    var schoolFeeSchema = new SchoolFeeSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<SchoolFeeSchemaInitializer>>());
    await schoolFeeSchema.EnsureCreatedAsync();

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

    var cloudSyncSchema = new CloudSyncSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<CloudSyncSchemaInitializer>>());
    await cloudSyncSchema.EnsureCreatedAsync();

    var schoolDefaultFeeSchema = new SchoolDefaultFeeSchemaInitializer(
        sqlConnectionString,
        scope.ServiceProvider.GetRequiredService<ILogger<SchoolDefaultFeeSchemaInitializer>>());
    await schoolDefaultFeeSchema.EnsureCreatedAsync();

    if (app.Environment.IsDevelopment())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CloudReadOnlyMiddleware>();
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP Scolaire API v1"));
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
