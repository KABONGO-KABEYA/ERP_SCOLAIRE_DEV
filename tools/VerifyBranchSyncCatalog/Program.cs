using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Infrastructure.CloudSync;

/// <summary>Vérification statique post-intégration Branches (pas de drain, ACTIF=0).</summary>
var order = CloudSyncCatalog.SyncOrder;
var idxBranches = -1;
var idxCourses = -1;
for (var i = 0; i < order.Count; i++)
{
    if (order[i].Table.Equals("Branches", StringComparison.OrdinalIgnoreCase))
    {
        idxBranches = i;
    }

    if (order[i].Table.Equals("Courses", StringComparison.OrdinalIgnoreCase))
    {
        idxCourses = i;
    }
}

Console.WriteLine($"Branches index={idxBranches} type={order[idxBranches].ClrType.Name}");
Console.WriteLine($"Courses index={idxCourses} type={order[idxCourses].ClrType.Name}");
if (idxBranches < 0 || idxCourses < 0 || idxBranches >= idxCourses)
{
    Console.Error.WriteLine("FAIL: Branches doit précéder Courses dans SyncOrder.");
    return 1;
}

if (!CloudSyncCatalog.TryGetClrType("Branches", out var branchClr) || branchClr != typeof(Branch))
{
    Console.Error.WriteLine("FAIL: TryGetClrType(Branches).");
    return 1;
}

if (!CloudSyncCatalog.TryGetClrType("Courses", out var courseClr) || courseClr != typeof(Course))
{
    Console.Error.WriteLine("FAIL: TryGetClrType(Courses).");
    return 1;
}

// Régression catalogue : tables validées précédemment toujours mappées
string[] mustExist =
[
    "Payments", "PaymentLines", "ClassFeeAmounts", "FinRetenue",
    "FinDestinationRepartition", "FinCleRepartition", "Adresse", "Students"
];
foreach (var t in mustExist)
{
    if (!CloudSyncCatalog.TryGetClrType(t, out _))
    {
        Console.Error.WriteLine($"FAIL: mapping manquant (régression) {t}");
        return 1;
    }
}

Console.WriteLine("OK: mapping Branches, ordre Branches→Courses, catalogue régressions OK.");
return 0;
