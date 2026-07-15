namespace SchoolManagement.Application.Configuration.Database;

/// <summary>
/// Mode d'authentification SQL Server.
/// Seul SQL Server est supporté en v1 ; Windows est prévu pour extension future.
/// </summary>
public enum DatabaseAuthenticationMode
{
    SqlServer,
    Windows
}
