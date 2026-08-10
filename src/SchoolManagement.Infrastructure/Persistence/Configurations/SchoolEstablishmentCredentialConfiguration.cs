using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.SchoolEstablishment;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public sealed class SchoolEstablishmentCredentialConfiguration
    : IEntityTypeConfiguration<SchoolEstablishmentCredential>
{
    public void Configure(EntityTypeBuilder<SchoolEstablishmentCredential> builder)
    {
        builder.ToTable("SchoolEstablishmentCredentials");
        builder.Property(x => x.TokenType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SecretHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RevokedReason).HasMaxLength(500);
        builder.Property(x => x.BootstrapSyncStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.LastBootstrapSyncError).HasMaxLength(1000);

        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => new { x.SchoolId, x.CredentialVersion }).IsUnique();
        builder.HasIndex(x => x.SchoolId)
            .IsUnique()
            .HasFilter($"[{nameof(SchoolEstablishmentCredential.Status)}] = '{SchoolEstablishmentCredentialStatuses.Active}'")
            .HasDatabaseName("UX_SchoolEstablishmentCredential_Active");
        builder.HasIndex(x => x.BootstrapSyncPending);
        builder.HasIndex(x => x.IsDeleted);
    }
}
