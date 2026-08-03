using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Deliberation;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class DeliberationDecisionConfiguration : AuditableEntityConfiguration<DeliberationDecision>
{
    public override void Configure(EntityTypeBuilder<DeliberationDecision> builder)
    {
        base.Configure(builder);
        builder.ToTable("DeliberationDecisions");
        builder.Property(d => d.ProposedDecision).HasConversion<int>();
        builder.Property(d => d.FinalDecision).HasConversion<int>();
        builder.Property(d => d.Observation).HasMaxLength(2000);
        builder.Property(d => d.DecidedByUserName).HasMaxLength(150).IsRequired();
        builder.HasOne(d => d.School).WithMany().HasForeignKey(d => d.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.AcademicYear).WithMany().HasForeignKey(d => d.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.ClassRoom).WithMany().HasForeignKey(d => d.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.AcademicPeriod).WithMany().HasForeignKey(d => d.AcademicPeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Student).WithMany().HasForeignKey(d => d.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.SchoolId, d.AcademicYearId, d.ClassRoomId, d.AcademicPeriodId, d.StudentId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class DeliberationDecisionEventConfiguration : AuditableEntityConfiguration<DeliberationDecisionEvent>
{
    public override void Configure(EntityTypeBuilder<DeliberationDecisionEvent> builder)
    {
        base.Configure(builder);
        builder.ToTable("DeliberationDecisionEvents");
        builder.Property(e => e.ProposedDecision).HasConversion<int>();
        builder.Property(e => e.FinalDecision).HasConversion<int>();
        builder.Property(e => e.Observation).HasMaxLength(2000);
        builder.Property(e => e.UserName).HasMaxLength(150).IsRequired();
        builder.HasOne(e => e.Decision)
            .WithMany(d => d.Events)
            .HasForeignKey(e => e.DecisionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.DecisionId, e.OccurredAtUtc });
    }
}

public class StudentRemedialSessionConfiguration : AuditableEntityConfiguration<StudentRemedialSession>
{
    public override void Configure(EntityTypeBuilder<StudentRemedialSession> builder)
    {
        base.Configure(builder);
        builder.ToTable("StudentRemedialSessions");
        builder.Property(s => s.SessionKind).HasConversion<int>();
        builder.HasOne(s => s.Decision)
            .WithOne(d => d.RemedialSession)
            .HasForeignKey<StudentRemedialSession>(s => s.DecisionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.DecisionId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class StudentRemedialCourseConfiguration : AuditableEntityConfiguration<StudentRemedialCourse>
{
    public override void Configure(EntityTypeBuilder<StudentRemedialCourse> builder)
    {
        base.Configure(builder);
        builder.ToTable("StudentRemedialCourses");
        builder.Property(c => c.Status).HasConversion<int>();
        builder.HasOne(c => c.RemedialSession)
            .WithMany(s => s.Courses)
            .HasForeignKey(c => c.RemedialSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.Course).WithMany().HasForeignKey(c => c.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.RemedialSessionId, c.CourseId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class CourseExemptionConfiguration : AuditableEntityConfiguration<CourseExemption>
{
    public override void Configure(EntityTypeBuilder<CourseExemption> builder)
    {
        base.Configure(builder);
        builder.ToTable("CourseExemptions");
        builder.Property(e => e.Motive).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Observation).HasMaxLength(2000);
        builder.HasOne(e => e.Decision)
            .WithMany(d => d.Exemptions)
            .HasForeignKey(e => e.DecisionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Student).WithMany().HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.DecisionId, e.CourseId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
