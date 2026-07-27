using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class CardTemplateConfiguration : AuditableEntityConfiguration<CardTemplate>
{
    public override void Configure(EntityTypeBuilder<CardTemplate> builder)
    {
        base.Configure(builder);
        builder.ToTable("CarteModele");
        builder.Property(t => t.Name).HasMaxLength(120).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.WidthMm).HasPrecision(8, 2);
        builder.Property(t => t.HeightMm).HasPrecision(8, 2);
        builder.Property(t => t.Orientation).HasConversion<int>();
        builder.Property(t => t.Kind).HasConversion<int>();
        builder.Property(t => t.LayoutJsonFront).HasColumnType("nvarchar(max)");
        builder.Property(t => t.LayoutJsonBack).HasColumnType("nvarchar(max)");
        builder.HasOne(t => t.School).WithMany().HasForeignKey(t => t.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.SchoolId, t.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(t => new { t.SchoolId, t.IsActive });
    }
}

public class CardSchoolSettingsConfiguration : AuditableEntityConfiguration<CardSchoolSettings>
{
    public override void Configure(EntityTypeBuilder<CardSchoolSettings> builder)
    {
        base.Configure(builder);
        builder.ToTable("CarteParametres");
        builder.Property(s => s.CardNumberPrefix).HasMaxLength(20).IsRequired();
        builder.HasOne(s => s.School).WithMany().HasForeignKey(s => s.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.SchoolId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class StudentCardConfiguration : AuditableEntityConfiguration<StudentCard>
{
    public override void Configure(EntityTypeBuilder<StudentCard> builder)
    {
        base.Configure(builder);
        builder.ToTable("Carte");
        builder.Property(c => c.CardNumber).HasMaxLength(40).IsRequired();
        builder.Property(c => c.QrToken).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Status).HasConversion<int>();
        builder.Property(c => c.DeactivationReason).HasMaxLength(500);
        builder.Ignore(c => c.QrPayload);

        builder.HasOne(c => c.Student).WithMany().HasForeignKey(c => c.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.AcademicYear).WithMany().HasForeignKey(c => c.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Template).WithMany(t => t.Cards).HasForeignKey(c => c.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ReplacesCard).WithMany().HasForeignKey(c => c.ReplacesCardId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.SchoolId, c.CardNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(c => new { c.SchoolId, c.QrToken }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(c => new { c.SchoolId, c.StudentId, c.AcademicYearId, c.Status });
        builder.HasIndex(c => new { c.SchoolId, c.Status, c.ExpiresAt });
        // Une seule carte Active par élève / année scolaire.
        builder.HasIndex(c => new { c.SchoolId, c.StudentId, c.AcademicYearId })
            .IsUnique()
            .HasFilter($"[IsDeleted] = 0 AND [Status] = {(int)StudentCardStatus.Active}");
    }
}

public class StudentCardHistoryConfiguration : AuditableEntityConfiguration<StudentCardHistory>
{
    public override void Configure(EntityTypeBuilder<StudentCardHistory> builder)
    {
        base.Configure(builder);
        builder.ToTable("CarteHistorique");
        builder.Property(h => h.Action).HasConversion<int>();
        builder.Property(h => h.OldValue).HasMaxLength(2000);
        builder.Property(h => h.NewValue).HasMaxLength(2000);
        builder.Property(h => h.Notes).HasMaxLength(500);
        builder.HasOne(h => h.Card).WithMany(c => c.Histories).HasForeignKey(h => h.CardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(h => new { h.CardId, h.OccurredAt });
        builder.HasIndex(h => new { h.SchoolId, h.OccurredAt });
    }
}

public class StudentCardPrintLogConfiguration : AuditableEntityConfiguration<StudentCardPrintLog>
{
    public override void Configure(EntityTypeBuilder<StudentCardPrintLog> builder)
    {
        base.Configure(builder);
        builder.ToTable("CarteImpression");
        builder.Property(p => p.Reason).HasMaxLength(500);
        builder.HasOne(p => p.Card).WithMany(c => c.PrintLogs).HasForeignKey(p => p.CardId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.CardId, p.PrintedAt });
    }
}
