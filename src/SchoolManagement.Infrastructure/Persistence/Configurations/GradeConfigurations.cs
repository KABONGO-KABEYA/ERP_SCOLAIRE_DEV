using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Grades;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class EvaluationConfiguration : AuditableEntityConfiguration<Evaluation>
{
    public override void Configure(EntityTypeBuilder<Evaluation> builder)
    {
        base.Configure(builder);
        builder.ToTable("Evaluations");
        builder.Ignore(e => e.SchoolId);
        builder.Property(e => e.Title).HasMaxLength(150).IsRequired();
        builder.Property(e => e.EvaluationType).HasConversion<int>();
        builder.Property(e => e.Weight).HasPrecision(5, 2);
        builder.HasOne(e => e.AcademicYear).WithMany().HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AcademicPeriod).WithMany().HasForeignKey(e => e.AcademicPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ClassRoom).WithMany().HasForeignKey(e => e.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.ClassRoomId, e.AcademicPeriodId });
    }
}

public class GradeEntryConfiguration : AuditableEntityConfiguration<GradeEntry>
{
    public override void Configure(EntityTypeBuilder<GradeEntry> builder)
    {
        base.Configure(builder);
        builder.ToTable("GradeEntries");
        builder.Property(g => g.Score).HasPrecision(5, 2);
        builder.HasOne(g => g.Evaluation).WithMany(e => e.Grades).HasForeignKey(g => g.EvaluationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(g => g.Student).WithMany().HasForeignKey(g => g.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(g => new { g.EvaluationId, g.StudentId }).IsUnique();
    }
}

public class PeriodResultConfiguration : AuditableEntityConfiguration<PeriodResult>
{
    public override void Configure(EntityTypeBuilder<PeriodResult> builder)
    {
        base.Configure(builder);
        builder.ToTable("PeriodResults");
        builder.Ignore(p => p.SchoolId);
        builder.Property(p => p.Average).HasPrecision(5, 2);
        builder.Property(p => p.Percentage).HasPrecision(5, 2);
        builder.Property(p => p.CouncilDecision).HasConversion<int>();
        builder.HasOne(p => p.Student).WithMany().HasForeignKey(p => p.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.AcademicYear).WithMany().HasForeignKey(p => p.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.AcademicPeriod).WithMany().HasForeignKey(p => p.AcademicPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ClassRoom).WithMany().HasForeignKey(p => p.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.StudentId, p.AcademicPeriodId }).IsUnique();
        builder.HasIndex(p => new { p.ClassRoomId, p.AcademicPeriodId, p.Rank });
    }
}

public class ReportCardConfiguration : AuditableEntityConfiguration<ReportCard>
{
    public override void Configure(EntityTypeBuilder<ReportCard> builder)
    {
        base.Configure(builder);
        builder.ToTable("ReportCards");
        builder.Property(r => r.ReportNumber).HasMaxLength(50).IsRequired();
        builder.HasOne(r => r.Student).WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.ReportNumber).IsUnique();
        builder.HasIndex(r => new { r.StudentId, r.AcademicPeriodId });
    }
}

public class ReportCardDetailConfiguration : AuditableEntityConfiguration<ReportCardDetail>
{
    public override void Configure(EntityTypeBuilder<ReportCardDetail> builder)
    {
        base.Configure(builder);
        builder.ToTable("ReportCardDetails");
        builder.Property(d => d.Average).HasPrecision(5, 2);
        builder.Property(d => d.Coefficient).HasPrecision(5, 2);
        builder.HasOne(d => d.ReportCard).WithMany(r => r.Details).HasForeignKey(d => d.ReportCardId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(d => d.PeriodResult).WithMany(p => p.ReportCardDetails).HasForeignKey(d => d.PeriodResultId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(d => d.Course).WithMany().HasForeignKey(d => d.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}
