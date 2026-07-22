using Microsoft.Data.SqlClient;

namespace SchoolManagement.Application.Configuration.Database;

/// <summary>Teste la connectivité SQL Server et retourne des messages d'erreur explicites.</summary>
public sealed class DatabaseConnectionTester
{
    private readonly DatabaseConnectionFactory _connectionFactory;
    private readonly DatabaseConfigurationManager _configurationManager;

    public DatabaseConnectionTester(
        DatabaseConnectionFactory connectionFactory,
        DatabaseConfigurationManager configurationManager)
    {
        _connectionFactory = connectionFactory;
        _configurationManager = configurationManager;
    }

    public async Task<DatabaseConnectionTestResult> TestConnectionAsync(
        DatabaseConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var validation = _configurationManager.Validate(configuration);

        if (!validation.IsValid)
        {
            return DatabaseConnectionTestResult.Failure(
                string.Join(Environment.NewLine, validation.FieldErrors.Values));
        }

        var connectionString = _connectionFactory.BuildConnectionString(configuration);
        return await TestConnectionStringAsync(connectionString, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Teste une connection string brute (Docker / variables d'environnement).</summary>
    public static async Task<DatabaseConnectionTestResult> TestConnectionStringAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return DatabaseConnectionTestResult.Failure("La chaîne de connexion SQL est vide.");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 15;
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return DatabaseConnectionTestResult.Success();
        }
        catch (SqlException ex)
        {
            return DatabaseConnectionTestResult.Failure(TranslateSqlException(ex));
        }
        catch (TimeoutException)
        {
            return DatabaseConnectionTestResult.Failure(
                "Délai de connexion dépassé. Vérifiez le serveur, le port et le pare-feu réseau.");
        }
        catch (Exception ex)
        {
            return DatabaseConnectionTestResult.Failure(
                $"Erreur réseau ou serveur inaccessible : {ex.Message}");
        }
    }

    internal static string TranslateSqlException(SqlException exception)
    {
        return exception.Number switch
        {
            18456 => "Utilisateur ou mot de passe SQL Server incorrect.",
            4060 => "La base de données spécifiée est introuvable ou inaccessible.",
            4064 => "Impossible d'ouvrir la base de données. Vérifiez le nom de la base.",
            233 or 10054 or 10053 => "Connexion interrompue par le serveur ou le réseau.",
            53 or 11001 or -1 => "Serveur SQL inaccessible. Vérifiez le nom/IP, le port et que SQL Server est démarré.",
            1225 => "Connexion refusée : le port SQL Server est probablement fermé ou incorrect.",
            258 or -2 => "Délai de connexion dépassé. Le serveur ne répond pas.",
            17142 => "SQL Server n'accepte pas de connexions distantes pour le moment.",
            _ => string.IsNullOrWhiteSpace(exception.Message)
                ? "Échec de la connexion SQL Server."
                : exception.Message
        };
    }
}
