using Microsoft.Data.SqlClient;

namespace SchoolManagement.Application.Configuration.Database;

/// <summary>Construit la chaîne de connexion SQL Server à partir d'un objet de configuration.</summary>
public sealed class DatabaseConnectionFactory
{
    public string BuildConnectionString(DatabaseConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = BuildDataSource(configuration.Serveur, configuration.Port),
            InitialCatalog = configuration.Base,
            Encrypt = SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = true,
            MultipleActiveResultSets = true,
            ConnectTimeout = 15
        };

        switch (configuration.Authentification)
        {
            case DatabaseAuthenticationMode.Windows:
                builder.IntegratedSecurity = true;
                break;
            case DatabaseAuthenticationMode.SqlServer:
            default:
                builder.IntegratedSecurity = false;
                builder.UserID = configuration.Utilisateur;
                builder.Password = configuration.MotDePasse;
                break;
        }

        return builder.ConnectionString;
    }

    private static string BuildDataSource(string server, int port)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            return string.Empty;
        }

        if (port is <= 0 or 1433)
        {
            return server.Trim();
        }

        return $"{server.Trim()},{port}";
    }
}
