using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Desktop.Views;
using Serilog;

namespace SchoolManagement.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    public static IServiceProvider? Services { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        ProcessEnvironmentNormalizer.Apply();
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!await DatabaseStartupGate.EnsureConnectedAsync())
        {
            Shutdown();
            return;
        }

        if (!FileStorageStartupGate.EnsureConfigured())
        {
            Shutdown();
            return;
        }

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config => config.AddJsonFile("appsettings.json", optional: true))
            .UseSerilog((context, configuration) => configuration
                .WriteTo.File("logs/desktop-.log", rollingInterval: RollingInterval.Day))
            .ConfigureServices((context, services) => ConfigureServices(services, context.Configuration))
            .Build();

        Services = _host.Services;
        await _host.StartAsync();

        var uiErrorDialogShown = false;
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Erreur UI non gérée");

            var isRenderLoop = args.Exception is InvalidOperationException invalidOp
                && invalidOp.Message.Contains("Background", StringComparison.OrdinalIgnoreCase);

            if (args.Exception is not StackOverflowException
                && !isRenderLoop
                && !uiErrorDialogShown)
            {
                uiErrorDialogShown = true;
                try
                {
                    MessageBox.Show(
                        $"Une erreur est survenue :\n{GetDisplayMessage(args.Exception)}",
                        "ERP Scolaire",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    // Évite une boucle si l'affichage de l'erreur échoue aussi.
                }
            }

            args.Handled = true;
        };

        var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
        if (loginWindow.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var authSession = _host.Services.GetRequiredService<IAuthSessionService>();
        if (authSession.CurrentUser?.MustChangePassword == true)
        {
            var changePasswordWindow = _host.Services.GetRequiredService<ChangePasswordWindow>();
            if (changePasswordWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
        mainWindow.Activate();
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var apiBaseUrl = configuration["Api:BaseUrl"] ?? "http://localhost:5041/";

        services.AddSingleton<IAuthSessionService, AuthSessionService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton(_ => new Application.Configuration.FileStorage.FileStorageConfigurationManager(AppContext.BaseDirectory));
        services.AddSingleton<IStudentDossierPathResolver, StudentDossierPathResolver>();
        services.AddSingleton<IDocumentBrandingPathResolver, DocumentBrandingPathResolver>();
        services.AddSingleton<IApiClient, ApiClient>();
        services.AddTransient<AuthDelegatingHandler>();
        services.AddTransient<AuthApiService>();
        services.AddTransient<ISchoolApiService, SchoolApiService>();
        services.AddTransient<IEnrollmentWizardApiService, EnrollmentWizardApiService>();
        services.AddTransient<IGeographyApiService, GeographyApiService>();
        services.AddTransient<IGeographyAdminApiService, GeographyAdminApiService>();
        services.AddTransient<IEnrollmentFormPrintService, EnrollmentFormPrintService>();
        services.AddTransient<IStudentListPrintService, StudentListPrintService>();
        services.AddTransient<IStudentApiService, StudentApiService>();
        services.AddTransient<IPaymentApiService, PaymentApiService>();
        services.AddTransient<IGradeApiService, GradeApiService>();
        services.AddTransient<IAcademicApiService, AcademicApiService>();
        services.AddTransient<IDocumentApiService, DocumentApiService>();
        services.AddTransient<IDocumentBrandingApiService, DocumentBrandingApiService>();
        services.AddTransient<ISchoolFeeApiService, SchoolFeeApiService>();
        services.AddTransient<IReportApiService, ReportApiService>();
        services.AddTransient<IAdminApiService, AdminApiService>();

        services.AddHttpClient("SchoolApi", client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddHttpClient("SchoolApiAuth", client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .AddHttpMessageHandler<AuthDelegatingHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ChangePasswordWindow>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DocumentBrandingViewModel>();
        services.AddTransient<GeographyAdminViewModel>();
        services.AddTransient<SchoolFeeConfigurationViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<StudentDossierEditViewModel>();
        services.AddTransient<EnrollmentWizardViewModel>();
        services.AddTransient<PaymentsViewModel>();
        services.AddTransient<GradesViewModel>();
        services.AddTransient<AcademicViewModel>();
        services.AddTransient<DocumentsViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<AdministrationViewModel>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static string GetDisplayMessage(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}
