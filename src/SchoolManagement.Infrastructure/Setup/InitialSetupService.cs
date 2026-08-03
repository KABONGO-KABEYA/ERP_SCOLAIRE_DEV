using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Application.SchoolFees.Interfaces;
using SchoolManagement.Application.Setup.DTOs;
using SchoolManagement.Application.Setup.Interfaces;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Shared.Constants;

namespace SchoolManagement.Infrastructure.Setup;

public sealed class InitialSetupService : IInitialSetupService
{
    private readonly SchoolDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISchoolService _schoolService;
    private readonly ISchoolFeeService _schoolFeeService;
    private readonly ILogger<InitialSetupService> _logger;

    public InitialSetupService(
        SchoolDbContext db,
        IPasswordHasher passwordHasher,
        ISchoolService schoolService,
        ISchoolFeeService schoolFeeService,
        ILogger<InitialSetupService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _schoolService = schoolService;
        _schoolFeeService = schoolFeeService;
        _logger = logger;
    }

    public async Task<InitialSetupStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var hasSchool = await _db.Schools.AnyAsync(cancellationToken);
        var hasPermissions = await _db.Permissions.AnyAsync(cancellationToken);

        if (hasSchool)
        {
            return new InitialSetupStatusDto(
                NeedsSetup: false,
                HasPermissions: hasPermissions,
                Message: "Établissement déjà configuré.");
        }

