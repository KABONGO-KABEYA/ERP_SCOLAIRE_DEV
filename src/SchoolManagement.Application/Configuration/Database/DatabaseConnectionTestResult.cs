namespace SchoolManagement.Application.Configuration.Database;

/// <summary>Résultat d'un test de connexion SQL Server.</summary>
public sealed class DatabaseConnectionTestResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public static DatabaseConnectionTestResult Success() =>
        new() { IsSuccess = true, Message = "Connexion réussie." };

    public static DatabaseConnectionTestResult Failure(string message) =>
        new() { IsSuccess = false, Message = message };
}
