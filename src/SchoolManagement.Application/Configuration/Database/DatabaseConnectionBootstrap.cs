using SchoolManagement.Application.Configuration.Encryption;

namespace SchoolManagement.Application.Configuration.Database;

/// <summary>Charge ServeurDonnees.txt et valide la connexion SQL au démarrage.</summary>
public sealed class DatabaseConnectionBootstrap
{
    public DatabaseConnectionBootstrap(string applicationDirectory)
        : this(applicationDirectory, EncryptionServiceFactory.Create())
    {
    }

    public DatabaseConnectionBootstrap(string applicationDirectory, IEncryptionService encryptionService)
    {
        ApplicationDirectory = applicationDirectory;
        ConfigurationManager = new DatabaseConfigurationManager(applicationDirectory, encryptionService);
        ConnectionFactory = new DatabaseConnectionFactory();
        ConnectionTester = new DatabaseConnectionTester(ConnectionFactory, ConfigurationManager);
    }

    public string ApplicationDirectory { get; }

    public DatabaseConfigurationManager ConfigurationManager { get; }

    public DatabaseConnectionFactory ConnectionFactory { get; }

    public DatabaseConnectionTester ConnectionTester { get; }

    public DatabaseConfiguration LoadConfiguration()
    {
        ConfigurationManager.EnsureDefaultFileExists();
        return ConfigurationManager.LoadConfiguration();
    }

    public string BuildConnectionString(DatabaseConfiguration configuration) =>
        ConnectionFactory.BuildConnectionString(configuration);

    public async Task<(DatabaseConfiguration Configuration, string ConnectionString, DatabaseConnectionTestResult TestResult)>
        LoadValidateAndTestAsync(CancellationToken cancellationToken = default)
    {
        ConfigurationManager.EnsureDefaultFileExists();
        var configuration = ConfigurationManager.LoadConfiguration();
        var validation = ConfigurationManager.Validate(configuration);
        if (!validation.IsValid)
        {
            var message = string.Join(Environment.NewLine, validation.FieldErrors.Values);
            return (configuration, string.Empty, DatabaseConnectionTestResult.Failure(message));
        }

        var connectionString = ConnectionFactory.BuildConnectionString(configuration);
        var testResult = await ConnectionTester.TestConnectionAsync(configuration, cancellationToken)
            .ConfigureAwait(false);
        return (configuration, connectionString, testResult);
    }
}
