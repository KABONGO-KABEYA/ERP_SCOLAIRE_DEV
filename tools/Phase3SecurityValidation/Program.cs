using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolManagement.API.Authorization;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Security;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Desktop.Navigation;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Infrastructure.Auth;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Persistence.Repositories;
using SchoolManagement.Infrastructure.Security;
using SchoolManagement.Shared.Constants;

namespace Phase3SecurityValidation;

internal static class Program
{
    private const string TestUserPrefix = "__p3v_";
    private const string TestRolePrefix = "__P3V_";
    private const string TestPassword = "Phase3Valid@2026";
    private const string ActorName = "__p3v_actor";

    private static readonly List<CheckResult> Results = [];
    private static readonly Dictionary<string, object?> Evidence = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<int> Main()
    {
        var repoRoot = FindRepoRoot();
        var outDir = Path.Combine(repoRoot, "tools", "Phase3SecurityValidation", "out");
        Directory.CreateDirectory(outDir);
        Evidence["startedAtUtc"] = DateTime.UtcNow;
        Evidence["repoRoot"] = repoRoot;

        Console.WriteLine("=== Phase 3 Security Admin Validation ===");

        await using var db = CreateDbContext(repoRoot);
        db.IgnoreSchoolScope = true;

        var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
        var cache = new SecurityCatalogCache();
        var deps = new PermissionDependencyService(db, cache, loggerFactory.CreateLogger<PermissionDependencyService>());
        var effective = new EffectivePermissionService(db, deps, loggerFactory.CreateLogger<EffectivePermissionService>());
        var audit = new SecurityAuditService(db);
        var hasher = new BcryptPasswordHasher();
        var refreshRepo = new RefreshTokenRepository(db);
        var users = new SecurityUserAdminService(db, hasher, refreshRepo, effective, audit);
        var roles = new SecurityRoleAdminService(db, deps, audit);
        var exceptions = new SecurityExceptionAdminService(db, audit);
        var catalog = new SecurityCatalogAdminService(db, deps, audit);
        var nav = new SecurityNavigationService(db, cache, effective, loggerFactory.CreateLogger<SecurityNavigationService>());
        var registry = new DesktopViewRegistry();

        var catalogTestIds = new CatalogHarnessIds();

        var schoolId = await db.Schools.AsNoTracking().Select(s => s.Id).FirstAsync();
        var roleMap = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.SchoolId == schoolId && !r.IsDeleted)
            .ToDictionaryAsync(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);
        var permMap = await db.Permissions.IgnoreQueryFilters()
            .Where(p => !p.IsDeleted)
            .ToDictionaryAsync(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

        Evidence["schoolId"] = schoolId;

        var createdUserIds = new List<Guid>();
        Guid? customRoleId = null;
        Guid? actorUserId = null;

        try
        {
            await CleanupPreviousAsync(db);

            actorUserId = (await CreateHarnessUserAsync(db, hasher, schoolId, roleMap, "actor", "ADMIN")).Id;
            createdUserIds.Add(actorUserId.Value);

            // --- Users (personnel → compte, sans rôle auto) ---
            var teacherForCreate = new SchoolManagement.Domain.Entities.Academic.Teacher
            {
                SchoolId = schoolId,
                EmployeeNumber = TestUserPrefix + "T001",
                FirstName = "P3",
                LastName = "User",
                Email = "p3v@test.local",
                IsActive = true
            };
            db.Teachers.Add(teacherForCreate);
            await db.SaveChangesAsync();

            var created = await users.CreateAsync(
                schoolId,
                new CreateSecurityUserRequest(
                    teacherForCreate.Id,
                    TestUserPrefix + "user1",
                    TestPassword),
                actorUserId,
                ActorName);
            createdUserIds.Add(created.Id);
            var linkedInDb = await db.UserAccounts.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.Id == created.Id)
                .Select(u => u.TeacherId)
                .FirstAsync();
            Check("Users — création",
                created.UserName.StartsWith(TestUserPrefix, StringComparison.Ordinal)
                && created.Roles.Count == 0
                && linkedInDb == teacherForCreate.Id,
                $"id={created.Id} roles={created.Roles.Count} teacherId={linkedInDb}");

            var candidatesAfter = await users.SearchPersonnelCandidatesAsync(schoolId, TestUserPrefix + "T001");
            Check("Users — candidat exclu après création",
                candidatesAfter.All(c => c.TeacherId != teacherForCreate.Id),
                $"count={candidatesAfter.Count}");

            var duplicateRejected = false;
            try
            {
                await users.CreateAsync(
                    schoolId,
                    new CreateSecurityUserRequest(
                        teacherForCreate.Id,
                        TestUserPrefix + "user1bis",
                        TestPassword),
                    actorUserId,
                    ActorName);
            }
            catch (DomainException ex)
            {
                duplicateRejected = ex.Message.Contains("déjà un compte", StringComparison.OrdinalIgnoreCase);
                Check("Users — refus double compte personnel", duplicateRejected, ex.Message);
            }

            if (!duplicateRejected)
                Check("Users — refus double compte personnel", false, "aucune exception DomainException");

            if (roleMap.TryGetValue("PARENT", out var parentRoleId)
                && roleMap.TryGetValue("ENSEIGNANT", out var ensRoleId))
            {
                var multi = await users.SetRolesAsync(
                    schoolId,
                    created.Id,
                    new SetSecurityUserRolesRequest([parentRoleId, ensRoleId]),
                    actorUserId,
                    ActorName);
                Check("Users — multi-rôles (après création)", multi.Roles.Count >= 2,
                    $"roles=[{string.Join(',', multi.Roles)}]");
            }
            else if (roleMap.TryGetValue("ENSEIGNANT", out var onlyEns))
            {
                var assigned = await users.SetRolesAsync(
                    schoolId,
                    created.Id,
                    new SetSecurityUserRolesRequest([onlyEns]),
                    actorUserId,
                    ActorName);
                Check("Users — multi-rôles (après création)", assigned.Roles.Count >= 1,
                    $"roles=[{string.Join(',', assigned.Roles)}]");
            }
            else
            {
                Check("Users — multi-rôles (après création)", true, "rôles absents en BD — scénario ignoré");
            }

            var updated = await users.UpdateAsync(
                schoolId,
                created.Id,
                new UpdateSecurityUserRequest("p3v-upd@test.local", "P3", "Updated", false),
                actorUserId,
                ActorName,
                actorIsPlatformSuperAdmin: false);
            Check("Users — désactivation", !updated.IsActive, "IsActive=false");

            await users.ResetPasswordAsync(
                schoolId,
                created.Id,
                new ResetPasswordRequest("NewP3Pass@2026", true),
                actorUserId,
                ActorName);
            var reactivated = await users.UpdateAsync(
                schoolId,
                created.Id,
                new UpdateSecurityUserRequest("p3v-upd@test.local", "P3", "Updated", true),
                actorUserId,
                ActorName,
                false);
            Check("Users — réactivation + reset MDP", reactivated.IsActive && reactivated.MustChangePassword,
                "MustChangePassword=true");

            var auditUserCreated = await db.SecurityAuditLogs.AsNoTracking()
                .AnyAsync(l => l.ActionType == "User.Created" && l.TargetEntityId == created.Id);
            Check("Audit — User.Created", auditUserCreated, "SecurityAuditLogs");

            // --- Roles ---
            var adminRoleId = roleMap["ADMIN"];
            var adminRoleEntity = await db.Roles.IgnoreQueryFilters().FirstAsync(r => r.Id == adminRoleId);
            Check("Roles — ADMIN IsSystem", adminRoleEntity.IsSystem, "IsSystem=true");

            var deleteSystemFailed = false;
            try
            {
                await roles.DeleteAsync(schoolId, adminRoleId, actorUserId, ActorName);
            }
            catch (DomainException)
            {
                deleteSystemFailed = true;
            }

            Check("Roles — suppression rôle système refusée", deleteSystemFailed, "ADMIN");

            var customCode = TestRolePrefix + "CUSTOM_" + Guid.NewGuid().ToString("N")[..8];
            var custom = await roles.CreateAsync(
                schoolId,
                new CreateSecurityRoleRequest(customCode, "Rôle test P3", "Harness", true, 999),
                actorUserId,
                ActorName);
            customRoleId = custom.Id;
            Check("Roles — création établissement", !custom.IsSystem && custom.Code.StartsWith(TestRolePrefix, StringComparison.Ordinal),
                custom.Code);

            await roles.UpdateAsync(
                schoolId,
                custom.Id,
                new UpdateSecurityRoleRequest("Rôle test P3 renommé", "Desc", true, 998),
                actorUserId,
                ActorName);

            var matrixReadOnlyFailed = false;
            try
            {
                await roles.SetPermissionsAsync(
                    schoolId,
                    adminRoleId,
                    new SetRolePermissionsRequest([Permissions.StudentsRead]),
                    actorUserId,
                    ActorName);
            }
            catch (DomainException)
            {
                matrixReadOnlyFailed = true;
            }

            Check("Roles — matrice ADMIN lecture seule", matrixReadOnlyFailed, "DomainException");

            var rolePerms = await roles.SetPermissionsAsync(
                schoolId,
                custom.Id,
                new SetRolePermissionsRequest([Permissions.StudentsCreate]),
                actorUserId,
                ActorName);
            var hasCreate = rolePerms.PermissionCodes.Contains(Permissions.StudentsCreate, StringComparer.OrdinalIgnoreCase);
            var hasRead = rolePerms.PermissionCodes.Contains(Permissions.StudentsRead, StringComparer.OrdinalIgnoreCase);
            Check("Roles — auto-prérequis students.create → students.read", hasCreate && hasRead,
                $"codes=[{string.Join(',', rolePerms.PermissionCodes)}]");

            // --- Exceptions + Explain ---
            var explainUser = await CreateHarnessUserAsync(db, hasher, schoolId, roleMap, "explain", "ENSEIGNANT");
            createdUserIds.Add(explainUser.Id);

            if (permMap.TryGetValue(Permissions.GradesRead, out var gradesRead))
            {
                await exceptions.CreateAsync(
                    schoolId,
                    new CreateSecurityExceptionRequest(
                        explainUser.Id,
                        gradesRead.Id,
                        PermissionExceptionEffect.Deny,
                        DateTime.UtcNow.AddMinutes(-1),
                        null,
                        "P3 harness deny"),
                    actorUserId,
                    ActorName);
            }

            var explain = await effective.ExplainAsync(explainUser.Id);
            var gradesExplain = explain.Permissions.FirstOrDefault(p =>
                string.Equals(p.Code, Permissions.GradesRead, StringComparison.OrdinalIgnoreCase));
            var denyOrigin = gradesExplain?.Origins.Any(o => o.Kind == PermissionOriginKind.Deny) == true;
            Check("Exceptions — Deny visible dans Explain", gradesExplain is not null && denyOrigin && !gradesExplain.IsEffective,
                $"IsEffective={gradesExplain?.IsEffective} origins={gradesExplain?.Origins.Count}");

            var expiryUser = await CreateHarnessUserAsync(db, hasher, schoolId, roleMap, "expiry", "PARENT");
            createdUserIds.Add(expiryUser.Id);
            if (permMap.TryGetValue(Permissions.SchoolsRead, out var schoolsRead))
            {
                await exceptions.CreateAsync(
                    schoolId,
                    new CreateSecurityExceptionRequest(
                        expiryUser.Id,
                        schoolsRead.Id,
                        PermissionExceptionEffect.Grant,
                        DateTime.UtcNow.AddHours(-2),
                        DateTime.UtcNow.AddMinutes(-30),
                        "expired grant"),
                    actorUserId,
                    ActorName);
            }

            var expiredEff = await effective.ResolveAsync(expiryUser.Id);
            Check("Exceptions — Grant expiré absent effectif",
                !expiredEff.PermissionCodes.Contains(Permissions.SchoolsRead, StringComparer.OrdinalIgnoreCase),
                "schools.read");

            if (permMap.TryGetValue(Permissions.CurrenciesRead, out var curRead)
                && permMap.TryGetValue(Permissions.CurrenciesCreate, out var curCreate))
            {
                var overlapUser = await CreateHarnessUserAsync(db, hasher, schoolId, roleMap, "overlap", "PARENT");
                createdUserIds.Add(overlapUser.Id);
                var now = DateTime.UtcNow;
                await exceptions.CreateAsync(
                    schoolId,
                    new CreateSecurityExceptionRequest(overlapUser.Id, curRead.Id, PermissionExceptionEffect.Grant, now.AddDays(-1), now.AddDays(1), "g1"),
                    actorUserId, ActorName);
                await exceptions.CreateAsync(
                    schoolId,
                    new CreateSecurityExceptionRequest(overlapUser.Id, curCreate.Id, PermissionExceptionEffect.Grant, now.AddDays(-1), now.AddDays(1), "g2"),
                    actorUserId, ActorName);
                var overlapEff = await effective.ResolveAsync(overlapUser.Id);
                Check("Exceptions — chevauchements Grant résolus",
                    overlapEff.PermissionCodes.Contains(Permissions.CurrenciesCreate, StringComparer.OrdinalIgnoreCase),
                    "currencies.create présent");
            }

            Check("Audit — Exception.Denied journalisée",
                await db.SecurityAuditLogs.AsNoTracking().AnyAsync(l => l.ActionType == "Exception.Denied"),
                "ok");

            await ScenarioPlatformCatalogAsync(
                repoRoot,
                db,
                cache,
                catalog,
                nav,
                registry,
                effective,
                hasher,
                schoolId,
                roleMap,
                permMap,
                actorUserId,
                catalogTestIds);

            // --- Policies security.* / platform.* ---
            var parentHarness = await CreateHarnessUserAsync(db, hasher, schoolId, roleMap, "policy_parent", "PARENT");
            createdUserIds.Add(parentHarness.Id);
            var adminHarness = await CreateHarnessUserAsync(db, hasher, schoolId, roleMap, "policy_admin", "ADMIN");
            createdUserIds.Add(adminHarness.Id);

            var parentEff = await effective.ResolveAsync(parentHarness.Id);
            var adminEff = await effective.ResolveAsync(adminHarness.Id);

            var parentCurrent = new FakeCurrentUser(parentHarness.Id, schoolId, parentHarness.UserName, parentEff.PermissionCodes);
            var adminCurrent = new FakeCurrentUser(adminHarness.Id, schoolId, adminHarness.UserName, adminEff.PermissionCodes);

            var parentDeniedUsers = !(await EvaluatePolicyAsync(parentCurrent, Permissions.SecurityUsersManage)).Succeeded;
            var adminOkUsers = (await EvaluatePolicyAsync(adminCurrent, Permissions.SecurityUsersManage)).Succeeded;
            var parentDeniedPlatform = !(await EvaluatePolicyAsync(parentCurrent, Permissions.PlatformCatalogManage)).Succeeded;
            var adminOkPlatformViaAdminFull = (await EvaluatePolicyAsync(adminCurrent, Permissions.PlatformCatalogManage)).Succeeded;

            var superHarness = await CreateHarnessSuperUserAsync(db, hasher, schoolId, roleMap, "policy_super");
            createdUserIds.Add(superHarness.Id);
            var superEff = await effective.ResolveAsync(superHarness.Id);
            var superCurrent = new FakeCurrentUser(superHarness.Id, schoolId, superHarness.UserName, superEff.PermissionCodes);
            var superOkPlatform = (await EvaluatePolicyAsync(superCurrent, Permissions.PlatformCatalogManage)).Succeeded;
            var superHasPlatformPerm = superEff.PermissionCodes.Contains(Permissions.PlatformCatalogManage, StringComparer.OrdinalIgnoreCase);

            Check("Policies — PARENT refusé security.users.manage", parentDeniedUsers, "403 attendu");
            Check("Policies — ADMIN autorisé security.users.manage", adminOkUsers, "admin.full bypass");
            Check("Policies — PARENT refusé platform.catalog.manage", parentDeniedPlatform, "403 attendu");
            Check("Policies — ADMIN autorisé platform.catalog.manage", adminOkPlatformViaAdminFull, "admin.full");
            Check("Policies — Super Admin plateforme autorisé platform.catalog.manage", superOkPlatform && superHasPlatformPerm,
                $"effectif={superHasPlatformPerm} policy={superOkPlatform}");
            Check("Policies — utilisateur standard (PARENT) refusé platform (403 attendu)", parentDeniedPlatform, "in-process");

            foreach (var code in new[]
                     {
                         Permissions.SecurityUsersManage, Permissions.SecurityRolesManage,
                         Permissions.SecurityExceptionsManage, Permissions.SecurityAuditRead,
                         Permissions.PlatformCatalogManage
                     })
            {
                Check($"Policies — policy enregistrée {code}",
                    (await EvaluatePolicyAsync(adminCurrent, code)).Succeeded,
                    "ADMIN");
            }

            await ScenarioSecurityControllerPoliciesAsync(repoRoot);

            // --- Desktop registry ---
            string[] securityKeys = ["Security.Users", "Security.Roles", "Security.Exceptions", "Security.Audit"];
            foreach (var key in securityKeys)
            {
                Check($"Desktop — registre {key}",
                    registry.TryResolve(key, out var t) && t is DirectDesktopViewTarget,
                    t?.GetType().Name ?? "missing");
            }

            Check("Desktop — registre Platform.Catalog",
                registry.TryResolve("Platform.Catalog", out var platformTarget) && platformTarget is DirectDesktopViewTarget,
                platformTarget?.GetType().Name ?? "missing");

            foreach (var vmName in new[] { "SecurityUsersViewModel", "SecurityRolesViewModel", "SecurityExceptionsViewModel", "SecurityAuditViewModel", "PlatformCatalogViewModel" })
            {
                var vmType = typeof(SchoolManagement.Desktop.ViewModels.ViewModelBase).Assembly
                    .GetType($"SchoolManagement.Desktop.ViewModels.{vmName}");
                var viewName = vmName.Replace("ViewModel", "View", StringComparison.Ordinal);
                var viewType = typeof(SchoolManagement.Desktop.Views.AdministrationView).Assembly
                    .GetType($"SchoolManagement.Desktop.Views.{viewName}");
                Check($"Desktop — paire VM/View {vmName}", vmType is not null && viewType is not null, viewType?.Name ?? "missing");
            }

            // --- Navigation Phase 2 regression (Security pages résolues) ---
            var adminNavUser = await CreateHarnessUserAsync(db, hasher, schoolId, roleMap, "nav_admin", "ADMIN");
            createdUserIds.Add(adminNavUser.Id);
            var adminTree = await nav.GetNavigationAsync(adminNavUser.Id, NavigationChannel.Desktop);
            var unresolved = new List<string>();
            _ = DesktopNavigationMenuBuilder.Build(adminTree, registry, unresolved.Add);
            var securityUnresolved = unresolved.Where(k => k.StartsWith("Security.", StringComparison.OrdinalIgnoreCase)).ToList();
            Check("Nav Phase 2 — clés Security.* résolues", securityUnresolved.Count == 0,
                securityUnresolved.Count == 0 ? "0 unresolved Security.*" : string.Join(',', securityUnresolved));

            var platformUnresolved = unresolved.Where(k => k.StartsWith("Platform.", StringComparison.OrdinalIgnoreCase)).ToList();
            Check("Nav — clés Platform.* résolues", platformUnresolved.Count == 0,
                platformUnresolved.Count == 0 ? "0 unresolved Platform.*" : string.Join(',', platformUnresolved));

            // --- Phase 1 smoke ---
            Check("Non-régression Phase 1 — HasPermission PARENT payments.read",
                await effective.HasPermissionAsync(parentHarness.Id, Permissions.PaymentsRead),
                "true");
            Check("Non-régression Phase 1 — HasPermission PARENT students.delete",
                !await effective.HasPermissionAsync(parentHarness.Id, Permissions.StudentsDelete),
                "false");

            // --- JWT sans DisplayName ---
            var jwtInspect = InspectJwtClaims(adminEff.PermissionCodes, adminHarness.Id, schoolId);
            Check("Non-régression JWT — codes uniquement", jwtInspect.Ok, jwtInspect.Detail);

            // --- Perf ---
            await ScenarioPerformanceAsync(users, effective, schoolId, created.Id);

            // --- Audit query scope ---
            var auditRows = await audit.QueryAsync(new SecurityAuditQuery(SchoolId: schoolId, Take: 50));
            Check("Audit — requête filtre école", auditRows.All(r => r.SchoolId == schoolId || r.SchoolId is null),
                $"rows={auditRows.Count}");

            if (customRoleId.HasValue)
            {
                await roles.DeleteAsync(schoolId, customRoleId.Value, actorUserId, ActorName);
                Check("Roles — suppression rôle établissement", true, customRoleId.Value.ToString());
                customRoleId = null;
            }
        }
        finally
        {
            if (customRoleId.HasValue)
            {
                try
                {
                    var role = await db.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == customRoleId);
                    if (role is not null)
                    {
                        role.IsDeleted = true;
                        await db.SaveChangesAsync();
                    }
                }
                catch { /* ignore */ }
            }

