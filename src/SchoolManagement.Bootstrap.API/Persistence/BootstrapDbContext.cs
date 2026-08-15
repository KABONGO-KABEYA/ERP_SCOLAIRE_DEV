using Microsoft.EntityFrameworkCore;
using SchoolManagement.Bootstrap.API.Persistence.Entities;

namespace SchoolManagement.Bootstrap.API.Persistence;

public sealed class BootstrapDbContext : DbContext
{
    public BootstrapDbContext(DbContextOptions<BootstrapDbContext> options)
        : base(options)
    {
    }

    public DbSet<BootstrapSchoolRegistryEntry> SchoolRegistry => Set<BootstrapSchoolRegistryEntry>();

    public DbSet<BootstrapSchoolEstablishmentCredential> EstablishmentCredentials =>
        Set<BootstrapSchoolEstablishmentCredential>();

    public DbSet<BootstrapEstablishmentSession> EstablishmentSessions =>
        Set<BootstrapEstablishmentSession>();

    public DbSet<UpdateRelease> UpdateReleases => Set<UpdateRelease>();

    public DbSet<UpdateReleaseArtifact> UpdateReleaseArtifacts => Set<UpdateReleaseArtifact>();

    public DbSet<UpdateReleaseTarget> UpdateReleaseTargets => Set<UpdateReleaseTarget>();

    public DbSet<UpdateAgentCredential> UpdateAgentCredentials => Set<UpdateAgentCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BootstrapSchoolRegistryEntry>(entity =>
        {
            entity.ToTable("BootstrapSchoolRegistry");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SchoolName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ActivationBaseUrl).HasMaxLength(500).IsRequired();
            entity.Property(e => e.CloudBaseUrl).HasMaxLength(500).IsRequired();
            entity.Property(e => e.PublicKeyFingerprint).HasMaxLength(128);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => e.SchoolId).IsUnique().HasDatabaseName("UX_BootstrapSchoolRegistry_SchoolId");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_BootstrapSchoolRegistry_IsActive");
        });

        modelBuilder.Entity<BootstrapSchoolEstablishmentCredential>(entity =>
        {
            entity.ToTable("BootstrapSchoolEstablishmentCredentials");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.SecretHash).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.RevokedReason).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(128);

            entity.HasOne(e => e.School)
                .WithMany(s => s.Credentials)
                .HasForeignKey(e => e.SchoolId)
                .HasPrincipalKey(s => s.SchoolId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SchoolId, e.CredentialVersion })
                .IsUnique()
                .HasDatabaseName("IX_EstablishmentCredential_SchoolId_Version");

            // Au plus un credential Active par école.
            entity.HasIndex(e => e.SchoolId)
                .IsUnique()
                .HasFilter($"[{nameof(BootstrapSchoolEstablishmentCredential.Status)}] = '{EstablishmentCredentialStatuses.Active}'")
                .HasDatabaseName("UX_EstablishmentCredential_Active");
        });

        modelBuilder.Entity<BootstrapEstablishmentSession>(entity =>
        {
            entity.ToTable("BootstrapEstablishmentSessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeviceId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();

            entity.HasOne(e => e.School)
                .WithMany(s => s.Sessions)
                .HasForeignKey(e => e.SchoolId)
                .HasPrincipalKey(s => s.SchoolId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Credential)
                .WithMany()
                .HasForeignKey(e => e.CredentialId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.DeviceId, e.Status })
                .HasDatabaseName("IX_Session_Device_Status");
        });

        modelBuilder.Entity<UpdateRelease>(entity =>
        {
            entity.ToTable("UpdateRelease", table =>
            {
                table.HasCheckConstraint("CK_UpdateRelease_Channel", "[Channel] IN (N'DEV', N'PROD')");
                table.HasCheckConstraint("CK_UpdateRelease_Status", "[Status] IN (N'Draft', N'Published', N'Blocked')");
                table.HasCheckConstraint(
                    "CK_UpdateRelease_SchemaRange",
                    "[FromSchemaVersion] >= 1 AND [SchemaVersion] >= [FromSchemaVersion]");
            });
            entity.HasKey(e => e.ReleaseId);
            entity.Property(e => e.Version).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Channel).HasMaxLength(16).IsRequired();
            entity.Property(e => e.MinimumDesktopVersion).HasMaxLength(64).IsRequired();
            entity.Property(e => e.MinimumApiVersion).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(16).IsRequired();
            entity.Property(e => e.ReleaseNotes).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(e => e.BlockedReason).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(128);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.Channel, e.Version })
                .IsUnique()
                .HasDatabaseName("UX_UpdateRelease_Channel_Version");
            entity.HasIndex(e => new { e.Channel, e.Status })
                .HasDatabaseName("IX_UpdateRelease_Channel_Status");
        });

        modelBuilder.Entity<UpdateReleaseArtifact>(entity =>
        {
            entity.ToTable("UpdateReleaseArtifact", table =>
            {
                table.HasCheckConstraint(
                    "CK_UpdateReleaseArtifact_Type",
                    "[Type] IN (N'Desktop', N'Api', N'Migration', N'Mobile')");
                table.HasCheckConstraint(
                    "CK_UpdateReleaseArtifact_Sha256",
                    "LEN([Sha256]) = 64 AND [Sha256] NOT LIKE N'%[^0-9a-f]%'");
            });
            entity.HasKey(e => e.ArtifactId);
            entity.Property(e => e.Type).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Version).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Url).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Sha256).HasMaxLength(64).IsRequired().IsFixedLength();
            entity.Property(e => e.Signature).HasMaxLength(1024);
            entity.HasOne(e => e.Release)
                .WithMany(r => r.Artifacts)
                .HasForeignKey(e => e.ReleaseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.ReleaseId, e.Type })
                .IsUnique()
                .HasDatabaseName("UX_UpdateReleaseArtifact_Release_Type");
        });

        modelBuilder.Entity<UpdateReleaseTarget>(entity =>
        {
            entity.ToTable("UpdateReleaseTarget");
            entity.HasKey(e => e.TargetId);
            entity.HasOne(e => e.Release)
                .WithMany(r => r.Targets)
                .HasForeignKey(e => e.ReleaseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.School)
                .WithMany()
                .HasForeignKey(e => e.SchoolId)
                .HasPrincipalKey(s => s.SchoolId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            entity.HasIndex(e => new { e.ReleaseId, e.SchoolId })
                .IsUnique()
                .HasFilter("[SchoolId] IS NOT NULL")
                .HasDatabaseName("UX_UpdateReleaseTarget_Release_School");
            entity.HasIndex(e => e.ReleaseId)
                .IsUnique()
                .HasFilter("[SchoolId] IS NULL")
                .HasDatabaseName("UX_UpdateReleaseTarget_Release_Global");
        });

        modelBuilder.Entity<UpdateAgentCredential>(entity =>
        {
            entity.ToTable("UpdateAgentCredential");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SecretHash).HasMaxLength(64).IsRequired().IsFixedLength();
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.RevokedReason).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(128);

            entity.HasOne(e => e.School)
                .WithMany()
                .HasForeignKey(e => e.SchoolId)
                .HasPrincipalKey(s => s.SchoolId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.SchoolId, e.CredentialVersion })
                .IsUnique()
                .HasDatabaseName("IX_UpdateAgentCredential_SchoolId_Version");

            entity.HasIndex(e => e.SchoolId)
                .IsUnique()
                .HasFilter($"[{nameof(UpdateAgentCredential.Status)}] = '{UpdateAgentCredentialStatuses.Active}'")
                .HasDatabaseName("UX_UpdateAgentCredential_Active");
        });
    }
}
