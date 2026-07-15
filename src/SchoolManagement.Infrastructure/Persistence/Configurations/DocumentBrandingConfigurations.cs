using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class SchoolLogoConfiguration : AuditableEntityConfiguration<SchoolLogo>
{
    public override void Configure(EntityTypeBuilder<SchoolLogo> builder)
    {
        base.Configure(builder);
        builder.ToTable("EcoleLogo");
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ImagePath).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SchoolId, x.Name });
        builder.HasIndex(x => new { x.SchoolId, x.IsPrimary });
    }
}

public class SchoolDocumentHeaderConfiguration : AuditableEntityConfiguration<SchoolDocumentHeader>
{
    public override void Configure(EntityTypeBuilder<SchoolDocumentHeader> builder)
    {
        base.Configure(builder);
        builder.ToTable("EcoleEntete");
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.DocumentType).HasConversion<int>();
        builder.Property(x => x.ApplicableDocumentTypes).HasMaxLength(200);
        builder.Property(x => x.PrintMode).HasConversion<int>();
        builder.Property(x => x.ImagePath).HasMaxLength(500);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SchoolId, x.DocumentType, x.Name });
    }
}

public class SchoolSignatureConfiguration : AuditableEntityConfiguration<SchoolSignature>
{
    public override void Configure(EntityTypeBuilder<SchoolSignature> builder)
    {
        base.Configure(builder);
        builder.ToTable("EcoleSignature");
        builder.Property(x => x.SignatoryName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Function).HasMaxLength(150).IsRequired();
        builder.Property(x => x.DocumentType).HasConversion<int>();
        builder.Property(x => x.ApplicableDocumentTypes).HasMaxLength(200);
        builder.Property(x => x.ImagePath).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SchoolId, x.Function });
    }
}

public class SchoolStampConfiguration : AuditableEntityConfiguration<SchoolStamp>
{
    public override void Configure(EntityTypeBuilder<SchoolStamp> builder)
    {
        base.Configure(builder);
        builder.ToTable("EcoleCachet");
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ImagePath).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SchoolId, x.Name });
    }
}

public class SchoolDocumentFooterConfiguration : AuditableEntityConfiguration<SchoolDocumentFooter>
{
    public override void Configure(EntityTypeBuilder<SchoolDocumentFooter> builder)
    {
        base.Configure(builder);
        builder.ToTable("EcolePiedPage");
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(150);
        builder.Property(x => x.Website).HasMaxLength(200);
        builder.Property(x => x.PoBox).HasMaxLength(50);
        builder.Property(x => x.SchoolMotto).HasMaxLength(200);
        builder.Property(x => x.FreeText).HasMaxLength(2000);
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SchoolId).IsUnique();
    }
}
