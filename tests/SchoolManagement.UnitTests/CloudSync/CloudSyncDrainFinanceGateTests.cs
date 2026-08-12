using FluentAssertions;
using SchoolManagement.Domain.Entities.Sync;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using Xunit;

namespace SchoolManagement.UnitTests.CloudSync;

/// <summary>
/// Garde EnsureFinance : 0 ou 1 appel par drain, uniquement pour unités financières.
/// </summary>
public sealed class CloudSyncDrainFinanceGateTests
{
    [Theory]
    [InlineData("Entity", "UserAccounts", false)]
    [InlineData("Payment", "Payments", true)]
    [InlineData("FinanceBatch", "StudentFeeBalances", true)]
    [InlineData("Entity", "Payments", true)] // CriticalTable même si AggregateType Entity
    [InlineData("Entity", "FinDevise", false)]
    [InlineData("Entity", "FinRetenue", false)]
    [InlineData("Entity", "FinCleRepartition", false)]
    [InlineData("Entity", "FinDestinationRepartition", false)]
    [InlineData("Entity", "FeeTypes", false)]
    public void IsFinancialSyncUnit_classifies_as_expected(
        string aggregateType,
        string tableName,
        bool expected)
    {
        var unit = CreateUnit(aggregateType, tableName);
        CloudSyncEngine.IsFinancialSyncUnit(unit).Should().Be(expected);
    }

    [Fact]
    public async Task UserAccount_alone_does_not_call_EnsureFinance()
    {
        var ensureCalls = 0;
        var processCalls = 0;
        var user = CreateUnit("Entity", "UserAccounts", attemptCount: 0);

        var result = await CloudSyncEngine.ExecuteFinanceGatedUnitLoopAsync(
            [user],
            async _ =>
            {
                ensureCalls++;
                await Task.CompletedTask;
            },
            async (unit, _) =>
            {
                processCalls++;
                unit.AttemptCount++; // simule ProcessUnit
                unit.Status = SyncOutboxStatus.Completed;
                return OkResult();
            },
            CancellationToken.None);

        ensureCalls.Should().Be(0);
        processCalls.Should().Be(1);
        result.EnsureFinanceCallCount.Should().Be(0);
        result.UnitsSucceeded.Should().Be(1);
        result.FinancePrepFailed.Should().BeFalse();
        user.Status.Should().Be(SyncOutboxStatus.Completed);
    }

