using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Geography;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class CountryConfiguration : AuditableEntityConfiguration<Country>
{
    public override void Configure(EntityTypeBuilder<Country> builder)
    {
        base.Configure(builder);
        builder.ToTable("Pays");
        builder.Property(c => c.Id).HasColumnName("IdPays");
        builder.Property(c => c.Code).HasColumnName("CodePays").HasMaxLength(10).IsRequired();
        builder.Property(c => c.Name).HasColumnName("NomPays").HasMaxLength(150).IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("Actif");
        builder.Property(c => c.CreatedAt).HasColumnName("DateCreation");
        builder.Property(c => c.UpdatedAt).HasColumnName("DateModification");
        builder.HasIndex(c => c.Code).IsUnique();
    }
}

public class ProvinceConfiguration : AuditableEntityConfiguration<Province>
{
    public override void Configure(EntityTypeBuilder<Province> builder)
    {
        base.Configure(builder);
        builder.ToTable("Province");
        builder.Property(p => p.Id).HasColumnName("IdProvince");
        builder.Property(p => p.CountryId).HasColumnName("IdPays");
        builder.Property(p => p.Code).HasColumnName("CodeProvince").HasMaxLength(10).IsRequired();
        builder.Property(p => p.Name).HasColumnName("NomProvince").HasMaxLength(150).IsRequired();
        builder.Property(p => p.IsActive).HasColumnName("Actif");
        builder.Property(p => p.CreatedAt).HasColumnName("DateCreation");
        builder.Property(p => p.UpdatedAt).HasColumnName("DateModification");
        builder.HasOne(p => p.Country).WithMany(c => c.Provinces).HasForeignKey(p => p.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.CountryId, p.Code }).IsUnique();
    }
}

public class CityConfiguration : AuditableEntityConfiguration<City>
{
    public override void Configure(EntityTypeBuilder<City> builder)
    {
        base.Configure(builder);
        builder.ToTable("Ville");
        builder.Property(c => c.Id).HasColumnName("IdVille");
        builder.Property(c => c.ProvinceId).HasColumnName("IdProvince");
        builder.Property(c => c.Code).HasColumnName("CodeVille").HasMaxLength(10).IsRequired();
        builder.Property(c => c.Name).HasColumnName("NomVille").HasMaxLength(150).IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("Actif");
        builder.Property(c => c.CreatedAt).HasColumnName("DateCreation");
        builder.Property(c => c.UpdatedAt).HasColumnName("DateModification");
        builder.HasOne(c => c.Province).WithMany(p => p.Cities).HasForeignKey(c => c.ProvinceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.ProvinceId, c.Code }).IsUnique();
    }
}

public class CommuneConfiguration : AuditableEntityConfiguration<Commune>
{
    public override void Configure(EntityTypeBuilder<Commune> builder)
    {
        base.Configure(builder);
        builder.ToTable("Commune");
        builder.Property(c => c.Id).HasColumnName("IdCommune");
        builder.Property(c => c.CityId).HasColumnName("IdVille");
        builder.Property(c => c.Code).HasColumnName("CodeCommune").HasMaxLength(10).IsRequired();
        builder.Property(c => c.Name).HasColumnName("NomCommune").HasMaxLength(150).IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("Actif");
        builder.Property(c => c.CreatedAt).HasColumnName("DateCreation");
        builder.Property(c => c.UpdatedAt).HasColumnName("DateModification");
        builder.HasOne(c => c.City).WithMany(v => v.Communes).HasForeignKey(c => c.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.CityId, c.Code }).IsUnique();
    }
}

public class PostalAddressConfiguration : AuditableEntityConfiguration<PostalAddress>
{
    public override void Configure(EntityTypeBuilder<PostalAddress> builder)
    {
        base.Configure(builder);
        builder.ToTable("Adresse");
        builder.Property(a => a.Id).HasColumnName("IdAdresse");
        builder.Property(a => a.CountryId).HasColumnName("IdPays");
        builder.Property(a => a.ProvinceId).HasColumnName("IdProvince");
        builder.Property(a => a.CityId).HasColumnName("IdVille");
        builder.Property(a => a.CommuneId).HasColumnName("IdCommune");
        builder.Property(a => a.Neighborhood).HasColumnName("Quartier").HasMaxLength(150);
        builder.Property(a => a.Avenue).HasColumnName("Avenue").HasMaxLength(200);
        builder.Property(a => a.HouseNumber).HasColumnName("NumeroMaison").HasMaxLength(30);
        builder.Property(a => a.CreatedAt).HasColumnName("DateCreation");
        builder.Property(a => a.UpdatedAt).HasColumnName("DateModification");
        builder.HasOne(a => a.Country).WithMany().HasForeignKey(a => a.CountryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Province).WithMany().HasForeignKey(a => a.ProvinceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.City).WithMany().HasForeignKey(a => a.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Commune).WithMany(c => c.Addresses).HasForeignKey(a => a.CommuneId).OnDelete(DeleteBehavior.Restrict);
    }
}
