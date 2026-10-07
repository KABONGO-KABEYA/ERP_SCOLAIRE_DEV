using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.OpenApi.Models;
using SchoolManagement.API.Extensions;
using SchoolManagement.API.Hosting;
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

// Console immÃ©diat (Coolify) â€” avant UseSerilog / Build, sinon les fatals sont invisibles.
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
    await FatalExitAsync("DÃ©marrage API Ã©chouÃ© : {Error}", ex.ToString());
}

async Task RunAsync()
{
// Initialize PDF generation before any request, including payment situations and withholdings.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Service Windows : cwd = System32 â€” forcer la racine sur le dossier de l'exe.
    ContentRootPath = AppContext.BaseDirectory,
});

// Permet d'hÃ©berger l'API comme service Windows (setup serveur).
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
    await FatalExitAsync("ClÃ© de chiffrement configuration : {Error}", ex.Message);
}

var encryption = EncryptionServiceFactory.Create();
var databaseBootstrap = new DatabaseConnectionBootstrap(AppContext.BaseDirectory, encryption);

// Docker / cloud : prioritÃ© Ã  la connection string d'environnement (sans DPAPI).
// Docker / Coolify : SQL_CONNECTION_STRING prime sur appsettings.Production (Ã©vite localhost\INSTANCE Windows).
var envConnectionString =
    Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

// Alias JWT issus de .env / Coolify (JWT_SECRET_KEY â†’ Jwt__SecretKey)
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
            "Connexion SQL Server impossible via SQL_CONNECTION_STRING. {Error} VÃ©rifiez rÃ©seau Docker Coolify (mÃªme rÃ©seau que SQL) et port 1433.",
            databaseTestResult.Message);
    }

    Log.Information("Connexion SQL Server validÃ©e via variable d'environnement (Docker/cloud).");
}
else
{
    (_, sqlConnectionString, databaseTestResult) = await databaseBootstrap.LoadValidateAndTestAsync();
    if (!databaseTestResult.IsSuccess)
    {
        await FatalExitAsync(
            "Connexion SQL Server impossible. Corrigez {ConfigFile} ou dÃ©finissez SQL_CONNECTION_STRING. {Error}",
            DatabaseConfigurationManager.FileName,
            databaseTestResult.Message);
    }

    Log.Information("Connexion SQL Server validÃ©e via {ConfigFile}.", DatabaseConfigurationManager.FileName);
}

try
{
    DatabaseEnvironmentGuard.EnsureSafe(builder.Environment, sqlConnectionString);
    Log.Information(
        "Garde BD OK â€” Env={Env}, Database={Database}",
        builder.Environment.EnvironmentName,
        DatabaseEnvironmentGuard.ExtractDatabaseName(sqlConnectionString));
}
catch (Exception ex)
{
    await FatalExitAsync("Garde environnement / base de donnÃ©es : {Error}", ex.Message);
}

var fileStorageManager = new FileStorageConfigurationManager(AppContext.BaseDirectory);
var fileStorageRoot =
    builder.Configuration["FileStorage:Root"]
    ?? Environment.GetEnvironmentVariable("FILE_STORAGE_ROOT");
if (!string.IsNullOrWhiteSpace(fileStorageRoot))
{
    Directory.CreateDirectory(fileStorageRoot);
    fileStorageManager.SaveConfiguration(new FileStorageConfiguration { Racine = fileStorageRoot.Trim() });
    Log.Information("Dossier fichiers configurÃ© via FILE_STORAGE_ROOT={Root}.", fileStorageRoot);
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
        "Configuration fichiers invalide. DÃ©finissez FILE_STORAGE_ROOT ou corrigez {ConfigFile}.{NewLine}{Error}",
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
        "Dossier partagÃ© inaccessible. Corrigez FILE_STORAGE_ROOT / {ConfigFile}.{NewLine}{Error}",
        FileStorageConfigurationManager.FileName,
        Environment.NewLine,
        fileStorageTestResult.Message);
}

Log.Information("Dossier partagÃ© validÃ©.");

var cloudConfigManager = new CloudDatabaseConfigurationManager(AppContext.BaseDirectory, encryption);
builder.Services.AddSingleton(cloudConfigManager);
builder.Services.AddSingleton(databaseBootstrap.ConfigurationManager);
builder.Services.AddSingleton(fileStorageManager);
builder.Services.AddSingleton<StartupReadiness>();
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
        "Sync cloud : fichier {File} prÃ©sent â€” ACTIF={Actif}, SERVEUR={Serveur}, INTERVALLE={Interval} min.",
        CloudDatabaseConfigurationManager.FileName,
        cloudPreview.Actif ? 1 : 0,
        cloudPreview.Serveur,
        cloudPreview.IntervalleMinutes);
}
else
{
    Log.Information(
        "Sync cloud inactive â€” crÃ©ez {File} (voir scripts/configure-cloud-sync.ps1).",
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
    "DÃ©ploiement API : Role={Role}, ReadOnly={ReadOnly}",
    deploymentOptions.Role,
    deploymentOptions.IsCloudReadOnly);

builder.Services.AddControllers();
var deploymentRolePreview = builder.Configuration["Deployment:Role"]
    ?? Environment.GetEnvironmentVariable("Deployment__Role")
    ?? "Local";
if (!deploymentRolePreview.Equals("Cloud", StringComparison.OrdinalIgnoreCase)
    && builder.Configuration.GetValue("LocalServerDiscovery:Advertise", true))
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
        Description = "API REST pour la gestion scolaire (Desktop + Mobile). Authentification JWT Bearer. En Mode Cloud (ReadOnly), seules les Ã©critures auth/health/notes sont autorisÃ©es."
    });

    // Ã‰vite les collisions de schÃ©mas (plusieurs DTO avec le mÃªme nom court).
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

var readiness = app.Services.GetRequiredService<StartupReadiness>();

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

app.MapGet("/", () => Results.Redirect("/swagger/index.html"))
    .AllowAnonymous();

app.MapControllers();
app.MapHub<SchoolManagement.API.Hubs.ParentNotificationsHub>("/hubs/parent-notifications");

// Démarrer le host (signal Running au SCM) AVANT le schéma SQL long.
await app.StartAsync();
Console.WriteLine($"Boot: listening ready ({builder.Configuration["ASPNETCORE_URLS"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "urls-from-host"})");

try
{
    await SchemaAndSeedBootstrap.EnsureAsync(app, sqlConnectionString);
    readiness.MarkReady();
    Log.Information("API prête — schéma / seed terminés.");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Échec schéma / seed après démarrage du host.");
    try { await app.StopAsync(); } catch { /* ignore */ }
    await FatalExitAsync("Démarrage API échoué (schéma/seed) : {Error}", ex.ToString());
    return;
}

await app.WaitForShutdownAsync();
} // RunAsync

public partial class Program;
