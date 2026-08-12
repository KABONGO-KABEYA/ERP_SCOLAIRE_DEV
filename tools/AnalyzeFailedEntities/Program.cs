using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Infrastructure.Persistence;

var apiDir = @"d:\Mes Projet\ERP_Administration_Scolaire_2026\src\SchoolManagement.API\bin\Debug\net8.0";
var enc = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var localCs = factory.BuildConnectionString(new DatabaseConfigurationManager(apiDir, enc).LoadConfiguration());
var cloudCfg = new CloudDatabaseConfigurationManager(apiDir, enc).LoadConfiguration();
Console.WriteLine($"ACTIF={cloudCfg.Actif}");
var cloudCs = factory.BuildConnectionString(cloudCfg.ToDatabaseConfiguration());
var lo = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(localCs, s=>s.CommandTimeout(30)).Options;
var co = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(cloudCs, s=>s.CommandTimeout(60)).Options;
await using var local = new SchoolDbContext(lo){SuppressCloudSyncEnqueue=true,IgnoreSchoolScope=true};
await using var cloud = new SchoolDbContext(co){SuppressCloudSyncEnqueue=true};

var failedCourseIds = await local.SyncOutboxUnits
    .Where(u => !u.IsDeleted && u.AggregateType=="Entity" && u.Status==SchoolManagement.Domain.Enums.SyncOutboxStatus.Failed)
    .SelectMany(u => u.Items.Where(i=>!i.IsDeleted).Select(i=>i.EntityId))
    .Distinct().ToListAsync();
var courses = await local.Set<Course>().IgnoreQueryFilters().AsNoTracking()
    .Where(c => failedCourseIds.Contains(c.Id)).ToListAsync();
var branchIds = courses.Select(c=>c.BranchId).Where(x=>x!=null).Select(x=>x!.Value).Distinct().ToList();
Console.WriteLine($"Failed courses={courses.Count} distinct BranchIds={branchIds.Count}");

var cloudBranchCount = await cloud.Set<Branch>().IgnoreQueryFilters().AsNoTracking().CountAsync();
Console.WriteLine($"Cloud Branches total={cloudBranchCount}");
var missing=0; var present=0;
foreach (var bid in branchIds)
{
    var ok = await cloud.Set<Branch>().IgnoreQueryFilters().AsNoTracking().AnyAsync(b=>b.Id==bid);
    var localB = await local.Set<Branch>().IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(b=>b.Id==bid);
    Console.WriteLine($"Branch {bid}: local={(localB!=null)} cloud={ok} name={localB?.Name} school={localB?.SchoolId}");
    if (ok) present++; else missing++;
}
Console.WriteLine($"Branch parents: present={present} missing={missing}");

var cloudCourseHits=0;
foreach (var c in courses.Take(5))
{
    var exists=await cloud.Set<Course>().IgnoreQueryFilters().AsNoTracking().AnyAsync(x=>x.Id==c.Id);
    if(exists) cloudCourseHits++;
}
Console.WriteLine($"Sample courses already on cloud (of 5): {cloudCourseHits}");

// catalog presence
Console.WriteLine($"Has Branches table in sync catalog? check via Courses ensure parents manually N/A");
