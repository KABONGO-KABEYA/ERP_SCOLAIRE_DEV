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
    }
}
