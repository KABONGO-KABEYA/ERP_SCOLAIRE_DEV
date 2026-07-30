using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.System;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public sealed class ApplicationVersionConfiguration : IEntityTypeConfiguration<ApplicationVersion>
{
    public void Configure(EntityTypeBuilder<ApplicationVersion> builder)
    {
        builder.ToTable("ApplicationVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).HasMaxLength(32).IsRequired();
        builder.Property(x => x.MinimumVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReleaseNotes).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.DesktopUrl).HasMaxLength(1000);
        builder.Property(x => x.MobileUrl).HasMaxLength(1000);
        builder.Property(x => x.Sha256).HasMaxLength(128);
        builder.HasIndex(x => x.Active);
        builder.HasIndex(x => x.Version);
    }
}
