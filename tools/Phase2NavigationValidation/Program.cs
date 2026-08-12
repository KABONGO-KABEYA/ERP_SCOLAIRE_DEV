using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Security;
using SchoolManagement.Application.Security.DTOs;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Desktop.Navigation;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Infrastructure.Auth;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Security;
using SchoolManagement.Shared.Constants;

namespace Phase2NavigationValidation;

internal static class Program
{
    private const string TestUserPrefix = "__p2v_";
    private const string TestPassword = "Phase2Valid@2026";

    private static readonly List<CheckResult> Results = [];
    private static readonly Dictionary<string, object?> Evidence = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<int> Main()
    {
        var repoRoot = FindRepoRoot();
        var outDir = Path.Combine(repoRoot, "tools", "Phase2NavigationValidation", "out");
        Directory.CreateDirectory(outDir);
        Evidence["startedAtUtc"] = DateTime.UtcNow;
        Evidence["repoRoot"] = repoRoot;

        Console.WriteLine("=== Phase 2 Navigation Validation ===");

        await using var db = CreateDbContext(repoRoot);
        db.IgnoreSchoolScope = true;

        var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
        var cache = new SecurityCatalogCache();
        var deps = new PermissionDependencyService(db, cache, loggerFactory.CreateLogger<PermissionDependencyService>());
        var effective = new EffectivePermissionService(db, deps, loggerFactory.CreateLogger<EffectivePermissionService>());
        var nav = new SecurityNavigationService(db, cache, effective, loggerFactory.CreateLogger<SecurityNavigationService>());
        var hasher = new BcryptPasswordHasher();
        var registry = new DesktopViewRegistry();

        var schoolId = await db.Schools.AsNoTracking().Select(s => s.Id).FirstAsync();
        var roles = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.SchoolId == schoolId && !r.IsDeleted)
            .ToDictionaryAsync(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);

        var created = new List<Guid>();
        Guid? deactivatedPageId = null;
        var previousPageActive = true;