    [Fact]
    public async Task Payment_alone_calls_EnsureFinance_once_before_ProcessUnit()
    {
        var sequence = new List<string>();
        var payment = CreateUnit("Payment", "Payments");

        var result = await CloudSyncEngine.ExecuteFinanceGatedUnitLoopAsync(
            [payment],
            async _ =>
            {
                sequence.Add("ensure");
                await Task.CompletedTask;
            },
            async (unit, _) =>
            {
                sequence.Add("process:" + unit.AggregateType);
                unit.AttemptCount++;
                unit.Status = SyncOutboxStatus.Completed;
                return OkResult();
            },
            CancellationToken.None);

        result.EnsureFinanceCallCount.Should().Be(1);
        sequence.Should().Equal("ensure", "process:Payment");
        payment.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task Multiple_Payments_call_EnsureFinance_once()
    {
        var ensureCalls = 0;
        var p1 = CreateUnit("Payment", "Payments");
        var p2 = CreateUnit("Payment", "PaymentLines");

        var result = await CloudSyncEngine.ExecuteFinanceGatedUnitLoopAsync(
            [p1, p2],
            async _ =>
            {
                ensureCalls++;
                await Task.CompletedTask;
            },
            async (unit, _) =>
            {
                unit.AttemptCount++;
                unit.Status = SyncOutboxStatus.Completed;
                return OkResult();
            },
            CancellationToken.None);

        ensureCalls.Should().Be(1);
        result.EnsureFinanceCallCount.Should().Be(1);
        result.UnitsSucceeded.Should().Be(2);
        p1.AttemptCount.Should().Be(1);
        p2.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task User_then_Payment_processes_User_without_finance_then_Payment_with_finance()
    {
        var sequence = new List<string>();
        var user = CreateUnit("Entity", "UserAccounts");
        var payment = CreateUnit("Payment", "Payments");

        var result = await CloudSyncEngine.ExecuteFinanceGatedUnitLoopAsync(
            [user, payment],
            async _ =>
            {
                sequence.Add("ensure");
                await Task.CompletedTask;
            },
            async (unit, _) =>
            {
                sequence.Add("process:" + unit.Items.First().TableName);
                unit.AttemptCount++;
                unit.Status = SyncOutboxStatus.Completed;
                return OkResult();
            },
            CancellationToken.None);

        result.EnsureFinanceCallCount.Should().Be(1);
        sequence.Should().Equal("process:UserAccounts", "ensure", "process:Payments");
    }

    [Fact]
    public async Task User_then_Payment_with_EnsureFinance_failure_completes_User_keeps_Payment_Pending()
    {
        var user = CreateUnit("Entity", "UserAccounts", attemptCount: 0);
        var payment = CreateUnit("Payment", "Payments", attemptCount: 0);
        payment.Status = SyncOutboxStatus.Pending;

        var result = await CloudSyncEngine.ExecuteFinanceGatedUnitLoopAsync(
            [user, payment],
            _ => throw new InvalidOperationException(
                "Cannot insert duplicate key ... IX_FinDevise_Code ... (EUR)"),
            async (unit, _) =>
            {
                unit.AttemptCount++;
                unit.Status = SyncOutboxStatus.Completed;
                return OkResult();
            },
            CancellationToken.None);

        result.EnsureFinanceCallCount.Should().Be(1);
        result.FinancePrepFailed.Should().BeTrue();
        result.UnitsSucceeded.Should().Be(1);
        result.UnitsFailed.Should().Be(0);
        user.Status.Should().Be(SyncOutboxStatus.Completed);
        user.AttemptCount.Should().Be(1);
        payment.Status.Should().Be(SyncOutboxStatus.Pending);
        payment.AttemptCount.Should().Be(0);
        result.FinancePrepError.Should().Contain("IX_FinDevise_Code");
    }

    [Fact]
    public async Task Lot_without_financial_never_calls_EnsureFinance()
    {
        var ensureCalls = 0;
        var units = new[]
        {
            CreateUnit("Entity", "UserAccounts"),
            CreateUnit("Entity", "Students"),
            CreateUnit("Entity", "Teachers"),
            CreateUnit("Entity", "FinDevise"),
            CreateUnit("Entity", "FeeTypes")
        };

        var result = await CloudSyncEngine.ExecuteFinanceGatedUnitLoopAsync(
            units,
            async _ =>
            {
                ensureCalls++;
                await Task.CompletedTask;
            },
            async (unit, _) =>
            {
                unit.Status = SyncOutboxStatus.Completed;
                return OkResult();
            },
            CancellationToken.None);

        ensureCalls.Should().Be(0);
        result.EnsureFinanceCallCount.Should().Be(0);
        result.UnitsSucceeded.Should().Be(5);
    }

    [Fact]
    public async Task Payment_multi_items_still_invokes_ProcessUnit_once_per_unit_after_EnsureFinance()
    {
        // Simulate multi-item Payment unit: ProcessUnit remains the atomic boundary (unchanged).
        var payment = CreateUnit("Payment", "Payments");
        payment.Items.Add(new SyncOutboxItem
        {
            Id = Guid.NewGuid(),
            TableName = "PaymentLines",
            EntityId = Guid.NewGuid(),
            Operation = SyncOperationType.Insert,
            Status = SyncOutboxStatus.Pending,
            Sequence = 1,
            CreatedAt = DateTime.UtcNow
        });
        payment.Items.Add(new SyncOutboxItem
        {
            Id = Guid.NewGuid(),
            TableName = "FinRepartitionRecette",
            EntityId = Guid.NewGuid(),
            Operation = SyncOperationType.Insert,
            Status = SyncOutboxStatus.Pending,
            Sequence = 2,
            CreatedAt = DateTime.UtcNow
        });
        payment.ExpectedItemCount = 3;

        var processCount = 0;
        var ensureCalls = 0;
        var verifyWouldRun = false;

        var result = await CloudSyncEngine.ExecuteFinanceGatedUnitLoopAsync(
            [payment],
            async _ =>
            {
                ensureCalls++;
                await Task.CompletedTask;
            },
            async (unit, _) =>
            {
                processCount++;
                // Mimic ProcessUnit contract: one call per unit (TX + optional verify inside).
                unit.AttemptCount++;
                verifyWouldRun = true;
                unit.Status = SyncOutboxStatus.Completed;
                return new CloudSyncEngine.FinanceGatedUnitProcessResult(
                    true,
                    false,
                    unit.ExpectedItemCount,
                    0,
                    null,
                    unit.Items.Select(i => i.TableName).ToList());
            },
            CancellationToken.None);

        ensureCalls.Should().Be(1);
        processCount.Should().Be(1);
        verifyWouldRun.Should().BeTrue();
        result.UnitsSucceeded.Should().Be(1);
        result.RecordsSucceeded.Should().Be(3);
        payment.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task CriticalOnly_style_Payment_calls_EnsureFinance_once()
    {
        // criticalOnly filtre côté DrainAsync ; ici on simule un lot déjà filtré = Payments.
        var payment = CreateUnit("Payment", "Payments");
        payment.Priority = SyncPriority.Critical;

        var result = await CloudSyncEngine.ExecuteFinanceGatedUnitLoopAsync(
            [payment],
            async _ => await Task.CompletedTask,
            async (unit, _) =>
            {
                unit.AttemptCount++;
                unit.Status = SyncOutboxStatus.Completed;
                return OkResult();
            },
            CancellationToken.None);

        result.EnsureFinanceCallCount.Should().Be(1);
        CloudSyncEngine.IsFinancialSyncUnit(payment).Should().BeTrue();
    }

    [Fact]
    public async Task After_EnsureFinance_failure_later_non_financial_units_still_process()
    {
        var user1 = CreateUnit("Entity", "UserAccounts");
        var payment = CreateUnit("Payment", "Payments", attemptCount: 0);
        var user2 = CreateUnit("Entity", "Students");

        var result = await CloudSyncEngine.ExecuteFinanceGatedUnitLoopAsync(
            [user1, payment, user2],
            _ => throw new InvalidOperationException("FinDevise EUR conflict"),
            async (unit, _) =>
            {
                unit.AttemptCount++;
                unit.Status = SyncOutboxStatus.Completed;
                return OkResult();
            },
            CancellationToken.None);

        result.UnitsSucceeded.Should().Be(2);
        result.UnitsFailed.Should().Be(0);
        user1.Status.Should().Be(SyncOutboxStatus.Completed);
        user2.Status.Should().Be(SyncOutboxStatus.Completed);
        payment.Status.Should().Be(SyncOutboxStatus.Pending);
        payment.AttemptCount.Should().Be(0);
        result.FinancePrepFailed.Should().BeTrue();
    }

    private static CloudSyncEngine.FinanceGatedUnitProcessResult OkResult() =>
        new(true, false, 1, 0, null, ["ok"]);

    private static SyncOutboxUnit CreateUnit(
        string aggregateType,
        string tableName,
        int attemptCount = 0)
    {
        var entityId = Guid.NewGuid();
        return new SyncOutboxUnit
        {
            Id = Guid.NewGuid(),
            SchoolId = Guid.NewGuid(),
            AggregateType = aggregateType,
            AggregateId = entityId,
            Priority = SyncPriority.Normal,
            Status = SyncOutboxStatus.Pending,
            AttemptCount = attemptCount,
            ExpectedItemCount = 1,
            CreatedAt = DateTime.UtcNow,
            Items =
            [
                new SyncOutboxItem
                {
                    Id = Guid.NewGuid(),
                    TableName = tableName,
                    EntityId = entityId,
                    Operation = SyncOperationType.Insert,
                    Status = SyncOutboxStatus.Pending,
                    Sequence = 0,
                    CreatedAt = DateTime.UtcNow
                }
            ]
        };
    }
}
