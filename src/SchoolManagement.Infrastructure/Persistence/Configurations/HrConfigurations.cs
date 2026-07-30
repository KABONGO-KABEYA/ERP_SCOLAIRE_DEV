using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Hr;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class HrDepartmentConfiguration : AuditableEntityConfiguration<HrDepartment>
{
    public override void Configure(EntityTypeBuilder<HrDepartment> builder)
    {
        base.Configure(builder);
        builder.ToTable("HrDepartments");
        builder.Property(d => d.Code).HasMaxLength(20).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(d => new { d.SchoolId, d.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class HrJobFunctionConfiguration : AuditableEntityConfiguration<HrJobFunction>
{
    public override void Configure(EntityTypeBuilder<HrJobFunction> builder)
    {
        base.Configure(builder);
        builder.ToTable("HrJobFunctions");
        builder.Property(f => f.Name).HasMaxLength(120).IsRequired();
        builder.HasOne(f => f.Department).WithMany(d => d.JobFunctions).HasForeignKey(f => f.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(f => new { f.SchoolId, f.Name }).HasFilter("[IsDeleted] = 0");
    }
}

public class PersonnelHrProfileConfiguration : AuditableEntityConfiguration<PersonnelHrProfile>
{
    public override void Configure(EntityTypeBuilder<PersonnelHrProfile> builder)
    {
        base.Configure(builder);
        builder.ToTable("PersonnelHrProfiles");
        builder.Property(p => p.MiddleName).HasMaxLength(100);
        builder.Property(p => p.BirthPlace).HasMaxLength(120);
        builder.Property(p => p.Nationality).HasMaxLength(80);
        builder.Property(p => p.MaritalStatus).HasMaxLength(40);
        builder.Property(p => p.IdCardNumber).HasMaxLength(60);
        builder.Property(p => p.Grade).HasMaxLength(80);
        builder.Property(p => p.Service).HasMaxLength(120);
        builder.Property(p => p.SupervisorName).HasMaxLength(160);
        builder.Property(p => p.WorkLocation).HasMaxLength(120);
        builder.Property(p => p.CurrencyCode).HasMaxLength(10);
        builder.Property(p => p.BankName).HasMaxLength(120);
        builder.Property(p => p.BankAccountNumber).HasMaxLength(60);
        builder.Property(p => p.BankAccountHolder).HasMaxLength(160);
        builder.Property(p => p.EmergencyContactName).HasMaxLength(160);
        builder.Property(p => p.EmergencyContactRelation).HasMaxLength(60);
        builder.Property(p => p.EmergencyContactPhone).HasMaxLength(40);
        builder.Property(p => p.EmergencyContactAddress).HasMaxLength(300);
        builder.Property(p => p.PhotoPath).HasMaxLength(500);
        builder.Property(p => p.BaseSalary).HasPrecision(18, 2);
        builder.HasOne(p => p.Teacher).WithMany().HasForeignKey(p => p.TeacherId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Department).WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(p => p.JobFunction).WithMany().HasForeignKey(p => p.JobFunctionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(p => p.TeacherId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
