using Microsoft.EntityFrameworkCore;
using SchoolManagement.Bootstrap.API.Establishment;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Persistence;
using SchoolManagement.Bootstrap.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BootstrapOptions>(builder.Configuration.GetSection(BootstrapOptions.SectionName));

var bootstrapConnection =
    builder.Configuration["Bootstrap:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("BOOTSTRAP_CONNECTION_STRING")
    ?? Environment.GetEnvironmentVariable("Bootstrap__ConnectionString");

if (!string.IsNullOrWhiteSpace(bootstrapConnection))
{
    builder.Services.AddDbContext<BootstrapDbContext>(options =>
        options.UseSqlServer(bootstrapConnection, sql =>
            sql.MigrationsAssembly(typeof(BootstrapDbContext).Assembly.FullName)));
    builder.Services.AddScoped<IBootstrapSchoolRegistryRepository, EfBootstrapSchoolRegistryRepository>();
}
else
{
    // Dev / tests sans SQL : InMemory pour permettre l'injection du repository.
    builder.Services.AddDbContext<BootstrapDbContext>(options =>
        options.UseInMemoryDatabase("BootstrapRegistryDev"));
    builder.Services.AddScoped<IBootstrapSchoolRegistryRepository, EfBootstrapSchoolRegistryRepository>();
}

// Phase 8 : SQL-first — scoped (DbContext) ; ParentActivation + establishment.
builder.Services.AddScoped<SchoolRegistry>();
builder.Services.AddSingleton<BootstrapSessionStore>();
builder.Services.AddSingleton<
    SchoolManagement.Application.ParentActivation.BootstrapRelay.IBootstrapRelayOutboundAuth,
    StaticSharedKeyBootstrapRelayOutboundAuth>();
builder.Services.AddScoped<BootstrapOrchestrator>();
builder.Services.AddScoped<EstablishmentService>();
builder.Services.AddScoped<IUpdateReleaseCatalog, UpdateReleaseCatalog>();
builder.Services.AddScoped<IUpdateAgentCredentialService, UpdateAgentCredentialService>();
builder.Services.AddHostedService<LegacyEnvSchoolRegistryMigrator>();
builder.Services.AddHttpClient("school-relay");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(bootstrapConnection))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
    // Garde pour tests (InMemory via ConfigureTestServices) et providers non relationnels.
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/health", async (
    IServiceProvider sp,
    Microsoft.Extensions.Options.IOptions<BootstrapOptions> bootstrapOptions,
    CancellationToken ct) =>
{
    var options = bootstrapOptions.Value;
    var payload = new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["service"] = "bootstrap",
        ["registry"] = string.IsNullOrWhiteSpace(bootstrapConnection) ? "inmemory" : "sql",
        ["allowLegacyEnvSchoolRegistry"] = options.AllowLegacyEnvSchoolRegistry,
        ["legacyEnvSchoolsConfigured"] = options.Schools.Count,
    };

    try
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
        var schools = await db.SchoolRegistry.CountAsync(ct);
        var activeCredentials = await db.EstablishmentCredentials
            .CountAsync(
                c => c.Status == SchoolManagement.Bootstrap.API.Persistence.Entities.EstablishmentCredentialStatuses.Active,
                ct);
        var ecoleTest = await db.SchoolRegistry
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SchoolId == SchoolRegistry.EcoleTestSchoolId, ct);

        payload["schoolsRegistered"] = schools;
        payload["activeCredentials"] = activeCredentials;
        payload["ecoleTestPresent"] = ecoleTest is not null;
        if (ecoleTest is not null)
        {
            payload["ecoleTest"] = new
            {
                schoolId = ecoleTest.SchoolId,
                schoolName = ecoleTest.SchoolName,
                activationBaseUrl = ecoleTest.ActivationBaseUrl,
                cloudBaseUrl = ecoleTest.CloudBaseUrl,
                serverInstanceId = ecoleTest.ServerInstanceId,
                isActive = ecoleTest.IsActive,
            };
        }
    }
    catch
    {
        payload["registry"] = "error";
    }

    return Results.Ok(payload);
});

app.Run();

public partial class Program;
