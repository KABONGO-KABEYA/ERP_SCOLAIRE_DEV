using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Notifications;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class SchoolNotificationConfiguration : AuditableEntityConfiguration<SchoolNotification>
{
    public override void Configure(EntityTypeBuilder<SchoolNotification> builder)
    {
        base.Configure(builder);
        builder.ToTable("SchoolNotifications");
        builder.Property(n => n.Category).HasConversion<int>();
        builder.Property(n => n.EventType).HasConversion<int>();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.DataJson).HasMaxLength(4000);
        builder.Property(n => n.DeepLink).HasMaxLength(500);
        builder.HasIndex(n => new { n.SchoolId, n.OccurredAt });
        builder.HasIndex(n => new { n.SchoolId, n.StudentId, n.OccurredAt });
        builder.HasIndex(n => new { n.SchoolId, n.Category, n.OccurredAt });
    }
}

public class NotificationRecipientConfiguration : AuditableEntityConfiguration<NotificationRecipient>
{
    public override void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        base.Configure(builder);
        builder.ToTable("NotificationRecipients");
        builder.HasOne(r => r.Notification)
            .WithMany(n => n.Recipients)
            .HasForeignKey(r => r.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.UserAccountId, r.IsRead, r.CreatedAt });
        builder.HasIndex(r => new { r.NotificationId, r.UserAccountId }).IsUnique();
    }
}

public class ParentDeviceTokenConfiguration : AuditableEntityConfiguration<ParentDeviceToken>
{
    public override void Configure(EntityTypeBuilder<ParentDeviceToken> builder)
    {
        base.Configure(builder);
        builder.ToTable("ParentDeviceTokens");
        builder.Property(t => t.Token).HasMaxLength(512).IsRequired();
        builder.Property(t => t.Platform).HasMaxLength(20).IsRequired();
        builder.HasIndex(t => new { t.UserAccountId, t.Token }).IsUnique();
        builder.HasIndex(t => t.Token);
    }
}
