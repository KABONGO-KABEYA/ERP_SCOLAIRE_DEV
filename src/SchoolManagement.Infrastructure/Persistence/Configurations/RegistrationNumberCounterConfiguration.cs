using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Students;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class RegistrationNumberCounterConfiguration : AuditableEntityConfiguration<RegistrationNumberCounter>
{
    public override void Configure(EntityTypeBuilder<RegistrationNumberCounter> builder)
    {
        base.Configure(builder);
        builder.ToTable("RegistrationNumberCounters");
        builder.Property(c => c.Year).IsRequired();
        builder.Property(c => c.NextValue).IsRequired();
        builder.HasOne(c => c.School).WithMany().HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.SchoolId, c.Year }).IsUnique();
    }
}
