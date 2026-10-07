using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.UnitTests.CloudSync;

public sealed class CloudSyncTariffIdentityTests
{
    internal static SchoolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options)
        { SuppressCloudSyncEnqueue = true };

    internal static ClassFeeAmount CreateTariff(Guid? schoolId = null) => new()
    {
        SchoolId = schoolId ?? Guid.NewGuid(), AcademicYearId = Guid.NewGuid(),
        PedagogicalClassId = Guid.NewGuid(), FeePricingCategoryId = Guid.NewGuid(),
        FeeTypeId = Guid.NewGuid(), FeeInstallmentId = Guid.NewGuid(), Amount = 65
    };

    internal static ClassFeeAmount CopyTariff(ClassFeeAmount tariff, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(), SchoolId = tariff.SchoolId,
        AcademicYearId = tariff.AcademicYearId, PedagogicalClassId = tariff.PedagogicalClassId,
        FeePricingCategoryId = tariff.FeePricingCategoryId, FeeTypeId = tariff.FeeTypeId,
        FeeInstallmentId = tariff.FeeInstallmentId, Amount = tariff.Amount,
        IsDeleted = tariff.IsDeleted
    };

    internal static SyncOutboxItem Item(string table, Guid id) => new()
        { TableName = table, EntityId = id, Operation = SyncOperationType.Insert };

    [Fact]
    public async Task Tariff_updates_existing_cloud_identity_and_verifies_without_changing_local_id()
    {
        await using var local = CreateContext();
        await using var remote = CreateContext();
        var tariff = CreateTariff();
        var cloud = CopyTariff(tariff);
        cloud.Amount = 10;
        local.Add(tariff);
        remote.Add(cloud);
        await local.SaveChangesAsync();
        await remote.SaveChangesAsync();
        remote.ChangeTracker.Clear();
        var item = Item("ClassFeeAmounts", tariff.Id);

        for (var i = 0; i < 2; i++)
        {
            await CloudSyncEngine.ApplyTypedItemAsync<ClassFeeAmount>(local, remote, item, default);
            await remote.SaveChangesAsync();
            remote.ChangeTracker.Clear();
            (await CloudSyncEngine.CountAppliedItemOnCloudAsync(local, remote, item, default)).Should().Be(1);
        }

        var result = await remote.Set<ClassFeeAmount>().IgnoreQueryFilters().SingleAsync();
        result.Id.Should().Be(cloud.Id);
        result.Amount.Should().Be(65);
        (await local.Set<ClassFeeAmount>().IgnoreQueryFilters().SingleAsync()).Id.Should().Be(tariff.Id);
        item.EntityId.Should().Be(tariff.Id);
    }

    [Fact]
    public async Task Remembered_identity_survives_tariff_key_change_and_soft_delete()
    {
        await using var local = CreateContext();
        await using var remote = CreateContext();
        var tariff = CreateTariff();
        var cloud = CopyTariff(tariff);
        local.Add(tariff);
        remote.Add(cloud);
        await local.SaveChangesAsync();
        await remote.SaveChangesAsync();
        remote.ChangeTracker.Clear();
        var item = Item("ClassFeeAmounts", tariff.Id);
        await CloudSyncEngine.ApplyTypedItemAsync<ClassFeeAmount>(local, remote, item, default);
        await remote.SaveChangesAsync();
        remote.ChangeTracker.Clear();

        tariff.FeeInstallmentId = Guid.NewGuid();
        await local.SaveChangesAsync();
        await CloudSyncEngine.ApplyTypedItemAsync<ClassFeeAmount>(local, remote, item, default);
        await remote.SaveChangesAsync();
        remote.ChangeTracker.Clear();
        var updated = await remote.Set<ClassFeeAmount>().IgnoreQueryFilters().SingleAsync();
        updated.Id.Should().Be(cloud.Id);
        updated.FeeInstallmentId.Should().Be(tariff.FeeInstallmentId);

        remote.ChangeTracker.Clear();
        tariff.IsDeleted = true;
        await local.SaveChangesAsync();
        item.Operation = SyncOperationType.Delete;
        await CloudSyncEngine.ApplyTypedItemAsync<ClassFeeAmount>(local, remote, item, default);
        await remote.SaveChangesAsync();
        (await remote.Set<ClassFeeAmount>().IgnoreQueryFilters().SingleAsync()).IsDeleted.Should().BeTrue();
        (await local.Set<SyncEntityIdentity>().SingleAsync()).CloudId.Should().Be(cloud.Id);
        tariff.Id.Should().NotBe(cloud.Id);
    }

    [Fact]
    public async Task Finance_reference_preparation_updates_existing_tariff_instead_of_inserting_duplicate()
    {
        await using var local = CreateContext();
        await using var remote = CreateContext();
        var tariff = CreateTariff();
        var cloud = CopyTariff(tariff);
        local.Add(tariff);
        remote.Add(cloud);
        await local.SaveChangesAsync();
        await remote.SaveChangesAsync();
        remote.ChangeTracker.Clear();
        await CloudSyncEngine.UpsertAllAsync<ClassFeeAmount>(local, remote, default);
        await CloudSyncEngine.UpsertAllAsync<ClassFeeAmount>(local, remote, default);
        (await remote.Set<ClassFeeAmount>().IgnoreQueryFilters().SingleAsync()).Id.Should().Be(cloud.Id);
    }

    [Fact]
    public async Task Balance_maps_tariff_and_existing_balance_identity_preserving_frozen_amount()
    {
        await using var local = CreateContext();
        await using var remote = CreateContext();
        var tariff = CreateTariff();
        var cloudTariff = CopyTariff(tariff);
        var balance = new StudentFeeBalance { StudentId = Guid.NewGuid(), ClassFeeAmountId = tariff.Id,
            AmountDue = 40, AmountPaid = 15 };
        var cloudBalance = new StudentFeeBalance { StudentId = balance.StudentId,
            ClassFeeAmountId = cloudTariff.Id, AmountDue = 40 };
        local.AddRange(tariff, balance);
        remote.AddRange(cloudTariff, cloudBalance);
        await local.SaveChangesAsync();
        await remote.SaveChangesAsync();
        remote.ChangeTracker.Clear();
        var item = Item("StudentFeeBalances", balance.Id);
        await CloudSyncEngine.ApplyTypedItemAsync<StudentFeeBalance>(local, remote, item, default);
        await remote.SaveChangesAsync();
        remote.ChangeTracker.Clear();
        var result = await remote.Set<StudentFeeBalance>().IgnoreQueryFilters().SingleAsync();
        result.Id.Should().Be(cloudBalance.Id);
        result.ClassFeeAmountId.Should().Be(cloudTariff.Id);
        result.AmountDue.Should().Be(40);
        result.AmountPaid.Should().Be(15);
        (await CloudSyncEngine.CountAppliedItemOnCloudAsync(local, remote, item, default)).Should().Be(1);
        balance.ClassFeeAmountId.Should().Be(tariff.Id);
        (await local.Set<StudentFeeBalance>().IgnoreQueryFilters().SingleAsync()).Id.Should().Be(balance.Id);
    }

    [Theory]
    [InlineData("SchoolId")]
    [InlineData("AcademicYearId")]
    [InlineData("PedagogicalClassId")]
    [InlineData("FeePricingCategoryId")]
    [InlineData("FeeTypeId")]
    [InlineData("FeeInstallmentId")]
    public async Task Matching_requires_every_key_component_including_school(string property)
    {
        await using var remote = CreateContext();
        var tariff = CreateTariff();
        var other = CopyTariff(tariff);
        typeof(ClassFeeAmount).GetProperty(property)!.SetValue(other, Guid.NewGuid());
        remote.Add(other);
        await remote.SaveChangesAsync();
        (await CloudSyncNaturalKey.ResolveTariffIdAsync(remote, tariff, default)).Should().Be(tariff.Id);
    }

    [Fact]
    public async Task Deleted_cloud_tariff_is_not_reused_and_local_tombstone_does_not_delete_replacement()
    {
        await using var local = CreateContext();
        await using var remote = CreateContext();
        var tariff = CreateTariff();
        var deletedCloud = CopyTariff(tariff);
        deletedCloud.IsDeleted = true;
        remote.Add(deletedCloud);
        await remote.SaveChangesAsync();
        (await CloudSyncNaturalKey.ResolveTariffIdAsync(remote, tariff, default)).Should().Be(tariff.Id);
        tariff.IsDeleted = true;
        var replacement = CopyTariff(tariff);
        replacement.IsDeleted = false;
        local.Add(tariff);
        remote.Add(replacement);
        await local.SaveChangesAsync();
        await remote.SaveChangesAsync();
        remote.ChangeTracker.Clear();
        var item = Item("ClassFeeAmounts", tariff.Id);
        item.Operation = SyncOperationType.Delete;
        await CloudSyncEngine.ApplyTypedItemAsync<ClassFeeAmount>(local, remote, item, default);
        await remote.SaveChangesAsync();
        (await remote.Set<ClassFeeAmount>().IgnoreQueryFilters().SingleAsync(x => x.Id == replacement.Id))
            .IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Recovery_requeues_only_known_failures_for_current_school_and_is_idempotent()
    {
        await using var local = CreateContext();
        var school = Guid.NewGuid();
        const string error = "[Structural] IX_ClassFeeAmounts_Year_Class_Category_FeeType_Installment";
        SyncOutboxUnit Unit(Guid schoolId, SyncOutboxStatus status, string message) => new()
        {
            SchoolId = schoolId, Status = status, AttemptCount = 8, LastError = message,
            Items = [new SyncOutboxItem { TableName = "ClassFeeAmounts", Status = status, LastError = message }]
        };
        var failed = Unit(school, SyncOutboxStatus.Failed, error);
        var dead = Unit(school, SyncOutboxStatus.DeadLetter, error);
        var otherSchool = Unit(Guid.NewGuid(), SyncOutboxStatus.Failed, error);
        var unrelated = Unit(school, SyncOutboxStatus.Failed, "Other error");
        var completed = Unit(school, SyncOutboxStatus.Completed, error);
        local.AddRange(failed, dead, otherSchool, unrelated, completed);
        await local.SaveChangesAsync();
        (await CloudSyncEngine.RequeueTariffFailuresForSchoolAsync(local, school, default)).Should().Be(2);
        (await CloudSyncEngine.RequeueTariffFailuresForSchoolAsync(local, school, default)).Should().Be(0);
        failed.Status.Should().Be(SyncOutboxStatus.Pending);
        dead.AttemptCount.Should().Be(0);
        failed.Items.Single().LastError.Should().BeNull();
        otherSchool.Status.Should().Be(SyncOutboxStatus.Failed);
        unrelated.Status.Should().Be(SyncOutboxStatus.Failed);
        completed.Status.Should().Be(SyncOutboxStatus.Completed);
    }
}
