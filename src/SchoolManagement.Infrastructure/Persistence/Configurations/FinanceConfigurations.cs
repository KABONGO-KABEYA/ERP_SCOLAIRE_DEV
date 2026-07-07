using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Finance;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : AuditableEntityConfiguration<Payment>
{
    public override void Configure(EntityTypeBuilder<Payment> builder)
    {
        base.Configure(builder);
        builder.ToTable("Payments");
        builder.Property(p => p.ReceiptNumber).HasMaxLength(50).IsRequired();
        builder.Property(p => p.TotalAmount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasConversion<int>();
        builder.Property(p => p.Status).HasConversion<int>();
        builder.HasOne(p => p.Student).WithMany().HasForeignKey(p => p.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.AcademicYear).WithMany().HasForeignKey(p => p.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.CashRegister).WithMany().HasForeignKey(p => p.CashRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Bank).WithMany().HasForeignKey(p => p.BankId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(p => p.ReceiptNumber).IsUnique();
        builder.HasIndex(p => new { p.PaymentDate, p.SchoolId });
        builder.HasIndex(p => new { p.StudentId, p.AcademicYearId });
    }
}

public class PaymentLineConfiguration : AuditableEntityConfiguration<PaymentLine>
{
    public override void Configure(EntityTypeBuilder<PaymentLine> builder)
    {
        base.Configure(builder);
        builder.ToTable("PaymentLines");
        builder.Property(l => l.Amount).HasPrecision(18, 2);
        builder.Property(l => l.Currency).HasConversion<int>();
        builder.HasOne(l => l.Payment).WithMany(p => p.Lines).HasForeignKey(l => l.PaymentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.FeeType).WithMany().HasForeignKey(l => l.FeeTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentReversalConfiguration : AuditableEntityConfiguration<PaymentReversal>
{
    public override void Configure(EntityTypeBuilder<PaymentReversal> builder)
    {
        base.Configure(builder);
        builder.ToTable("PaymentReversals");
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        builder.HasOne(r => r.Payment).WithOne(p => p.Reversal).HasForeignKey<PaymentReversal>(r => r.PaymentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CashMovementConfiguration : AuditableEntityConfiguration<CashMovement>
{
    public override void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        base.Configure(builder);
        builder.ToTable("CashMovements");
        builder.Property(m => m.Amount).HasPrecision(18, 2);
        builder.Property(m => m.BalanceAfter).HasPrecision(18, 2);
        builder.Property(m => m.Currency).HasConversion<int>();
        builder.HasOne(m => m.CashRegister).WithMany().HasForeignKey(m => m.CashRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Payment).WithMany().HasForeignKey(m => m.PaymentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(m => new { m.CashRegisterId, m.MovementDate });
    }
}

public class StudentFeeBalanceConfiguration : AuditableEntityConfiguration<StudentFeeBalance>
{
    public override void Configure(EntityTypeBuilder<StudentFeeBalance> builder)
    {
        base.Configure(builder);
        builder.ToTable("StudentFeeBalances");
        builder.Property(b => b.AmountDue).HasPrecision(18, 2);
        builder.Property(b => b.AmountPaid).HasPrecision(18, 2);
        builder.Property(b => b.Currency).HasConversion<int>();
        builder.HasOne(b => b.Student).WithMany().HasForeignKey(b => b.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.FeeType).WithMany().HasForeignKey(b => b.FeeTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Settings.AcademicYear>().WithMany().HasForeignKey(b => b.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => new { b.StudentId, b.AcademicYearId, b.FeeTypeId }).IsUnique();
    }
}
