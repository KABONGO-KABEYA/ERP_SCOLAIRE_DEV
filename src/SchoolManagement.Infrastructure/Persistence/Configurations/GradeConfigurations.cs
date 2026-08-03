using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Grades;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class EvaluationTypeDefinitionConfiguration : AuditableEntityConfiguration<EvaluationTypeDefinition>
{
    public override void Configure(EntityTypeBuilder<EvaluationTypeDefinition> builder)
    {
        base.Configure(builder);
        builder.ToTable("EvaluationTypes");
        builder.Property(t => t.Code).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.HasOne(t => t.School).WithMany().HasForeignKey(t => t.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.SchoolId, t.Code }).IsUnique();
    }
}

public class EvaluationConfiguration : AuditableEntityConfiguration<Evaluation>
{
    public override void Configure(EntityTypeBuilder<Evaluation> builder)
    {
        base.Configure(builder);
        builder.ToTable("Evaluations");
        builder.Property(e => e.Title).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Weight).HasPrecision(5, 2);
        builder.HasOne(e => e.Enrollment).WithMany(en => en.Evaluations).HasForeignKey(e => e.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AcademicYear).WithMany().HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AcademicPeriod).WithMany().HasForeignKey(e => e.AcademicPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.CourseAssignment).WithMany(ca => ca.Evaluations).HasForeignKey(e => e.CourseAssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.EvaluationType).WithMany(t => t.Evaluations).HasForeignKey(e => e.EvaluationTypeId).OnDelete(DeleteBehavior.Restrict);
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

public class ClassPeriodResultValidationConfiguration : AuditableEntityConfiguration<ClassPeriodResultValidation>
{
    public override void Configure(EntityTypeBuilder<ClassPeriodResultValidation> builder)
    {
        base.Configure(builder);
        builder.ToTable("ClassPeriodResultValidations");
        builder.Property(v => v.Status).HasConversion<int>();
        builder.Property(v => v.ValidatedByUserName).HasMaxLength(150);
        builder.Property(v => v.LockedByUserName).HasMaxLength(150);
        builder.Property(v => v.Observations).HasMaxLength(1000);
        builder.HasOne(v => v.School).WithMany().HasForeignKey(v => v.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.AcademicYear).WithMany().HasForeignKey(v => v.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.ClassRoom).WithMany().HasForeignKey(v => v.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.AcademicPeriod).WithMany().HasForeignKey(v => v.AcademicPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => new { v.SchoolId, v.AcademicYearId, v.ClassRoomId, v.AcademicPeriodId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class ClassPeriodResultValidationEventConfiguration : AuditableEntityConfiguration<ClassPeriodResultValidationEvent>
{
    public override void Configure(EntityTypeBuilder<ClassPeriodResultValidationEvent> builder)
    {
        base.Configure(builder);
        builder.ToTable("ClassPeriodResultValidationEvents");
        builder.Property(e => e.Operation).HasConversion<int>();
        builder.Property(e => e.UserName).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Observations).HasMaxLength(1000);
        builder.HasOne(e => e.Validation)
            .WithMany(v => v.Events)
            .HasForeignKey(e => e.ValidationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.ValidationId, e.OccurredAtUtc });
    }
}

public class ClassPeriodDeliberationMinutesConfiguration : AuditableEntityConfiguration<ClassPeriodDeliberationMinutes>
{
    public override void Configure(EntityTypeBuilder<ClassPeriodDeliberationMinutes> builder)
    {
        base.Configure(builder);
        builder.ToTable("ClassPeriodDeliberationMinutes");
        builder.Property(m => m.GeneralObservations).HasMaxLength(4000);
        builder.Property(m => m.CouncilDecisions).HasMaxLength(4000);
        builder.Property(m => m.PedagogicalRecommendations).HasMaxLength(4000);
        builder.Property(m => m.RecordedByUserName).HasMaxLength(150).IsRequired();
        builder.HasOne(m => m.School).WithMany().HasForeignKey(m => m.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.AcademicYear).WithMany().HasForeignKey(m => m.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.ClassRoom).WithMany().HasForeignKey(m => m.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.AcademicPeriod).WithMany().HasForeignKey(m => m.AcademicPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => new { m.SchoolId, m.AcademicYearId, m.ClassRoomId, m.AcademicPeriodId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
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
