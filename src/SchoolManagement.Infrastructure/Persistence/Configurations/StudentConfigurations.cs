using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Students;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : AuditableEntityConfiguration<Student>
{
    public override void Configure(EntityTypeBuilder<Student> builder)
    {
        base.Configure(builder);
        builder.ToTable("Students");
        builder.Property(s => s.RegistrationNumber).HasMaxLength(30).IsRequired();
        builder.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Gender).HasConversion<int>();
        builder.HasOne(s => s.ResidenceAddress).WithMany().HasForeignKey(s => s.AddressId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(s => new { s.SchoolId, s.RegistrationNumber }).IsUnique();
        builder.HasIndex(s => new { s.LastName, s.FirstName });
    }
}

public class GuardianConfiguration : AuditableEntityConfiguration<Guardian>
{
    public override void Configure(EntityTypeBuilder<Guardian> builder)
    {
        base.Configure(builder);
        builder.ToTable("Guardians");
        builder.Property(g => g.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(g => g.LastName).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Gender).HasConversion<int>();
        builder.HasOne(g => g.ResidenceAddress).WithMany().HasForeignKey(g => g.AddressId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(g => new { g.SchoolId, g.LastName, g.FirstName });
    }
}

public class StudentGuardianConfiguration : AuditableEntityConfiguration<StudentGuardian>
{
    public override void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        base.Configure(builder);
        builder.ToTable("StudentGuardians");
        builder.Property(sg => sg.Relationship).HasMaxLength(50).IsRequired();
        builder.HasOne(sg => sg.Student).WithMany(s => s.Guardians).HasForeignKey(sg => sg.StudentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(sg => sg.Guardian).WithMany(g => g.Students).HasForeignKey(sg => sg.GuardianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(sg => new { sg.StudentId, sg.GuardianId }).IsUnique();
    }
}

public class StudentDocumentConfiguration : AuditableEntityConfiguration<StudentDocument>
{
    public override void Configure(EntityTypeBuilder<StudentDocument> builder)
    {
        base.Configure(builder);
        builder.ToTable("StudentDocuments");
        builder.Property(d => d.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(255).IsRequired();
        builder.HasOne(d => d.Student).WithMany(s => s.Documents).HasForeignKey(d => d.StudentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EnrollmentConfiguration : AuditableEntityConfiguration<Enrollment>
{
    public override void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        base.Configure(builder);
        builder.ToTable("Enrollments");
        builder.Property(e => e.Status).HasConversion<int>();
        builder.HasOne(e => e.Student).WithMany(s => s.Enrollments).HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AcademicYear).WithMany().HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ClassRoom).WithMany().HasForeignKey(e => e.ClassRoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.FeePricingCategory).WithMany().HasForeignKey(e => e.FeePricingCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.StudentId, e.AcademicYearId, e.IsActive })
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(e => new { e.AcademicYearId, e.ClassRoomId });
        builder.HasIndex(e => new { e.AcademicYearId, e.FeePricingCategoryId });
    }
}

public class EnrollmentPricingCategoryHistoryConfiguration : AuditableEntityConfiguration<EnrollmentPricingCategoryHistory>
{
    public override void Configure(EntityTypeBuilder<EnrollmentPricingCategoryHistory> builder)
    {
        base.Configure(builder);
        builder.ToTable("EnrollmentPricingCategoryHistory");
        builder.Property(h => h.Notes).HasMaxLength(500);
        builder.HasOne(h => h.Enrollment).WithMany().HasForeignKey(h => h.EnrollmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(h => h.PreviousFeePricingCategory).WithMany().HasForeignKey(h => h.PreviousFeePricingCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.NewFeePricingCategory).WithMany().HasForeignKey(h => h.NewFeePricingCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(h => new { h.EnrollmentId, h.ChangedAt });
    }
}

public class StudentStatusHistoryConfiguration : AuditableEntityConfiguration<StudentStatusHistory>
{
    public override void Configure(EntityTypeBuilder<StudentStatusHistory> builder)
    {
        base.Configure(builder);
        builder.ToTable("StudentStatusHistory");
        builder.Property(h => h.PreviousStatus).HasConversion<int>();
        builder.Property(h => h.NewStatus).HasConversion<int>();
        builder.HasOne(h => h.Student).WithMany(s => s.StatusHistory).HasForeignKey(h => h.StudentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(h => new { h.StudentId, h.EffectiveDate });
    }
}
