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
        builder.Property(p => p.PaymentMethod).IsRequired(false);
        builder.Property(p => p.TotalAmount).HasPrecision(18, 2);
        builder.Property(p => p.FeeCurrencyAmount).HasPrecision(18, 2);
        builder.Property(p => p.PaymentCurrencyAmount).HasPrecision(18, 2);
        builder.Property(p => p.AppliedExchangeRate).HasPrecision(18, 6);
        builder.Property(p => p.Currency).HasConversion<int>();
        builder.Property(p => p.Status).HasConversion<int>();
        builder.HasOne<CurrencyDefinition>().WithMany().HasForeignKey(p => p.FeeCurrencyId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CurrencyDefinition>().WithMany().HasForeignKey(p => p.PaymentCurrencyId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExchangeRate>().WithMany().HasForeignKey(p => p.ExchangeRateId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Student).WithMany().HasForeignKey(p => p.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.AcademicYear).WithMany().HasForeignKey(p => p.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.CashRegister).WithMany().HasForeignKey(p => p.CashRegisterId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(l => l.PhysicalReceiptNumber).HasMaxLength(50);
        builder.HasOne(l => l.Payment).WithMany(p => p.Lines).HasForeignKey(l => l.PaymentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(l => l.FeeType).WithMany().HasForeignKey(l => l.FeeTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.FeeInstallment).WithMany().HasForeignKey(l => l.FeeInstallmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.PaymentId, l.FeeInstallmentId });
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
        builder.HasOne(m => m.CashRegister).WithMany().HasForeignKey(m => m.CashRegisterId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
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
        builder.HasOne(b => b.ClassFeeAmount).WithMany().HasForeignKey(b => b.ClassFeeAmountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => new { b.StudentId, b.ClassFeeAmountId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(b => b.ClassFeeAmountId);
    }
}

public class RevenueAllocationDestinationConfiguration : AuditableEntityConfiguration<RevenueAllocationDestination>
{
    public override void Configure(EntityTypeBuilder<RevenueAllocationDestination> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinDestinationRepartition");
        builder.Property(d => d.Code).HasMaxLength(20).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(120).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(500);
        builder.HasIndex(d => new { d.SchoolId, d.Code }).IsUnique();
    }
}

public class RevenueAllocationKeyConfiguration : AuditableEntityConfiguration<RevenueAllocationKey>
{
    public override void Configure(EntityTypeBuilder<RevenueAllocationKey> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinCleRepartition");
        builder.Property(k => k.Name).HasMaxLength(150).IsRequired();
        builder.Property(k => k.Notes).HasMaxLength(500);
        builder.HasOne(k => k.AcademicYear).WithMany().HasForeignKey(k => k.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(k => k.FeeType).WithMany().HasForeignKey(k => k.FeeTypeId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne(k => k.WithholdingType).WithMany().HasForeignKey(k => k.WithholdingTypeId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasIndex(k => new { k.SchoolId, k.AcademicYearId, k.FeeTypeId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [FeeTypeId] IS NOT NULL");
        builder.HasIndex(k => new { k.SchoolId, k.AcademicYearId, k.WithholdingTypeId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [WithholdingTypeId] IS NOT NULL");
        builder.HasIndex(k => new { k.SchoolId, k.FeeTypeId, k.StartDate });
        builder.HasIndex(k => new { k.SchoolId, k.WithholdingTypeId, k.StartDate });
    }
}

public class RevenueAllocationKeyDetailConfiguration : AuditableEntityConfiguration<RevenueAllocationKeyDetail>
{
    public override void Configure(EntityTypeBuilder<RevenueAllocationKeyDetail> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinCleRepartitionDetail");
        builder.Property(d => d.CalculationType).HasConversion<int>();
        builder.Property(d => d.Value).HasPrecision(18, 4);
        builder.HasOne(d => d.AllocationKey).WithMany(k => k.Details).HasForeignKey(d => d.AllocationKeyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(d => d.Destination).WithMany().HasForeignKey(d => d.DestinationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.AllocationKeyId, d.DestinationId }).IsUnique();
    }
}

public class RevenueAllocationEntryConfiguration : AuditableEntityConfiguration<RevenueAllocationEntry>
{
    public override void Configure(EntityTypeBuilder<RevenueAllocationEntry> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinRepartitionRecette");
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.AppliedPercentage).HasPrecision(18, 4);
        builder.Property(e => e.CalculationType).HasConversion<int>();
        builder.HasOne(e => e.Payment).WithMany().HasForeignKey(e => e.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.AllocationKey).WithMany().HasForeignKey(e => e.AllocationKeyId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne(e => e.Destination).WithMany().HasForeignKey(e => e.DestinationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.FeeType).WithMany().HasForeignKey(e => e.FeeTypeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.WithholdingType).WithMany().HasForeignKey(e => e.WithholdingTypeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.AcademicYear).WithMany().HasForeignKey(e => e.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.SchoolId, e.AllocatedAt });
        builder.HasIndex(e => e.PaymentId);
        builder.HasIndex(e => new { e.SchoolId, e.DestinationId, e.AcademicYearId });
    }
}

public class ExpenseRequestConfiguration : AuditableEntityConfiguration<ExpenseRequest>
{
    public override void Configure(EntityTypeBuilder<ExpenseRequest> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinDemandePaiement");
        builder.Property(r => r.Reference).HasMaxLength(40).IsRequired();
        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.RequestedAmount).HasPrecision(18, 2);
        builder.Property(r => r.Currency).HasConversion<int>();
        builder.Property(r => r.Status).HasConversion<int>();
        builder.HasOne(r => r.School).WithMany().HasForeignKey(r => r.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.AcademicYear).WithMany().HasForeignKey(r => r.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Destination).WithMany().HasForeignKey(r => r.DestinationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.SchoolId, r.Reference }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(r => new { r.SchoolId, r.Status }).HasFilter("[IsDeleted] = 0");
    }
}

public class ExpensePaymentConfiguration : AuditableEntityConfiguration<ExpensePayment>
{
    public override void Configure(EntityTypeBuilder<ExpensePayment> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinDepense");
        builder.Property(p => p.Reference).HasMaxLength(40).IsRequired();
        builder.Property(p => p.Label).HasMaxLength(500).IsRequired();
        builder.Property(p => p.BeneficiaryName).HasMaxLength(150).IsRequired();
        builder.Property(p => p.AuthorizedByName).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.Currency).HasConversion<int>();
        builder.HasOne(p => p.School).WithMany().HasForeignKey(p => p.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.AcademicYear).WithMany().HasForeignKey(p => p.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Destination).WithMany().HasForeignKey(p => p.DestinationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ExpenseRequest).WithMany().HasForeignKey(p => p.ExpenseRequestId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(p => new { p.SchoolId, p.Reference }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(p => new { p.SchoolId, p.ExpenseDate, p.DestinationId }).HasFilter("[IsDeleted] = 0");
    }
}

public class WithholdingTypeConfiguration : AuditableEntityConfiguration<WithholdingType>
{
    public override void Configure(EntityTypeBuilder<WithholdingType> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinRetenue");
        builder.Property(t => t.Code).HasMaxLength(20).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.HasIndex(t => new { t.SchoolId, t.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class WithholdingConfigurationConfiguration : AuditableEntityConfiguration<WithholdingConfiguration>
{
    public override void Configure(EntityTypeBuilder<WithholdingConfiguration> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinRetenueConfiguration");
        builder.Property(c => c.CalculationMode).HasConversion<int>();
        builder.Property(c => c.Value).HasPrecision(18, 4);
        builder.HasOne(c => c.AcademicYear).WithMany().HasForeignKey(c => c.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.WithholdingType).WithMany().HasForeignKey(c => c.WithholdingTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.FeeType).WithMany().HasForeignKey(c => c.FeeTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.FeeInstallment).WithMany().HasForeignKey(c => c.FeeInstallmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.PricingCategory).WithMany().HasForeignKey(c => c.PricingCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new
            {
                c.SchoolId,
                c.AcademicYearId,
                c.WithholdingTypeId,
                c.FeeTypeId,
                c.FeeInstallmentId,
                c.PricingCategoryId
            })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(c => new { c.SchoolId, c.AcademicYearId, c.IsActive });
    }
}

public class WithholdingApplicationConfiguration : AuditableEntityConfiguration<WithholdingApplication>
{
    public override void Configure(EntityTypeBuilder<WithholdingApplication> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinRetenueApplication");
        builder.Property(a => a.Amount).HasPrecision(18, 4);
        builder.HasOne(a => a.WithholdingConfiguration).WithMany().HasForeignKey(a => a.WithholdingConfigurationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Payment).WithMany().HasForeignKey(a => a.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.PaymentLine).WithMany().HasForeignKey(a => a.PaymentLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new
            {
                a.SchoolId,
                a.StudentId,
                a.AcademicYearId,
                a.WithholdingConfigurationId
            })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(a => new { a.SchoolId, a.PaymentId });
    }
}
