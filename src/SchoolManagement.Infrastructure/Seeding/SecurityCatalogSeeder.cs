using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Shared.Constants;

namespace SchoolManagement.Infrastructure.Seeding;

/// <summary>
/// Seed catalogue sécurité, dépendances et rôles système francophones.
/// L'invalidation du cache catalogue est automatique via
/// <c>SecurityCatalogCacheInvalidationInterceptor</c> à chaque SaveChanges.
/// </summary>
public sealed class SecurityCatalogSeeder
{
    private readonly SchoolDbContext _context;
    private readonly ILogger<SecurityCatalogSeeder> _logger;

    public SecurityCatalogSeeder(SchoolDbContext context, ILogger<SecurityCatalogSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _context.IgnoreSchoolScope = true;
        try
        {
            await SeedPermissionsMetadataAsync(cancellationToken);
            await SeedPermissionDependenciesAsync(cancellationToken);
            await SeedNavigationCatalogAsync(cancellationToken);
            await SeedSystemRolesAsync(cancellationToken);
            _logger.LogInformation("Catalogue sécurité Phase 0 initialisé.");
        }
        finally
        {
            _context.IgnoreSchoolScope = false;
        }
    }

    private async Task SeedPermissionsMetadataAsync(CancellationToken cancellationToken)
    {
        var meta = BuildPermissionMetadata();
        foreach (var code in Permissions.All)
        {
            var (display, business, help) = meta.TryGetValue(code, out var m)
                ? m
                : (code, code, code);

            var existing = await _context.Permissions.FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
            if (existing is null)
            {
                var lastDot = code.LastIndexOf('.');
                var module = lastDot > 0 ? code[..lastDot] : code;
                var actionToken = lastDot > 0 ? code[(lastDot + 1)..] : "read";
                _context.Permissions.Add(new Permission
                {
                    Code = code,
                    Module = module,
                    Action = ParsePermissionAction(actionToken),
                    Description = business,
                    DisplayName = display,
                    BusinessDescription = business,
                    HelpText = help,
                    IsActive = true
                });
            }
            else
            {
                if (string.IsNullOrWhiteSpace(existing.DisplayName) || existing.DisplayName == existing.Code)
                {
                    existing.DisplayName = display;
                }

                if (string.IsNullOrWhiteSpace(existing.BusinessDescription)
                    || existing.BusinessDescription == existing.Code
                    || existing.BusinessDescription == existing.Description)
                {
                    existing.BusinessDescription = business;
                }

                if (string.IsNullOrWhiteSpace(existing.HelpText))
                {
                    existing.HelpText = help;
                }

                if (string.IsNullOrWhiteSpace(existing.Description) || existing.Description == existing.Code)
                {
                    existing.Description = business;
                }

                existing.IsActive = true;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPermissionDependenciesAsync(CancellationToken cancellationToken)
    {
        var edges = new (string Dependent, string Requires)[]
        {
            (Permissions.StudentsCreate, Permissions.StudentsRead),
            (Permissions.StudentsUpdate, Permissions.StudentsRead),
            (Permissions.StudentsDelete, Permissions.StudentsRead),
            (Permissions.SchoolsUpdate, Permissions.SchoolsRead),
            (Permissions.PaymentsCreate, Permissions.PaymentsRead),
            (Permissions.PaymentsValidate, Permissions.PaymentsRead),
            (Permissions.PaymentsCancel, Permissions.PaymentsRead),
            (Permissions.PaymentsNotesUpdate, Permissions.PaymentsRead),
            (Permissions.PaymentsPaidMutation, Permissions.PaymentsRead),
            (Permissions.PricingCategoriesAssign, Permissions.PaymentsRead),
            (Permissions.RevenueAllocationManage, Permissions.RevenueAllocationRead),
            (Permissions.WithholdingsManage, Permissions.WithholdingsRead),
            (Permissions.CurrenciesCreate, Permissions.CurrenciesRead),
            (Permissions.CurrenciesUpdate, Permissions.CurrenciesRead),
            (Permissions.CurrenciesDelete, Permissions.CurrenciesRead),
            (Permissions.ExchangeRatesCreate, Permissions.ExchangeRatesRead),
            (Permissions.ExchangeRatesUpdate, Permissions.ExchangeRatesRead),
            (Permissions.ExchangeRatesDelete, Permissions.ExchangeRatesRead),
            (Permissions.ExchangeRatesActivate, Permissions.ExchangeRatesRead),
            (Permissions.PaymentFxOverride, Permissions.PaymentsRead),
            (Permissions.StudentCardsCreate, Permissions.StudentCardsRead),
            (Permissions.StudentCardsUpdate, Permissions.StudentCardsRead),
            (Permissions.StudentCardsDelete, Permissions.StudentCardsRead),
            (Permissions.StudentCardsPrint, Permissions.StudentCardsRead),
            (Permissions.StudentCardsRenew, Permissions.StudentCardsRead),
            (Permissions.StudentCardsDeclareLost, Permissions.StudentCardsRead),
            (Permissions.CardTemplatesManage, Permissions.CardTemplatesRead),
            (Permissions.GradesCreate, Permissions.GradesRead),
            (Permissions.GradesUpdate, Permissions.GradesRead),
            (Permissions.GradesDelete, Permissions.GradesUpdate),
            (Permissions.GradesEvaluationDeleteWithGrades, Permissions.GradesDelete),
            (Permissions.GradesRecalculate, Permissions.GradesRead),
            (Permissions.GradesPublish, Permissions.GradesRead),
            (Permissions.GradesUnpublish, Permissions.GradesPublish),
            (Permissions.GradesCotationDelegate, Permissions.GradesRead),
            (Permissions.GradesCotationScopeClass, Permissions.GradesCreate),
            (Permissions.ResultsValidationValidate, Permissions.ResultsValidationRead),
            (Permissions.ResultsValidationLock, Permissions.ResultsValidationRead),
            (Permissions.ResultsValidationUnlock, Permissions.ResultsValidationRead),
            (Permissions.DeliberationPvWrite, Permissions.DeliberationPvRead),
            (Permissions.DeliberationDecisionWrite, Permissions.DeliberationDecisionRead),
            (Permissions.AccountingManage, Permissions.AccountingRead),
            (Permissions.PersonnelManage, Permissions.PersonnelRead),
            (Permissions.TeachersManage, Permissions.SchoolsRead),
            (Permissions.GeographyManage, Permissions.SchoolsRead),
            (Permissions.ParentActivationManage, Permissions.SchoolsRead),
            (Permissions.PedagogicalPeriodsManage, Permissions.GradesRead),
            (Permissions.SecurityUsersManage, Permissions.SecurityAuditRead),
            (Permissions.SecurityRolesManage, Permissions.SecurityAuditRead),
            (Permissions.SecurityExceptionsManage, Permissions.SecurityAuditRead),
        };

        var permissions = await _context.Permissions.ToListAsync(cancellationToken);
        var byCode = permissions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var (dependentCode, requiresCode) in edges)
        {
            if (!byCode.TryGetValue(dependentCode, out var dependent)
                || !byCode.TryGetValue(requiresCode, out var requires))
            {
                continue;
            }

            if (dependent.Id == requires.Id)
            {
                continue;
            }

            var exists = await _context.PermissionDependencies.AnyAsync(
                d => d.PermissionId == dependent.Id && d.RequiresPermissionId == requires.Id,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            _context.PermissionDependencies.Add(new PermissionDependency
            {
                PermissionId = dependent.Id,
                RequiresPermissionId = requires.Id,
                IsActive = true
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedNavigationCatalogAsync(CancellationToken cancellationToken)
    {
        var modules = new (string Code, string Name, string Icon, int Order, (string Code, string Name, int Order, (string Code, string Name, string DesktopKey, string? Perm, int Order)[] Pages)[] Functions)[]
        {
            ("DASHBOARD", "Tableau de bord", "ViewDashboard", 10,
            [
                ("VUE", "Vue", 1,
                [
                    ("MAIN", "Tableau de bord", "Dashboard.Main", Permissions.ReportsRead, 1)
                ])
            ]),
            ("SETTINGS", "Paramètres", "Cog", 20,
            [
                ("REFERENTIELS", "Référentiels", 1,
                [
                    ("ETABLISSEMENT", "Établissement", "Settings.Etablissement", Permissions.SchoolsRead, 1),
                    ("STRUCTURE", "Structure pédagogique", "Settings.StructurePedagogique", Permissions.SchoolsRead, 2),
                    ("ANNEES", "Années scolaires", "Settings.AnneesScolaires", Permissions.SchoolsRead, 3),
                    ("MATIERES", "Configuration des cours", "Settings.Matieres", Permissions.SchoolsRead, 4),
                    ("GEOGRAPHIE", "Géographie", "Settings.Geographie", Permissions.GeographyManage, 5),
                    ("USERS", "Utilisateurs", "Security.Users", Permissions.SecurityUsersManage, 6),
                    ("ROLES", "Rôles", "Security.Roles", Permissions.SecurityRolesManage, 7),
                    ("AUDIT", "Journal de sécurité", "Security.Audit", Permissions.SecurityAuditRead, 8)
                ]),
                ("CONFIG_FINANCE", "Configuration financière", 2,
                [
                    ("FRAIS", "Frais scolaires", "Settings.FraisScolaires", Permissions.SchoolsRead, 1),
                    ("REPARTITION", "Répartition des recettes", "Settings.RepartitionRecettes", Permissions.RevenueAllocationRead, 2),
                    ("RETENUES", "Retenues", "Settings.Retenues", Permissions.WithholdingsRead, 3),
                    ("MONNAIES", "Monnaies", "Settings.Monnaies", Permissions.CurrenciesRead, 4),
                    ("TAUX", "Taux de change", "Settings.TauxChange", Permissions.ExchangeRatesRead, 5),
                    ("HISTO_TAUX", "Historique des taux", "Settings.HistoriqueTaux", Permissions.ExchangeRateHistoryRead, 6)
                ]),
                ("ADMIN_SYSTEME", "Administration système", 3,
                [
                    ("SYNC_CLOUD", "Synchronisation cloud", "Settings.SyncCloud", Permissions.CloudSyncManage, 1),
                    ("MISES_A_JOUR", "Mises à jour", "Settings.MisesAJour", Permissions.UpdatesManage, 2),
                    ("PARENT_ACTIVATION", "Activation mobile parent", "Settings.ParentActivation", Permissions.ParentActivationManage, 3),
                    ("QR_ETABLISSEMENT", "QR établissement", "Settings.QrEtablissement", Permissions.SchoolsUpdate, 4)
                ])
            ]),
            ("PERSONNEL", "Personnel", "AccountTie", 30,
            [
                ("GESTION", "Gestion", 1,
                [
                    ("LISTE", "Liste du personnel", "Personnel.Liste", Permissions.PersonnelRead, 1),
                    ("NOUVEAU", "Nouveau personnel", "Personnel.Nouveau", Permissions.PersonnelManage, 2)
                ]),
                ("ORGANISATION", "Organisation", 2,
                [
                    ("FONCTIONS", "Fonctions / Postes", "Personnel.Fonctions", Permissions.PersonnelManage, 1),
                    ("DEPARTEMENTS", "Départements", "Personnel.Departements", Permissions.PersonnelManage, 2)
                ])
            ]),
            ("STUDENTS", "Élèves", "AccountGroup", 40,
            [
                ("DOSSIER", "Dossier", 1,
                [
                    ("LISTE", "Liste des élèves", "Students.Main", Permissions.StudentsRead, 1),
                    ("INSCRIPTION", "Assistant d'inscription", "Students.Enrollment", Permissions.StudentsCreate, 2)
                ])
            ]),
            ("STUDENT_CARDS", "Cartes élèves", "CardAccountDetails", 50,
            [
                ("CARTES", "Cartes", 1,
                [
                    ("MAIN", "Cartes élèves", "StudentCards.Main", Permissions.StudentCardsRead, 1)
                ])
            ]),
            ("ACADEMIC", "Académique", "School", 60,
            [
                ("STRUCTURE", "Structure", 1,
                [
                    ("MAIN", "Académique", "Academic.Main", Permissions.SchoolsRead, 1)
                ])
            ]),
            ("PEDAGOGICAL_CALENDAR", "Calendrier pédagogique", "CalendarClock", 70,
            [
                ("PERIODES", "Périodes", 1,
                [
                    ("MAIN", "Calendrier pédagogique", "PedagogicalPeriods.Main", Permissions.PedagogicalPeriodsManage, 1)
                ])
            ]),
            ("GRADES", "Cotation", "ClipboardEdit", 80,
            [
                ("SAISIE", "Saisie", 1,
                [
                    ("MAIN", "Cotation", "Grades.Main", Permissions.GradesRead, 1)
                ])
            ]),
            ("RESULTS", "Résultats scolaires", "SchoolOutline", 90,
            [
                ("CONSULTATION", "Consultation", 1,
                [
                    ("PAR_CLASSE", "Résultats par classe", "Results.ParClasse", Permissions.GradesRead, 1),
                    ("INDIVIDUEL", "Résultat individuel", "Results.Individuel", Permissions.GradesRead, 2)
                ]),
                ("CONSEIL", "Conseil de classe", 2,
                [
                    ("VALIDATION", "Validation des résultats", "Results.ValidationResultats", Permissions.ResultsValidationRead, 1),
                    ("DELIBERATION", "Délibération", "Results.Deliberation", Permissions.DeliberationPvRead, 2)
                ])
            ]),
            ("FINANCE", "Financier", "Cash", 100,
            [
                ("OPERATIONS", "Opérations", 1,
                [
                    ("ENCAISSEMENTS", "Encaissements", "Finance.Encaissements", Permissions.PaymentsRead, 1),
                    ("CATEGORIES", "Catégories tarifaires", "Finance.CategoriesTarifaires", Permissions.PaymentsRead, 2)
                ]),
                ("RAPPORTS", "Rapports", 2,
                [
                    ("RAPPORTS_FIN", "Rapports financiers", "Finance.Rapports", Permissions.ReportsRead, 1),
                    ("SITUATION", "Situation des paiements", "Finance.SituationPaiements", Permissions.PaymentsRead, 2)
                ]),
                ("COMPTABILITE", "Comptabilité", 3,
                [
                    ("DEPENSES", "Dépenses", "Finance.Depenses", Permissions.AccountingRead, 1)
                ])
            ]),
            ("DOCUMENTS", "Documents", "FileDocument", 110,
            [
                ("GESTION", "Gestion", 1,
                [
                    ("MAIN", "Documents", "Documents.Main", Permissions.SchoolsRead, 1)
                ])
            ]),
            ("STATISTICS", "Statistiques", "ChartBar", 120,
            [
                ("VUE", "Vue", 1,
                [
                    ("MAIN", "Statistiques", "Statistics.Main", Permissions.ReportsRead, 1)
                ])
            ]),
            ("PLATFORM", "Plateforme", "CloudCog", 140,
            [
                ("CATALOG", "Catalogue", 1,
                [
                    ("MAIN", "Catalogue sécurité", "Platform.Catalog", Permissions.PlatformCatalogManage, 1)
                ])
            ]),
        };

        foreach (var (moduleCode, moduleName, icon, moduleOrder, functions) in modules)
        {
            var module = await _context.SecurityModules.FirstOrDefaultAsync(m => m.Code == moduleCode, cancellationToken);
            if (module is null)
            {
                module = new SecurityModule
                {
                    Code = moduleCode,
                    Name = moduleName,
                    Icon = icon,
                    SortOrder = moduleOrder,
                    IsActive = true
                };
                _context.SecurityModules.Add(module);
                await _context.SaveChangesAsync(cancellationToken);
            }

            foreach (var (functionCode, functionName, functionOrder, pages) in functions)
            {
                var function = await _context.SecurityFunctions.FirstOrDefaultAsync(
                    f => f.ModuleId == module.Id && f.Code == functionCode, cancellationToken);
                if (function is null)
                {
                    function = new SecurityFunction
                    {
                        ModuleId = module.Id,
                        Code = functionCode,
                        Name = functionName,
                        SortOrder = functionOrder,
                        IsActive = true
                    };
                    _context.SecurityFunctions.Add(function);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                foreach (var (pageCode, pageName, desktopKey, perm, pageOrder) in pages)
                {
                    var page = await _context.SecurityPages.FirstOrDefaultAsync(
                        p => p.FunctionId == function.Id && p.Code == pageCode, cancellationToken);
                    if (page is null)
                    {
                        page = new SecurityPage
                        {
                            FunctionId = function.Id,
                            Code = pageCode,
                            Name = pageName,
                            DesktopViewKey = desktopKey,
                            RequiredPermissionCode = perm,
                            SortOrder = pageOrder,
                            IsActive = true,
                            IsAvailableOnDesktop = true,
                            IsAvailableOnWeb = false,
                            IsAvailableOnMobile = false
                        };
                        _context.SecurityPages.Add(page);
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    var openAction = await _context.SecurityActions.FirstOrDefaultAsync(
                        a => a.PageId == page.Id && a.Code == "OPEN", cancellationToken);
                    if (openAction is null)
                    {
                        openAction = new SecurityAction
                        {
                            PageId = page.Id,
                            Code = "OPEN",
                            Name = "Ouvrir",
                            Description = $"Accéder à {pageName}",
                            SortOrder = 1,
                            IsActive = true,
                            IsAvailableOnDesktop = true
                        };
                        _context.SecurityActions.Add(openAction);
                        await _context.SaveChangesAsync(cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(perm))
                    {
                        var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Code == perm, cancellationToken);
                        if (permission is not null && permission.SecurityActionId is null)
                        {
                            permission.SecurityActionId = openAction.Id;
                        }
                    }
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        await ReconcileMovedSecurityNavigationAsync(cancellationToken);
    }

    /// <summary>
    /// Migre le menu : Utilisateurs/Rôles/Audit sous Paramètres → Référentiels ;
    /// retire Exceptions (onglet Utilisateurs) et l'ancien module Sécurité → Administration.
    /// </summary>
    private async Task ReconcileMovedSecurityNavigationAsync(CancellationToken cancellationToken)
    {
        var settingsModule = await _context.SecurityModules
            .FirstOrDefaultAsync(m => m.Code == "SETTINGS", cancellationToken);
        if (settingsModule is null)
            return;

        var referentiels = await _context.SecurityFunctions
            .FirstOrDefaultAsync(f => f.ModuleId == settingsModule.Id && f.Code == "REFERENTIELS", cancellationToken);
        if (referentiels is null)
            return;

        // Ancienne page Settings.Utilisateurs : désactiver si la nouvelle USERS existe déjà.
        var legacyUsers = await _context.SecurityPages
            .FirstOrDefaultAsync(p => p.FunctionId == referentiels.Id && p.Code == "UTILISATEURS", cancellationToken);
        var modernUsers = await _context.SecurityPages
            .FirstOrDefaultAsync(p => p.FunctionId == referentiels.Id && p.Code == "USERS", cancellationToken);
        if (legacyUsers is not null)
        {
            if (modernUsers is null)
            {
                legacyUsers.Code = "USERS";
                legacyUsers.Name = "Utilisateurs";
                legacyUsers.DesktopViewKey = "Security.Users";
                legacyUsers.RequiredPermissionCode = Permissions.SecurityUsersManage;
                legacyUsers.SortOrder = 6;
                legacyUsers.IsActive = true;
            }
            else
            {
                legacyUsers.IsActive = false;
            }
        }

        await UpsertReferentielsPageAsync(
            referentiels.Id, "USERS", "Utilisateurs", "Security.Users",
            Permissions.SecurityUsersManage, 6, cancellationToken);
        await UpsertReferentielsPageAsync(
            referentiels.Id, "ROLES", "Rôles", "Security.Roles",
            Permissions.SecurityRolesManage, 7, cancellationToken);
        await UpsertReferentielsPageAsync(
            referentiels.Id, "AUDIT", "Journal de sécurité", "Security.Audit",
            Permissions.SecurityAuditRead, 8, cancellationToken);

        // Désactiver l'ancien module Sécurité (Administration) et ses pages.
        var securityModule = await _context.SecurityModules
            .FirstOrDefaultAsync(m => m.Code == "SECURITY", cancellationToken);
        if (securityModule is not null)
        {
            securityModule.IsActive = false;
            var securityFunctions = await _context.SecurityFunctions
                .Where(f => f.ModuleId == securityModule.Id)
                .ToListAsync(cancellationToken);
            foreach (var fn in securityFunctions)
            {
                fn.IsActive = false;
                var pages = await _context.SecurityPages
                    .Where(p => p.FunctionId == fn.Id)
                    .ToListAsync(cancellationToken);
                foreach (var page in pages)
                    page.IsActive = false;
            }
        }

        // Sécurité : ne pas laisser une page Exceptions active ailleurs.
        var exceptionPages = await _context.SecurityPages
            .Where(p => p.Code == "EXCEPTIONS" || p.DesktopViewKey == "Security.Exceptions")
            .ToListAsync(cancellationToken);
        foreach (var page in exceptionPages)
            page.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertReferentielsPageAsync(
        Guid functionId,
        string code,
        string name,
        string desktopKey,
        string permission,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        var page = await _context.SecurityPages
            .FirstOrDefaultAsync(p => p.FunctionId == functionId && p.Code == code, cancellationToken);
        if (page is null)
        {
            page = new SecurityPage
            {
                FunctionId = functionId,
                Code = code,
                Name = name,
                DesktopViewKey = desktopKey,
                RequiredPermissionCode = permission,
                SortOrder = sortOrder,
                IsActive = true,
                IsAvailableOnDesktop = true,
                IsAvailableOnWeb = false,
                IsAvailableOnMobile = false
            };
            _context.SecurityPages.Add(page);
            await _context.SaveChangesAsync(cancellationToken);

            _context.SecurityActions.Add(new SecurityAction
            {
                PageId = page.Id,
                Code = "OPEN",
                Name = "Ouvrir",
                Description = $"Accéder à {name}",
                SortOrder = 1,
                IsActive = true,
                IsAvailableOnDesktop = true
            });
            return;
        }

        page.Name = name;
        page.DesktopViewKey = desktopKey;
        page.RequiredPermissionCode = permission;
        page.SortOrder = sortOrder;
        page.IsActive = true;
    }

    private async Task SeedSystemRolesAsync(CancellationToken cancellationToken)
    {
        var schools = await _context.Schools.AsNoTracking().Select(s => s.Id).ToListAsync(cancellationToken);
        if (schools.Count == 0)
        {
            return;
        }

        var roleDefs = new (string Code, string Name, UserRole SystemRole, int SortOrder)[]
        {
            ("ADMIN", "Administrateur", UserRole.Administrateur, 10),
            ("DIRECTION", "Direction", UserRole.Direction, 20),
            ("ENSEIGNANT", "Enseignant", UserRole.Enseignant, 30),
            ("PARENT", "Parent", UserRole.Parent, 40),
            ("COMPTABLE", "Comptable", UserRole.Comptable, 50),
            ("CAISSIER", "Caissier", UserRole.Comptable, 60),
            ("PREFET", "Préfet des études", UserRole.Direction, 70),
            ("PROMOTEUR", "Promoteur", UserRole.Direction, 80),
        };

        var allPermissions = await _context.Permissions.Where(p => p.IsActive).ToListAsync(cancellationToken);

        foreach (var schoolId in schools)
        {
            foreach (var (code, name, systemRole, sortOrder) in roleDefs)
            {
                var role = await _context.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(
                    r => r.SchoolId == schoolId && r.Code == code && !r.IsDeleted, cancellationToken);
                if (role is null)
                {
                    // Rôle soft-deleted : réactiver plutôt que violer l'index unique.
                    role = await _context.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(
                        r => r.SchoolId == schoolId && r.Code == code, cancellationToken);
                    if (role is not null)
                    {
                        role.IsDeleted = false;
                        role.DeletedAt = null;
                        role.DeletedBy = null;
                        role.Name = name;
                        role.SystemRole = systemRole;
                        role.IsSystem = true;
                        role.IsAssignable = true;
                        role.SortOrder = sortOrder;
                    }
                }

                if (role is null)
                {
                    role = new Role
                    {
                        SchoolId = schoolId,
                        Code = code,
                        Name = name,
                        SystemRole = systemRole,
                        IsSystem = true,
                        IsAssignable = true,
                        SortOrder = sortOrder
                    };
                    _context.Roles.Add(role);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    role.IsSystem = true;
                    role.IsAssignable = true;
                    role.SortOrder = sortOrder;
                    if (string.IsNullOrWhiteSpace(role.Name))
                    {
                        role.Name = name;
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                }

                if (code == "ADMIN")
                {
                    foreach (var permission in allPermissions)
                    {
                        var existingRp = await _context.RolePermissions.IgnoreQueryFilters().FirstOrDefaultAsync(
                            rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id, cancellationToken);
                        if (existingRp is null)
                        {
                            _context.RolePermissions.Add(new RolePermission
                            {
                                RoleId = role.Id,
                                PermissionId = permission.Id
                            });
                        }
                        else if (existingRp.IsDeleted)
                        {
                            existingRp.IsDeleted = false;
                            existingRp.DeletedAt = null;
                            existingRp.DeletedBy = null;
                        }
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            await AssignRolePermissionsAsync(schoolId, "DIRECTION",
            [
                Permissions.SchoolsRead, Permissions.SchoolsUpdate,
                Permissions.StudentsRead, Permissions.StudentsCreate, Permissions.StudentsUpdate,
                Permissions.ResultsValidationRead, Permissions.ResultsValidationValidate,
                Permissions.DeliberationPvRead, Permissions.DeliberationPvWrite,
                Permissions.DeliberationDecisionRead, Permissions.DeliberationDecisionWrite,
                Permissions.GradesRead, Permissions.GradesCreate, Permissions.GradesUpdate,
                Permissions.GradesDelete, Permissions.GradesEvaluationDeleteWithGrades,
                Permissions.GradesRecalculate, Permissions.GradesPublish, Permissions.GradesUnpublish,
                Permissions.GradesCotationDelegate,
                Permissions.ReportsRead,
                Permissions.TeachersManage,
                Permissions.PedagogicalPeriodsManage
            ], cancellationToken);

            await AssignRolePermissionsAsync(schoolId, "ENSEIGNANT",
            [
                Permissions.StudentsRead, Permissions.SchoolsRead,
                Permissions.GradesRead, Permissions.GradesCreate, Permissions.GradesUpdate,
                Permissions.GradesDelete,
                Permissions.ResultsValidationRead,
                Permissions.DeliberationPvRead,
                Permissions.DeliberationDecisionRead
            ], cancellationToken);

            await AssignRolePermissionsAsync(schoolId, "PARENT",
            [
                Permissions.PaymentsRead, Permissions.GradesRead, Permissions.ReportsRead
            ], cancellationToken);

            await AssignRolePermissionsAsync(schoolId, "COMPTABLE",
            [
                Permissions.StudentsRead, Permissions.SchoolsRead,
                Permissions.PaymentsRead, Permissions.PaymentsCreate, Permissions.PaymentsValidate,
                Permissions.PaymentsCancel, Permissions.PaymentsNotesUpdate, Permissions.PaymentsPaidMutation,
                Permissions.PricingCategoriesAssign, Permissions.PaymentFxOverride,
                Permissions.AccountingRead, Permissions.ReportsRead
            ], cancellationToken);

            await AssignRolePermissionsAsync(schoolId, "CAISSIER",
            [
                Permissions.StudentsRead, Permissions.SchoolsRead,
                Permissions.PaymentsRead, Permissions.PaymentsCreate
            ], cancellationToken);

            await AssignRolePermissionsAsync(schoolId, "PREFET",
            [
                Permissions.SchoolsRead,
                Permissions.StudentsRead,
                Permissions.GradesRead, Permissions.GradesCreate, Permissions.GradesUpdate,
                Permissions.GradesDelete, Permissions.GradesRecalculate,
                Permissions.GradesCotationDelegate,
                Permissions.ResultsValidationRead, Permissions.ResultsValidationValidate,
                Permissions.DeliberationPvRead, Permissions.DeliberationPvWrite,
                Permissions.DeliberationDecisionRead, Permissions.DeliberationDecisionWrite
            ], cancellationToken);

            await AssignRolePermissionsAsync(schoolId, "PROMOTEUR",
            [
                Permissions.SchoolsRead,
                Permissions.StudentsRead,
                Permissions.GradesRead, Permissions.GradesPublish, Permissions.GradesUnpublish,
                Permissions.ResultsValidationRead, Permissions.ResultsValidationValidate,
                Permissions.ResultsValidationLock, Permissions.ResultsValidationUnlock,
                Permissions.DeliberationPvRead, Permissions.DeliberationPvWrite,
                Permissions.DeliberationDecisionRead, Permissions.DeliberationDecisionWrite,
                Permissions.ReportsRead
            ], cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task AssignRolePermissionsAsync(
        Guid schoolId,
        string roleCode,
        IEnumerable<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var role = await _context.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(
            r => r.SchoolId == schoolId && r.Code == roleCode && !r.IsDeleted, cancellationToken);
        if (role is null)
        {
            return;
        }

        var codes = permissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissions = await _context.Permissions
            .Where(p => codes.Contains(p.Code))
            .ToListAsync(cancellationToken);

        foreach (var permission in permissions)
        {
            var existingRp = await _context.RolePermissions.IgnoreQueryFilters().FirstOrDefaultAsync(
                rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id, cancellationToken);
            if (existingRp is null)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id
                });
            }
            else if (existingRp.IsDeleted)
            {
                existingRp.IsDeleted = false;
                existingRp.DeletedAt = null;
                existingRp.DeletedBy = null;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, (string Display, string Business, string Help)> BuildPermissionMetadata()
    {
        var map = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);
        void Add(string code, string display, string business, string? help = null) =>
            map[code] = (display, business, help ?? business);

        Add(Permissions.StudentsRead, "Élèves — Lecture", "Consulter les dossiers élèves.");
        Add(Permissions.StudentsCreate, "Élèves — Création", "Créer un élève ou une inscription.", "Nécessite aussi la lecture des élèves.");
        Add(Permissions.StudentsUpdate, "Élèves — Modification", "Modifier un dossier élève.");
        Add(Permissions.StudentsDelete, "Élèves — Suppression", "Supprimer ou archiver un élève.");
        Add(Permissions.SchoolsRead, "Établissement — Lecture", "Consulter les paramètres de l'école.");
        Add(Permissions.SchoolsUpdate, "Établissement — Modification", "Modifier les paramètres de l'école.");
        Add(Permissions.PaymentsRead, "Paiements — Lecture", "Consulter les encaissements et soldes.");
        Add(Permissions.PaymentsCreate, "Paiements — Création", "Enregistrer un encaissement.");
        Add(Permissions.PaymentsValidate, "Paiements — Validation", "Valider un paiement.");
        Add(Permissions.PaymentsCancel, "Paiements — Annulation", "Annuler un encaissement déjà enregistré.");
        Add(Permissions.PaymentsNotesUpdate, "Paiements — Notes", "Modifier les notes d'un paiement.");
        Add(Permissions.PaymentsPaidMutation, "Paiements — Modifier encaissé", "Modifier le montant ou le détail d'un paiement complet.");
        Add(Permissions.PricingCategoriesAssign, "Catégories tarifaires — Affectation", "Attribuer ou changer la catégorie tarifaire d'un élève.");
        Add(Permissions.RevenueAllocationRead, "Répartition — Lecture", "Consulter les clés de répartition.");
        Add(Permissions.RevenueAllocationManage, "Répartition — Gestion", "Configurer la répartition des recettes.");
        Add(Permissions.WithholdingsRead, "Retenues — Lecture", "Consulter les retenues.");
        Add(Permissions.WithholdingsManage, "Retenues — Gestion", "Configurer les retenues.");
        Add(Permissions.CurrenciesRead, "Monnaies — Lecture", "Consulter les monnaies.");
        Add(Permissions.CurrenciesCreate, "Monnaies — Création", "Ajouter une monnaie.");
        Add(Permissions.CurrenciesUpdate, "Monnaies — Modification", "Modifier une monnaie.");
        Add(Permissions.CurrenciesDelete, "Monnaies — Suppression", "Supprimer une monnaie.");
        Add(Permissions.ExchangeRatesRead, "Taux — Lecture", "Consulter les taux de change.");
        Add(Permissions.ExchangeRatesCreate, "Taux — Création", "Créer un taux de change.");
        Add(Permissions.ExchangeRatesUpdate, "Taux — Modification", "Modifier un taux de change.");
        Add(Permissions.ExchangeRatesDelete, "Taux — Suppression", "Supprimer un taux de change.");
        Add(Permissions.ExchangeRatesActivate, "Taux — Activation", "Activer un taux de change.");
        Add(Permissions.ExchangeRateHistoryRead, "Historique taux — Lecture", "Consulter l'historique des taux.");
        Add(Permissions.PaymentFxOverride, "Encaissement — Forcer le taux", "Modifier le taux pendant un encaissement.");
        Add(Permissions.StudentCardsRead, "Cartes — Lecture", "Consulter les cartes élèves.");
        Add(Permissions.StudentCardsCreate, "Cartes — Création", "Émettre une carte élève.");
        Add(Permissions.StudentCardsUpdate, "Cartes — Modification", "Modifier une carte élève.");
        Add(Permissions.StudentCardsDelete, "Cartes — Suppression", "Supprimer une carte élève.");
        Add(Permissions.StudentCardsPrint, "Cartes — Impression", "Imprimer une carte élève.");
        Add(Permissions.StudentCardsRenew, "Cartes — Renouvellement", "Renouveler une carte élève.");
        Add(Permissions.StudentCardsDeclareLost, "Cartes — Perte", "Déclarer une carte perdue.");
        Add(Permissions.CardTemplatesRead, "Modèles cartes — Lecture", "Consulter les modèles de cartes.");
        Add(Permissions.CardTemplatesManage, "Modèles cartes — Gestion", "Gérer les modèles de cartes.");
        Add(Permissions.GradesRead, "Notes — Lecture", "Consulter les notes et résultats.");
        Add(Permissions.GradesCreate, "Notes — Saisie", "Saisir des notes.");
        Add(Permissions.GradesUpdate, "Notes — Modification", "Modifier des notes.");
        Add(Permissions.GradesDelete, "Cotation — Supprimer évaluation (vide)", "Supprimer une évaluation sans notes saisies.");
        Add(Permissions.GradesEvaluationDeleteWithGrades, "Cotation — Supprimer évaluation notée", "Supprimer une évaluation contenant des notes.");
        Add(Permissions.GradesRecalculate, "Cotation — Recalcul manuel", "Recalculer moyennes et rangs (maintenance / correction).");
        Add(Permissions.GradesPublish, "Cotation — Publier", "Rendre visibles les résultats de cotation (portail parent). Indépendant de la validation officielle.");
        Add(Permissions.GradesUnpublish, "Cotation — Dépublier", "Retirer la visibilité des résultats publiés.");
        Add(Permissions.GradesCotationDelegate, "Cotation — Session déléguée", "Ouvrir la cotation au nom d'un enseignant.");
        Add(Permissions.GradesCotationScopeClass, "Cotation — Périmètre titulaire", "Coter toutes les matières des classes où l'enseignant est affecté.");
        Add(Permissions.ResultsValidationRead, "Validation résultats — Lecture", "Consulter l'état de validation.");
        Add(Permissions.ResultsValidationValidate, "Validation résultats — Valider", "Valider les résultats d'une classe.");
        Add(Permissions.ResultsValidationLock, "Validation résultats — Verrouiller", "Verrouiller les résultats.");
        Add(Permissions.ResultsValidationUnlock, "Validation résultats — Déverrouiller", "Déverrouiller les résultats.");
        Add(Permissions.DeliberationPvRead, "Délibération PV — Lecture", "Consulter le procès-verbal.");
        Add(Permissions.DeliberationPvWrite, "Délibération PV — Écriture", "Rédiger le procès-verbal.");
        Add(Permissions.DeliberationDecisionRead, "Délibération décisions — Lecture", "Consulter les décisions.");
        Add(Permissions.DeliberationDecisionWrite, "Délibération décisions — Écriture", "Saisir les décisions.");
        Add(Permissions.ReportsRead, "Rapports — Lecture", "Consulter tableaux de bord et rapports.");
        Add(Permissions.AccountingRead, "Comptabilité — Lecture", "Consulter la comptabilité.");
        Add(Permissions.AccountingManage, "Comptabilité — Gestion", "Gérer les écritures comptables.");
        Add(Permissions.PersonnelRead, "Personnel — Lecture", "Consulter le personnel et les référentiels RH.");
        Add(Permissions.PersonnelManage, "Personnel — Gestion", "Créer ou modifier fiches personnel, départements et fonctions.");
        Add(Permissions.TeachersManage, "Enseignants — Administration", "Gérer les fiches enseignants (matricule, affectation).");
        Add(Permissions.GeographyManage, "Géographie — Administration", "Gérer le référentiel géographique (pays, provinces, villes).");
        Add(Permissions.CloudSyncManage, "Sync cloud — Gestion", "Consulter l'état et lancer la synchronisation cloud.");
        Add(Permissions.UpdatesManage, "Mises à jour — Gestion", "Publier et activer les versions de l'application.");
        Add(Permissions.ParentActivationManage, "Activation parent — Support", "Émettre des jetons QR / lien d'activation mobile parent.");
        Add(Permissions.PedagogicalPeriodsManage, "Calendrier pédagogique — Gestion", "Créer, ouvrir, clôturer et verrouiller les périodes scolaires.");
        Add(Permissions.AdminFull, "Administrateur établissement", "Accès complet aux fonctions de l'établissement.", "Bypass des permissions de l'école (hors Super Admin plateforme).");
        Add(Permissions.SecurityUsersManage, "Sécurité — Utilisateurs", "Gérer les comptes utilisateurs de l'école.");
        Add(Permissions.SecurityRolesManage, "Sécurité — Rôles", "Gérer les rôles et leurs permissions.");
        Add(Permissions.SecurityExceptionsManage, "Sécurité — Exceptions", "Gérer les exceptions Grant/Deny datées.");
        Add(Permissions.SecurityAuditRead, "Sécurité — Audit", "Consulter le journal d'audit sécurité.");
        Add(Permissions.PlatformCatalogManage, "Plateforme — Catalogue", "Gérer le catalogue modules/permissions (Super Admin).");
        Add(Permissions.PlatformSuperAdmin, "Plateforme — Super Admin", "Marqueur de privilège Super Administrateur plateforme.");

        return map;
    }

    internal static PermissionAction ParsePermissionAction(string actionToken) =>
        actionToken.Trim().ToLowerInvariant() switch
        {
            "read" => PermissionAction.Read,
            "create" => PermissionAction.Create,
            "update" => PermissionAction.Update,
            "delete" => PermissionAction.Delete,
            "export" => PermissionAction.Export,
            "approve" => PermissionAction.Approve,
            "print" => PermissionAction.Print,
            "renew" => PermissionAction.Renew,
            "declare-lost" => PermissionAction.Update,
            "validate" => PermissionAction.Approve,
            "lock" or "unlock" => PermissionAction.Approve,
            "manage" => PermissionAction.Update,
            "full" or "superadmin" => PermissionAction.Approve,
            _ => PermissionAction.Read
        };
}
