using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Finance;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class CurrencyDefinitionConfiguration : AuditableEntityConfiguration<CurrencyDefinition>
{
    public override void Configure(EntityTypeBuilder<CurrencyDefinition> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinDevise");
        builder.Property(c => c.Code).HasMaxLength(10).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(120).IsRequired();
        builder.Property(c => c.Symbol).HasMaxLength(10).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class SchoolCurrencyConfiguration : AuditableEntityConfiguration<SchoolCurrency>
{
    public override void Configure(EntityTypeBuilder<SchoolCurrency> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinEtablissementDevise");
        builder.HasOne(c => c.School).WithMany().HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Currency).WithMany(c => c.SchoolCurrencies).HasForeignKey(c => c.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.SchoolId, c.CurrencyId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(c => new { c.SchoolId, c.IsPrimary })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsPrimary] = 1");
    }
}

public class ExchangeRateTypeConfiguration : AuditableEntityConfiguration<ExchangeRateType>
{
    public override void Configure(EntityTypeBuilder<ExchangeRateType> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinTypeTaux");
        builder.Property(t => t.Code).HasMaxLength(40).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.HasIndex(t => t.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ExchangeRateConfiguration : AuditableEntityConfiguration<ExchangeRate>
{
    public override void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinTauxChange");
        builder.Property(r => r.Rate).HasPrecision(18, 6);
        builder.Property(r => r.Notes).HasMaxLength(500);
        builder.HasOne(r => r.SourceCurrency).WithMany().HasForeignKey(r => r.SourceCurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.TargetCurrency).WithMany().HasForeignKey(r => r.TargetCurrencyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.RateType).WithMany().HasForeignKey(r => r.RateTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.SourceCurrencyId, r.TargetCurrencyId, r.RateTypeId, r.IsActive })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
    }
}

public class ExchangeRateHistoryConfiguration : AuditableEntityConfiguration<ExchangeRateHistory>
{
    public override void Configure(EntityTypeBuilder<ExchangeRateHistory> builder)
    {
        base.Configure(builder);
        builder.ToTable("FinHistoriqueTaux");
        builder.Property(h => h.OldRate).HasPrecision(18, 6);
        builder.Property(h => h.NewRate).HasPrecision(18, 6);
        builder.Property(h => h.Action).HasMaxLength(40).IsRequired();
        builder.Property(h => h.MachineName).HasMaxLength(120);
        builder.Property(h => h.IpAddress).HasMaxLength(64);
        builder.HasOne(h => h.ExchangeRate).WithMany().HasForeignKey(h => h.ExchangeRateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(h => new { h.ExchangeRateId, h.OccurredAt });
    }
}
