namespace SchoolManagement.UpdateAgent;

public static class AgentServiceNames
{
    public const string WindowsServiceName = "ErpScolaireUpdateAgent";

    public const string WindowsServiceDisplayName = "ERP Scolaire Update Agent";

    /// <summary>Compte local dédié — jamais LocalSystem, jamais Administrators.</summary>
    public const string WindowsAccountName = "ErpScolaireUpdateAgent";

    public const string ProgramDataFolder = "ERP_SCOLAIRE";

    public const string DataFolderName = "UpdateAgent";

    public const string BackupsFolderName = "Backups";

    public const string ApiWindowsServiceName = "ErpScolaireApi";
}

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string BootstrapBaseUrl { get; set; } = string.Empty;

    public string Channel { get; set; } = "PROD";

    public bool RunOnce { get; set; }

    public int CheckIntervalHours { get; set; } = 6;

    public string[] AllowedHosts { get; set; } = [];

    public string? DataRoot { get; set; }

    /// <summary>Dossier Api installé. Le swap utilise les frères Api.Previous / Api.Incoming-*.</summary>
    public string? ApiInstallRoot { get; set; }

    /// <summary>false en production tant que le lot n'est pas activé. true dans les tests contrôlés.</summary>
    public bool AutoDeploy { get; set; }

    public string HealthUrl { get; set; } = "http://127.0.0.1:5096/api/health";

    public int HealthIntervalMs { get; set; } = 2000;

    public int HealthBudgetSeconds { get; set; } = 90;

    public int HealthSuccessRequired { get; set; } = 3;

    /// <summary>
    /// Chaîne SQL école. Vide tant que le lot permissions SQL n'est pas validé.
    /// L'agent délègue backup/migration au module Updates ; il n'ouvre pas de connexion ADO.
    /// </summary>
    public string DatabaseConnectionString { get; set; } = string.Empty;

    public string ExpectedDatabaseName { get; set; } = string.Empty;

    public string? BackupRoot { get; set; }

    public long MinFreeDiskBytes { get; set; } = 500_000_000;

    public long MinBackupBytes { get; set; } = 1;

    public int StopTimeoutSeconds { get; set; } = 60;

    public int StartTimeoutSeconds { get; set; } = 60;
}

public sealed class AgentException : Exception
{
    public AgentException(string message) : base(message)
    {
    }

    public AgentException(string message, Exception inner) : base(message, inner)
    {
    }
}