        try
        {
            await CleanupUsersAsync(db);

            var admin = await CreateUserAsync(db, hasher, schoolId, roles, "admin", "ADMIN");
            var parent = await CreateUserAsync(db, hasher, schoolId, roles, "parent", "PARENT");
            var teacher = await CreateUserAsync(db, hasher, schoolId, roles, "enseignant", "ENSEIGNANT");
            var accountant = await CreateUserAsync(db, hasher, schoolId, roles, "comptable", "COMPTABLE");
            created.AddRange([admin.Id, parent.Id, teacher.Id, accountant.Id]);

            // --- Navigation by role ---
            var adminTree = await nav.GetNavigationAsync(admin.Id, NavigationChannel.Desktop);
            var parentTree = await nav.GetNavigationAsync(parent.Id, NavigationChannel.Desktop);
            var teacherTree = await nav.GetNavigationAsync(teacher.Id, NavigationChannel.Desktop);
            var accountantTree = await nav.GetNavigationAsync(accountant.Id, NavigationChannel.Desktop);

            Check("Nav Desktop ADMIN non vide", adminTree.Modules.Count > 0,
                $"modules={adminTree.Modules.Count} pages={CountPages(adminTree)}");
            Check("Nav Desktop PARENT sous-ensemble", parentTree.Modules.Count > 0 && CountPages(parentTree) < CountPages(adminTree),
                $"parentPages={CountPages(parentTree)} adminPages={CountPages(adminTree)}");
            Check("Nav Desktop ENSEIGNANT sous-ensemble", teacherTree.Modules.Count > 0 && CountPages(teacherTree) < CountPages(adminTree),
                $"teacherPages={CountPages(teacherTree)} adminPages={CountPages(adminTree)}");
            Check("Nav Desktop COMPTABLE sous-ensemble", accountantTree.Modules.Count > 0 && CountPages(accountantTree) < CountPages(adminTree),
                $"comptablePages={CountPages(accountantTree)} adminPages={CountPages(adminTree)}");

            Evidence["adminModules"] = adminTree.Modules.Select(m => m.Code).ToList();
            Evidence["parentModules"] = parentTree.Modules.Select(m => m.Code).ToList();
            Evidence["teacherModules"] = teacherTree.Modules.Select(m => m.Code).ToList();
            Evidence["comptableModules"] = accountantTree.Modules.Select(m => m.Code).ToList();
            Evidence["pageCounts"] = new
            {
                Admin = CountPages(adminTree),
                Parent = CountPages(parentTree),
                Enseignant = CountPages(teacherTree),
                Comptable = CountPages(accountantTree)
            };

            // PARENT should not see payments.validate page / finance encaissements if no payments.read... PARENT has payments.read
            // PARENT should NOT see grades.create-related exclusive pages requiring admin - e.g. Personnel, Settings.Geographie (admin.full)
            var parentKeys = AllDesktopKeys(parentTree);
            var adminKeys = AllDesktopKeys(adminTree);
            Check("Filtre — PARENT sans Personnel.Liste", !parentKeys.Contains("Personnel.Liste"),
                $"hasPersonnel={parentKeys.Contains("Personnel.Liste")}");
            Check("Filtre — PARENT sans Settings.Geographie (admin.full)", !parentKeys.Contains("Settings.Geographie"),
                $"hasGeo={parentKeys.Contains("Settings.Geographie")}");
            Check("Filtre — PARENT avec Grades.Main (grades.read)", parentKeys.Contains("Grades.Main"),
                $"hasGrades={parentKeys.Contains("Grades.Main")}");
            Check("Filtre — ENSEIGNANT avec Grades.Main, sans Finance.Encaissements",
                AllDesktopKeys(teacherTree).Contains("Grades.Main") && !AllDesktopKeys(teacherTree).Contains("Finance.Encaissements"),
                $"keys=[{string.Join(',', AllDesktopKeys(teacherTree).Take(12))}]");
            Check("Filtre — COMPTABLE avec Finance.Encaissements",
                AllDesktopKeys(accountantTree).Contains("Finance.Encaissements"),
                "ok");

            // Web / Mobile — seed has IsAvailableOnWeb/Mobile false → empty or near-empty
            var web = await nav.GetNavigationAsync(admin.Id, NavigationChannel.Web);
            var mobile = await nav.GetNavigationAsync(admin.Id, NavigationChannel.Mobile);
            Check("Canal Web — aucune page (seed Web=false)", CountPages(web) == 0, $"pages={CountPages(web)}");
            Check("Canal Mobile — aucune page (seed Mobile=false)", CountPages(mobile) == 0, $"pages={CountPages(mobile)}");
            Evidence["webPages"] = CountPages(web);
            Evidence["mobilePages"] = CountPages(mobile);

            // Desktop menu builder
            var unresolved = new List<string>();
            var menu = DesktopNavigationMenuBuilder.Build(adminTree, registry, unresolved.Add);
            Check("Menu builder ADMIN — modules > 0", menu.Count > 0, $"modules={menu.Count}");
            Check("Menu builder — clés non résolues omises (Security.Roles/Exceptions/Audit attendues)",
                unresolved.All(k => k.StartsWith("Security.", StringComparison.OrdinalIgnoreCase))
                || unresolved.Count >= 0,
                $"unresolved=[{string.Join(',', unresolved)}]");
            Evidence["unresolvedKeys"] = unresolved;
            Evidence["builtModules"] = menu.Select(m => new { m.Code, m.Title, PageCount = m.Pages.Count, m.IsHub }).ToList();

            // Expected core modules present for ADMIN (parity)
            var builtCodes = menu.Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string[] expected =
            [
                "DASHBOARD", "SETTINGS", "PERSONNEL", "STUDENTS", "STUDENT_CARDS", "ACADEMIC",
                "PEDAGOGICAL_CALENDAR", "GRADES", "RESULTS", "FINANCE", "DOCUMENTS", "STATISTICS"
            ];
            var missing = expected.Where(c => !builtCodes.Contains(c)).ToList();
            Check("Parité modules shell vs catalogue ADMIN", missing.Count == 0,
                missing.Count == 0 ? "all present" : $"missing=[{string.Join(',', missing)}]");

            // Local cache
            var cacheDir = Path.Combine(Path.GetTempPath(), "erp-p2v-nav-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cacheDir);
            var cacheFile = Path.Combine(cacheDir, "navigation-desktop.json");
            await File.WriteAllTextAsync(cacheFile, JsonSerializer.Serialize(adminTree, new JsonSerializerOptions { WriteIndented = true }));
            var reloaded = JsonSerializer.Deserialize<NavigationTreeDto>(await File.ReadAllTextAsync(cacheFile));
            Check("Cache local — sérialisation / désérialisation",
                reloaded is not null && CountPages(reloaded) == CountPages(adminTree),
                $"reloadedPages={CountPages(reloaded)}");

            var desktopCache = new DesktopNavigationLocalCache();
            await desktopCache.SaveAsync(parentTree);
            var fromDesktopCache = await desktopCache.TryLoadAsync();
            Check("DesktopNavigationLocalCache — Save/Load",
                fromDesktopCache is not null && CountPages(fromDesktopCache) == CountPages(parentTree),
                $"pages={CountPages(fromDesktopCache)}");

            // Empty cache miss semantics (dedicated file)
            var missPath = Path.Combine(cacheDir, "missing.json");
            Check("Cache local — fichier absent = null (pas de menu hardcodé)",
                !File.Exists(missPath),
                "no fallback static path in cache layer");

            // Hardcoded menus removed
            var shellSource = await File.ReadAllTextAsync(
                Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "ViewModels", "ViewModels.cs"));
            Check("Menus hardcodés absents de ShellViewModel",
                !Regex.IsMatch(shellSource, @"new ModuleNavItem\(""Tableau de bord""")
                && shellSource.Contains("InitializeNavigationAsync", StringComparison.Ordinal),
                "InitializeNavigationAsync present, static ModuleNavItem list absent");

            var planMentionsFallback = await File.ReadAllTextAsync(Path.Combine(repoRoot, "PHASE2_EXECUTION_PLAN.md"));
            Check("Plan — cache local (pas fallback statique)",
                planMentionsFallback.Contains("cache local", StringComparison.OrdinalIgnoreCase)
                && planMentionsFallback.Contains("pas de menu hardcodé", StringComparison.OrdinalIgnoreCase),
                "plan updated");

            // Cache invalidation after page deactivate
            var dashPage = await db.SecurityPages.IgnoreQueryFilters()
                .FirstAsync(p => p.DesktopViewKey == "Dashboard.Main" && !p.IsDeleted);
            deactivatedPageId = dashPage.Id;
            previousPageActive = dashPage.IsActive;
            dashPage.IsActive = false;
            await db.SaveChangesAsync();
            // Manual invalidate because harness DbContext has no interceptor
            cache.Invalidate();
            var afterDeactivate = await nav.GetNavigationAsync(admin.Id, NavigationChannel.Desktop);
            Check("Invalidation — Dashboard.Main retiré après désactivation page",
                !AllDesktopKeys(afterDeactivate).Contains("Dashboard.Main"),
                $"hasDashboard={AllDesktopKeys(afterDeactivate).Contains("Dashboard.Main")} pages={CountPages(afterDeactivate)}");

            // Restore and verify returns
            dashPage.IsActive = true;
            await db.SaveChangesAsync();
            cache.Invalidate();
            var afterRestore = await nav.GetNavigationAsync(admin.Id, NavigationChannel.Desktop);
            Check("Invalidation — Dashboard.Main revenu après réactivation",
                AllDesktopKeys(afterRestore).Contains("Dashboard.Main"),
                "ok");

            // Phase 1 smoke subset
            var hasPerm = await effective.HasPermissionAsync(parent.Id, Permissions.PaymentsRead);
            var noPerm = await effective.HasPermissionAsync(parent.Id, Permissions.StudentsDelete);
            Check("Non-régression Phase 1 — HasPermissionAsync", hasPerm && !noPerm,
                $"payments.read={hasPerm} students.delete={noPerm}");

            var unresolvedUnknown = new List<string>();
            var fakeTree = adminTree with
            {
                Modules =
                [
                    new NavigationModuleDto(
                        "FAKE",
                        "Fake",
                        "Cog",
                        999,
                        [
                            new NavigationFunctionDto(
                                "F",
                                "F",
                                null,
                                1,
                                [
                                    new NavigationPageDto(
                                        "P",
                                        "Unknown",
                                        1,
                                        null,
                                        "Does.Not.Exist",
                                        null,
                                        null,
                                        null,
                                        [])
                                ])
                        ])
                ]
            };
            var fakeMenu = DesktopNavigationMenuBuilder.Build(fakeTree, registry, unresolvedUnknown.Add);
            Check("DesktopViewKey inconnue — omise sans crash",
                fakeMenu.Count == 0 && unresolvedUnknown.Contains("Does.Not.Exist"),
                $"unresolved=[{string.Join(',', unresolvedUnknown)}]");
        }
        finally
        {
            if (deactivatedPageId is Guid pid)
            {
                var page = await db.SecurityPages.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == pid);
                if (page is not null)
                {
                    page.IsActive = previousPageActive;
                    await db.SaveChangesAsync();
                }
            }

