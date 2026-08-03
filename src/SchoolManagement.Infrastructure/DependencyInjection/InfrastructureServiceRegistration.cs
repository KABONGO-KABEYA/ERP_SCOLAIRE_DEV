namespace SchoolManagement.Infrastructure.DependencyInjection;

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.CloudSync;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Infrastructure.Auth;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Notifications;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Application.Notifications.Interfaces;
using SchoolManagement.Infrastructure.Persistence.Repositories;
using SchoolManagement.Application.Enrollment.Interfaces;
using SchoolManagement.Infrastructure.Seeding;
using SchoolManagement.Infrastructure.Services;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "La chaîne de connexion SQL Server est manquante. Configurez ServeurDonnees.txt.");
        }

        services.AddDbContext<SchoolDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(SchoolDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(3);
            }));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<ICurriculumSeedService, CurriculumSeeder>();
        services.AddScoped<CurriculumSeeder>();
        services.AddScoped<ISectionConsolidationService, SectionConsolidationService>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<IStudentDossierStorageService, StudentDossierStorageService>();
        services.AddSingleton<IDocumentBrandingStorageService, DocumentBrandingStorageService>();
        services.AddScoped<IEnrollmentMaintenanceService, EnrollmentMaintenanceService>();
        services.AddSingleton<IPushNotificationSender, LoggingPushNotificationSender>();

        services.AddSingleton<DatabaseConnectionFactory>();
        // Legacy full-table sync — utilisé uniquement pour bootstrap initial (v1 → outbox).
        services.AddScoped<ICloudDatabaseSyncService, CloudDatabaseSyncService>();
        services.AddScoped<ICloudSyncEngine, CloudSyncEngine>();
        services.AddScoped<ICloudSyncFacade, CloudSyncFacade>();
        services.AddHostedService<CloudSyncHostedService>();
        services.AddScoped<SchoolManagement.Application.Updates.Interfaces.IAppUpdateService,
            SchoolManagement.Infrastructure.Updates.AppUpdateService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                    ?? throw new InvalidOperationException("JWT settings missing.");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                // SignalR : token via query string ?access_token=
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }
}
