using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public sealed class SchoolSubscriptionConfiguration : AuditableEntityConfiguration<SchoolSubscription>
{
    public override void Configure(EntityTypeBuilder<SchoolSubscription> builder)
    {
        base.Configure(builder);
        builder.ToTable("SchoolSubscriptions");
        builder.Property(s => s.InstallationDate).IsRequired();
        builder.Property(s => s.DurationMonths).IsRequired();
        builder.Property(s => s.ExpirationDate).IsRequired();
        builder.Property(s => s.IsActive).IsRequired();
        builder.HasOne(s => s.School)
            .WithMany()
            .HasForeignKey(s => s.SchoolId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.SchoolId)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_SchoolSubscriptions_SchoolId");
    }
}
