using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SchoolManagement.Infrastructure.Persistence;

/// <summary>Crée les tables du moteur de notifications parent.</summary>
public sealed class NotificationSchemaInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<NotificationSchemaInitializer> _logger;

    public NotificationSchemaInitializer(
        string connectionString,
        ILogger<NotificationSchemaInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.SchoolNotifications', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SchoolNotifications
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SchoolNotifications PRIMARY KEY,
                    SchoolId UNIQUEIDENTIFIER NOT NULL,
                    StudentId UNIQUEIDENTIFIER NULL,
                    Category INT NOT NULL,
                    EventType INT NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Body NVARCHAR(2000) NOT NULL,
                    DataJson NVARCHAR(4000) NULL,
                    DeepLink NVARCHAR(500) NULL,
                    OccurredAt DATETIME2 NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_SchoolNotifications_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL
                );
                CREATE INDEX IX_SchoolNotifications_School_Occurred
                    ON dbo.SchoolNotifications(SchoolId, OccurredAt DESC);
                CREATE INDEX IX_SchoolNotifications_School_Student_Occurred
                    ON dbo.SchoolNotifications(SchoolId, StudentId, OccurredAt DESC);
                CREATE INDEX IX_SchoolNotifications_School_Category_Occurred
                    ON dbo.SchoolNotifications(SchoolId, Category, OccurredAt DESC);
                CREATE INDEX IX_SchoolNotifications_IsDeleted
                    ON dbo.SchoolNotifications(IsDeleted);
            END
            """, cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.NotificationRecipients', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.NotificationRecipients
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_NotificationRecipients PRIMARY KEY,
                    NotificationId UNIQUEIDENTIFIER NOT NULL,
                    UserAccountId UNIQUEIDENTIFIER NOT NULL,
                    GuardianId UNIQUEIDENTIFIER NULL,
                    IsRead BIT NOT NULL CONSTRAINT DF_NotificationRecipients_IsRead DEFAULT(0),
                    ReadAt DATETIME2 NULL,
                    DeliveredAt DATETIME2 NULL,
                    PushSentAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_NotificationRecipients_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL,
                    CONSTRAINT FK_NotificationRecipients_Notification
                        FOREIGN KEY (NotificationId) REFERENCES dbo.SchoolNotifications(Id) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IX_NotificationRecipients_Notification_User
                    ON dbo.NotificationRecipients(NotificationId, UserAccountId)
                    WHERE IsDeleted = 0;
                CREATE INDEX IX_NotificationRecipients_User_Read_Created
                    ON dbo.NotificationRecipients(UserAccountId, IsRead, CreatedAt DESC);
                CREATE INDEX IX_NotificationRecipients_IsDeleted
                    ON dbo.NotificationRecipients(IsDeleted);
            END
            """, cancellationToken);

        await ExecAsync(connection, """
            IF OBJECT_ID(N'dbo.ParentDeviceTokens', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ParentDeviceTokens
                (
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ParentDeviceTokens PRIMARY KEY,
                    SchoolId UNIQUEIDENTIFIER NOT NULL,
                    UserAccountId UNIQUEIDENTIFIER NOT NULL,
                    Token NVARCHAR(512) NOT NULL,
                    Platform NVARCHAR(20) NOT NULL,
                    LastSeenAt DATETIME2 NOT NULL,
                    CreatedAt DATETIME2 NOT NULL,
                    CreatedBy UNIQUEIDENTIFIER NULL,
                    UpdatedAt DATETIME2 NULL,
                    UpdatedBy UNIQUEIDENTIFIER NULL,
                    IsDeleted BIT NOT NULL CONSTRAINT DF_ParentDeviceTokens_IsDeleted DEFAULT(0),
                    DeletedAt DATETIME2 NULL,
                    DeletedBy UNIQUEIDENTIFIER NULL
                );
                CREATE UNIQUE INDEX IX_ParentDeviceTokens_User_Token
                    ON dbo.ParentDeviceTokens(UserAccountId, Token)
                    WHERE IsDeleted = 0;
                CREATE INDEX IX_ParentDeviceTokens_Token
                    ON dbo.ParentDeviceTokens(Token);
                CREATE INDEX IX_ParentDeviceTokens_IsDeleted
                    ON dbo.ParentDeviceTokens(IsDeleted);
            END
            """, cancellationToken);

        _logger.LogInformation("Schéma notifications parent vérifié.");
    }

    private static async Task ExecAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
