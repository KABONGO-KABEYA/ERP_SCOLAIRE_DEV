using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;

/// <summary>
/// Analyse lecture seule des unités Failed (UserAccounts / UserRoleAssignments / UserPermissionExceptions).
/// Aucune modification de statut, aucun retry.
/// </summary>
var apiDir = args.Length > 0
    ? Path.GetFullPath(args[0])
    : @"d:\Mes Projet\ERP_Administration_Scolaire_2026\src\SchoolManagement.API\bin\Debug\net8.0";

var enc = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var cloudCfg = new CloudDatabaseConfigurationManager(apiDir, enc).LoadConfiguration();
Console.WriteLine($"=== Analyse Failed User* (lecture seule) ===");
Console.WriteLine($"ACTIF={cloudCfg.Actif} (doit rester 0)");

var localCs = factory.BuildConnectionString(new DatabaseConfigurationManager(apiDir, enc).LoadConfiguration());
var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());
var lo = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(localCs, s => s.CommandTimeout(30)).Options;
var co = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cloudCs, s => s.CommandTimeout(60)).Options;

await using var local = new SchoolDbContext(lo) { SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true };
await using var cloud = new SchoolDbContext(co) { SuppressCloudSyncEnqueue = true };

var targetTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "UserAccounts", "UserRoleAssignments", "UserPermissionExceptions"
};

var units = await local.SyncOutboxUnits
    .Include(u => u.Items)
    .Where(u => !u.IsDeleted
                && u.Status == SyncOutboxStatus.Failed
                && u.LastError != null
                && u.LastError.Contains("Confirmation cloud"))
    .OrderBy(u => u.CreatedAt)
    .ToListAsync();

units = units
    .Where(u => u.Items.Any(i => !i.IsDeleted && targetTables.Contains(i.TableName)))
    .ToList();

Console.WriteLine($"Unités Failed ciblées: {units.Count}\n");

var categories = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
{
    ["ORPHANED_LOCAL_ENTITY"] = 0,
    ["LOCAL_ENTITY_EXISTS"] = 0,
    ["PARENT_MISSING"] = 0,
    ["OTHER"] = 0
};

var details = new List<UnitAnalysis>();

foreach (var unit in units)
{
    var items = unit.Items.Where(i => !i.IsDeleted).OrderBy(i => i.Sequence).ToList();
    var primary = items.FirstOrDefault(i => targetTables.Contains(i.TableName)) ?? items.First();

    var entityLocal = await LoadEntityPresenceAsync(local, primary.TableName, primary.EntityId);
    var aggregateLocal = unit.AggregateId is Guid aggId && aggId != primary.EntityId
        ? await LoadEntityPresenceAsync(local, primary.TableName, aggId)
        : entityLocal;

    var cloudCount = await CountCloudAsync(cloud, primary.TableName, primary.EntityId);

    Guid? parentUserId = null;
    ParentPresence? parent = null;
    if (primary.TableName.Equals("UserRoleAssignments", StringComparison.OrdinalIgnoreCase))
    {
        var row = await local.Set<UserRoleAssignment>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == primary.EntityId);
        parentUserId = row?.UserId;
        parent = parentUserId is Guid uid ? await LoadUserParentAsync(local, cloud, uid) : null;
    }
    else if (primary.TableName.Equals("UserPermissionExceptions", StringComparison.OrdinalIgnoreCase))
    {
        var row = await local.Set<UserPermissionException>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == primary.EntityId);
        parentUserId = row?.UserId;
        parent = parentUserId is Guid uid ? await LoadUserParentAsync(local, cloud, uid) : null;
    }

    var category = Classify(entityLocal, cloudCount, primary.TableName, parent);
    categories[category]++;

    details.Add(new UnitAnalysis(
        unit.Id,
        unit.AggregateType,
        unit.AggregateId,
        primary.TableName,
        primary.EntityId,
        primary.Operation,
        unit.AttemptCount,
        Truncate(unit.LastError, 120),
        entityLocal.Exists,
        entityLocal.IsDeleted,
        aggregateLocal.Exists,
        aggregateLocal.IsDeleted,
        cloudCount,
        parentUserId,
        parent?.LocalExists,
        parent?.LocalIsDeleted,
        parent?.CloudCount,
        category,
        BuildNote(entityLocal, cloudCount, primary, parent)));
}

