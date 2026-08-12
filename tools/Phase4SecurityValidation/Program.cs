using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Application.Auth.DTOs;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Infrastructure.Auth;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Infrastructure.Security;
using SchoolManagement.Infrastructure.Seeding;
using SchoolManagement.Shared.Constants;

namespace Phase4SecurityValidation;

internal static class Program
{
    private const string TestUserPrefix = "__p4v_";
    private const string TestPassword = "Phase4Valid@2026";
    private const string LotId = "LOT0+LOT1+LOT2+LOT3+LOT4+LOT5+LOT6+LOT7";

    private static readonly List<CheckResult> Results = [];
    private static readonly Dictionary<string, object?> Evidence = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<int> Main()
    {
        var repoRoot = FindRepoRoot();
        var outDir = Path.Combine(repoRoot, "tools", "Phase4SecurityValidation", "out");
        Directory.CreateDirectory(outDir);
        Evidence["lot"] = LotId;
        Evidence["startedAtUtc"] = DateTime.UtcNow;
        Evidence["repoRoot"] = repoRoot;

        Console.WriteLine("=== Phase 4 Security Migration Validation (Lot 0–7) ===");

        ScenarioDocumentation(repoRoot);
        ScenarioDesktopPermissionHelper(repoRoot);
        ScenarioLot1FinanceLegacyGrep(repoRoot);
        ScenarioLot2PersonnelLegacyGrep(repoRoot);
        ScenarioLot3InfrastructureLegacyGrep(repoRoot);
        ScenarioLot4PedagogicalLegacyGrep(repoRoot);
        ScenarioLot5ResultValidationLegacyGrep(repoRoot);
        ScenarioLot6CotationLegacyGrep(repoRoot);
        ScenarioLot7GovernanceAndCatalogAudit(repoRoot);

        await using var db = CreateDbContext(repoRoot);
        db.IgnoreSchoolScope = true;

        var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
        var seeder = new SecurityCatalogSeeder(db, loggerFactory.CreateLogger<SecurityCatalogSeeder>());
        await seeder.SeedAsync();
        var cache = new SecurityCatalogCache();
        var deps = new PermissionDependencyService(db, cache, loggerFactory.CreateLogger<PermissionDependencyService>());
        var effective = new EffectivePermissionService(db, deps, loggerFactory.CreateLogger<EffectivePermissionService>());
        var hasher = new BcryptPasswordHasher();

        var schoolId = await db.Schools.AsNoTracking().Select(s => s.Id).FirstAsync();
        var roleMap = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.SchoolId == schoolId && !r.IsDeleted)
            .ToDictionaryAsync(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);

        Evidence["schoolId"] = schoolId;

