using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class SchoolConfiguration : AuditableEntityConfiguration<School>
{
    public override void Configure(EntityTypeBuilder<School> builder)
    {
        base.Configure(builder);
        builder.ToTable("Schools");
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.DefaultCurrency).HasConversion<int>();
        builder.HasIndex(s => s.Name);
    }
}

public class AcademicYearConfiguration : AuditableEntityConfiguration<AcademicYear>
{
    public override void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        base.Configure(builder);
        builder.ToTable("AcademicYears");
        builder.Property(a => a.Label).HasMaxLength(50).IsRequired();
        builder.HasOne(a => a.School).WithMany(s => s.AcademicYears).HasForeignKey(a => a.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.SchoolId, a.Label }).IsUnique();
        builder.HasIndex(a => new { a.SchoolId, a.IsCurrent }).HasFilter("[IsCurrent] = 1 AND [IsDeleted] = 0");
    }
}

public class SectionConfiguration : AuditableEntityConfiguration<Section>
{
    public override void Configure(EntityTypeBuilder<Section> builder)
    {
        base.Configure(builder);
        builder.ToTable("Sections");
        builder.Property(s => s.Code).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Cycle).HasConversion<int>();
        builder.HasOne(s => s.School).WithMany().HasForeignKey(s => s.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.SchoolId, s.Code }).IsUnique();
    }
}

public class StudyOptionConfiguration : AuditableEntityConfiguration<StudyOption>
{
    public override void Configure(EntityTypeBuilder<StudyOption> builder)
    {
        base.Configure(builder);
        builder.ToTable("StudyOptions");
        builder.Property(o => o.Code).HasMaxLength(20).IsRequired();
        builder.Property(o => o.Name).HasMaxLength(100).IsRequired();
        builder.Property(o => o.HumanitiesSection).HasMaxLength(100);
        builder.HasOne(o => o.School).WithMany().HasForeignKey(o => o.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.SchoolId, o.Code }).IsUnique();
    }
}

public class PedagogicalClassConfiguration : AuditableEntityConfiguration<PedagogicalClass>
{
    public override void Configure(EntityTypeBuilder<PedagogicalClass> builder)
    {
        base.Configure(builder);
        builder.ToTable("PedagogicalClasses");
        builder.Property(p => p.TemplateCode).HasMaxLength(50).IsRequired();
        builder.Property(p => p.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(p => p.HumanitiesSection).HasMaxLength(100);
        builder.Property(p => p.StudyOption).HasMaxLength(100);
        builder.Property(p => p.Program).HasConversion<int>();
        builder.HasOne(p => p.School).WithMany().HasForeignKey(p => p.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.SchoolId, p.TemplateCode }).IsUnique();
        builder.HasIndex(p => new { p.SchoolId, p.IsEnabled });
    }
}

public class ClassRoomConfiguration : AuditableEntityConfiguration<ClassRoom>
{
    public override void Configure(EntityTypeBuilder<ClassRoom> builder)
    {
        base.Configure(builder);
        builder.ToTable("ClassRooms");
        builder.Property(c => c.Code).HasMaxLength(30).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Observations).HasMaxLength(500);
        builder.HasOne(c => c.School).WithMany().HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.AcademicYear).WithMany(y => y.ClassRooms).HasForeignKey(c => c.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.PedagogicalClass).WithMany(p => p.Locals).HasForeignKey(c => c.PedagogicalClassId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Section).WithMany(s => s.ClassRooms).HasForeignKey(c => c.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.StudyOption).WithMany(o => o.ClassRooms).HasForeignKey(c => c.StudyOptionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(c => new { c.AcademicYearId, c.Code }).IsUnique();
        builder.HasIndex(c => new { c.PedagogicalClassId, c.AcademicYearId, c.Name }).IsUnique()
            .HasFilter("[PedagogicalClassId] IS NOT NULL AND [IsDeleted] = 0");
    }
}

public class CourseConfiguration : AuditableEntityConfiguration<Course>
{
    public override void Configure(EntityTypeBuilder<Course> builder)
    {
        base.Configure(builder);
        builder.ToTable("Courses");
        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Coefficient).HasPrecision(5, 2);
        builder.HasOne(c => c.School).WithMany().HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ClassRoom).WithMany(r => r.Courses).HasForeignKey(c => c.ClassRoomId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(c => new { c.SchoolId, c.Code });
    }
}

public class AcademicPeriodConfiguration : AuditableEntityConfiguration<AcademicPeriod>
{
    public override void Configure(EntityTypeBuilder<AcademicPeriod> builder)
    {
        base.Configure(builder);
        builder.ToTable("AcademicPeriods");
        builder.Property(p => p.Name).HasMaxLength(50).IsRequired();
        builder.Property(p => p.PeriodType).HasConversion<int>();
        builder.HasOne(p => p.AcademicYear).WithMany(y => y.Periods).HasForeignKey(p => p.AcademicYearId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => new { p.AcademicYearId, p.OrderIndex }).IsUnique();
    }
}

public class FeeTypeConfiguration : AuditableEntityConfiguration<FeeType>
{
    public override void Configure(EntityTypeBuilder<FeeType> builder)
    {
        base.Configure(builder);
        builder.ToTable("FeeTypes");
        builder.Property(f => f.Code).HasMaxLength(20).IsRequired();
        builder.Property(f => f.Name).HasMaxLength(150).IsRequired();
        builder.Property(f => f.DefaultAmount).HasPrecision(18, 2);
        builder.Property(f => f.Currency).HasConversion<int>();
        builder.HasOne(f => f.School).WithMany().HasForeignKey(f => f.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(f => new { f.SchoolId, f.Code }).IsUnique();
    }
}

public class BankConfiguration : AuditableEntityConfiguration<Bank>
{
    public override void Configure(EntityTypeBuilder<Bank> builder)
    {
        base.Configure(builder);
        builder.ToTable("Banks");
        builder.Property(b => b.Name).HasMaxLength(150).IsRequired();
        builder.Property(b => b.Currency).HasConversion<int>();
        builder.HasOne(b => b.School).WithMany().HasForeignKey(b => b.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CashRegisterConfiguration : AuditableEntityConfiguration<CashRegister>
{
    public override void Configure(EntityTypeBuilder<CashRegister> builder)
    {
        base.Configure(builder);
        builder.ToTable("CashRegisters");
        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Currency).HasConversion<int>();
        builder.HasOne(c => c.School).WithMany().HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.SchoolId, c.Code }).IsUnique();
    }
}

public class AppConfigurationConfiguration : AuditableEntityConfiguration<AppConfiguration>
{
    public override void Configure(EntityTypeBuilder<AppConfiguration> builder)
    {
        base.Configure(builder);
        builder.ToTable("AppConfigurations");
        builder.Property(c => c.Key).HasMaxLength(100).IsRequired();
        builder.HasOne(c => c.School).WithMany().HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.SchoolId, c.Key }).IsUnique();
    }
}