Console.WriteLine("=== RÉPARTITION PAR CATÉGORIE ===");
foreach (var kv in categories.OrderByDescending(k => k.Value))
{
    Console.WriteLine($"  {kv.Key}: {kv.Value}");
}

Console.WriteLine("\n=== DÉTAIL PAR UNITÉ ===");
foreach (var d in details)
{
    Console.WriteLine($"\nUnit={d.UnitId}");
    Console.WriteLine($"  Table={d.TableName} EntityId={d.EntityId} Op={d.Operation} Attempt={d.AttemptCount}");
    Console.WriteLine($"  AggregateType={d.AggregateType} AggregateId={d.AggregateId}");
    Console.WriteLine($"  EntityLocal: exists={d.EntityLocalExists} isDeleted={d.EntityLocalIsDeleted}");
    Console.WriteLine($"  AggregateLocal: exists={d.AggregateLocalExists} isDeleted={d.AggregateLocalIsDeleted}");
    Console.WriteLine($"  CloudCount={d.CloudCount}");
    if (d.ParentUserId is not null)
    {
        Console.WriteLine($"  ParentUserId={d.ParentUserId} parentLocal={d.ParentLocalExists} parentDeleted={d.ParentLocalIsDeleted} parentCloud={d.ParentCloudCount}");
    }

    Console.WriteLine($"  Category={d.Category}");
    Console.WriteLine($"  Note={d.Note}");
    Console.WriteLine($"  LastError={d.LastError}");
}

Console.WriteLine("\n=== RECOMMANDATIONS ===");
PrintRecommendations(categories, details);

return 0;

static string Classify(
    EntityPresence entityLocal,
    int cloudCount,
    string tableName,
    ParentPresence? parent)
{
    if (!entityLocal.Exists)
    {
        return "ORPHANED_LOCAL_ENTITY";
    }

    if (tableName.Equals("UserRoleAssignments", StringComparison.OrdinalIgnoreCase)
        || tableName.Equals("UserPermissionExceptions", StringComparison.OrdinalIgnoreCase))
    {
        if (parent is null || !parent.LocalExists || parent.CloudCount == 0)
        {
            return "PARENT_MISSING";
        }
    }

    if (cloudCount == 0)
    {
        return "LOCAL_ENTITY_EXISTS";
    }

    return "OTHER";
}

static string BuildNote(EntityPresence entity, int cloudCount, SyncOutboxItem primary, ParentPresence? parent)
{
    if (!entity.Exists)
    {
        return "EntityId absent en local (IgnoreQueryFilters) — outbox orpheline, ApplyTypedItemAsync no-op.";
    }

    if (entity.IsDeleted)
    {
        return "Entité locale IsDeleted=true — poussée possible en soft-delete mais cloud COUNT=0.";
    }

    if (parent is { LocalExists: false })
    {
        return "Entité enfant présente mais UserAccount parent absent localement.";
    }

    if (parent is { CloudCount: 0 })
    {
        return "Entité enfant locale OK mais UserAccount parent absent cloud.";
    }

    if (cloudCount == 0)
    {
        return "Entité locale présente, cloud absent — échec push ou skip moteur inattendu.";
    }

    return $"Entité présente local et cloud (COUNT={cloudCount}) malgré Failed verify.";
}

