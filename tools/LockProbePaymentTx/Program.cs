using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Configuration.Database;
using SchoolManagement.Application.Configuration.Encryption;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Infrastructure.Persistence;

var apiDir = args.Length > 0
    ? args[0]
    : @"d:\Mes Projet\ERP_Administration_Scolaire_2026\src\SchoolManagement.API\bin\Debug\net8.0";

var encryption = new EncryptionService();
var factory = new DatabaseConnectionFactory();
var cloudMgr = new CloudDatabaseConfigurationManager(apiDir, encryption);
var cloudCs = factory.BuildConnectionString(cloudMgr.LoadConfiguration().ToDatabaseConfiguration());
var opts = new DbContextOptionsBuilder<SchoolDbContext>()
    .UseSqlServer(cloudCs, s => s.CommandTimeout(40))
    .Options;
var paymentId = Guid.Parse("4EFE0AEE-EFEF-49A5-A07B-E7E81AC62371");

Console.WriteLine("=== LockProbe: TX uncommitted + EnsureParent other connection ===");
await using var txCtx = new SchoolDbContext(opts) { SuppressCloudSyncEnqueue = true };
await using var tx = await txCtx.Database.BeginTransactionAsync();
var p = await txCtx.Set<Payment>().IgnoreQueryFilters().FirstAsync(x => x.Id == paymentId);
p.Notes = (p.Notes ?? string.Empty) + ".";
await txCtx.SaveChangesAsync();
Console.WriteLine("TX: Payment Modified saved (uncommitted)");

var sw = Stopwatch.StartNew();
try
{
    await using var other = new SchoolDbContext(opts) { SuppressCloudSyncEnqueue = true };
    var exists = await other.Set<Payment>().IgnoreQueryFilters().AsNoTracking()
        .AnyAsync(x => x.Id == paymentId);
    sw.Stop();
    Console.WriteLine($"OTHER AnyAsync OK exists={exists} in {sw.ElapsedMilliseconds} ms — NO lock block");
}
catch (Exception ex)
{
    sw.Stop();
    Console.WriteLine($"OTHER AnyAsync FAIL in {sw.ElapsedMilliseconds} ms");
    Console.WriteLine(ex.Message);
    Console.WriteLine(">>> HYPOTHESIS CONFIRMED: self-blocking by open unit TX");
}
finally
{
    await tx.RollbackAsync();
    Console.WriteLine("TX rolled back");
}
