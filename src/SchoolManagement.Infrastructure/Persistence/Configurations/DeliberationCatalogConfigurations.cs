using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Deliberation;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class ResultMentionDefinitionConfiguration : AuditableEntityConfiguration<ResultMentionDefinition>
{
    public override void Configure(EntityTypeBuilder<ResultMentionDefinition> builder)
    {
        base.Configure(builder);
        builder.ToTable("ResultMentionDefinitions");
        builder.Property(m => m.Label).HasMaxLength(100).IsRequired();
        builder.Property(m => m.MinPercentageInclusive).HasPrecision(9, 2);
        builder.Property(m => m.MaxPercentageInclusive).HasPrecision(9, 2);
        builder.HasOne(m => m.School).WithMany().HasForeignKey(m => m.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.SchoolId, m.SortOrder });
        builder.HasIndex(m => new { m.SchoolId, m.Label }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ConductDefinitionConfiguration : AuditableEntityConfiguration<ConductDefinition>
{
    public override void Configure(EntityTypeBuilder<ConductDefinition> builder)
    {
        base.Configure(builder);
        builder.ToTable("ConductDefinitions");
        builder.Property(c => c.Label).HasMaxLength(100).IsRequired();
        builder.HasOne(c => c.School).WithMany().HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.SchoolId, c.SortOrder });
        builder.HasIndex(c => new { c.SchoolId, c.Label }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class StudentPeriodConductConfiguration : AuditableEntityConfiguration<StudentPeriodConduct>
{
    public override void Configure(EntityTypeBuilder<StudentPeriodConduct> builder)
    {
        base.Configure(builder);
        builder.ToTable("StudentPeriodConducts");
        builder.Property(c => c.Observation).HasMaxLength(1000);
        builder.Property(c => c.RecordedByUserName).HasMaxLength(150).IsRequired();
        builder.HasOne(c => c.School).WithMany().HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.AcademicYear).WithMany().HasForeignKey(c => c.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ClassRoom).WithMany().HasForeignKey(c => c.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.AcademicPeriod).WithMany().HasForeignKey(c => c.AcademicPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Student).WithMany().HasForeignKey(c => c.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ConductDefinition).WithMany().HasForeignKey(c => c.ConductDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.SchoolId, c.AcademicYearId, c.ClassRoomId, c.AcademicPeriodId, c.StudentId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class PedagogicalBonusPointConfiguration : AuditableEntityConfiguration<PedagogicalBonusPoint>
{
    public override void Configure(EntityTypeBuilder<PedagogicalBonusPoint> builder)
    {
        base.Configure(builder);
        builder.ToTable("PedagogicalBonusPoints");
        builder.Property(b => b.PointsAdded).HasPrecision(9, 2);
        builder.Property(b => b.Motive).HasMaxLength(500).IsRequired();
        builder.Property(b => b.RecordedByUserName).HasMaxLength(150).IsRequired();
        builder.HasOne(b => b.School).WithMany().HasForeignKey(b => b.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.AcademicYear).WithMany().HasForeignKey(b => b.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.ClassRoom).WithMany().HasForeignKey(b => b.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.AcademicPeriod).WithMany().HasForeignKey(b => b.AcademicPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Student).WithMany().HasForeignKey(b => b.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Course).WithMany().HasForeignKey(b => b.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => new { b.SchoolId, b.ClassRoomId, b.AcademicPeriodId, b.StudentId, b.IsCancelled });
    }
}

public class DeliberationAuditEntryConfiguration : AuditableEntityConfiguration<DeliberationAuditEntry>
{
    public override void Configure(EntityTypeBuilder<DeliberationAuditEntry> builder)
    {
        base.Configure(builder);
        builder.ToTable("DeliberationAuditEntries");
        builder.Property(a => a.ActionCode).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Observation).HasMaxLength(2000);
        builder.Property(a => a.UserName).HasMaxLength(150).IsRequired();
        builder.HasIndex(a => new { a.SchoolId, a.ClassRoomId, a.AcademicPeriodId, a.OccurredAtUtc });
    }
}
