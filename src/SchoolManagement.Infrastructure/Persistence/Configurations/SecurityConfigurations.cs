using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;

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
        builder.Property(u => u.IsPlatformSuperAdmin).HasDefaultValue(false);
        builder.HasIndex(u => new { u.SchoolId, u.UserName }).IsUnique();
        builder.HasIndex(u => new { u.SchoolId, u.Email }).IsUnique();
        builder.HasIndex(u => u.TeacherId)
            .IsUnique()
            .HasFilter("[TeacherId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(u => u.GuardianId)
            .IsUnique()
            .HasFilter("[GuardianId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasOne(u => u.ResidenceAddress).WithMany().HasForeignKey(u => u.AddressId).OnDelete(DeleteBehavior.SetNull);
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
        builder.Property(r => r.IsSystem).HasDefaultValue(false);
        builder.Property(r => r.IsAssignable).HasDefaultValue(true);
        builder.Property(r => r.SortOrder).HasDefaultValue(0);
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
        builder.Property(p => p.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(p => p.BusinessDescription).IsRequired();
        builder.Property(p => p.IsActive).HasDefaultValue(true);
        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasIndex(p => p.SecurityActionId);
        builder.HasOne(p => p.SecurityAction)
            .WithMany(a => a.Permissions)
            .HasForeignKey(p => p.SecurityActionId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.HasOne<School>().WithMany().HasForeignKey(a => a.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.SchoolId, a.Timestamp });
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
        builder.HasOne<School>().WithMany().HasForeignKey(l => l.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.SchoolId, l.LoginAt });
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

public class SecurityModuleConfiguration : AuditableEntityConfiguration<SecurityModule>
{
    public override void Configure(EntityTypeBuilder<SecurityModule> builder)
    {
        base.Configure(builder);
        builder.ToTable("SecurityModules");
        builder.Property(m => m.Code).HasMaxLength(50).IsRequired();
        builder.Property(m => m.Name).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Icon).HasMaxLength(100);
        builder.Property(m => m.IsActive).HasDefaultValue(true);
        builder.HasIndex(m => m.Code).IsUnique();
    }
}

public class SecurityFunctionConfiguration : AuditableEntityConfiguration<SecurityFunction>
{
    public override void Configure(EntityTypeBuilder<SecurityFunction> builder)
    {
        base.Configure(builder);
        builder.ToTable("SecurityFunctions");
        builder.Property(f => f.Code).HasMaxLength(50).IsRequired();
        builder.Property(f => f.Name).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Icon).HasMaxLength(100);
        builder.Property(f => f.IsActive).HasDefaultValue(true);
        builder.HasOne(f => f.Module).WithMany(m => m.Functions).HasForeignKey(f => f.ModuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(f => new { f.ModuleId, f.Code }).IsUnique();
    }
}

public class SecurityPageConfiguration : AuditableEntityConfiguration<SecurityPage>
{
    public override void Configure(EntityTypeBuilder<SecurityPage> builder)
    {
        base.Configure(builder);
        builder.ToTable("SecurityPages");
        builder.Property(p => p.Code).HasMaxLength(80).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.RequiredPermissionCode).HasMaxLength(100);
        builder.Property(p => p.DesktopViewKey).HasMaxLength(150);
        builder.Property(p => p.WebRoute).HasMaxLength(200);
        builder.Property(p => p.MobileScreenKey).HasMaxLength(150);
        builder.Property(p => p.DeepLink).HasMaxLength(300);
        builder.Property(p => p.IsActive).HasDefaultValue(true);
        builder.HasOne(p => p.Function).WithMany(f => f.Pages).HasForeignKey(p => p.FunctionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.FunctionId, p.Code }).IsUnique();
        builder.HasIndex(p => p.DesktopViewKey);
    }
}

public class SecurityActionConfiguration : AuditableEntityConfiguration<SecurityAction>
{
    public override void Configure(EntityTypeBuilder<SecurityAction> builder)
    {
        base.Configure(builder);
        builder.ToTable("SecurityActions");
        builder.Property(a => a.Code).HasMaxLength(80).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(150).IsRequired();
        builder.Property(a => a.IsActive).HasDefaultValue(true);
        builder.HasOne(a => a.Page).WithMany(p => p.Actions).HasForeignKey(a => a.PageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.PageId, a.Code }).IsUnique();
    }
}

public class PermissionDependencyConfiguration : AuditableEntityConfiguration<PermissionDependency>
{
    public override void Configure(EntityTypeBuilder<PermissionDependency> builder)
    {
        base.Configure(builder);
        builder.ToTable("PermissionDependencies");
        builder.Property(d => d.IsActive).HasDefaultValue(true);
        builder.HasOne(d => d.Permission)
            .WithMany(p => p.Dependencies)
            .HasForeignKey(d => d.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(d => d.RequiresPermission)
            .WithMany(p => p.RequiredBy)
            .HasForeignKey(d => d.RequiresPermissionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => new { d.PermissionId, d.RequiresPermissionId }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_PermissionDependencies_NoSelf",
            "[PermissionId] <> [RequiresPermissionId]"));
    }
}

public class UserPermissionExceptionConfiguration : AuditableEntityConfiguration<UserPermissionException>
{
    public override void Configure(EntityTypeBuilder<UserPermissionException> builder)
    {
        base.Configure(builder);
        builder.ToTable("UserPermissionExceptions");
        builder.Property(e => e.Effect).HasConversion<int>();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.HasOne<School>().WithMany().HasForeignKey(e => e.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.User)
            .WithMany(u => u.PermissionExceptions)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Permission).WithMany().HasForeignKey(e => e.PermissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.GrantedByUser).WithMany().HasForeignKey(e => e.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.UserId, e.PermissionId, e.Effect, e.ValidFrom, e.ValidTo });
        builder.HasIndex(e => new { e.SchoolId, e.UserId });
    }
}

public class SecurityAuditLogConfiguration : AuditableEntityConfiguration<SecurityAuditLog>
{
    public override void Configure(EntityTypeBuilder<SecurityAuditLog> builder)
    {
        base.Configure(builder);
        builder.ToTable("SecurityAuditLogs");
        builder.Property(l => l.ActorUserName).HasMaxLength(100).IsRequired();
        builder.Property(l => l.ActorKind).HasConversion<int>();
        builder.Property(l => l.ActionType).HasMaxLength(80).IsRequired();
        builder.Property(l => l.TargetEntityType).HasMaxLength(100);
        builder.Property(l => l.TargetUserName).HasMaxLength(100);
        builder.Property(l => l.Summary).HasMaxLength(500).IsRequired();
        builder.Property(l => l.IpAddress).HasMaxLength(64);
        builder.Property(l => l.UserAgent).HasMaxLength(500);
        builder.HasOne<School>().WithMany().HasForeignKey(l => l.SchoolId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(l => new { l.SchoolId, l.OccurredAtUtc });
        builder.HasIndex(l => new { l.ActionType, l.OccurredAtUtc });
    }
}
