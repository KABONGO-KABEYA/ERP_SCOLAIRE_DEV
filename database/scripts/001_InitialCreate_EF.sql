IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Announcements] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [TargetAudience] nvarchar(max) NOT NULL,
        [PublishedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NULL,
        [IsPublished] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Announcements] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [UserName] nvarchar(100) NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [EntityName] nvarchar(100) NOT NULL,
        [EntityId] uniqueidentifier NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [CalendarEvents] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NULL,
        [Title] nvarchar(200) NOT NULL,
        [EventType] nvarchar(50) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [Description] nvarchar(max) NULL,
        [IsHoliday] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_CalendarEvents] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Guardians] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Phone] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [Address] nvarchar(max) NULL,
        [Profession] nvarchar(max) NULL,
        [NationalId] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Guardians] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [LoginHistory] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [UserName] nvarchar(100) NOT NULL,
        [IsSuccessful] bit NOT NULL,
        [FailureReason] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [LoginAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_LoginHistory] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [Module] nvarchar(50) NOT NULL,
        [Action] int NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Description] nvarchar(max) NULL,
        [SystemRole] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Schools] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [LegalName] nvarchar(max) NULL,
        [RegistrationNumber] nvarchar(max) NULL,
        [Address] nvarchar(max) NULL,
        [City] nvarchar(max) NULL,
        [Province] nvarchar(max) NULL,
        [Country] nvarchar(max) NULL,
        [Phone] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [Website] nvarchar(max) NULL,
        [LogoPath] nvarchar(max) NULL,
        [DocumentHeader] nvarchar(max) NULL,
        [DefaultCurrency] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Schools] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Students] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [RegistrationNumber] nvarchar(30) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [MiddleName] nvarchar(max) NULL,
        [Gender] int NOT NULL,
        [DateOfBirth] date NOT NULL,
        [PlaceOfBirth] nvarchar(max) NULL,
        [Nationality] nvarchar(max) NULL,
        [Address] nvarchar(max) NULL,
        [Phone] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [PhotoPath] nvarchar(max) NULL,
        [BloodGroup] nvarchar(max) NULL,
        [MedicalNotes] nvarchar(max) NULL,
        [IsArchived] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Students] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Teachers] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [EmployeeNumber] nvarchar(30) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Phone] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [Specialization] nvarchar(max) NULL,
        [HireDate] date NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Teachers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [UserAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [UserName] nvarchar(100) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [Phone] nvarchar(max) NULL,
        [TeacherId] uniqueidentifier NULL,
        [GuardianId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [MustChangePassword] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_UserAccounts] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [Id] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [AcademicYears] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [Label] nvarchar(50) NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [IsCurrent] bit NOT NULL,
        [IsClosed] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_AcademicYears] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AcademicYears_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [AppConfigurations] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_AppConfigurations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AppConfigurations_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Banks] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [AccountNumber] nvarchar(max) NULL,
        [Branch] nvarchar(max) NULL,
        [Currency] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Banks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Banks_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [CashRegisters] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Currency] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_CashRegisters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashRegisters_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [FeeTypes] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [DefaultAmount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [IsMandatory] bit NOT NULL,
        [IsRecurring] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_FeeTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeeTypes_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Sections] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Cycle] int NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Sections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sections_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [StudyOptions] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Cycle] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_StudyOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudyOptions_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [DisciplineRecords] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NULL,
        [IncidentDate] date NOT NULL,
        [IncidentType] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Sanction] nvarchar(max) NULL,
        [ReportedByUserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_DisciplineRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DisciplineRecords_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [MeritRecords] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NULL,
        [AwardDate] date NOT NULL,
        [MeritType] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_MeritRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MeritRecords_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [ReportCards] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NOT NULL,
        [AcademicPeriodId] uniqueidentifier NOT NULL,
        [ReportNumber] nvarchar(50) NOT NULL,
        [GeneratedAt] datetime2 NOT NULL,
        [PdfPath] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_ReportCards] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportCards_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [StudentDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [DocumentType] nvarchar(50) NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [StoragePath] nvarchar(max) NOT NULL,
        [MimeType] nvarchar(max) NULL,
        [FileSizeBytes] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_StudentDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentDocuments_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [StudentGuardians] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [GuardianId] uniqueidentifier NOT NULL,
        [Relationship] nvarchar(50) NOT NULL,
        [IsPrimary] bit NOT NULL,
        [CanPickup] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_StudentGuardians] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentGuardians_Guardians_GuardianId] FOREIGN KEY ([GuardianId]) REFERENCES [Guardians] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StudentGuardians_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [StudentStatusHistory] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NULL,
        [PreviousStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [EffectiveDate] date NOT NULL,
        [Reason] nvarchar(max) NULL,
        [DestinationSchool] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_StudentStatusHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentStatusHistory_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [TeacherAttendances] (
        [Id] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [AttendanceDate] date NOT NULL,
        [IsPresent] bit NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_TeacherAttendances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeacherAttendances_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Token] nvarchar(500) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [IsRevoked] bit NOT NULL,
        [RevokedAt] datetime2 NULL,
        [ReplacedByToken] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_UserAccounts_UserId] FOREIGN KEY ([UserId]) REFERENCES [UserAccounts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [UserRoleAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_UserRoleAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserRoleAssignments_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserRoleAssignments_UserAccounts_UserId] FOREIGN KEY ([UserId]) REFERENCES [UserAccounts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [AcademicPeriods] (
        [Id] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [PeriodType] int NOT NULL,
        [OrderIndex] int NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NOT NULL,
        [IsClosed] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_AcademicPeriods] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AcademicPeriods_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NOT NULL,
        [CashRegisterId] uniqueidentifier NOT NULL,
        [BankId] uniqueidentifier NULL,
        [ReceiptNumber] nvarchar(50) NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [Status] int NOT NULL,
        [PaymentMethod] nvarchar(max) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [ReceivedByUserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Banks_BankId] FOREIGN KEY ([BankId]) REFERENCES [Banks] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Payments_CashRegisters_CashRegisterId] FOREIGN KEY ([CashRegisterId]) REFERENCES [CashRegisters] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Payments_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [StudentFeeBalances] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NOT NULL,
        [FeeTypeId] uniqueidentifier NOT NULL,
        [AmountDue] decimal(18,2) NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_StudentFeeBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentFeeBalances_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StudentFeeBalances_FeeTypes_FeeTypeId] FOREIGN KEY ([FeeTypeId]) REFERENCES [FeeTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StudentFeeBalances_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [ClassRooms] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NOT NULL,
        [SectionId] uniqueidentifier NOT NULL,
        [StudyOptionId] uniqueidentifier NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Level] int NOT NULL,
        [MaxCapacity] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_ClassRooms] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ClassRooms_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassRooms_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassRooms_Sections_SectionId] FOREIGN KEY ([SectionId]) REFERENCES [Sections] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClassRooms_StudyOptions_StudyOptionId] FOREIGN KEY ([StudyOptionId]) REFERENCES [StudyOptions] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [PeriodResults] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NOT NULL,
        [AcademicPeriodId] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NOT NULL,
        [Average] decimal(5,2) NOT NULL,
        [Percentage] decimal(5,2) NOT NULL,
        [Rank] int NOT NULL,
        [ClassSize] int NOT NULL,
        [Appreciation] nvarchar(max) NULL,
        [CouncilDecision] int NOT NULL,
        [IsPublished] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_PeriodResults] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PeriodResults_AcademicPeriods_AcademicPeriodId] FOREIGN KEY ([AcademicPeriodId]) REFERENCES [AcademicPeriods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PeriodResults_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [CashMovements] (
        [Id] uniqueidentifier NOT NULL,
        [CashRegisterId] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NULL,
        [MovementDate] datetime2 NOT NULL,
        [MovementType] nvarchar(max) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [BalanceAfter] decimal(18,2) NOT NULL,
        [Description] nvarchar(max) NULL,
        [UserId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_CashMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CashMovements_CashRegisters_CashRegisterId] FOREIGN KEY ([CashRegisterId]) REFERENCES [CashRegisters] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CashMovements_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE SET NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentLines] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NOT NULL,
        [FeeTypeId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] int NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_PaymentLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentLines_FeeTypes_FeeTypeId] FOREIGN KEY ([FeeTypeId]) REFERENCES [FeeTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentLines_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentReversals] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [ReversedAt] datetime2 NOT NULL,
        [ReversedByUserId] uniqueidentifier NOT NULL,
        [ApprovedByUserId] uniqueidentifier NULL,
        [IsApproved] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_PaymentReversals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentReversals_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Courses] (
        [Id] uniqueidentifier NOT NULL,
        [SchoolId] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Coefficient] decimal(5,2) NOT NULL,
        [MaxScore] int NOT NULL,
        [IsOptional] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Courses_ClassRooms_ClassRoomId] FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Courses_Schools_SchoolId] FOREIGN KEY ([SchoolId]) REFERENCES [Schools] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Enrollments] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NOT NULL,
        [Status] int NOT NULL,
        [EnrollmentDate] date NOT NULL,
        [EndDate] date NULL,
        [Notes] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Enrollments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Enrollments_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Enrollments_ClassRooms_ClassRoomId] FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Enrollments_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [StudentAttendances] (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NOT NULL,
        [CourseAssignmentId] uniqueidentifier NULL,
        [AttendanceDate] date NOT NULL,
        [IsPresent] bit NOT NULL,
        [IsLate] bit NOT NULL,
        [Justification] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_StudentAttendances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StudentAttendances_ClassRooms_ClassRoomId] FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StudentAttendances_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [CourseAssignments] (
        [Id] uniqueidentifier NOT NULL,
        [TeacherId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_CourseAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseAssignments_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CourseAssignments_ClassRooms_ClassRoomId] FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CourseAssignments_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CourseAssignments_Teachers_TeacherId] FOREIGN KEY ([TeacherId]) REFERENCES [Teachers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [Evaluations] (
        [Id] uniqueidentifier NOT NULL,
        [AcademicYearId] uniqueidentifier NOT NULL,
        [AcademicPeriodId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [ClassRoomId] uniqueidentifier NOT NULL,
        [CourseAssignmentId] uniqueidentifier NULL,
        [Title] nvarchar(150) NOT NULL,
        [EvaluationType] int NOT NULL,
        [Weight] decimal(5,2) NOT NULL,
        [MaxScore] int NOT NULL,
        [EvaluationDate] date NOT NULL,
        [IsOpen] bit NOT NULL,
        [IsPublished] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_Evaluations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Evaluations_AcademicPeriods_AcademicPeriodId] FOREIGN KEY ([AcademicPeriodId]) REFERENCES [AcademicPeriods] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Evaluations_AcademicYears_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [AcademicYears] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Evaluations_ClassRooms_ClassRoomId] FOREIGN KEY ([ClassRoomId]) REFERENCES [ClassRooms] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Evaluations_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [ReportCardDetails] (
        [Id] uniqueidentifier NOT NULL,
        [ReportCardId] uniqueidentifier NOT NULL,
        [PeriodResultId] uniqueidentifier NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [Average] decimal(5,2) NOT NULL,
        [Coefficient] decimal(5,2) NOT NULL,
        [TeacherComment] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_ReportCardDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReportCardDetails_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ReportCardDetails_PeriodResults_PeriodResultId] FOREIGN KEY ([PeriodResultId]) REFERENCES [PeriodResults] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ReportCardDetails_ReportCards_ReportCardId] FOREIGN KEY ([ReportCardId]) REFERENCES [ReportCards] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [ScheduleSlots] (
        [Id] uniqueidentifier NOT NULL,
        [CourseAssignmentId] uniqueidentifier NOT NULL,
        [DayOfWeek] int NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [Room] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_ScheduleSlots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ScheduleSlots_CourseAssignments_CourseAssignmentId] FOREIGN KEY ([CourseAssignmentId]) REFERENCES [CourseAssignments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE TABLE [GradeEntries] (
        [Id] uniqueidentifier NOT NULL,
        [EvaluationId] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [Score] decimal(5,2) NOT NULL,
        [Comment] nvarchar(max) NULL,
        [IsAbsent] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] uniqueidentifier NULL,
        CONSTRAINT [PK_GradeEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GradeEntries_Evaluations_EvaluationId] FOREIGN KEY ([EvaluationId]) REFERENCES [Evaluations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GradeEntries_Students_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [Students] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AcademicPeriods_AcademicYearId_OrderIndex] ON [AcademicPeriods] ([AcademicYearId], [OrderIndex]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AcademicPeriods_IsDeleted] ON [AcademicPeriods] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AcademicYears_IsDeleted] ON [AcademicYears] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_AcademicYears_SchoolId_IsCurrent] ON [AcademicYears] ([SchoolId], [IsCurrent]) WHERE [IsCurrent] = 1 AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AcademicYears_SchoolId_Label] ON [AcademicYears] ([SchoolId], [Label]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Announcements_IsDeleted] ON [Announcements] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Announcements_SchoolId_PublishedAt] ON [Announcements] ([SchoolId], [PublishedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AppConfigurations_IsDeleted] ON [AppConfigurations] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AppConfigurations_SchoolId_Key] ON [AppConfigurations] ([SchoolId], [Key]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_EntityName_EntityId] ON [AuditEntries] ([EntityName], [EntityId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_IsDeleted] ON [AuditEntries] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_Timestamp] ON [AuditEntries] ([Timestamp]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Banks_IsDeleted] ON [Banks] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Banks_SchoolId] ON [Banks] ([SchoolId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CalendarEvents_IsDeleted] ON [CalendarEvents] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CalendarEvents_SchoolId_StartDate] ON [CalendarEvents] ([SchoolId], [StartDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashMovements_CashRegisterId_MovementDate] ON [CashMovements] ([CashRegisterId], [MovementDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashMovements_IsDeleted] ON [CashMovements] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashMovements_PaymentId] ON [CashMovements] ([PaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CashRegisters_IsDeleted] ON [CashRegisters] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CashRegisters_SchoolId_Code] ON [CashRegisters] ([SchoolId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClassRooms_AcademicYearId_Code] ON [ClassRooms] ([AcademicYearId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ClassRooms_IsDeleted] ON [ClassRooms] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ClassRooms_SchoolId] ON [ClassRooms] ([SchoolId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ClassRooms_SectionId] ON [ClassRooms] ([SectionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ClassRooms_StudyOptionId] ON [ClassRooms] ([StudyOptionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseAssignments_AcademicYearId] ON [CourseAssignments] ([AcademicYearId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseAssignments_ClassRoomId] ON [CourseAssignments] ([ClassRoomId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseAssignments_CourseId] ON [CourseAssignments] ([CourseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseAssignments_IsDeleted] ON [CourseAssignments] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CourseAssignments_TeacherId_CourseId_ClassRoomId_AcademicYearId] ON [CourseAssignments] ([TeacherId], [CourseId], [ClassRoomId], [AcademicYearId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_ClassRoomId] ON [Courses] ([ClassRoomId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_IsDeleted] ON [Courses] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_SchoolId_Code] ON [Courses] ([SchoolId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DisciplineRecords_IsDeleted] ON [DisciplineRecords] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DisciplineRecords_StudentId_IncidentDate] ON [DisciplineRecords] ([StudentId], [IncidentDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enrollments_AcademicYearId_ClassRoomId] ON [Enrollments] ([AcademicYearId], [ClassRoomId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enrollments_ClassRoomId] ON [Enrollments] ([ClassRoomId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enrollments_IsDeleted] ON [Enrollments] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Enrollments_StudentId_AcademicYearId_IsActive] ON [Enrollments] ([StudentId], [AcademicYearId], [IsActive]) WHERE [IsActive] = 1 AND [IsDeleted] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Evaluations_AcademicPeriodId] ON [Evaluations] ([AcademicPeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Evaluations_AcademicYearId] ON [Evaluations] ([AcademicYearId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Evaluations_ClassRoomId_AcademicPeriodId] ON [Evaluations] ([ClassRoomId], [AcademicPeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Evaluations_CourseId] ON [Evaluations] ([CourseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Evaluations_IsDeleted] ON [Evaluations] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FeeTypes_IsDeleted] ON [FeeTypes] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FeeTypes_SchoolId_Code] ON [FeeTypes] ([SchoolId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GradeEntries_EvaluationId_StudentId] ON [GradeEntries] ([EvaluationId], [StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GradeEntries_IsDeleted] ON [GradeEntries] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GradeEntries_StudentId] ON [GradeEntries] ([StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Guardians_IsDeleted] ON [Guardians] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Guardians_SchoolId_LastName_FirstName] ON [Guardians] ([SchoolId], [LastName], [FirstName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LoginHistory_IsDeleted] ON [LoginHistory] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LoginHistory_LoginAt] ON [LoginHistory] ([LoginAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LoginHistory_UserId_LoginAt] ON [LoginHistory] ([UserId], [LoginAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MeritRecords_IsDeleted] ON [MeritRecords] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MeritRecords_StudentId] ON [MeritRecords] ([StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentLines_FeeTypeId] ON [PaymentLines] ([FeeTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentLines_IsDeleted] ON [PaymentLines] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentLines_PaymentId] ON [PaymentLines] ([PaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentReversals_IsDeleted] ON [PaymentReversals] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentReversals_PaymentId] ON [PaymentReversals] ([PaymentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_AcademicYearId] ON [Payments] ([AcademicYearId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_BankId] ON [Payments] ([BankId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_CashRegisterId] ON [Payments] ([CashRegisterId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_IsDeleted] ON [Payments] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_PaymentDate_SchoolId] ON [Payments] ([PaymentDate], [SchoolId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_ReceiptNumber] ON [Payments] ([ReceiptNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_StudentId_AcademicYearId] ON [Payments] ([StudentId], [AcademicYearId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PeriodResults_AcademicPeriodId] ON [PeriodResults] ([AcademicPeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PeriodResults_ClassRoomId_AcademicPeriodId_Rank] ON [PeriodResults] ([ClassRoomId], [AcademicPeriodId], [Rank]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PeriodResults_IsDeleted] ON [PeriodResults] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PeriodResults_StudentId_AcademicPeriodId] ON [PeriodResults] ([StudentId], [AcademicPeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Code] ON [Permissions] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Permissions_IsDeleted] ON [Permissions] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_IsDeleted] ON [RefreshTokens] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReportCardDetails_CourseId] ON [ReportCardDetails] ([CourseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReportCardDetails_IsDeleted] ON [ReportCardDetails] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReportCardDetails_PeriodResultId] ON [ReportCardDetails] ([PeriodResultId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReportCardDetails_ReportCardId] ON [ReportCardDetails] ([ReportCardId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReportCards_IsDeleted] ON [ReportCards] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReportCards_ReportNumber] ON [ReportCards] ([ReportNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReportCards_StudentId_AcademicPeriodId] ON [ReportCards] ([StudentId], [AcademicPeriodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_IsDeleted] ON [RolePermissions] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_PermissionId] ON [RolePermissions] ([RoleId], [PermissionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Roles_IsDeleted] ON [Roles] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_SchoolId_Code] ON [Roles] ([SchoolId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScheduleSlots_CourseAssignmentId_DayOfWeek_StartTime] ON [ScheduleSlots] ([CourseAssignmentId], [DayOfWeek], [StartTime]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ScheduleSlots_IsDeleted] ON [ScheduleSlots] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Schools_IsDeleted] ON [Schools] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Schools_Name] ON [Schools] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sections_IsDeleted] ON [Sections] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Sections_SchoolId_Code] ON [Sections] ([SchoolId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentAttendances_ClassRoomId_AttendanceDate] ON [StudentAttendances] ([ClassRoomId], [AttendanceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentAttendances_IsDeleted] ON [StudentAttendances] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentAttendances_StudentId_AttendanceDate] ON [StudentAttendances] ([StudentId], [AttendanceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentDocuments_IsDeleted] ON [StudentDocuments] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentDocuments_StudentId] ON [StudentDocuments] ([StudentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentFeeBalances_AcademicYearId] ON [StudentFeeBalances] ([AcademicYearId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentFeeBalances_FeeTypeId] ON [StudentFeeBalances] ([FeeTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentFeeBalances_IsDeleted] ON [StudentFeeBalances] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudentFeeBalances_StudentId_AcademicYearId_FeeTypeId] ON [StudentFeeBalances] ([StudentId], [AcademicYearId], [FeeTypeId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentGuardians_GuardianId] ON [StudentGuardians] ([GuardianId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentGuardians_IsDeleted] ON [StudentGuardians] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudentGuardians_StudentId_GuardianId] ON [StudentGuardians] ([StudentId], [GuardianId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Students_IsDeleted] ON [Students] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Students_LastName_FirstName] ON [Students] ([LastName], [FirstName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Students_SchoolId_RegistrationNumber] ON [Students] ([SchoolId], [RegistrationNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentStatusHistory_IsDeleted] ON [StudentStatusHistory] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudentStatusHistory_StudentId_EffectiveDate] ON [StudentStatusHistory] ([StudentId], [EffectiveDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StudyOptions_IsDeleted] ON [StudyOptions] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StudyOptions_SchoolId_Code] ON [StudyOptions] ([SchoolId], [Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TeacherAttendances_IsDeleted] ON [TeacherAttendances] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeacherAttendances_TeacherId_AttendanceDate] ON [TeacherAttendances] ([TeacherId], [AttendanceDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Teachers_IsDeleted] ON [Teachers] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Teachers_SchoolId_EmployeeNumber] ON [Teachers] ([SchoolId], [EmployeeNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserAccounts_IsDeleted] ON [UserAccounts] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserAccounts_SchoolId_Email] ON [UserAccounts] ([SchoolId], [Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserAccounts_SchoolId_UserName] ON [UserAccounts] ([SchoolId], [UserName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoleAssignments_IsDeleted] ON [UserRoleAssignments] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoleAssignments_RoleId] ON [UserRoleAssignments] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserRoleAssignments_UserId_RoleId] ON [UserRoleAssignments] ([UserId], [RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706114538_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260706114538_InitialCreate', N'8.0.11');
END;
GO

COMMIT;
GO