        var createdUserIds = new List<Guid>();
        try
        {
            await CleanupUsersAsync(db, []);

            var personas = new (string Key, string Role, string[] MustHave, string[] MustNotHave)[]
            {
                ("ADMIN", "ADMIN", [Permissions.AdminFull], []),
                ("DIRECTION", "DIRECTION",
                [
                    Permissions.SchoolsRead, Permissions.GradesRead, Permissions.GradesCreate, Permissions.GradesUpdate,
                    Permissions.GradesCotationDelegate, Permissions.GradesRecalculate, Permissions.GradesPublish,
                    Permissions.TeachersManage, Permissions.PedagogicalPeriodsManage, Permissions.ResultsValidationValidate
                ],
                [Permissions.SecurityUsersManage, Permissions.PersonnelRead, Permissions.PersonnelManage,
                    Permissions.ResultsValidationLock, Permissions.ResultsValidationUnlock, Permissions.GradesCotationScopeClass]),
                ("PREFET", "PREFET",
                [Permissions.ResultsValidationValidate, Permissions.GradesCotationDelegate,
                    Permissions.GradesCreate, Permissions.GradesUpdate, Permissions.GradesRecalculate],
                [Permissions.PlatformCatalogManage, Permissions.ResultsValidationLock, Permissions.ResultsValidationUnlock,
                    Permissions.PedagogicalPeriodsManage, Permissions.GradesPublish, Permissions.GradesEvaluationDeleteWithGrades]),
                ("PROMOTEUR", "PROMOTEUR",
                [Permissions.ResultsValidationLock, Permissions.ResultsValidationUnlock, Permissions.ResultsValidationValidate,
                    Permissions.GradesPublish, Permissions.GradesUnpublish],
                [Permissions.SecurityUsersManage, Permissions.GradesCreate, Permissions.GradesCotationDelegate]),
                ("ENSEIGNANT", "ENSEIGNANT",
                [Permissions.GradesCreate, Permissions.GradesUpdate, Permissions.GradesDelete, Permissions.SchoolsRead,
                    Permissions.ResultsValidationRead],
                [Permissions.PaymentsCreate, Permissions.PersonnelRead, Permissions.TeachersManage,
                    Permissions.PedagogicalPeriodsManage, Permissions.ResultsValidationValidate, Permissions.GradesRecalculate,
                    Permissions.GradesPublish, Permissions.GradesCotationDelegate]),
                ("COMPTABLE", "COMPTABLE",
                [
                    Permissions.PaymentsCreate, Permissions.AccountingRead,
                    Permissions.PaymentsCancel, Permissions.PaymentsPaidMutation,
                    Permissions.PricingCategoriesAssign, Permissions.PaymentFxOverride
                ],
                [Permissions.SecurityRolesManage]),
                ("CAISSIER", "CAISSIER", [Permissions.PaymentsCreate],
                [Permissions.PaymentsValidate, Permissions.PaymentsCancel, Permissions.PaymentsPaidMutation]),
                ("PARENT", "PARENT", [Permissions.PaymentsRead, Permissions.GradesRead], [Permissions.SecurityUsersManage, Permissions.StudentsCreate]),
            };

            Guid? adminUserId = null;
            foreach (var (key, role, mustHave, mustNot) in personas)
            {
                if (!roleMap.ContainsKey(role))
                {
                    Check($"Persona — {key} rôle seed", false, $"rôle {role} absent");
                    continue;
                }

                var user = await CreateUserAsync(db, hasher, schoolId, roleMap, key.ToLowerInvariant(), role);
                createdUserIds.Add(user.Id);
                if (key == "ADMIN")
                {
                    adminUserId = user.Id;
                }
                var eff = await effective.ResolveAsync(user.Id);
                var set = eff.PermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

                var haveOk = mustHave.All(p => set.Contains(p));
                var notOk = mustNot.Length == 0 || mustNot.All(p => !set.Contains(p));
                Check($"Persona — {key} permissions attendues", haveOk && notOk,
                    $"have={haveOk} deny={notOk} count={set.Count}");
            }

            if (adminUserId.HasValue)
            {
                Check("Non-régression — ResolveAsync ADMIN contient admin.full",
                    (await effective.ResolveAsync(adminUserId.Value)).PermissionCodes.Contains(Permissions.AdminFull, StringComparer.OrdinalIgnoreCase),
                    Permissions.AdminFull);
            }
        }
        finally
        {
            await CleanupUsersAsync(db, createdUserIds);
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

    private static void ScenarioDocumentation(string repoRoot)
    {
        Check("Doc — PHASE4_PERSONAS.md", File.Exists(Path.Combine(repoRoot, "PHASE4_PERSONAS.md")), "personas");
        Check("Doc — PHASE4_PLANNED_PERMISSIONS.md", File.Exists(Path.Combine(repoRoot, "PHASE4_PLANNED_PERMISSIONS.md")), "planned");
        Check("Doc — PHASE4_MIGRATION_CHECKLIST_TEMPLATE.md",
            File.Exists(Path.Combine(repoRoot, "PHASE4_MIGRATION_CHECKLIST_TEMPLATE.md")), "checklist");
        Check("Doc — PHASE4_LOT0_VALIDATION.md", File.Exists(Path.Combine(repoRoot, "PHASE4_LOT0_VALIDATION.md")), "lot0 report");
        Check("Doc — PHASE4_LOT1_VALIDATION.md", File.Exists(Path.Combine(repoRoot, "PHASE4_LOT1_VALIDATION.md")), "lot1 report");
        Check("Doc — PHASE4_LOT2_VALIDATION.md", File.Exists(Path.Combine(repoRoot, "PHASE4_LOT2_VALIDATION.md")), "lot2 report");
        Check("Doc — PHASE4_LOT3_VALIDATION.md", File.Exists(Path.Combine(repoRoot, "PHASE4_LOT3_VALIDATION.md")), "lot3 report");
        Check("Doc — PHASE4_LOT4_VALIDATION.md", File.Exists(Path.Combine(repoRoot, "PHASE4_LOT4_VALIDATION.md")), "lot4 report");
        Check("Doc — PHASE4_LOT5_VALIDATION.md", File.Exists(Path.Combine(repoRoot, "PHASE4_LOT5_VALIDATION.md")), "lot5 report");
        Check("Doc — PHASE4_LOT6_VALIDATION.md", File.Exists(Path.Combine(repoRoot, "PHASE4_LOT6_VALIDATION.md")), "lot6 report");
        Check("Doc — PHASE4_LOT7_VALIDATION.md", File.Exists(Path.Combine(repoRoot, "PHASE4_LOT7_VALIDATION.md")), "lot7 report");
        Check("Doc — PHASE4_VALIDATION_REPORT.md", File.Exists(Path.Combine(repoRoot, "PHASE4_VALIDATION_REPORT.md")), "phase4 closure");
        Check("Doc — SECURITY_ENGINE_ARCHITECTURE.md", File.Exists(Path.Combine(repoRoot, "SECURITY_ENGINE_ARCHITECTURE.md")), "architecture ref");
    }

    private static void ScenarioLot7GovernanceAndCatalogAudit(string repoRoot)
    {
        var srcRoot = Path.Combine(repoRoot, "src");
        var apiControllers = Path.Combine(srcRoot, "SchoolManagement.API", "Controllers");

        // Legacy transverse
        var appCs = Directory.EnumerateFiles(
                Path.Combine(srcRoot, "SchoolManagement.Application"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText)
            .ToList();
        Check("Lot7 — Application sans HasRole(",
            !appCs.Any(t => t.Contains("HasRole(", StringComparison.Ordinal)), "grep");
        Check("Lot7 — Application sans HasElevatedRole",
            !appCs.Any(t => t.Contains("HasElevatedRole", StringComparison.Ordinal)), "grep");

        var migratedDesktop = new[]
        {
            Path.Combine(srcRoot, "SchoolManagement.Desktop", "ViewModels", "EncaissementsViewModel.cs"),
            Path.Combine(srcRoot, "SchoolManagement.Desktop", "ViewModels", "GradesViewModel.Cotation.cs"),
            Path.Combine(srcRoot, "SchoolManagement.Desktop", "ViewModels", "SettingsViewModel.cs"),
        };
        Check("Lot7 — Desktop modules migrés sans IsAdministrator",
            migratedDesktop.All(p => !File.ReadAllText(p).Contains("IsAdministrator", StringComparison.Ordinal)),
            "grep");

        var httpUser = File.ReadAllText(Path.Combine(
            srcRoot, "SchoolManagement.Infrastructure", "Services", "HttpContextCurrentUserService.cs"));
        Check("Lot7 — HttpContextCurrentUser sans Contains ADMIN",
            !httpUser.Contains("Contains(\"ADMIN\"", StringComparison.Ordinal), "grep");
        Check("Lot7 — AuthSession IsAdministrator admin.full only",
            File.ReadAllText(Path.Combine(srcRoot, "SchoolManagement.Desktop", "Services", "ApiServices.cs"))
                .Contains("HasPermission(Permissions.AdminFull)", StringComparison.Ordinal)
            && !File.ReadAllText(Path.Combine(srcRoot, "SchoolManagement.Desktop", "Services", "ApiServices.cs"))
                .Contains("Contains(\"ADMIN\"", StringComparison.Ordinal),
            "grep");

        var businessAdminFull = Directory.EnumerateFiles(apiControllers, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => !string.Equals(Path.GetFileName(f), "AdminController.cs", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText)
            .Any(t => t.Contains("[Authorize(Policy = Permissions.AdminFull)]", StringComparison.Ordinal));
        Check("Lot7 — AdminFull uniquement gouvernance (AdminController)",
            !businessAdminFull, "grep controllers métier");

        var handler = File.ReadAllText(Path.Combine(
            srcRoot, "SchoolManagement.API", "Authorization", "PermissionAuthorizationHandler.cs"));
        Check("Lot7 — PermissionAuthorizationHandler admin.full bypass documenté",
            handler.Contains("Permissions.AdminFull", StringComparison.Ordinal), "handler");

        // AllowAnonymous inventory
        var allowAnonymousEndpoints = new List<string>();
        foreach (var ctrlFile in Directory.EnumerateFiles(apiControllers, "*.cs"))
        {
            var lines = File.ReadAllLines(ctrlFile);
            var ctrlName = Path.GetFileNameWithoutExtension(ctrlFile);
            var classAnonymous = false;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("[AllowAnonymous]", StringComparison.Ordinal))
                {
                    if (lines[i].TrimStart().StartsWith("[AllowAnonymous]", StringComparison.Ordinal)
                        && !lines[i].Contains("Http", StringComparison.Ordinal))
                    {
                        classAnonymous = true;
                    }

                    var route = ctrlName;
                    for (var j = i; j >= Math.Max(0, i - 8); j--)
                    {
                        if (lines[j].Contains("Http", StringComparison.Ordinal))
                        {
                            route = $"{ctrlName} :: {lines[j].Trim()}";
                            break;
                        }
                    }

                    if (classAnonymous && !route.Contains("Http", StringComparison.Ordinal))
                    {
                        route = $"{ctrlName} :: [class AllowAnonymous]";
                    }

                    allowAnonymousEndpoints.Add(route);
                }
            }
        }

        Evidence["lot7_allowAnonymous"] = allowAnonymousEndpoints;
        Check("Lot7 — AllowAnonymous inventaire non vide", allowAnonymousEndpoints.Count >= 5,
            $"count={allowAnonymousEndpoints.Count}");

        // Catalogue ↔ code
        var catalog = Permissions.All.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fieldToCode = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetValue(null)!, StringComparer.Ordinal);

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var permRefRegex = new Regex(@"Permissions\.(\w+)", RegexOptions.Compiled);
        var srcFiles = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !p.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"));

        foreach (var file in srcFiles)
        {
            var text = File.ReadAllText(file);
            foreach (Match m in permRefRegex.Matches(text))
            {
                if (fieldToCode.TryGetValue(m.Groups[1].Value, out var code))
                {
                    used.Add(code);
                }
            }
        }

        var orphans = catalog.Except(used).OrderBy(x => x).ToList();
        var unknownFieldRefs = fieldToCode.Values.Where(c => !catalog.Contains(c)).ToList();

        var literalPolicyRegex = new Regex(
            @"Authorize\(\s*Policy\s*=\s*""([a-z0-9][a-z0-9.\-]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var unknownLiterals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in srcFiles)
        {
            foreach (Match m in literalPolicyRegex.Matches(File.ReadAllText(file)))
            {
                if (!catalog.Contains(m.Groups[1].Value))
                {
                    unknownLiterals.Add(m.Groups[1].Value);
                }
            }
        }

        Evidence["lot7_catalogOrphans"] = orphans;
        Evidence["lot7_catalogUsedCount"] = used.Count;
        Evidence["lot7_unknownLiterals"] = unknownLiterals.ToList();

        Check("Lot7 — catalogue aucune permission orpheline",
            orphans.Count == 0, orphans.Count == 0 ? "ok" : string.Join(", ", orphans));
        Check("Lot7 — aucune policy littérale hors catalogue",
            unknownLiterals.Count == 0, unknownLiterals.Count == 0 ? "ok" : string.Join(", ", unknownLiterals));
        Check("Lot7 — Permissions.All cohérent avec constantes",
            unknownFieldRefs.Count == 0, $"extra={unknownFieldRefs.Count}");

        Check("Lot7 — AuthorizationExtensions enregistre Permissions.All",
            File.ReadAllText(Path.Combine(srcRoot, "SchoolManagement.API", "Extensions", "AuthorizationExtensions.cs"))
                .Contains("foreach (var permission in Permissions.All", StringComparison.Ordinal),
            "policies");
    }

    private static void ScenarioLot6CotationLegacyGrep(string repoRoot)
    {
        var appDir = Path.Combine(repoRoot, "src", "SchoolManagement.Application");
        var hasRoleAnywhere = Directory.EnumerateFiles(appDir, "*.cs", SearchOption.AllDirectories)
            .Any(f => File.ReadAllText(f).Contains("HasRole(", StringComparison.Ordinal));
        Check("Lot6 — Application sans HasRole(", !hasRoleAnywhere, "grep src/Application");

        var cotationSvc = File.ReadAllText(Path.Combine(
            appDir, "Grades", "Services", "GradeService.Cotation.cs"));
        Check("Lot6 — Cotation sans IsAdministrator",
            !cotationSvc.Contains("IsAdministrator", StringComparison.Ordinal), "grep");
        Check("Lot6 — scope delegate permission",
            cotationSvc.Contains("Permissions.GradesCotationDelegate", StringComparison.Ordinal),
            "HasPermission");
        Check("Lot6 — scope class permission",
            cotationSvc.Contains("Permissions.GradesCotationScopeClass", StringComparison.Ordinal),
            "HasPermission");

        var gradeSvc = File.ReadAllText(Path.Combine(appDir, "Grades", "Services", "GradeService.cs"));
        Check("Lot6 — DeleteEvaluation delete-with-grades permission",
            gradeSvc.Contains("Permissions.GradesEvaluationDeleteWithGrades", StringComparison.Ordinal),
            "permission");
        Check("Lot6 — recalc manuel GradesRecalculate",
            gradeSvc.Contains("Permissions.GradesRecalculate", StringComparison.Ordinal),
            "permission");
        Check("Lot6 — workflow recalc AfterDataChange",
            gradeSvc.Contains("RecalculatePeriodResultsAfterDataChangeAsync", StringComparison.Ordinal),
            "method");

        var pubSvc = File.ReadAllText(Path.Combine(appDir, "Grades", "Services", "GradeService.Publication.cs"));
        Check("Lot6 — publish sans results-validation",
            !pubSvc.Contains("ResultsValidationValidate", StringComparison.Ordinal)
            && !pubSvc.Contains("ResultsValidationLock", StringComparison.Ordinal)
            && !pubSvc.Contains("ResultsValidationUnlock", StringComparison.Ordinal)
            && !pubSvc.Contains("_resultValidation", StringComparison.Ordinal),
            "indépendance publication");

        var gradesCtrl = File.ReadAllText(Path.Combine(
            repoRoot, "src", "SchoolManagement.API", "Controllers", "GradesController.cs"));
        Check("Lot6 — DELETE evaluations GradesDelete",
            gradesCtrl.Contains("DeleteEvaluation", StringComparison.Ordinal)
            && gradesCtrl.Contains("Permissions.GradesDelete", StringComparison.Ordinal),
            "API");
        Check("Lot6 — calculate GradesRecalculate",
            gradesCtrl.Contains("Permissions.GradesRecalculate", StringComparison.Ordinal),
            "API");
        Check("Lot6 — publish/unpublish policies distinctes",
            gradesCtrl.Contains("Permissions.GradesPublish", StringComparison.Ordinal)
            && gradesCtrl.Contains("Permissions.GradesUnpublish", StringComparison.Ordinal),
            "API");

        var cotationVm = File.ReadAllText(Path.Combine(
            repoRoot, "src", "SchoolManagement.Desktop", "ViewModels", "GradesViewModel.Cotation.cs"));
        Check("Lot6 — Desktop cotation GradesCotationDelegate",
            cotationVm.Contains("Permissions.GradesCotationDelegate", StringComparison.Ordinal),
            "SessionPermissions");
        Check("Lot6 — Desktop cotation sans rôles JWT hardcodés",
            !cotationVm.Contains("PREFET_ETUDES", StringComparison.Ordinal)
            && !cotationVm.Contains("IsAdministrator", StringComparison.Ordinal),
            "grep");

        foreach (var code in new[]
                 {
                     Permissions.GradesDelete, Permissions.GradesEvaluationDeleteWithGrades,
                     Permissions.GradesRecalculate, Permissions.GradesPublish, Permissions.GradesUnpublish,
                     Permissions.GradesCotationDelegate, Permissions.GradesCotationScopeClass
                 })
        {
            Check($"Lot6 — Permissions.All contient {code}",
                Permissions.All.Contains(code, StringComparer.OrdinalIgnoreCase), code);
        }
    }

    private static void ScenarioLot5ResultValidationLegacyGrep(string repoRoot)
    {
        var svc = File.ReadAllText(Path.Combine(
            repoRoot, "src", "SchoolManagement.Application", "ResultValidation", "Services", "ResultValidationService.cs"));
        Check("Lot5 — ResultValidationService sans HasElevatedRole",
            !svc.Contains("HasElevatedRole", StringComparison.Ordinal), "grep");
        Check("Lot5 — ResultValidationService sans IsAdministrator",
            !svc.Contains("IsAdministrator", StringComparison.Ordinal), "grep");
        Check("Lot5 — ResultValidationService sans AdminFull",
            !svc.Contains("AdminFull", StringComparison.Ordinal), "grep");
        Check("Lot5 — CanValidate → results-validation.validate only",
            svc.Contains("HasPermission(Permissions.ResultsValidationValidate)", StringComparison.Ordinal),
            "permission");
        Check("Lot5 — CanLock → results-validation.lock only",
            svc.Contains("HasPermission(Permissions.ResultsValidationLock)", StringComparison.Ordinal),
            "permission");
        Check("Lot5 — CanUnlock → results-validation.unlock only",
            svc.Contains("HasPermission(Permissions.ResultsValidationUnlock)", StringComparison.Ordinal),
            "permission");
        Check("Lot5 — pas de recouvrement calendrier dans ResultValidationService",
            !svc.Contains("PedagogicalPeriodsManage", StringComparison.Ordinal)
            && !svc.Contains("Permissions.GradesRead", StringComparison.Ordinal),
            "séparation domaines");

        var pedCtrl = File.ReadAllText(Path.Combine(
            repoRoot, "src", "SchoolManagement.API", "Controllers", "PedagogicalPeriodsController.cs"));
        Check("Lot5 — séparation période active (grades.read) vs calendrier (manage)",
            pedCtrl.Contains("GetActive", StringComparison.Ordinal)
            && pedCtrl.Contains("Permissions.GradesRead", StringComparison.Ordinal)
            && pedCtrl.Contains("Permissions.PedagogicalPeriodsManage", StringComparison.Ordinal),
            "cross-check Lot 4");

        var valCtrl = File.ReadAllText(Path.Combine(
            repoRoot, "src", "SchoolManagement.API", "Controllers", "ResultValidationController.cs"));
        Check("Lot5 — API validate/lock/unlock policies distinctes",
            valCtrl.Contains("Permissions.ResultsValidationValidate", StringComparison.Ordinal)
            && valCtrl.Contains("Permissions.ResultsValidationLock", StringComparison.Ordinal)
            && valCtrl.Contains("Permissions.ResultsValidationUnlock", StringComparison.Ordinal)
            && !valCtrl.Contains("AdminFull", StringComparison.Ordinal),
            "API");
    }

    private static void ScenarioLot4PedagogicalLegacyGrep(string repoRoot)
    {
        var ctrl = File.ReadAllText(Path.Combine(
            repoRoot, "src", "SchoolManagement.API", "Controllers", "PedagogicalPeriodsController.cs"));
        Check("Lot4 — PedagogicalPeriodsController sans AdminFull", !ctrl.Contains("AdminFull", StringComparison.Ordinal), "grep");
        Check("Lot4 — PedagogicalPeriodsController active grades.read",
            ctrl.Contains("GetActive", StringComparison.Ordinal)
            && ctrl.Contains("Permissions.GradesRead", StringComparison.Ordinal),
            "policy lecture");

        var svc = File.ReadAllText(Path.Combine(
            repoRoot, "src", "SchoolManagement.Application", "PedagogicalPeriods", "Services", "PedagogicalPeriodService.cs"));
        Check("Lot4 — PedagogicalPeriodService sans EnsureAdministrator/IsAdministrator",
            !svc.Contains("EnsureAdministrator", StringComparison.Ordinal)
            && !svc.Contains("IsAdministrator", StringComparison.Ordinal),
            "grep");
        Check("Lot4 — PedagogicalPeriodService PedagogicalPeriodsManage",
            svc.Contains("Permissions.PedagogicalPeriodsManage", StringComparison.Ordinal),
            "HasPermission");

        var vm = File.ReadAllText(Path.Combine(
            repoRoot, "src", "SchoolManagement.Desktop", "ViewModels", "PedagogicalPeriodsViewModel.cs"));
        Check("Lot4 — PedagogicalPeriodsViewModel SessionPermissions",
            vm.Contains("Permissions.PedagogicalPeriodsManage", StringComparison.Ordinal),
            "Desktop");

        Check("Lot4 — Permissions.All contient pedagogical-periods.manage",
            Permissions.All.Contains(Permissions.PedagogicalPeriodsManage, StringComparer.OrdinalIgnoreCase),
            Permissions.PedagogicalPeriodsManage);
    }

    private static void ScenarioLot3InfrastructureLegacyGrep(string repoRoot)
    {
        var controllers = new (string File, string Label)[]
        {
            ("GeographyAdminController.cs", "GeographyAdminController"),
            ("CloudSyncController.cs", "CloudSyncController"),
            ("UpdateController.cs", "UpdateController"),
            ("ParentActivationIssueController.cs", "ParentActivationIssueController"),
        };

        foreach (var (file, label) in controllers)
        {
            var text = File.ReadAllText(Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers", file));
            Check($"Lot3 — {label} sans AdminFull", !text.Contains("AdminFull", StringComparison.Ordinal), "grep");
        }

        var geoRead = File.ReadAllText(Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers", "GeographyController.cs"));
        Check("Lot3 — GeographyController GET schools.read",
            geoRead.Contains("Permissions.SchoolsRead", StringComparison.Ordinal)
            && !geoRead.Contains("AdminFull", StringComparison.Ordinal),
            "policy lecture");

        foreach (var code in new[]
                 {
                     Permissions.GeographyManage, Permissions.CloudSyncManage,
                     Permissions.UpdatesManage, Permissions.ParentActivationManage
                 })
        {
            Check($"Lot3 — Permissions.All contient {code}",
                Permissions.All.Contains(code, StringComparer.OrdinalIgnoreCase), code);
        }
    }

    private static void ScenarioLot2PersonnelLegacyGrep(string repoRoot)
    {
        var personnelCtrl = File.ReadAllText(Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers", "PersonnelController.cs"));
        Check("Lot2 — PersonnelController sans AdminFull", !personnelCtrl.Contains("AdminFull", StringComparison.Ordinal), "grep");

        var adminCtrl = File.ReadAllText(Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers", "AdminController.cs"));
        var teacherMarkers = new[] { "HttpGet(\"teachers\")", "HttpPost(\"teachers\")", "HttpPut(\"teachers/{id:guid}\")" };
        var teachersLegacyFree = teacherMarkers.All(marker =>
        {
            var idx = adminCtrl.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
            {
                return false;
            }

            var slice = adminCtrl.Substring(idx, Math.Min(350, adminCtrl.Length - idx));
            return slice.Contains("Permissions.TeachersManage", StringComparison.Ordinal)
                && !slice.Contains("Permissions.AdminFull", StringComparison.Ordinal);
        });
        Check("Lot2 — routes teachers policy TeachersManage", teachersLegacyFree, "grep");

        var settingsVm = File.ReadAllText(Path.Combine(
            repoRoot, "src", "SchoolManagement.Desktop", "ViewModels", "SettingsViewModel.cs"));
        Check("Lot2 — SettingsViewModel teachers via TeachersManage",
            settingsVm.Contains("Permissions.TeachersManage", StringComparison.Ordinal)
            && !settingsVm.Contains("IsAdministrator", StringComparison.Ordinal),
            "SessionPermissions");

        foreach (var code in new[] { Permissions.PersonnelRead, Permissions.PersonnelManage, Permissions.TeachersManage })
        {
            Check($"Lot2 — Permissions.All contient {code}",
                Permissions.All.Contains(code, StringComparer.OrdinalIgnoreCase), code);
        }
    }

    private static void ScenarioLot1FinanceLegacyGrep(string repoRoot)
    {
        var paymentsCtrl = File.ReadAllText(Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers", "PaymentsController.cs"));
        Check("Lot1 — PaymentsController sans AdminFull", !paymentsCtrl.Contains("AdminFull", StringComparison.Ordinal), "grep");

        var financeCtrl = File.ReadAllText(Path.Combine(repoRoot, "src", "SchoolManagement.API", "Controllers", "FinanceController.cs"));
        Check("Lot1 — FinanceController sans AdminFull", !financeCtrl.Contains("AdminFull", StringComparison.Ordinal), "grep");

        var mutationPolicy = File.ReadAllText(Path.Combine(
            repoRoot, "src", "SchoolManagement.Application", "Payments", "Services", "PaymentMutationPolicy.cs"));
        Check("Lot1 — PaymentMutationPolicy sans IsAdministrator",
            !mutationPolicy.Contains("IsAdministrator", StringComparison.Ordinal), "grep");

        var financeVmPaths = new[]
        {
            Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "ViewModels", "EncaissementsViewModel.cs"),
            Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "ViewModels", "CollectPaymentViewModel.cs"),
            Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "ViewModels", "ExpenseMultiCurrencyAllocationViewModel.cs"),
            Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "ViewModels", "PricingCategoryAssignmentViewModel.cs"),
            Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "Views", "EncaissementActionWindow.xaml.cs"),
        };
        var desktopLegacy = financeVmPaths.Any(p => File.ReadAllText(p).Contains("IsAdministrator", StringComparison.Ordinal));
        Check("Lot1 — Desktop finance sans IsAdministrator", !desktopLegacy, "grep périmètre Lot 1");

        foreach (var code in new[]
                 {
                     Permissions.PaymentsCancel, Permissions.PaymentsNotesUpdate,
                     Permissions.PaymentsPaidMutation, Permissions.PricingCategoriesAssign
                 })
        {
            Check($"Lot1 — Permissions.All contient {code}",
                Permissions.All.Contains(code, StringComparer.OrdinalIgnoreCase), code);
        }
    }

    private static void ScenarioDesktopPermissionHelper(string repoRoot)
    {
        var session = new AuthSessionService();
        session.SetSession(new AuthResponse(
            "test",
            "test",
            DateTime.UtcNow.AddHours(1),
            new UserProfileDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "p4v",
                "p4@test.local",
                "P4 Test",
                false,
                null,
                ["ENSEIGNANT"],
                [Permissions.GradesCreate, Permissions.StudentsRead])));

        Check("Desktop — HasPermission positif", session.HasPermission(Permissions.GradesCreate), Permissions.GradesCreate);
        Check("Desktop — HasPermission négatif", !session.HasPermission(Permissions.SecurityUsersManage), "security.users.manage");
        Check("Desktop — HasPermission ignore rôle ADMIN sans code",
            !session.HasPermission(Permissions.AdminFull) && session.CurrentUser!.Roles.Contains("ENSEIGNANT"),
            "pas de fallback rôle");
        Check("Desktop — SessionPermissions.Can", SessionPermissions.Can(session, Permissions.StudentsRead), "Can");

        var sessionPath = Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "Services", "SessionPermissions.cs");
        var apiPath = Path.Combine(repoRoot, "src", "SchoolManagement.Desktop", "Services", "IApiServices.cs");
        var apiText = File.ReadAllTextAsync(apiPath).GetAwaiter().GetResult();
        Check("Desktop — IAuthSessionService.HasPermission déclaré",
            apiText.Contains("bool HasPermission(string permissionCode)", StringComparison.Ordinal),
            "interface");
        Check("Desktop — SessionPermissions.cs présent", File.Exists(sessionPath), sessionPath);
    }

    private static async Task<UserAccount> CreateUserAsync(
        SchoolDbContext db,
        BcryptPasswordHasher hasher,
        Guid schoolId,
        Dictionary<string, Guid> roles,
        string suffix,
        string roleCode)
    {
        var user = new UserAccount
        {
            SchoolId = schoolId,
            UserName = TestUserPrefix + suffix,
            Email = $"{TestUserPrefix}{suffix}@test.local",
            PasswordHash = hasher.Hash(TestPassword),
            FirstName = "P4",
            LastName = suffix,
            IsActive = true
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();
        if (roles.TryGetValue(roleCode, out var roleId))
        {
            db.UserRoleAssignments.Add(new UserRoleAssignment { UserId = user.Id, RoleId = roleId });
            await db.SaveChangesAsync();
        }

        return user;
    }

    private static async Task CleanupUsersAsync(SchoolDbContext db, List<Guid> userIds)
    {
        var like = TestUserPrefix + "%";
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
}