static void PrintRecommendations(Dictionary<string, int> categories, List<UnitAnalysis> details)
{
    if (categories["ORPHANED_LOCAL_ENTITY"] > 0)
    {
        Console.WriteLine($"- ORPHANED_LOCAL_ENTITY ({categories["ORPHANED_LOCAL_ENTITY"]}): marquer Completed manuellement ou archiver après revue métier — rien à pousser.");
    }

    if (categories["LOCAL_ENTITY_EXISTS"] > 0)
    {
        Console.WriteLine($"- LOCAL_ENTITY_EXISTS ({categories["LOCAL_ENTITY_EXISTS"]}): retry ciblé unitaire après diagnostic ApplyTypedItemAsync (entité présente, cloud vide).");
    }

    if (categories["PARENT_MISSING"] > 0)
    {
        Console.WriteLine($"- PARENT_MISSING ({categories["PARENT_MISSING"]}): synchroniser d'abord le UserAccount parent, puis retry enfant.");
    }

    if (categories["OTHER"] > 0)
    {
        Console.WriteLine($"- OTHER ({categories["OTHER"]}): revue manuelle — entité déjà cloud ou cas atypique.");
        foreach (var d in details.Where(x => x.Category == "OTHER"))
        {
            Console.WriteLine($"    Unit {d.UnitId} cloudCount={d.CloudCount}");
        }
    }
}

static async Task<EntityPresence> LoadEntityPresenceAsync(SchoolDbContext db, string tableName, Guid id)
{
    return tableName.ToUpperInvariant() switch
    {
        "USERACCOUNTS" => await LoadTypedPresenceAsync<UserAccount>(db, id),
        "USERROLEASSIGNMENTS" => await LoadTypedPresenceAsync<UserRoleAssignment>(db, id),
        "USERPERMISSIONEXCEPTIONS" => await LoadTypedPresenceAsync<UserPermissionException>(db, id),
        _ => new EntityPresence(false, false)
    };
}

static async Task<EntityPresence> LoadTypedPresenceAsync<T>(SchoolDbContext db, Guid id)
    where T : AuditableEntity
{
    var row = await db.Set<T>().IgnoreQueryFilters().AsNoTracking()
        .Where(e => e.Id == id)
        .Select(e => new { e.IsDeleted })
        .FirstOrDefaultAsync();
    return row is null ? new EntityPresence(false, false) : new EntityPresence(true, row.IsDeleted);
}

static async Task<int> CountCloudAsync(SchoolDbContext db, string tableName, Guid id)
{
    return tableName.ToUpperInvariant() switch
    {
        "USERACCOUNTS" => await db.Set<UserAccount>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "USERROLEASSIGNMENTS" => await db.Set<UserRoleAssignment>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        "USERPERMISSIONEXCEPTIONS" => await db.Set<UserPermissionException>().IgnoreQueryFilters().CountAsync(e => e.Id == id),
        _ => -1
    };
}

static async Task<ParentPresence> LoadUserParentAsync(SchoolDbContext local, SchoolDbContext cloud, Guid userId)
{
    var row = await local.Set<UserAccount>().IgnoreQueryFilters().AsNoTracking()
        .Where(e => e.Id == userId)
        .Select(e => new { e.IsDeleted })
        .FirstOrDefaultAsync();
    var cloudCount = await cloud.Set<UserAccount>().IgnoreQueryFilters().CountAsync(e => e.Id == userId);
    return new ParentPresence(row is not null, row?.IsDeleted ?? false, cloudCount);
}

static string Truncate(string? s, int max) =>
    string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max];

sealed record EntityPresence(bool Exists, bool IsDeleted);
sealed record ParentPresence(bool LocalExists, bool LocalIsDeleted, int CloudCount);
sealed record UnitAnalysis(
    Guid UnitId,
    string AggregateType,
    Guid? AggregateId,
    string TableName,
    Guid EntityId,
    SyncOperationType Operation,
    int AttemptCount,
    string LastError,
    bool EntityLocalExists,
    bool EntityLocalIsDeleted,
    bool AggregateLocalExists,
    bool AggregateLocalIsDeleted,
    int CloudCount,
    Guid? ParentUserId,
    bool? ParentLocalExists,
    bool? ParentLocalIsDeleted,
    int? ParentCloudCount,
    string Category,
    string Note);
