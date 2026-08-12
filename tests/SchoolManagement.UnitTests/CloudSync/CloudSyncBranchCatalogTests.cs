using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Infrastructure.CloudSync;
using Xunit;

namespace SchoolManagement.UnitTests.CloudSync;

public class CloudSyncBranchCatalogTests
{
    [Fact]
    public void SyncOrder_places_Branches_before_Courses()
    {
        var order = CloudSyncCatalog.SyncOrder;
        var idxBranches = IndexOf(order, "Branches");
        var idxCourses = IndexOf(order, "Courses");

        Assert.True(idxBranches >= 0, "Branches manquant dans SyncOrder");
        Assert.True(idxCourses >= 0, "Courses manquant dans SyncOrder");
        Assert.True(idxBranches < idxCourses, "Branches doit précéder Courses");
        Assert.Equal(typeof(Branch), order[idxBranches].ClrType);
        Assert.Equal(typeof(Course), order[idxCourses].ClrType);
    }

    [Fact]
    public void TryGetClrType_maps_Branches_to_Branch()
    {
        Assert.True(CloudSyncCatalog.TryGetClrType("Branches", out var clr));
        Assert.Equal(typeof(Branch), clr);
    }

    [Fact]
    public void Catalog_still_maps_previously_validated_tables()
    {
        string[] tables =
        [
            "Payments",
            "PaymentLines",
            "ClassFeeAmounts",
            "FinRetenue",
            "FinDestinationRepartition",
            "FinCleRepartition",
            "Adresse",
            "Students",
            "Guardians",
            "StudentGuardians"
        ];

        foreach (var table in tables)
        {
            Assert.True(CloudSyncCatalog.TryGetClrType(table, out _), $"Mapping perdu: {table}");
        }
    }

    private static int IndexOf(
        IReadOnlyList<(string Table, Type ClrType)> order,
        string tableName)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i].Table.Equals(tableName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
