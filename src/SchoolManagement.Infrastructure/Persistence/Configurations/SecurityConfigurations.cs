using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Security;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

public class UserAccountConfiguration : AuditableEntityConfiguration<UserAccount>
{
    public override void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        base.Configure(builder);
        builder.ToTable("UserAccounts");
        builder.Property(u => u.UserName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.HasIndex(u => new { u.SchoolId, u.UserName }).IsUnique();
        builder.HasIndex(u => new { u.SchoolId, u.Email }).IsUnique();
    }
}

public class RoleConfiguration : AuditableEntityConfiguration<Role>
{
    public override void Configure(EntityTypeBuilder<Role> builder)
    {
        base.Configure(builder);
        builder.ToTable("Roles");
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Code).HasMaxLength(50).IsRequired();
        builder.Property(r => r.SystemRole).HasConversion<int>();
        builder.HasIndex(r => new { r.SchoolId, r.Code }).IsUnique();
    }
}

public class PermissionConfiguration : AuditableEntityConfiguration<Permission>
{
    public override void Configure(EntityTypeBuilder<Permission> builder)
    {
        base.Configure(builder);
        builder.ToTable("Permissions");
        builder.Property(p => p.Code).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Module).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Action).HasConversion<int>();
        builder.HasIndex(p => p.Code).IsUnique();
    }
}

public class RolePermissionConfiguration : AuditableEntityConfiguration<RolePermission>
{
    public override void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        base.Configure(builder);
        builder.ToTable("RolePermissions");
        builder.HasOne(rp => rp.Role).WithMany(r => r.Permissions).HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(rp => rp.Permission).WithMany(p => p.Roles).HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
    }
}

public class UserRoleAssignmentConfiguration : AuditableEntityConfiguration<UserRoleAssignment>
{
    public override void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        base.Configure(builder);
        builder.ToTable("UserRoleAssignments");
        builder.HasOne(ur => ur.User).WithMany(u => u.Roles).HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ur => ur.Role).WithMany(r => r.Users).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
    }
}

public class AuditEntryConfiguration : AuditableEntityConfiguration<AuditEntry>
{
    public override void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        base.Configure(builder);
        builder.ToTable("AuditEntries");
        builder.Property(a => a.UserName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => new { a.EntityName, a.EntityId });
    }
}

public class LoginHistoryConfiguration : AuditableEntityConfiguration<LoginHistory>
{
    public override void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        base.Configure(builder);
        builder.ToTable("LoginHistory");
        builder.Property(l => l.UserName).HasMaxLength(100).IsRequired();
        builder.HasIndex(l => l.LoginAt);
        builder.HasIndex(l => new { l.UserId, l.LoginAt });
    }
}

public class RefreshTokenConfiguration : AuditableEntityConfiguration<RefreshToken>
{
    public override void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        base.Configure(builder);
        builder.ToTable("RefreshTokens");
        builder.Property(t => t.Token).HasMaxLength(500).IsRequired();
        builder.HasOne(t => t.User).WithMany(u => u.RefreshTokens).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => t.Token).IsUnique();
    }
}