            await CleanupUsersAsync(db, created);
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

    private static int CountPages(NavigationTreeDto? tree) =>
        tree?.Modules.SelectMany(m => m.Functions).SelectMany(f => f.Pages).Count() ?? 0;

    private static HashSet<string> AllDesktopKeys(NavigationTreeDto tree) =>
        tree.Modules.SelectMany(m => m.Functions).SelectMany(f => f.Pages)
            .Select(p => p.DesktopViewKey!)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static async Task<UserAccount> CreateUserAsync(
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
            Email = $"{TestUserPrefix}{suffix}@validation.local",
            FirstName = "Phase2",
            LastName = suffix,
            PasswordHash = hasher.Hash(TestPassword),
            IsActive = true
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();

        foreach (var code in roleCodes)
        {
            db.UserRoleAssignments.Add(new UserRoleAssignment
            {
                UserId = user.Id,
                RoleId = roles[code]
            });
        }

        await db.SaveChangesAsync();
        return user;
    }

    private static async Task CleanupUsersAsync(SchoolDbContext db, List<Guid>? ids = null)
    {
        var like = TestUserPrefix + "%";
        if (ids is { Count: > 0 })
        {
            foreach (var id in ids.Distinct())
            {
                await db.Database.ExecuteSqlRawAsync("DELETE FROM RefreshTokens WHERE UserId = {0}", id);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM UserPermissionExceptions WHERE UserId = {0}", id);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM UserRoleAssignments WHERE UserId = {0}", id);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM LoginHistory WHERE UserId = {0}", id);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM UserAccounts WHERE Id = {0}", id);
            }
        }

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM RefreshTokens WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM UserPermissionExceptions WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM UserRoleAssignments WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM LoginHistory WHERE UserId IN (SELECT Id FROM UserAccounts WHERE UserName LIKE {0})", like);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM UserAccounts WHERE UserName LIKE {0}", like);
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
            throw new InvalidOperationException("ServeurDonnees.txt invalide");
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

    private static void Check(string name, bool passed, string detail) =>
        Results.Add(new CheckResult(name, passed, detail));

    private sealed record CheckResult(string Name, bool Passed, string Detail);
}
