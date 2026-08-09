using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Sync;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class SyncOutboxUnitConfiguration : AuditableEntityConfiguration<SyncOutboxUnit>
{
    public override void Configure(EntityTypeBuilder<SyncOutboxUnit> builder)
    {
        base.Configure(builder);
        builder.ToTable("SyncOutboxUnit");
        builder.Property(u => u.AggregateType).HasMaxLength(80).IsRequired();
        builder.Property(u => u.Priority).HasConversion<int>();
        builder.Property(u => u.Status).HasConversion<int>();
        builder.Property(u => u.LastError).HasMaxLength(2000);
        builder.HasOne<School>().WithMany().HasForeignKey(u => u.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => new { u.SchoolId, u.Status, u.Priority, u.CreatedAt });
        builder.HasIndex(u => new { u.AggregateType, u.AggregateId });
        builder.HasMany(u => u.Items).WithOne(i => i.Unit).HasForeignKey(i => i.UnitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SyncOutboxItemConfiguration : AuditableEntityConfiguration<SyncOutboxItem>
{
    public override void Configure(EntityTypeBuilder<SyncOutboxItem> builder)
    {
        base.Configure(builder);
        builder.ToTable("SyncOutboxItem");
        builder.Property(i => i.TableName).HasMaxLength(128).IsRequired();
        builder.Property(i => i.Operation).HasConversion<int>();
        builder.Property(i => i.Status).HasConversion<int>();
        builder.Property(i => i.LastError).HasMaxLength(2000);
        builder.HasIndex(i => new { i.UnitId, i.Sequence });
        builder.HasIndex(i => new { i.TableName, i.EntityId, i.Status });
    }
}

public class SyncJournalEntryConfiguration : AuditableEntityConfiguration<SyncJournalEntry>
{
    public override void Configure(EntityTypeBuilder<SyncJournalEntry> builder)
    {
        base.Configure(builder);
        builder.ToTable("SyncJournal");
        builder.Property(j => j.TablesTouched).HasMaxLength(2000);
        builder.Property(j => j.ErrorSummary).HasMaxLength(4000);
        builder.HasOne<School>().WithMany().HasForeignKey(j => j.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(j => new { j.SchoolId, j.StartedAt });
    }
}

public class SyncWatermarkConfiguration : AuditableEntityConfiguration<SyncWatermark>
{
    public override void Configure(EntityTypeBuilder<SyncWatermark> builder)
    {
        base.Configure(builder);
        builder.ToTable("SyncWatermark");
        builder.Property(w => w.TableName).HasMaxLength(128).IsRequired();
        builder.HasOne<School>().WithMany().HasForeignKey(w => w.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(w => new { w.SchoolId, w.TableName }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