        return new InitialSetupStatusDto(
            NeedsSetup: true,
            HasPermissions: hasPermissions,
            Message: hasPermissions
                ? "Configuration initiale requise."
                : "Permissions système absentes — redémarrez l'API avec SEED_DATABASE=true.");
    }

    public async Task<CompleteInitialSetupResultDto> CompleteAsync(
        CompleteInitialSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _db.Schools.AnyAsync(cancellationToken))
        {
            throw new DomainException("La configuration initiale a déjà été effectuée.");
        }

        ValidateRequest(request);

        if (!await _db.Permissions.AnyAsync(cancellationToken))
        {
            throw new DomainException(
                "Les permissions système sont absentes. Redémarrez le service API (SEED_DATABASE=true) puis réessayez.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var school = new School
        {
            Name = request.SchoolName.Trim(),
            LegalName = NullIfWhiteSpace(request.LegalName),
            Address = NullIfWhiteSpace(request.Address),
            City = NullIfWhiteSpace(request.City),
            Province = NullIfWhiteSpace(request.Province),
            Phone = NullIfWhiteSpace(request.Phone),
            Email = NullIfWhiteSpace(request.Email),
            Country = "RDC",
            DefaultCurrency = request.DefaultCurrency,
            IsActive = true,
        };

        if (!string.IsNullOrWhiteSpace(request.LogoBase64) && !string.IsNullOrWhiteSpace(request.LogoFileName))
        {
            school.LogoPath = await SaveLogoAsync(request.LogoFileName!, request.LogoBase64!, cancellationToken);
        }

        _db.Schools.Add(school);
        await _db.SaveChangesAsync(cancellationToken);

        var adminRole = await EnsureRolesAsync(school.Id, cancellationToken);

        var adminUserName = request.AdminUserName.Trim();
        if (await _db.UserAccounts.AnyAsync(u => u.UserName == adminUserName, cancellationToken))
        {
            throw new DomainException($"Le nom d'utilisateur « {adminUserName} » existe déjà.");
        }

        var admin = new UserAccount
        {
            SchoolId = school.Id,
            UserName = adminUserName,
            Email = request.AdminEmail.Trim(),
            PasswordHash = _passwordHasher.Hash(request.AdminPassword),
            FirstName = request.AdminFirstName.Trim(),
            LastName = request.AdminLastName.Trim(),
            IsActive = true,
            MustChangePassword = false,
        };
        _db.UserAccounts.Add(admin);
        await _db.SaveChangesAsync(cancellationToken);

        _db.UserRoleAssignments.Add(new UserRoleAssignment
        {
            UserId = admin.Id,
            RoleId = adminRole.Id,
        });
        await _db.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);

        var year = await _schoolService.CreateAcademicYearAsync(
            school.Id,
            new CreateAcademicYearRequest(
                request.AcademicYearLabel.Trim(),
                request.AcademicYearStart,
                request.AcademicYearEnd,
                SetAsCurrent: true),
            cancellationToken);

        await SeedFinanceBasicsAsync(school.Id, request, cancellationToken);

        _logger.LogInformation(
            "Configuration initiale terminée — école {School} ({SchoolId}), admin {Admin}",
            school.Name, school.Id, admin.UserName);

        return new CompleteInitialSetupResultDto(
            school.Id,
            year.Id,
            admin.Id,
            school.Name,
            admin.UserName);
    }

    private async Task SeedFinanceBasicsAsync(
        Guid schoolId,
        CompleteInitialSetupRequest request,
        CancellationToken cancellationToken)
    {
        var categories = request.PricingCategoryNames is { Count: > 0 }
            ? request.PricingCategoryNames
            : ["Général"];

        foreach (var name in categories.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            await _schoolFeeService.CreatePricingCategoryAsync(
                schoolId,
                new CreateFeePricingCategoryRequest(name.Trim(), null, true),
                cancellationToken);
        }

        var installments = request.InstallmentNames is { Count: > 0 }
            ? request.InstallmentNames
            : new[] { "Inscription", "1ère tranche", "2ème tranche", "3ème tranche" };

        var order = 1;
        foreach (var name in installments.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            await _schoolFeeService.CreateInstallmentAsync(
                schoolId,
                new SaveFeeInstallmentRequest(name.Trim(), order++, true),
                cancellationToken);
        }

        var feeTypes = request.FeeTypes is { Count: > 0 }
            ? request.FeeTypes
            : new[]
            {
                new InitialFeeTypeRequest("Frais scolaires", request.DefaultCurrency, true),
                new InitialFeeTypeRequest("Frais d'inscription", request.DefaultCurrency, true),
            };

        Guid? defaultFeeId = null;
        foreach (var fee in feeTypes.Where(f => !string.IsNullOrWhiteSpace(f.Name)))
        {
            var created = await _schoolFeeService.CreateFeeTypeAsync(
                schoolId,
                new CreateFeeTypeRequest(fee.Name.Trim(), fee.Currency, fee.IsMandatory, true),
                cancellationToken);
            defaultFeeId ??= created.Id;
        }

        if (defaultFeeId is Guid feeId)
        {
            var school = await _db.Schools.FirstAsync(s => s.Id == schoolId, cancellationToken);
            school.DefaultFeeTypeId = feeId;
            school.DefaultCurrency = request.DefaultCurrency;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Role> EnsureRolesAsync(Guid schoolId, CancellationToken cancellationToken)
    {
        var allPermissions = await _db.Permissions.ToListAsync(cancellationToken);

        Role CreateRole(string code, string name, UserRole systemRole)
        {
            var role = new Role
            {
                SchoolId = schoolId,
                Code = code,
                Name = name,
                SystemRole = systemRole,
            };
            _db.Roles.Add(role);
            return role;
        }

        var admin = CreateRole("ADMIN", "Administrateur", UserRole.Administrateur);
        CreateRole("DIRECTION", "Direction", UserRole.Direction);
        CreateRole("TEACHER", "Enseignant", UserRole.Enseignant);
        CreateRole("PARENT", "Parent", UserRole.Parent);
        CreateRole("COMPTABLE", "Comptable", UserRole.Comptable);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var permission in allPermissions)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = admin.Id,
                PermissionId = permission.Id,
            });
        }

        await AssignPermissionsAsync("DIRECTION",
        [
            Permissions.SchoolsRead, Permissions.SchoolsUpdate,
            Permissions.StudentsRead, Permissions.StudentsCreate, Permissions.StudentsUpdate,
            Permissions.ResultsValidationRead, Permissions.ResultsValidationValidate,
            Permissions.DeliberationPvRead, Permissions.DeliberationPvWrite,
            Permissions.DeliberationDecisionRead, Permissions.DeliberationDecisionWrite,
        ], cancellationToken);

        await AssignPermissionsAsync("TEACHER",
        [
            Permissions.StudentsRead,
            Permissions.ResultsValidationRead,
            Permissions.DeliberationPvRead,
            Permissions.DeliberationDecisionRead,
        ], cancellationToken);

        await AssignPermissionsAsync("COMPTABLE",
        [
            Permissions.StudentsRead,
            Permissions.PaymentsRead, Permissions.PaymentsCreate, Permissions.PaymentsValidate,
        ], cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        return admin;
    }

    private async Task AssignPermissionsAsync(
        string roleCode,
        IEnumerable<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode, cancellationToken);
        if (role is null) return;

        var codes = permissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissions = await _db.Permissions
            .Where(p => codes.Contains(p.Code))
            .ToListAsync(cancellationToken);

        foreach (var permission in permissions)
        {
            if (!await _db.RolePermissions.AnyAsync(
                    rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id, cancellationToken))
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                });
            }
        }
    }

    private static async Task<string> SaveLogoAsync(
        string fileName,
        string base64,
        CancellationToken cancellationToken)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "logo.png";

        var bytes = Convert.FromBase64String(base64);
        if (bytes.Length > 5 * 1024 * 1024)
            throw new DomainException("Le logo ne doit pas dépasser 5 Mo.");

        var root = Environment.GetEnvironmentVariable("FILE_STORAGE_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ERP Scolaire",
                "Dossier_Eleve");
        }

        var branding = Path.Combine(root, "Branding");
        Directory.CreateDirectory(branding);
        var path = Path.Combine(branding, $"school-logo{Path.GetExtension(safeName)}");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return path;
    }

    private static void ValidateRequest(CompleteInitialSetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SchoolName))
            throw new DomainException("Le nom de l'école est obligatoire.");
        if (string.IsNullOrWhiteSpace(request.AcademicYearLabel))
            throw new DomainException("Le libellé de l'année scolaire est obligatoire.");
        if (request.AcademicYearEnd <= request.AcademicYearStart)
            throw new DomainException("La date de fin d'année doit être postérieure à la date de début.");
        if (string.IsNullOrWhiteSpace(request.AdminUserName))
            throw new DomainException("Le nom d'utilisateur administrateur est obligatoire.");
        if (string.IsNullOrWhiteSpace(request.AdminEmail))
            throw new DomainException("L'email administrateur est obligatoire.");
        if (string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Length < 8)
            throw new DomainException("Le mot de passe administrateur doit contenir au moins 8 caractères.");
        if (string.IsNullOrWhiteSpace(request.AdminFirstName) || string.IsNullOrWhiteSpace(request.AdminLastName))
            throw new DomainException("Le prénom et le nom de l'administrateur sont obligatoires.");
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