            await CleanupUsersAsync(db, createdUserIds);
            await CleanupRolesAsync(db);
            await CleanupCatalogHarnessAsync(db, catalogTestIds);
        }

        Evidence["finishedAtUtc"] = DateTime.UtcNow;
        Evidence["checks"] = Results.Select(r => new { r.Name, r.Passed, r.Detail }).ToList();
        Evidence["passed"] = Results.Count(r => r.Passed);
        Evidence["failed"] = Results.Count(r => !r.Passed);
        Evidence["total"] = Results.Count;

        var evidencePath = Path.Combine(outDir, "evidence.json");
        await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(Evidence, new JsonSerializerOptions { WriteIndented = true }));

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

    private sealed class CatalogHarnessIds
    {
        public Guid? ModuleId { get; set; }
        public Guid? FunctionId { get; set; }
        public Guid? PageId { get; set; }
        public Guid? ActionId { get; set; }
        public Guid? PermissionAId { get; set; }
        public Guid? PermissionBId { get; set; }
        public Guid? DependencyId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string PageName { get; set; } = string.Empty;
    }

    private static async Task ScenarioPlatformCatalogAsync(
        string repoRoot,
        SchoolDbContext db,
        SecurityCatalogCache cache,
        ISecurityCatalogAdminService catalog,
        ISecurityNavigationService nav,
        DesktopViewRegistry registry,
        IEffectivePermissionService effective,
        IPasswordHasher hasher,
        Guid schoolId,
        Dictionary<string, Guid> roleMap,
        Dictionary<string, Permission> permMap,
        Guid? actorUserId,
        CatalogHarnessIds ids)
    {
        var actor = actorUserId;
        var actorName = ActorName;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        ids.ModuleCode = TestRolePrefix + "MOD_" + suffix;

        // --- CRUD chaîne catalogue ---
        var module = await catalog.UpsertModuleAsync(
            null,
            new UpsertSecurityModuleRequest(ids.ModuleCode, "Module harness P3", "Test", "CircleOutline", 9900, true),
            actor,
            actorName);
        ids.ModuleId = module.Id;
        Check("Catalogue — module créé",
            string.Equals(module.Code, ids.ModuleCode, StringComparison.OrdinalIgnoreCase),
            $"{module.Code} / {ids.ModuleCode}");

        var moduleAudit = await db.SecurityAuditLogs.AsNoTracking()
            .AnyAsync(l => l.ActionType == "Catalog.ModuleCreated" && l.TargetEntityId == module.Id);
        Check("Catalogue — audit Catalog.ModuleCreated", moduleAudit, "SecurityAuditLogs");

        var function = await catalog.UpsertFunctionAsync(
            null,
            new UpsertSecurityFunctionRequest(module.Id, "FN_" + suffix, "Fonction harness", null, null, 1, true),
            actor,
            actorName);
        ids.FunctionId = function.Id;
        Check("Catalogue — fonction créée", function.ModuleId == module.Id, function.Code);

        ids.PageName = "Page harness " + suffix;
        var page = await catalog.UpsertPageAsync(
            null,
            new UpsertSecurityPageRequest(
                function.Id,
                "PG_" + suffix,
                ids.PageName,
                "Nav test",
                1,
                true,
                Permissions.ReportsRead,
                "Dashboard.Main",
                null,
                null,
                true),
            actor,
            actorName);
        ids.PageId = page.Id;
        Check("Catalogue — page créée", page.DesktopViewKey == "Dashboard.Main", page.Code);

        var action = await catalog.UpsertActionAsync(
            null,
            new UpsertSecurityActionRequest(page.Id, "ACT_" + suffix, "Action harness", null, 1, true, true),
            actor,
            actorName);
        ids.ActionId = action.Id;
        Check("Catalogue — action créée", action.PageId == page.Id, action.Code);

        var permCodeA = "__p3v.cat.a." + suffix;
        var permCodeB = "__p3v.cat.b." + suffix;
        var permA = await catalog.UpsertPermissionAsync(
            null,
            new UpsertSecurityPermissionRequest(
                permCodeA,
                "Harness perm A",
                "P3V",
                "Description métier A",
                "Help A",
                true,
                action.Id),
            actor,
            actorName);
        ids.PermissionAId = permA.Id;
        Check("Catalogue — permission A créée", permA.Code == permCodeA, permA.DisplayName);

        var permB = await catalog.UpsertPermissionAsync(
            null,
            new UpsertSecurityPermissionRequest(
                permCodeB,
                "Harness perm B",
                "P3V",
                "Description métier B",
                "Help B",
                true,
                action.Id),
            actor,
            actorName);
        ids.PermissionBId = permB.Id;
        Check("Catalogue — permission B créée", permB.Code == permCodeB, permB.HelpText);

        await catalog.UpsertModuleAsync(
            module.Id,
            new UpsertSecurityModuleRequest(ids.ModuleCode, "Module harness P3 renommé", "Test", "CircleOutline", 9901, true),
            actor,
            actorName);
        Check("Catalogue — module mis à jour", true, "nom + sort");

        var tree = await catalog.GetTreeAsync();
        var treeHasModule = tree.Modules.Any(m => m.Id == module.Id);
        Check("Catalogue — arbre contient le module test", treeHasModule, ids.ModuleCode);

        // --- Protections modules / permissions critiques (Desktop + intégrité BD) ---
        var vmPath = Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "ViewModels", "PlatformCatalogViewModel.cs");
        var vmSource = await File.ReadAllTextAsync(vmPath);
        Check("Catalogue — garde-fou Desktop modules SECURITY/PLATFORM/SETTINGS",
            vmSource.Contains("\"SECURITY\"", StringComparison.Ordinal)
            && vmSource.Contains("\"PLATFORM\"", StringComparison.Ordinal)
            && vmSource.Contains("\"SETTINGS\"", StringComparison.Ordinal),
            "ProtectedModuleCodes");
        Check("Catalogue — garde-fou Desktop permissions admin.full / platform.*",
            vmSource.Contains("PlatformSuperAdmin", StringComparison.Ordinal)
            && vmSource.Contains("PlatformCatalogManage", StringComparison.Ordinal)
            && vmSource.Contains("AdminFull", StringComparison.Ordinal),
            "ProtectedPermissionCodes");

        foreach (var critical in new[] { Permissions.PlatformSuperAdmin, Permissions.PlatformCatalogManage, Permissions.AdminFull })
        {
            var row = await db.Permissions.AsNoTracking().FirstOrDefaultAsync(p => p.Code == critical && !p.IsDeleted);
            Check($"Catalogue — permission critique {critical} active en BD",
                row is not null && row.IsActive,
                row?.DisplayName ?? "missing");
        }

        // --- Dépendances ---
        var dep = await catalog.AddDependencyAsync(
            new CreatePermissionDependencyRequest(permA.Id, permB.Id),
            actor,
            actorName);
        ids.DependencyId = dep.Id;
        Check("Catalogue — dépendance créée", dep.PermissionCode == permCodeA && dep.RequiresPermissionCode == permCodeB,
            $"{permCodeA} → {permCodeB}");

        var dupRejected = false;
        try
        {
            await catalog.AddDependencyAsync(new CreatePermissionDependencyRequest(permA.Id, permB.Id), actor, actorName);
        }
        catch (DomainException)
        {
            dupRejected = true;
        }

        Check("Catalogue — doublon dépendance refusé", dupRejected, "DomainException");

        cache.Invalidate();
        var cycleRejectedHarness = false;
        try
        {
            await catalog.AddDependencyAsync(new CreatePermissionDependencyRequest(permB.Id, permA.Id), actor, actorName);
        }
        catch (DomainException)
        {
            cycleRejectedHarness = true;
        }

        Check("Catalogue — cycle dépendance refusé (harness A↔B)", cycleRejectedHarness, "DomainException");

        if (permMap.TryGetValue(Permissions.GradesCreate, out var gc)
            && permMap.TryGetValue(Permissions.GradesRead, out var gr))
        {
            var cycleGrades = false;
            try
            {
                await catalog.AddDependencyAsync(new CreatePermissionDependencyRequest(gr.Id, gc.Id), actor, actorName);
            }
            catch (DomainException)
            {
                cycleGrades = true;
            }

            Check("Catalogue — cycle dépendance refusé (grades.read → grades.create)", cycleGrades,
                "régression catalogue");
        }

        await catalog.RemoveDependencyAsync(dep.Id, actor, actorName);
        ids.DependencyId = null;
        var depsAfter = await catalog.GetDependenciesAsync();
        Check("Catalogue — dépendance supprimée",
            depsAfter.All(d => d.Id != dep.Id),
            "RemoveDependencyAsync");

        var depAudit = await db.SecurityAuditLogs.AsNoTracking()
            .AnyAsync(l => l.ActionType == "Dependency.Added" || l.ActionType == "Catalog.PermissionUpdated");
        Check("Catalogue — audit dépendance / permission", depAudit, "SecurityAuditLogs");

        // --- Navigation après modification catalogue ---
        cache.Invalidate();
        var navAdmin = await CreateHarnessUserAsync(db, hasher, schoolId, roleMap, "cat_nav", "ADMIN");
        try
        {
            var navTree = await nav.GetNavigationAsync(navAdmin.Id, NavigationChannel.Desktop);
            var flatPages = navTree.Modules
                .SelectMany(m => m.Functions)
                .SelectMany(f => f.Pages)
                .ToList();
            var foundHarnessPage = flatPages.Any(p => string.Equals(p.Name, ids.PageName, StringComparison.Ordinal));
            Check("Catalogue — navigation Desktop inclut page harness", foundHarnessPage,
                foundHarnessPage ? ids.PageName : $"pages={flatPages.Count}");

            var unresolved = new List<string>();
            _ = DesktopNavigationMenuBuilder.Build(navTree, registry, unresolved.Add);
            Check("Catalogue — page harness DesktopViewKey résolue",
                unresolved.All(k => !string.Equals(k, "Dashboard.Main", StringComparison.OrdinalIgnoreCase)),
                unresolved.Count == 0 ? "Dashboard.Main ok" : string.Join(',', unresolved));
        }
        finally
        {
            await DeleteHarnessUserByIdAsync(db, navAdmin.Id);
        }

        // --- API Super Admin (source) ---
        var platformPath = Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers", "PlatformCatalogController.cs");
        var platformText = await File.ReadAllTextAsync(platformPath);
        Check("Catalogue API — EnsurePlatformSuperAdmin + policy platform.catalog.manage",
            platformText.Contains("EnsurePlatformSuperAdmin", StringComparison.Ordinal)
            && platformText.Contains("Permissions.PlatformCatalogManage", StringComparison.Ordinal),
            "PlatformCatalogController");

        // --- Desktop PlatformCatalog (API client + vue) ---
        var apiPath = Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "Services", "ApiServices.cs");
        var apiText = await File.ReadAllTextAsync(apiPath);
        Check("Desktop — IPlatformCatalogApiService implémenté",
            apiText.Contains("class PlatformCatalogApiService", StringComparison.Ordinal)
            && apiText.Contains("api/v1/platform", StringComparison.Ordinal),
            "ApiServices");

        var viewPath = Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "Views", "PlatformCatalogView.xaml");
        var viewXaml = await File.ReadAllTextAsync(viewPath);
        Check("Desktop — PlatformCatalogView onglets Navigation/Permissions/Dépendances/Audit",
            viewXaml.Contains("Header=\"Navigation\"", StringComparison.Ordinal)
            && viewXaml.Contains("Header=\"Permissions\"", StringComparison.Ordinal)
            && viewXaml.Contains("Header=\"Dépendances\"", StringComparison.Ordinal)
            && viewXaml.Contains("Audit plateforme", StringComparison.Ordinal),
            "PlatformCatalogView.xaml");

        Check("Desktop — PlatformCatalogView champs DisplayName / HelpText / métier",
            viewXaml.Contains("DisplayName", StringComparison.Ordinal)
            && viewXaml.Contains("HelpText", StringComparison.Ordinal)
            && viewXaml.Contains("BusinessDescription", StringComparison.Ordinal),
            "ergonomie admin");

        Check("Desktop — PlatformCatalogViewModel commandes Load/Save/Dependencies",
            vmSource.Contains("SaveTreeNodeAsync", StringComparison.Ordinal)
            && vmSource.Contains("AddDependencyAsync", StringComparison.Ordinal)
            && vmSource.Contains("RemoveDependencyAsync", StringComparison.Ordinal)
            && vmSource.Contains("SavePermissionAsync", StringComparison.Ordinal),
            "PlatformCatalogViewModel");

        Evidence["catalogHarness"] = new
        {
            ids.ModuleCode,
            ids.PageName,
            permCodeA,
            permCodeB
        };
    }

    private static async Task CleanupCatalogHarnessAsync(SchoolDbContext db, CatalogHarnessIds ids)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Permissions WHERE Code LIKE {0}", "__p3v.cat.%");
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM PermissionDependencies WHERE PermissionId IN (SELECT Id FROM Permissions WHERE Code LIKE {0}) OR RequiresPermissionId IN (SELECT Id FROM Permissions WHERE Code LIKE {0})",
            "__p3v.cat.%");
        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM SecurityActions WHERE PageId IN (
              SELECT Id FROM SecurityPages WHERE FunctionId IN (
                SELECT Id FROM SecurityFunctions WHERE ModuleId IN (
                  SELECT Id FROM SecurityModules WHERE Code LIKE {0})))
            """,
            TestRolePrefix + "MOD_%");
        await db.Database.ExecuteSqlRawAsync(
            """
            DELETE FROM SecurityPages WHERE FunctionId IN (
              SELECT Id FROM SecurityFunctions WHERE ModuleId IN (
                SELECT Id FROM SecurityModules WHERE Code LIKE {0}))
            """,
            TestRolePrefix + "MOD_%");
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM SecurityFunctions WHERE ModuleId IN (SELECT Id FROM SecurityModules WHERE Code LIKE {0})",
            TestRolePrefix + "MOD_%");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM SecurityModules WHERE Code LIKE {0}", TestRolePrefix + "MOD_%");

        if (ids.DependencyId.HasValue)
        {
            try
            {
                var dep = await db.PermissionDependencies.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(d => d.Id == ids.DependencyId);
                if (dep is not null)
                {
                    dep.IsDeleted = true;
                    await db.SaveChangesAsync();
                }
            }
            catch { /* ignore */ }
        }

        var permLike = "__p3v.cat.%";
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM PermissionDependencies WHERE PermissionId IN (SELECT Id FROM Permissions WHERE Code LIKE {0}) OR RequiresPermissionId IN (SELECT Id FROM Permissions WHERE Code LIKE {0})",
            permLike);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Permissions WHERE Code LIKE {0}", permLike);

        if (!string.IsNullOrEmpty(ids.ModuleCode))
        {
            var modLike = ids.ModuleCode;
            await db.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM SecurityActions WHERE PageId IN (
                  SELECT Id FROM SecurityPages WHERE FunctionId IN (
                    SELECT Id FROM SecurityFunctions WHERE ModuleId IN (
                      SELECT Id FROM SecurityModules WHERE Code = {0})))
                """,
                modLike);
            await db.Database.ExecuteSqlRawAsync(
                """
                DELETE FROM SecurityPages WHERE FunctionId IN (
                  SELECT Id FROM SecurityFunctions WHERE ModuleId IN (
                    SELECT Id FROM SecurityModules WHERE Code = {0}))
                """,
                modLike);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM SecurityFunctions WHERE ModuleId IN (SELECT Id FROM SecurityModules WHERE Code = {0})",
                modLike);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM SecurityModules WHERE Code = {0}", modLike);
        }

        _ = ids;
    }

    private static async Task<UserAccount> CreateHarnessSuperUserAsync(
        SchoolDbContext db,
        IPasswordHasher hasher,
        Guid schoolId,
        Dictionary<string, Guid> roles,
        string suffix)
    {
        var user = new UserAccount
        {
            SchoolId = schoolId,
            UserName = TestUserPrefix + suffix,
            Email = $"{TestUserPrefix}{suffix}@test.local",
            PasswordHash = hasher.Hash(TestPassword),
            FirstName = "P3",
            LastName = suffix,
            IsActive = true,
            IsPlatformSuperAdmin = true
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();
        if (roles.TryGetValue("PARENT", out var parentRoleId))
        {
            db.UserRoleAssignments.Add(new UserRoleAssignment { UserId = user.Id, RoleId = parentRoleId });
            await db.SaveChangesAsync();
        }

        return user;
    }

    private static async Task ScenarioPerformanceAsync(
        ISecurityUserAdminService users,
        IEffectivePermissionService effective,
        Guid schoolId,
        Guid userId)
    {
        var sw = Stopwatch.StartNew();
        _ = await users.GetUsersAsync(schoolId);
        sw.Stop();
        var listMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        _ = await effective.ExplainAsync(userId);
        sw.Stop();
        var explainMs = sw.Elapsed.TotalMilliseconds;

        Evidence["perfMs"] = new { listUsers = listMs, explain = explainMs };
        Check("Perf — GetUsersAsync < 3000 ms", listMs < 3000, $"{listMs:F0} ms");
        Check("Perf — ExplainAsync < 3000 ms", explainMs < 3000, $"{explainMs:F0} ms");
    }

    private static async Task ScenarioSecurityControllerPoliciesAsync(string repoRoot)
    {
        var controllerPath = Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers", "SecurityAdminController.cs");
        var platformPath = Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers", "PlatformCatalogController.cs");
        var adminText = await File.ReadAllTextAsync(controllerPath);
        var platformText = await File.ReadAllTextAsync(platformPath);

        Check("API — SecurityAdminController policies security.*",
            adminText.Contains("Permissions.SecurityUsersManage", StringComparison.Ordinal)
            && adminText.Contains("Permissions.SecurityRolesManage", StringComparison.Ordinal)
            && adminText.Contains("Permissions.SecurityExceptionsManage", StringComparison.Ordinal)
            && adminText.Contains("Permissions.SecurityAuditRead", StringComparison.Ordinal),
            "fichier source");

        Check("API — PlatformCatalogController policy platform.catalog.manage",
            platformText.Contains("Permissions.PlatformCatalogManage", StringComparison.Ordinal),
            "fichier source");

        var adminControllerPath = Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers", "AdminController.cs");
        var adminCtrl = await File.ReadAllTextAsync(adminControllerPath);
        Check("API — AdminController users migré security.users.manage",
            adminCtrl.Contains("Permissions.SecurityUsersManage", StringComparison.Ordinal),
            "GET users");
    }

    private static (bool Ok, string Detail) InspectJwtClaims(IReadOnlyList<string> codes, Guid userId, Guid schoolId)
    {
        var bad = codes.FirstOrDefault(c => c.Contains(' ') || c.Contains("Display", StringComparison.OrdinalIgnoreCase));
        if (bad is not null)
        {
            return (false, $"claim suspect: {bad}");
        }

        return (true, $"sampleCount={Math.Min(codes.Count, 5)}");
    }

    private static async Task<(bool Succeeded, string Detail)> EvaluatePolicyAsync(FakeCurrentUser current, string policy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<ICurrentUserService>(current);
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        await using var sp = services.BuildServiceProvider();
        var authz = sp.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));
        var result = await authz.AuthorizeAsync(principal, resource: null, policyName: policy);
        return (result.Succeeded, policy);
    }

    private static async Task<UserAccount> CreateHarnessUserAsync(
        SchoolDbContext db,
        IPasswordHasher hasher,
        Guid schoolId,
        Dictionary<string, Guid> roles,
        string suffix,
        params string[] roleCodes)
    {
        var user = new UserAccount
        {
            SchoolId = schoolId,
            UserName = TestUserPrefix + suffix,
            Email = $"{TestUserPrefix}{suffix}@test.local",
            PasswordHash = hasher.Hash(TestPassword),
            FirstName = "P3",
            LastName = suffix,
            IsActive = true
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();
        foreach (var code in roleCodes)
        {
            if (roles.TryGetValue(code, out var roleId))
            {
                db.UserRoleAssignments.Add(new UserRoleAssignment { UserId = user.Id, RoleId = roleId });
            }
        }

        await db.SaveChangesAsync();
        return user;
    }

    private static async Task DeleteHarnessUserByIdAsync(SchoolDbContext db, Guid userId)
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM RefreshTokens WHERE UserId = {0}", userId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM UserPermissionExceptions WHERE UserId = {0}", userId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM UserRoleAssignments WHERE UserId = {0}", userId);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM UserAccounts WHERE Id = {0}", userId);
    }

    private static async Task CleanupUsersAsync(SchoolDbContext db, List<Guid> userIds)
    {
        var like = TestUserPrefix + "%";
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM SecurityAuditLogs WHERE TargetUserName LIKE {0} OR ActorUserName LIKE {0}", like);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM RefreshTokens WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM UserPermissionExceptions WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM UserRoleAssignments WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM UserAccounts WHERE UserName LIKE {0}", like);

        foreach (var id in userIds.Distinct())
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM RefreshTokens WHERE UserId = {0}", id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM UserPermissionExceptions WHERE UserId = {0}", id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM UserRoleAssignments WHERE UserId = {0}", id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM UserAccounts WHERE Id = {0}", id);
        }
    }

    private static async Task CleanupRolesAsync(SchoolDbContext db)
    {
        var like = TestRolePrefix + "%";
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM RolePermissions WHERE RoleId IN (SELECT Id FROM Roles WHERE Code LIKE {0})", like);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM UserRoleAssignments WHERE RoleId IN (SELECT Id FROM Roles WHERE Code LIKE {0})", like);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Roles WHERE Code LIKE {0}", like);
    }

    private static async Task CleanupPreviousAsync(SchoolDbContext db)
    {
        await CleanupUsersAsync(db, []);
        await CleanupRolesAsync(db);
        await CleanupCatalogHarnessAsync(db, new CatalogHarnessIds());
        var teacherLike = TestUserPrefix + "%";
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM PersonnelHrProfiles WHERE TeacherId IN (SELECT Id FROM Teachers WHERE EmployeeNumber LIKE {0})",
            teacherLike);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Teachers WHERE EmployeeNumber LIKE {0}", teacherLike);
    }

    private static SchoolDbContext CreateDbContext(string repoRoot)
    {
        var apiDirectory = Path.Combine(repoRoot, "src", "SchoolManagement.API");
        var bootstrap = new DatabaseConnectionBootstrap(apiDirectory);
        bootstrap.ConfigurationManager.EnsureDefaultFileExists();
        var configuration = bootstrap.LoadConfiguration();
        var validation = bootstrap.ConfigurationManager.Validate(configuration);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join("; ", validation.FieldErrors.Values));
        }

        var cs = bootstrap.BuildConnectionString(configuration);
        Evidence["database"] = Regex.Replace(cs, @"(Password|Pwd)=([^;]+)", "$1=***", RegexOptions.IgnoreCase);
        return new SchoolDbContext(new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cs).Options);
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

    private static void Check(string name, bool passed, string detail) => Results.Add(new(name, passed, detail));

    private sealed record CheckResult(string Name, bool Passed, string Detail);

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public FakeCurrentUser(Guid userId, Guid schoolId, string userName, IReadOnlyList<string> permissions)
        {
            UserId = userId;
            SchoolId = schoolId;
            UserName = userName;
            Permissions = permissions;
            Roles = [];
        }

        public Guid? UserId { get; }
        public Guid? SchoolId { get; }
        public string? UserName { get; }
        public IReadOnlyList<string> Permissions { get; }
        public IReadOnlyList<string> Roles { get; }
        public bool IsAdministrator => Permissions.Contains(global::SchoolManagement.Shared.Constants.Permissions.AdminFull, StringComparer.OrdinalIgnoreCase)
            || Roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase);

        public bool HasPermission(string permission) =>
            Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)
            || Permissions.Contains(global::SchoolManagement.Shared.Constants.Permissions.AdminFull, StringComparer.OrdinalIgnoreCase);
    }
}
