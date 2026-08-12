using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SchoolManagement.Desktop.Navigation;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.Updates;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Desktop.Views;
using SchoolManagement.LocalServerDiscovery;
using Serilog;

namespace SchoolManagement.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private bool _isShuttingDown;
    private bool _mainWindowCloseHooked;

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

        var httpFactory = _host.Services.GetRequiredService<IHttpClientFactory>();
        if (await InitialSetupViewModel.NeedsSetupAsync(httpFactory))
        {
            var setupWindow = _host.Services.GetRequiredService<InitialSetupWindow>();
            if (setupWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        var loginViewModel = _host.Services.GetRequiredService<LoginViewModel>();
        var loggedIn = await loginViewModel.TryAutoLoginAsync();

        if (!loggedIn)
        {
            var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        if (!await EnterAuthenticatedSessionAsync())
        {
            return;
        }

        _host.Services.GetRequiredService<DesktopUpdateCoordinator>().Start();
    }

    /// <summary>
    /// Déconnexion utilisateur : session nettoyée → écran Login (processus conservé).
    /// Distinct de la fermeture application (X / Alt+F4 → <see cref="Shutdown"/>).
    /// </summary>
    internal async Task LogoutToLoginAsync()
    {
        if (_host is null || _isShuttingDown)
        {
            return;
        }

        CloseSecondaryWindows();

        var shell = _host.Services.GetRequiredService<ShellViewModel>();
        shell.ResetForLogout();

        if (MainWindow is Desktop.MainWindow shellWindow)
        {
            shellWindow.Hide();
        }

        var loginViewModel = _host.Services.GetRequiredService<LoginViewModel>();
        loginViewModel.PrepareForFreshLogin();

        var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
        if (loginWindow.ShowDialog() != true)
        {
            ExitApplication();
            return;
        }

        if (!await EnterAuthenticatedSessionAsync())
        {
            return;
        }
    }

    private async Task<bool> EnterAuthenticatedSessionAsync()
    {
        if (_host is null)
        {
            return false;
        }

        var authSession = _host.Services.GetRequiredService<IAuthSessionService>();
        if (authSession.CurrentUser?.MustChangePassword == true)
        {
            var changePasswordWindow = _host.Services.GetRequiredService<ChangePasswordWindow>();
            if (changePasswordWindow.ShowDialog() != true)
            {
                ExitApplication();
                return false;
            }
        }

        var shellViewModel = _host.Services.GetRequiredService<ShellViewModel>();
        if (!await shellViewModel.InitializeNavigationAsync())
        {
            MessageBox.Show(
                shellViewModel.NavigationError
                ?? "Impossible de charger la navigation de l'application.",
                "ERP Scolaire",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ExitApplication();
            return false;
        }

        shellViewModel.RefreshCurrentAcademicYear();

        var mainWindowViewModel = _host.Services.GetRequiredService<MainWindowViewModel>();
        mainWindowViewModel.NotifyUserChanged();

        var mainWindow = _host.Services.GetRequiredService<Desktop.MainWindow>();
        MainWindow = mainWindow;
        EnsureMainWindowCloseEndsApplication(mainWindow);
        mainWindow.Show();
        mainWindow.Activate();
        return true;
    }

    private void EnsureMainWindowCloseEndsApplication(Desktop.MainWindow mainWindow)
    {
        if (_mainWindowCloseHooked)
        {
            return;
        }

        _mainWindowCloseHooked = true;
        mainWindow.Closed += OnMainWindowClosed;
    }

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        // X / Alt+F4 : fermeture complète — jamais retour Login.
        ExitApplication();
    }

    private void ExitApplication()
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        Shutdown();
    }

    private static void CloseSecondaryWindows()
    {
        foreach (Window window in Current.Windows.Cast<Window>().ToList())
        {
            if (window is Desktop.MainWindow or LoginWindow)
            {
                continue;
            }

            try
            {
                window.Close();
            }
            catch
            {
                // Ignore les fenêtres déjà en cours de fermeture.
            }
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var configuredBaseUrl = configuration["Api:BaseUrl"] ?? $"http://localhost:{DiscoveryConstants.ApiPort}/";
        var remoteBaseUrl = configuration["Api:RemoteBaseUrl"] ?? DiscoveryConstants.DefaultRemoteBaseUrl;

        services.AddLocalServerDiscovery(options =>
        {
            options.RemoteBaseUrl = remoteBaseUrl;
            options.EnableSubnetScan = true;
            options.EnableBackgroundRecheck = true;
        });

        services.AddSingleton<IAuthSessionService, AuthSessionService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton(_ => new Application.Configuration.FileStorage.FileStorageConfigurationManager(AppContext.BaseDirectory));
        services.AddSingleton<IStudentDossierPathResolver, StudentDossierPathResolver>();
        services.AddSingleton<IDocumentBrandingPathResolver, DocumentBrandingPathResolver>();
        services.AddSingleton<IApiClient, ApiClient>();
        services.AddTransient<AuthDelegatingHandler>();
        services.AddTransient<DiscoveryBaseAddressHandler>(sp =>
            new DiscoveryBaseAddressHandler(
                sp.GetRequiredService<ILocalServerDiscovery>(),
                configuredBaseUrl));
        services.AddTransient<AuthApiService>();
        services.AddTransient<ISchoolApiService, SchoolApiService>();
        services.AddTransient<IEnrollmentWizardApiService, EnrollmentWizardApiService>();
        services.AddTransient<IGeographyApiService, GeographyApiService>();
        services.AddTransient<IGeographyAdminApiService, GeographyAdminApiService>();
        services.AddTransient<IEnrollmentFormPrintService, EnrollmentFormPrintService>();
        services.AddTransient<IFeeTypeStatementPrintService, FeeTypeStatementPrintService>();
        services.AddTransient<IStudentListPrintService, StudentListPrintService>();
        services.AddTransient<IStudentCardPrintService, StudentCardPrintService>();
        services.AddTransient<IStudentApiService, StudentApiService>();
        services.AddTransient<IPaymentApiService, PaymentApiService>();
        services.AddTransient<IRevenueAllocationApiService, RevenueAllocationApiService>();
        services.AddTransient<IWithholdingApiService, WithholdingApiService>();
        services.AddTransient<ICurrencyApiService, CurrencyApiService>();
        services.AddTransient<IStudentCardApiService, StudentCardApiService>();
        services.AddTransient<ICloudSyncApiService, CloudSyncApiService>();
        services.AddTransient<IParentActivationApiService, ParentActivationApiService>();
        services.AddTransient<ISchoolEstablishmentApiService, SchoolEstablishmentApiService>();
        services.AddSingleton<IDesktopDialogs, WpfDesktopDialogs>();
        services.AddTransient<IFinanceApiService, FinanceApiService>();
        services.AddTransient<IGradeApiService, GradeApiService>();
        services.AddTransient<IBulletinApiService, BulletinApiService>();
        services.AddTransient<IResultValidationApiService, ResultValidationApiService>();
        services.AddTransient<IDeliberationApiService, DeliberationApiService>();
        services.AddTransient<IMentionsApiService, MentionsApiService>();
        services.AddTransient<IPedagogicalPeriodApiService, PedagogicalPeriodApiService>();
        services.AddTransient<IAcademicApiService, AcademicApiService>();
        services.AddTransient<IDocumentApiService, DocumentApiService>();
        services.AddTransient<IDocumentBrandingApiService, DocumentBrandingApiService>();
        services.AddTransient<ISchoolFeeApiService, SchoolFeeApiService>();
        services.AddTransient<ICourseConfigurationApiService, CourseConfigurationApiService>();
        services.AddTransient<IReportApiService, ReportApiService>();
        services.AddTransient<IPromoterDashboardApiService, PromoterDashboardApiService>();
        services.AddTransient<IAccountingApiService, AccountingApiService>();
        services.AddTransient<IAdminApiService, AdminApiService>();
        services.AddTransient<ISecurityAdminApiService, SecurityAdminApiService>();
        services.AddTransient<IPlatformCatalogApiService, PlatformCatalogApiService>();
        services.AddTransient<IUpdateAdminApiService, UpdateAdminApiService>();
        services.AddTransient<IPersonnelApiService, PersonnelApiService>();

        services.AddHttpClient("SchoolApi", client =>
        {
            client.BaseAddress = new Uri(DiscoveryConstants.PlaceholderBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .AddHttpMessageHandler<DiscoveryBaseAddressHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddHttpClient("SchoolApiAuth", client =>
        {
            client.BaseAddress = new Uri(DiscoveryConstants.PlaceholderBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .AddHttpMessageHandler<DiscoveryBaseAddressHandler>()
        .AddHttpMessageHandler<AuthDelegatingHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<IDesktopViewRegistry, DesktopViewRegistry>();
        services.AddSingleton<IDesktopNavigationLocalCache, DesktopNavigationLocalCache>();
        services.AddTransient<SecurityNavigationApiService>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<InitialSetupWindow>();
        services.AddTransient<InitialSetupViewModel>();
        services.AddTransient<ChangePasswordWindow>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DocumentBrandingViewModel>();
        services.AddTransient<GeographyAdminViewModel>();
        services.AddTransient<SchoolFeeConfigurationViewModel>();
        services.AddTransient<CourseConfigurationViewModel>();
        services.AddTransient<RevenueAllocationConfigViewModel>();
        services.AddTransient<WithholdingConfigViewModel>();
        services.AddTransient<MentionsConfigViewModel>();
        services.AddTransient<CurrencyManagementViewModel>();
        services.AddTransient<CloudSyncDashboardViewModel>();
        services.AddTransient<ParentActivationQrViewModel>();
        services.AddTransient<SchoolEstablishmentQrViewModel>();
        services.AddTransient<StudentsViewModel>();
        services.AddTransient<StudentCardsViewModel>();
        services.AddTransient<CardTemplateDesignerViewModel>();
        services.AddTransient<StudentDossierEditViewModel>();
        services.AddTransient<EnrollmentWizardViewModel>();
        services.AddTransient<PaymentsViewModel>();
        services.AddTransient<EncaissementsViewModel>();
        services.AddTransient<PricingCategoryAssignmentViewModel>();
        services.AddTransient<FinancialReportsViewModel>();
        services.AddTransient<PaymentSituationReportViewModel>();
        services.AddTransient<ExpensePaymentsViewModel>();
        services.AddTransient<FinanceHubViewModel>();
        services.AddTransient<GradesViewModel>();
        services.AddTransient<PedagogicalPeriodsViewModel>();
        services.AddTransient<AcademicViewModel>();
        services.AddTransient<DocumentsViewModel>();
        services.AddTransient<DocumentsHubViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<AdministrationViewModel>();
        services.AddTransient<SecurityUsersViewModel>();
        services.AddTransient<SecurityRolesViewModel>();
        services.AddTransient<SecurityExceptionsViewModel>();
        services.AddTransient<SecurityAuditViewModel>();
        services.AddTransient<PlatformCatalogViewModel>();
        services.AddTransient<PersonnelListViewModel>();
        services.AddTransient<PersonnelEditViewModel>();
        services.AddTransient<PersonnelDepartmentsViewModel>();
        services.AddTransient<PersonnelFunctionsViewModel>();
        services.AddTransient<PersonnelPlaceholderViewModel>();
        services.AddTransient<PersonnelHubViewModel>();
        services.AddTransient<ClassResultsViewModel>();
        services.AddTransient<IndividualResultViewModel>();
        services.AddTransient<DeliberationViewModel>();
        services.AddTransient<DeliberationWorkspaceViewModel>();
        services.AddTransient<ResultsPlaceholderViewModel>();
        services.AddTransient<ResultsHubViewModel>();
        services.AddTransient<ResultValidationViewModel>();

        services.AddDesktopUpdates(configuration);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _isShuttingDown = true;

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
