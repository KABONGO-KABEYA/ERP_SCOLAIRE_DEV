using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolManagement.API.Authorization;
using SchoolManagement.Application.Auth.DTOs;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Security;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Auth;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Repositories;
using SchoolManagement.Infrastructure.Security;
using SchoolManagement.Shared.Constants;

namespace Phase1SecurityValidation;

internal static class Program
{
    private const string TestUserPrefix = "__p1v_";
    private const string TestPassword = "Phase1Valid@2026";

    private static readonly List<CheckResult> Results = [];
    private static readonly Dictionary<string, object?> Evidence = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<int> Main(string[] args)
    {
        var repoRoot = FindRepoRoot();
        var outDir = Path.Combine(repoRoot, "tools", "Phase1SecurityValidation", "out");
        Directory.CreateDirectory(outDir);
        Evidence["startedAtUtc"] = DateTime.UtcNow;
        Evidence["repoRoot"] = repoRoot;

        Console.WriteLine("=== Phase 1 Security Validation ===");
        Console.WriteLine($"Repo: {repoRoot}");

        await using var db = CreateDbContext(repoRoot);
        db.IgnoreSchoolScope = true;

        var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning).AddConsole());
        var cache = new SecurityCatalogCache();
        var deps = new PermissionDependencyService(db, cache, loggerFactory.CreateLogger<PermissionDependencyService>());
        var effective = new EffectivePermissionService(db, deps, loggerFactory.CreateLogger<EffectivePermissionService>());
        var hasher = new BcryptPasswordHasher();
        var jwtSettings = LoadJwtSettings(repoRoot);
        var tokenService = new JwtTokenService(Options.Create(jwtSettings));
        var unitOfWork = new UnitOfWork(db);
        var userRepo = new UserAccountRepository(db);
        var refreshRepo = new RefreshTokenRepository(db);
        var auth = new AuthService(userRepo, refreshRepo, tokenService, hasher, unitOfWork, effective, db);

        var schoolId = await db.Schools.AsNoTracking().Select(s => s.Id).FirstAsync();
        Evidence["schoolId"] = schoolId;

        var roles = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.SchoolId == schoolId && !r.IsDeleted)
            .ToDictionaryAsync(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);
        var permissions = await db.Permissions.IgnoreQueryFilters()
            .Where(p => !p.IsDeleted)
            .ToDictionaryAsync(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        Evidence["roleCodes"] = roles.Keys.OrderBy(x => x).ToList();
        Evidence["permissionCount"] = permissions.Count;

        var createdUserIds = new List<Guid>();
        try
        {
            await CleanupPreviousAsync(db);

            // --- Scénarios Resolve / HasPermission ---
            var parentUser = await CreateUserAsync(db, hasher, schoolId, roles, "parent_single", "PARENT");
            createdUserIds.Add(parentUser.Id);
            await ScenarioSingleRoleAsync(effective, parentUser);

            var multiUser = await CreateUserAsync(db, hasher, schoolId, roles, "multi_roles", "ENSEIGNANT", "COMPTABLE");
            createdUserIds.Add(multiUser.Id);
            await ScenarioMultiRolesAsync(effective, multiUser);

            var grantUser = await CreateUserAsync(db, hasher, schoolId, roles, "grant_only", "PARENT");
            createdUserIds.Add(grantUser.Id);
            await ScenarioGrantAsync(db, effective, schoolId, grantUser, permissions);

            var denyUser = await CreateUserAsync(db, hasher, schoolId, roles, "deny", "ENSEIGNANT");
            createdUserIds.Add(denyUser.Id);
            await ScenarioDenyAsync(db, effective, schoolId, denyUser, permissions);

            var expiryUser = await CreateUserAsync(db, hasher, schoolId, roles, "expiry", "PARENT");
            createdUserIds.Add(expiryUser.Id);
            await ScenarioExpiryAsync(db, effective, schoolId, expiryUser, permissions);

            var depUser = await CreateUserAsync(db, hasher, schoolId, roles, "deps", Array.Empty<string>());
            createdUserIds.Add(depUser.Id);
            await ScenarioDependenciesAsync(db, effective, deps, schoolId, depUser, permissions);

            var adminUser = await CreateUserAsync(db, hasher, schoolId, roles, "admin", "ADMIN");
            createdUserIds.Add(adminUser.Id);
            await ScenarioAdminBypassAsync(effective, adminUser, permissions.Count);

            var superUser = await CreateUserAsync(db, hasher, schoolId, roles, "super", isPlatformSuperAdmin: true, "PARENT");
            createdUserIds.Add(superUser.Id);
            await ScenarioSuperAdminAsync(effective, superUser);

            await ScenarioHasPermissionAsync(effective, parentUser, adminUser);

            // --- JWT codes only ---
            await ScenarioJwtCodesOnlyAsync(tokenService, effective, parentUser, adminUser);

            // --- Auth login / refresh / IsActive ---
            await ScenarioAuthLifecycleAsync(auth, db, refreshRepo, unitOfWork, parentUser);

            // --- Policies ASP.NET ---
            await ScenarioPoliciesAsync(parentUser, adminUser, effective);

            // --- Perf + cache memory ---
            await ScenarioPerformanceAsync(effective, cache, deps, parentUser, adminUser);

            // --- Non-régression: inventaire policies endpoints + smoke Authorize dynamique ---
            await ScenarioEndpointPolicyInventoryAsync(repoRoot);
            await ScenarioDynamicPolicyProviderAsync();
        }
        finally
        {
            await CleanupUsersAsync(db, createdUserIds);
        }

        Evidence["finishedAtUtc"] = DateTime.UtcNow;
        Evidence["checks"] = Results.Select(r => new
        {
            r.Name,
            r.Passed,
            r.Detail
        }).ToList();
        Evidence["passed"] = Results.Count(r => r.Passed);
        Evidence["failed"] = Results.Count(r => !r.Passed);
        Evidence["total"] = Results.Count;

        var evidencePath = Path.Combine(outDir, "evidence.json");
        await File.WriteAllTextAsync(
            evidencePath,
            JsonSerializer.Serialize(Evidence, new JsonSerializerOptions { WriteIndented = true }));

        var summaryPath = Path.Combine(outDir, "summary.txt");
        await using (var sw = new StreamWriter(summaryPath))
        {
            foreach (var r in Results)
            {
                var line = $"{(r.Passed ? "PASS" : "FAIL")} | {r.Name} | {r.Detail}";
                Console.WriteLine(line);
                await sw.WriteLineAsync(line);
            }

            await sw.WriteLineAsync($"TOTAL: {Results.Count(r => r.Passed)}/{Results.Count} passed");
        }

        Console.WriteLine($"Evidence: {evidencePath}");
        return Results.All(r => r.Passed) ? 0 : 1;
    }

    private static async Task ScenarioSingleRoleAsync(IEffectivePermissionService effective, UserAccount user)
    {
        var result = await effective.ResolveAsync(user.Id);
        var ok = result.Roles.Count == 1
                 && result.Roles.Contains("PARENT", StringComparer.OrdinalIgnoreCase)
                 && result.PermissionCodes.Contains(Permissions.PaymentsRead, StringComparer.OrdinalIgnoreCase)
                 && result.PermissionCodes.Contains(Permissions.GradesRead, StringComparer.OrdinalIgnoreCase)
                 && !result.PermissionCodes.Contains(Permissions.PaymentsCreate, StringComparer.OrdinalIgnoreCase)
                 && !result.IsPlatformSuperAdmin;
        Check("Resolve — un seul rôle (PARENT)", ok,
            $"roles=[{string.Join(',', result.Roles)}] perms={result.PermissionCodes.Count} sample=[{string.Join(',', result.PermissionCodes.Take(5))}]");
        Evidence["singleRole"] = new { result.Roles, result.PermissionCodes, result.IsPlatformSuperAdmin };
    }

    private static async Task ScenarioMultiRolesAsync(IEffectivePermissionService effective, UserAccount user)
    {
        var result = await effective.ResolveAsync(user.Id);
        var ok = result.Roles.Contains("ENSEIGNANT", StringComparer.OrdinalIgnoreCase)
                 && result.Roles.Contains("COMPTABLE", StringComparer.OrdinalIgnoreCase)
                 && result.PermissionCodes.Contains(Permissions.GradesCreate, StringComparer.OrdinalIgnoreCase)
                 && result.PermissionCodes.Contains(Permissions.PaymentsValidate, StringComparer.OrdinalIgnoreCase);
        Check("Resolve — multi-rôles (ENSEIGNANT ∪ COMPTABLE)", ok,
            $"roles=[{string.Join(',', result.Roles)}] has grades.create={result.PermissionCodes.Contains(Permissions.GradesCreate)} has payments.validate={result.PermissionCodes.Contains(Permissions.PaymentsValidate)}");
        Evidence["multiRoles"] = new { result.Roles, PermissionCount = result.PermissionCodes.Count, result.PermissionCodes };
    }

    private static async Task ScenarioGrantAsync(
        SchoolDbContext db,
        IEffectivePermissionService effective,
        Guid schoolId,
        UserAccount user,
        Dictionary<string, Permission> permissions)
    {
        var before = await effective.ResolveAsync(user.Id);
        var hadCreate = before.PermissionCodes.Contains(Permissions.StudentsCreate, StringComparer.OrdinalIgnoreCase);

        await AddExceptionAsync(db, schoolId, user.Id, permissions[Permissions.StudentsRead].Id, PermissionExceptionEffect.Grant,
            DateTime.UtcNow.AddHours(-1), null);
        await AddExceptionAsync(db, schoolId, user.Id, permissions[Permissions.StudentsCreate].Id, PermissionExceptionEffect.Grant,
            DateTime.UtcNow.AddHours(-1), null);

        var after = await effective.ResolveAsync(user.Id);
        var ok = !hadCreate
                 && after.PermissionCodes.Contains(Permissions.StudentsRead, StringComparer.OrdinalIgnoreCase)
                 && after.PermissionCodes.Contains(Permissions.StudentsCreate, StringComparer.OrdinalIgnoreCase);
        Check("Resolve — Grant (students.read + students.create)", ok,
            $"beforeCreate={hadCreate} afterRead={after.PermissionCodes.Contains(Permissions.StudentsRead)} afterCreate={after.PermissionCodes.Contains(Permissions.StudentsCreate)}");
        Evidence["grant"] = new { beforeCreate = hadCreate, after.PermissionCodes };
    }

    private static async Task ScenarioDenyAsync(
        SchoolDbContext db,
        IEffectivePermissionService effective,
        Guid schoolId,
        UserAccount user,
        Dictionary<string, Permission> permissions)
    {
        var before = await effective.ResolveAsync(user.Id);
        var hadRead = before.PermissionCodes.Contains(Permissions.GradesRead, StringComparer.OrdinalIgnoreCase);
        var hadCreate = before.PermissionCodes.Contains(Permissions.GradesCreate, StringComparer.OrdinalIgnoreCase);

        await AddExceptionAsync(db, schoolId, user.Id, permissions[Permissions.GradesRead].Id, PermissionExceptionEffect.Deny,
            DateTime.UtcNow.AddHours(-1), null);

        var after = await effective.ResolveAsync(user.Id);
        var ok = hadRead && hadCreate
                 && !after.PermissionCodes.Contains(Permissions.GradesRead, StringComparer.OrdinalIgnoreCase)
                 && !after.PermissionCodes.Contains(Permissions.GradesCreate, StringComparer.OrdinalIgnoreCase);
        Check("Resolve — Deny (grades.read → retire aussi grades.create via closure)", ok,
            $"before read/create={hadRead}/{hadCreate} after read/create={after.PermissionCodes.Contains(Permissions.GradesRead)}/{after.PermissionCodes.Contains(Permissions.GradesCreate)}");
        Evidence["deny"] = new { hadRead, hadCreate, after.PermissionCodes };
    }

    private static async Task ScenarioExpiryAsync(
        SchoolDbContext db,
        IEffectivePermissionService effective,
        Guid schoolId,
        UserAccount user,
        Dictionary<string, Permission> permissions)
    {
        await AddExceptionAsync(db, schoolId, user.Id, permissions[Permissions.CurrenciesRead].Id, PermissionExceptionEffect.Grant,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddMinutes(-5)); // expired (ValidTo exclusive)
        await AddExceptionAsync(db, schoolId, user.Id, permissions[Permissions.SchoolsRead].Id, PermissionExceptionEffect.Grant,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2)); // active

        var result = await effective.ResolveAsync(user.Id);
        var ok = !result.PermissionCodes.Contains(Permissions.CurrenciesRead, StringComparer.OrdinalIgnoreCase)
                 && result.PermissionCodes.Contains(Permissions.SchoolsRead, StringComparer.OrdinalIgnoreCase);
        Check("Resolve — expiration exception (ValidTo exclusive)", ok,
            $"expired currencies.read={result.PermissionCodes.Contains(Permissions.CurrenciesRead)} active schools.read={result.PermissionCodes.Contains(Permissions.SchoolsRead)}");
        Evidence["expiry"] = new { result.PermissionCodes };
    }

    private static async Task ScenarioDependenciesAsync(
        SchoolDbContext db,
        IEffectivePermissionService effective,
        PermissionDependencyService deps,
        Guid schoolId,
        UserAccount user,
        Dictionary<string, Permission> permissions)
    {
        var closure = await deps.GetRequiredClosureAsync(Permissions.StudentsCreate);
        Check("Deps — closure students.create contient students.read",
            closure.Contains(Permissions.StudentsCreate) && closure.Contains(Permissions.StudentsRead),
            $"closure=[{string.Join(',', closure)}]");

        await AddExceptionAsync(db, schoolId, user.Id, permissions[Permissions.StudentsCreate].Id, PermissionExceptionEffect.Grant,
            DateTime.UtcNow.AddHours(-1), null);

        var incomplete = await effective.ResolveAsync(user.Id);
        var incompleteOk = !incomplete.PermissionCodes.Contains(Permissions.StudentsCreate, StringComparer.OrdinalIgnoreCase);

        await AddExceptionAsync(db, schoolId, user.Id, permissions[Permissions.StudentsRead].Id, PermissionExceptionEffect.Grant,
            DateTime.UtcNow.AddHours(-1), null);
        var complete = await effective.ResolveAsync(user.Id);
        var completeOk = complete.PermissionCodes.Contains(Permissions.StudentsCreate, StringComparer.OrdinalIgnoreCase)
                         && complete.PermissionCodes.Contains(Permissions.StudentsRead, StringComparer.OrdinalIgnoreCase);

        Check("Resolve — dépendances (create sans read = retiré ; avec read = OK)", incompleteOk && completeOk,
            $"incompleteCreate={incomplete.PermissionCodes.Contains(Permissions.StudentsCreate)} completeCreate={complete.PermissionCodes.Contains(Permissions.StudentsCreate)}");
        Evidence["dependencies"] = new
        {
            Closure = closure.ToList(),
            Incomplete = incomplete.PermissionCodes,
            Complete = complete.PermissionCodes
        };
    }

    private static async Task ScenarioAdminBypassAsync(
        IEffectivePermissionService effective,
        UserAccount user,
        int activePermissionCount)
    {
        var result = await effective.ResolveAsync(user.Id);
        var ok = result.Roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase)
                 && result.PermissionCodes.Count >= Math.Max(1, activePermissionCount - 2)
                 && result.PermissionCodes.Contains(Permissions.AdminFull, StringComparer.OrdinalIgnoreCase);
        Check("Resolve — bypass ADMIN (catalogue actif)", ok,
            $"roles=[{string.Join(',', result.Roles)}] permCount={result.PermissionCodes.Count} activeCatalog≈{activePermissionCount} hasAdminFull={result.PermissionCodes.Contains(Permissions.AdminFull)}");
        Evidence["adminBypass"] = new { result.Roles, PermissionCount = result.PermissionCodes.Count, HasAdminFull = result.PermissionCodes.Contains(Permissions.AdminFull) };
    }

    private static async Task ScenarioSuperAdminAsync(IEffectivePermissionService effective, UserAccount user)
    {
        var result = await effective.ResolveAsync(user.Id);
        var ok = result.IsPlatformSuperAdmin
                 && result.PermissionCodes.Contains(Permissions.PlatformSuperAdmin, StringComparer.OrdinalIgnoreCase)
                 && result.PermissionCodes.Contains(Permissions.PlatformCatalogManage, StringComparer.OrdinalIgnoreCase)
                 && result.Roles.Contains("PARENT", StringComparer.OrdinalIgnoreCase);
        Check("Resolve — Super Administrateur plateforme (platform.*)", ok,
            $"flag={result.IsPlatformSuperAdmin} platform.superadmin={result.PermissionCodes.Contains(Permissions.PlatformSuperAdmin)} platform.catalog.manage={result.PermissionCodes.Contains(Permissions.PlatformCatalogManage)}");
        Evidence["superAdmin"] = new { result.IsPlatformSuperAdmin, result.Roles, PlatformPerms = result.PermissionCodes.Where(p => p.StartsWith("platform.", StringComparison.OrdinalIgnoreCase)).ToList() };
    }

    private static async Task ScenarioHasPermissionAsync(
        IEffectivePermissionService effective,
        UserAccount parent,
        UserAccount admin)
    {
        var parentPayments = await effective.HasPermissionAsync(parent.Id, Permissions.PaymentsRead);
        var parentCreate = await effective.HasPermissionAsync(parent.Id, Permissions.StudentsCreate);
        var adminAny = await effective.HasPermissionAsync(admin.Id, Permissions.CurrenciesDelete);
        var empty = await effective.HasPermissionAsync(parent.Id, " ");
        var ok = parentPayments && !parentCreate && adminAny && !empty;
        Check("HasPermissionAsync (parent/admin/empty)", ok,
            $"parent.payments.read={parentPayments} parent.students.create={parentCreate} admin.currencies.delete={adminAny} empty={empty}");
        Evidence["hasPermission"] = new { parentPayments, parentCreate, adminAny, empty };
    }

    private static async Task ScenarioJwtCodesOnlyAsync(
        ITokenService tokenService,
        IEffectivePermissionService effective,
        UserAccount parent,
        UserAccount admin)
    {
        var parentEff = await effective.ResolveAsync(parent.Id);
        var adminEff = await effective.ResolveAsync(admin.Id);

        var parentJwt = tokenService.GenerateAccessToken(
            parent.Id, parent.SchoolId, parent.UserName, "Parent Test",
            parentEff.Roles, parentEff.PermissionCodes, parentEff.IsPlatformSuperAdmin);
        var adminJwt = tokenService.GenerateAccessToken(
            admin.Id, admin.SchoolId, admin.UserName, "Admin Test",
            adminEff.Roles, adminEff.PermissionCodes, adminEff.IsPlatformSuperAdmin);

        var parentCheck = InspectJwt(parentJwt, parentEff.PermissionCodes);
        var adminCheck = InspectJwt(adminJwt, adminEff.PermissionCodes);
        var forbidden = new[] { "DisplayName", "BusinessDescription", "HelpText", "displayName", "helpText" };
        var parentPayload = DecodePayloadJson(parentJwt);
        var hasMeta = forbidden.Any(f => parentPayload.Contains(f, StringComparison.Ordinal));

        Check("JWT — permissions = codes uniquement (parent)", parentCheck.Ok && !hasMeta, parentCheck.Detail);
        Check("JWT — permissions = codes uniquement (admin)", adminCheck.Ok, adminCheck.Detail);
        Check("JWT — claim platform_superadmin absent pour parent",
            !parentCheck.Claims.Any(c => c.Type == ClaimTypesCustom.PlatformSuperAdmin),
            $"claims types=[{string.Join(',', parentCheck.Claims.Select(c => c.Type).Distinct())}]");
        Check("JWT — claim platform_superadmin présent pour admin si flag",
            !adminEff.IsPlatformSuperAdmin || adminCheck.Claims.Any(c => c.Type == ClaimTypesCustom.PlatformSuperAdmin && c.Value == "true"),
            $"isPlatformSuperAdmin={adminEff.IsPlatformSuperAdmin}");

        Evidence["jwt"] = new
        {
            ParentPermissionClaims = parentCheck.PermissionValues,
            ParentForbiddenMetadataFound = hasMeta,
            ParentPayloadLength = parentPayload.Length,
            AdminPermissionClaimCount = adminCheck.PermissionValues.Count,
            SampleCodes = parentCheck.PermissionValues.Take(10).ToList()
        };
    }

    private static async Task ScenarioAuthLifecycleAsync(
        IAuthService auth,
        SchoolDbContext db,
        IRefreshTokenRepository refreshRepo,
        IUnitOfWork unitOfWork,
        UserAccount user)
    {
        try
        {
            var login = await auth.LoginAsync(new LoginRequest(user.UserName, TestPassword), "127.0.0.1");
            Check("Auth — login OK", !string.IsNullOrWhiteSpace(login.AccessToken) && !string.IsNullOrWhiteSpace(login.RefreshToken),
                $"user={user.UserName} tokenLen={login.AccessToken.Length}");

            AuthResponse? refreshed = null;
            try
            {
                refreshed = await auth.RefreshTokenAsync(new RefreshTokenRequest(login.RefreshToken), "127.0.0.1");
                Check("Auth — refresh OK", !string.IsNullOrWhiteSpace(refreshed.AccessToken),
                    $"newTokenLen={refreshed.AccessToken.Length}");
            }
            catch (Exception ex)
            {
                Check("Auth — refresh OK", false, ex.Message);
            }

            var entity = await db.UserAccounts.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
            entity.IsActive = false;
            await db.SaveChangesAsync();
            await refreshRepo.RevokeAllForUserAsync(user.Id);
            await unitOfWork.SaveChangesAsync();

            var loginInactiveFailed = false;
            try
            {
                await auth.LoginAsync(new LoginRequest(user.UserName, TestPassword), "127.0.0.1");
            }
            catch (UnauthorizedAccessException)
            {
                loginInactiveFailed = true;
            }

            var refreshInactiveFailed = false;
            try
            {
                var tokenToRefresh = refreshed?.RefreshToken ?? login.RefreshToken;
                await auth.RefreshTokenAsync(new RefreshTokenRequest(tokenToRefresh), "127.0.0.1");
            }
            catch (UnauthorizedAccessException)
            {
                refreshInactiveFailed = true;
            }

            Check("Auth — login refusé si IsActive=false", loginInactiveFailed, "UnauthorizedAccessException attendue");
            Check("Auth — refresh refusé si IsActive=false (ou token révoqué)", refreshInactiveFailed, "UnauthorizedAccessException attendue");

            entity.IsActive = true;
            await db.SaveChangesAsync();

            Evidence["authLifecycle"] = new
            {
                LoginOk = true,
                RefreshOk = refreshed is not null,
                LoginInactiveRejected = loginInactiveFailed,
                RefreshInactiveRejected = refreshInactiveFailed
            };
        }
        catch (Exception ex)
        {
            Check("Auth — login OK", false, ex.ToString());
            Check("Auth — refresh OK", false, "skipped after login failure");
            Check("Auth — login refusé si IsActive=false", false, "skipped");
            Check("Auth — refresh refusé si IsActive=false (ou token révoqué)", false, "skipped");
            Evidence["authLifecycleError"] = ex.ToString();
        }
    }

    private static async Task ScenarioPoliciesAsync(
        UserAccount parent,
        UserAccount admin,
        IEffectivePermissionService effective)
    {
        var parentEff = await effective.ResolveAsync(parent.Id);
        var adminEff = await effective.ResolveAsync(admin.Id);

        var parentCurrent = new FakeCurrentUserService(parent.Id, parent.SchoolId, parent.UserName, parentEff.Roles, parentEff.PermissionCodes);
        var adminCurrent = new FakeCurrentUserService(admin.Id, admin.SchoolId, admin.UserName, adminEff.Roles, adminEff.PermissionCodes);

        async Task<(bool Succeeded, string Detail)> EvaluateAsync(ICurrentUserService current, string policy)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization();
            services.AddSingleton(current);
            services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
            await using var sp = services.BuildServiceProvider();
            var authz = sp.GetRequiredService<IAuthorizationService>();
            var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
            var result = await authz.AuthorizeAsync(principal, resource: null, policyName: policy);
            return (result.Succeeded, $"policy={policy} succeeded={result.Succeeded}");
        }

        var parentAllow = await EvaluateAsync(parentCurrent, Permissions.PaymentsRead);
        var parentDeny = await EvaluateAsync(parentCurrent, Permissions.StudentsDelete);
        var adminAllow = await EvaluateAsync(adminCurrent, Permissions.StudentsDelete);
        var dynamicAllow = await EvaluateAsync(parentCurrent, Permissions.GradesRead);

        Check("Policy — PARENT autorisé payments.read", parentAllow.Succeeded, parentAllow.Detail);
        Check("Policy — PARENT refusé students.delete", !parentDeny.Succeeded, parentDeny.Detail);
        Check("Policy — ADMIN autorisé students.delete (admin.full)", adminAllow.Succeeded, adminAllow.Detail);
        Check("Policy — dynamique grades.read pour PARENT", dynamicAllow.Succeeded, dynamicAllow.Detail);

        Evidence["policies"] = new
        {
            parentAllow.Succeeded,
            ParentDenyStudentsDelete = !parentDeny.Succeeded,
            AdminAllowStudentsDelete = adminAllow.Succeeded,
            DynamicGradesRead = dynamicAllow.Succeeded
        };
    }

    private static async Task ScenarioPerformanceAsync(
        IEffectivePermissionService effective,
        SecurityCatalogCache cache,
        PermissionDependencyService deps,
        UserAccount parent,
        UserAccount admin)
    {
        cache.Invalidate();
        var sw = Stopwatch.StartNew();
        _ = await effective.ResolveAsync(parent.Id);
        sw.Stop();
        var coldMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        for (var i = 0; i < 50; i++)
        {
            _ = await effective.ResolveAsync(parent.Id);
            _ = await effective.ResolveAsync(admin.Id);
        }

        sw.Stop();
        var warmAvgMs = sw.Elapsed.TotalMilliseconds / 100.0;

        var snapshot = await deps.EnsureSnapshotAsync(CancellationToken.None);
        var estimatedBytes = EstimateSnapshotBytes(snapshot);

        Check("Perf — ResolveAsync cold < 3000 ms", coldMs < 3000, $"coldMs={coldMs:F1}");
        Check("Perf — ResolveAsync warm moyen < 500 ms", warmAvgMs < 500, $"warmAvgMs={warmAvgMs:F2} (n=100)");
        Check("Cache — snapshot chargé après invalidation", snapshot.ActivePermissionCodes.Count > 0,
            $"activeCodes={snapshot.ActivePermissionCodes.Count} prereqKeys={snapshot.PrerequisitesByCode.Count} ~{estimatedBytes} bytes");

        Evidence["performance"] = new
        {
            ColdResolveMs = coldMs,
            WarmAverageMs = warmAvgMs,
            ActivePermissionCodes = snapshot.ActivePermissionCodes.Count,
            PrerequisiteMapKeys = snapshot.PrerequisitesByCode.Count,
            EstimatedSnapshotBytes = estimatedBytes
        };
    }

    private static Task ScenarioEndpointPolicyInventoryAsync(string repoRoot)
    {
        var controllersDir = Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers");
        var files = Directory.GetFiles(controllersDir, "*.cs");
        var authorizeCount = 0;
        var policyCount = 0;
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            authorizeCount += Regex.Matches(text, @"\[Authorize").Count;
            policyCount += Regex.Matches(text, @"Authorize\(Policy\s*=").Count;
        }

        Check("Non-régression — inventaire [Authorize] conservé", authorizeCount > 50 && policyCount > 30,
            $"Authorize={authorizeCount} Policy=={policyCount} controllers={files.Length}");
        Evidence["endpointInventory"] = new { AuthorizeAttributes = authorizeCount, PolicyAttributes = policyCount, ControllerFiles = files.Length };
        return Task.CompletedTask;
    }

    private static async Task ScenarioDynamicPolicyProviderAsync()
    {
        var options = Options.Create(new AuthorizationOptions());
        var provider = new PermissionAuthorizationPolicyProvider(options);
        var known = await provider.GetPolicyAsync(Permissions.StudentsRead);
        var unknown = await provider.GetPolicyAsync("custom.future.permission");
        var ok = known is not null && unknown is not null
                 && known.Requirements.OfType<PermissionRequirement>().Any(r => r.Permission == Permissions.StudentsRead)
                 && unknown.Requirements.OfType<PermissionRequirement>().Any(r => r.Permission == "custom.future.permission");
        Check("PolicyProvider — résolution dynamique par code", ok,
            $"known={known is not null} unknownDynamic={unknown is not null}");
        Evidence["dynamicPolicyProvider"] = new { Known = known is not null, UnknownDynamic = unknown is not null };
    }

    private static JwtInspectResult InspectJwt(string jwt, IReadOnlyList<string> expectedCodes)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        var permClaims = token.Claims.Where(c => c.Type == ClaimTypesCustom.Permissions || c.Type == "permission" || c.Type == "permissions").ToList();
        // JwtSecurityToken may map custom claim type; also check short name
        if (permClaims.Count == 0)
        {
            permClaims = token.Claims.Where(c =>
                string.Equals(c.Type, ClaimTypesCustom.Permissions, StringComparison.OrdinalIgnoreCase)
                || c.Type.EndsWith("/permissions", StringComparison.OrdinalIgnoreCase)
                || c.Type == "perm").ToList();
        }

        // Fallback: any claim whose value looks like a permission code and type isn't role/sub
        if (permClaims.Count == 0)
        {
            permClaims = token.Claims
                .Where(c => expectedCodes.Contains(c.Value, StringComparer.OrdinalIgnoreCase)
                            && c.Type is not (ClaimTypes.Role or "role" or "unique_name" or "sub"))
                .ToList();
        }

        var values = permClaims.Select(c => c.Value).ToList();
        var codePattern = new Regex(@"^[a-z0-9]+([.\-][a-z0-9]+)+$", RegexOptions.IgnoreCase);
        var allCodes = values.All(v => codePattern.IsMatch(v) || string.Equals(v, Permissions.AdminFull, StringComparison.OrdinalIgnoreCase));
        var noSpacesOrLongText = values.All(v => v.Length < 80 && !v.Contains(' '));
        var coverage = expectedCodes.All(c => values.Contains(c, StringComparer.OrdinalIgnoreCase));
        var ok = allCodes && noSpacesOrLongText && coverage && values.Count == expectedCodes.Count;
        return new JwtInspectResult(ok,
            $"permClaims={values.Count} expected={expectedCodes.Count} allCodeShaped={allCodes} coverage={coverage}",
            token.Claims.ToList(),
            values);
    }

    private static string DecodePayloadJson(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return string.Empty;
        }

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
    }

    private static long EstimateSnapshotBytes(SecurityCatalogCache.CatalogSnapshot snapshot)
    {
        long bytes = 0;
        foreach (var code in snapshot.ActivePermissionCodes)
        {
            bytes += 24 + (code.Length * 2);
        }

        foreach (var (key, list) in snapshot.PrerequisitesByCode)
        {
            bytes += 24 + (key.Length * 2);
            foreach (var p in list)
            {
                bytes += 24 + (p.Length * 2);
            }
        }

        return bytes;
    }

    private static async Task AddExceptionAsync(
        SchoolDbContext db,
        Guid schoolId,
        Guid userId,
        Guid permissionId,
        PermissionExceptionEffect effect,
        DateTime validFrom,
        DateTime? validTo)
    {
        db.UserPermissionExceptions.Add(new UserPermissionException
        {
            SchoolId = schoolId,
            UserId = userId,
            PermissionId = permissionId,
            Effect = effect,
            ValidFrom = validFrom,
            ValidTo = validTo,
            Reason = "Phase1 validation harness"
        });
        await db.SaveChangesAsync();
    }

    private static async Task<UserAccount> CreateUserAsync(
        SchoolDbContext db,
        IPasswordHasher hasher,
        Guid schoolId,
        Dictionary<string, Guid> roles,
        string suffix,
        params string[] roleCodes)
        => await CreateUserAsync(db, hasher, schoolId, roles, suffix, isPlatformSuperAdmin: false, roleCodes);

    private static async Task<UserAccount> CreateUserAsync(
        SchoolDbContext db,
        IPasswordHasher hasher,
        Guid schoolId,
        Dictionary<string, Guid> roles,
        string suffix,
        bool isPlatformSuperAdmin,
        params string[] roleCodes)
    {
        var user = new UserAccount
        {
            SchoolId = schoolId,
            UserName = TestUserPrefix + suffix,
            Email = $"{TestUserPrefix}{suffix}@validation.local",
            FirstName = "Phase1",
            LastName = suffix,
            PasswordHash = hasher.Hash(TestPassword),
            IsActive = true,
            IsPlatformSuperAdmin = isPlatformSuperAdmin
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        foreach (var code in roleCodes)
        {
            if (!roles.TryGetValue(code, out var roleId))
            {
                throw new InvalidOperationException($"Rôle manquant: {code}");
            }

            db.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = user.Id,
                RoleId = roleId
            });
        }

        await db.SaveChangesAsync();
        return user;
    }

    private static async Task CleanupUsersAsync(SchoolDbContext db, List<Guid> userIds)
    {
        async Task PurgeByPrefixAsync()
        {
            var like = TestUserPrefix + "%";
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM RefreshTokens WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM UserPermissionExceptions WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM UserRoleAssignments WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM LoginHistory WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM UserAccounts WHERE UserName LIKE {0}", like);
        }

        foreach (var id in userIds.Distinct())
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM RefreshTokens WHERE UserId = {0}", id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM UserPermissionExceptions WHERE UserId = {0}", id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM UserRoleAssignments WHERE UserId = {0}", id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM LoginHistory WHERE UserId = {0}", id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM UserAccounts WHERE Id = {0}", id);
        }

        await PurgeByPrefixAsync();
    }

    private static async Task CleanupPreviousAsync(SchoolDbContext db) =>
        await CleanupUsersAsync(db, []);


    private static SchoolDbContext CreateDbContext(string repoRoot)
    {
        var apiDirectory = Path.Combine(repoRoot, "src", "SchoolManagement.API");
        var bootstrap = new DatabaseConnectionBootstrap(apiDirectory);
        bootstrap.ConfigurationManager.EnsureDefaultFileExists();
        var configuration = bootstrap.LoadConfiguration();
        var validation = bootstrap.ConfigurationManager.Validate(configuration);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException("ServeurDonnees.txt invalide: " + string.Join("; ", validation.FieldErrors.Values));
        }

        var cs = bootstrap.BuildConnectionString(configuration);
        Evidence["database"] = MaskConnectionString(cs);
        var options = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cs).Options;
        return new SchoolDbContext(options);
    }

    private static JwtSettings LoadJwtSettings(string repoRoot)
    {
        var apiDirectory = Path.Combine(repoRoot, "src", "SchoolManagement.API");
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();
        var settings = new JwtSettings();
        config.GetSection(JwtSettings.SectionName).Bind(settings);
        if (string.IsNullOrWhiteSpace(settings.SecretKey) || settings.SecretKey.Length < 32)
        {
            settings.SecretKey = "PHASE1_VALIDATION_SECRET_KEY_MIN_32_CHARS!!";
        }

        return settings;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SchoolManagement.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("SchoolManagement.sln introuvable.");
    }

    private static string MaskConnectionString(string cs)
    {
        return Regex.Replace(cs, @"(Password|Pwd)=([^;]+)", "$1=***", RegexOptions.IgnoreCase);
    }

    private static void Check(string name, bool passed, string detail)
    {
        Results.Add(new CheckResult(name, passed, detail));
    }

    private sealed record CheckResult(string Name, bool Passed, string Detail);

    private sealed record JwtInspectResult(
        bool Ok,
        string Detail,
        List<Claim> Claims,
        List<string> PermissionValues);

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(
            Guid userId,
            Guid schoolId,
            string userName,
            IReadOnlyList<string> roles,
            IReadOnlyList<string> permissions)
        {
            UserId = userId;
            SchoolId = schoolId;
            UserName = userName;
            Roles = roles;
            Permissions = permissions;
        }

        public Guid? UserId { get; }
        public Guid? SchoolId { get; }
        public string? UserName { get; }
        public IReadOnlyList<string> Permissions { get; }
        public IReadOnlyList<string> Roles { get; }

        public bool HasPermission(string permission) =>
            Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)
            || Permissions.Contains(SchoolManagement.Shared.Constants.Permissions.AdminFull, StringComparer.OrdinalIgnoreCase);

        public bool IsAdministrator =>
            HasPermission(SchoolManagement.Shared.Constants.Permissions.AdminFull)
            || Roles.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase));
    }
}
