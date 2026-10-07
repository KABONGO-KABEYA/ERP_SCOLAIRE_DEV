using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Documents;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public sealed class ParentNoticeConfiguration : AuditableEntityConfiguration<ParentNotice>
{
    public override void Configure(EntityTypeBuilder<ParentNotice> builder)
    {
        base.Configure(builder);
        builder.ToTable("ParentNotices");
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Subject).HasMaxLength(300);
        builder.Property(n => n.TargetDescription).HasMaxLength(500).IsRequired();
        builder.Property(n => n.ContentRtfBase64).IsRequired();
        builder.Property(n => n.SelectedInstallmentIdsJson).IsRequired();
        builder.Property(n => n.PageLayout).HasConversion<int>();
        builder.Property(n => n.PageOrientation).HasConversion<int>();
        builder.Property(n => n.RecipientStudentIdsJson).IsRequired();
        builder.Property(n => n.GeneratedSnapshotJson).IsRequired();
        builder.Property(n => n.Status).HasConversion<int>();
        builder.HasIndex(n => new { n.SchoolId, n.CreatedAt });
        builder.HasIndex(n => new { n.SchoolId, n.Status });
    }
}
