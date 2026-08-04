using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.ParentActivation;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public sealed class ParentActivationTokenConfiguration : IEntityTypeConfiguration<ParentActivationToken>
{
    public void Configure(EntityTypeBuilder<ParentActivationToken> builder)
    {
        builder.ToTable("ParentActivationTokens");
        builder.Property(x => x.SuggestedUserName).HasMaxLength(256);
        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.IsDeleted);
    }
}

public sealed class ParentActivationSessionConfiguration : IEntityTypeConfiguration<ParentActivationSession>
{
    public void Configure(EntityTypeBuilder<ParentActivationSession> builder)
    {
        builder.ToTable("ParentActivationSessions");
        builder.Property(x => x.DeviceId).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.ActivationTokenId);
        builder.HasIndex(x => x.IsDeleted);
    }
}
