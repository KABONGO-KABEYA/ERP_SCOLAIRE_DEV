using FluentAssertions;
using SchoolManagement.Application.Schools;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Schools.Services;
using SchoolManagement.Domain.Entities.Settings;
using Xunit;

namespace SchoolManagement.UnitTests.Schools;

public sealed class SchoolSubscriptionGateTests
{
    private static readonly Guid SchoolId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Valid_subscription_allows_access()
    {
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var row = Subscription(now.AddMonths(-1), now.AddMonths(11), isActive: true);
        var dto = SchoolSubscriptionService.Map(row, SchoolId, now);

        dto.IsValid.Should().BeTrue();
        dto.Status.Should().Be(SchoolSubscriptionStatuses.Valid);

        SchoolSubscriptionGate.FromLookup(dto, isUnavailable: false)
            .Decision.Should().Be(SchoolSubscriptionGateDecision.Allow);
    }

    [Fact]
    public void Expired_subscription_blocks_as_expired()
    {
        var now = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
        var expiration = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var dto = SchoolSubscriptionService.Map(
            Subscription(expiration.AddMonths(-12), expiration, isActive: true),
            SchoolId,
            now);

        dto.IsExpired.Should().BeTrue();
        dto.IsValid.Should().BeFalse();
        dto.Status.Should().Be(SchoolSubscriptionStatuses.Expired);

        SchoolSubscriptionGate.FromLookup(dto, isUnavailable: false)
            .Decision.Should().Be(SchoolSubscriptionGateDecision.BlockExpired);
    }

    [Fact]
    public void ExpirationDate_equal_to_utcNow_is_expired()
    {
        var expiration = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var dto = SchoolSubscriptionService.Map(
            Subscription(expiration.AddMonths(-12), expiration, isActive: true),
            SchoolId,
            utcNow: expiration);

        dto.IsExpired.Should().BeTrue();
        dto.IsValid.Should().BeFalse();

        SchoolSubscriptionGate.FromLookup(dto, isUnavailable: false)
            .Decision.Should().Be(SchoolSubscriptionGateDecision.BlockExpired);
    }

    [Fact]
    public void Inactive_subscription_blocks_even_if_date_is_valid()
    {
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var dto = SchoolSubscriptionService.Map(
            Subscription(now.AddMonths(-1), now.AddMonths(11), isActive: false),
            SchoolId,
            now);

        dto.IsExpired.Should().BeFalse();
        dto.IsActive.Should().BeFalse();
        dto.IsValid.Should().BeFalse();

        SchoolSubscriptionGate.FromLookup(dto, isUnavailable: false)
            .Decision.Should().Be(SchoolSubscriptionGateDecision.BlockExpired);
    }

    [Fact]
    public void Missing_row_is_not_configured_and_blocks()
    {
        var dto = SchoolSubscriptionService.Map(null, SchoolId, DateTime.UtcNow);

        dto.IsConfigured.Should().BeFalse();
        dto.Status.Should().Be(SchoolSubscriptionStatuses.NotConfigured);

        SchoolSubscriptionGate.FromLookup(dto, isUnavailable: false)
            .Decision.Should().Be(SchoolSubscriptionGateDecision.BlockNotConfigured);
    }

    [Fact]
    public void Api_unavailable_is_not_treated_as_expired()
    {
        var result = SchoolSubscriptionGate.FromLookup(subscription: null, isUnavailable: true);

        result.Decision.Should().Be(SchoolSubscriptionGateDecision.BlockUnavailable);
        result.Decision.Should().NotBe(SchoolSubscriptionGateDecision.BlockExpired);
    }

    [Fact]
    public void Api_unavailable_wins_over_stale_dto()
    {
        var now = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var dto = SchoolSubscriptionService.Map(
            Subscription(now.AddMonths(-1), now.AddMonths(11), isActive: true),
            SchoolId,
            now);

        SchoolSubscriptionGate.FromLookup(dto, isUnavailable: true)
            .Decision.Should().Be(SchoolSubscriptionGateDecision.BlockUnavailable);
    }

    private static SchoolSubscription Subscription(
        DateTime installed,
        DateTime expiration,
        bool isActive) =>
        new()
        {
            Id = Guid.NewGuid(),
            SchoolId = SchoolId,
            InstallationDate = installed,
            DurationMonths = 12,
            ExpirationDate = expiration,
            IsActive = isActive,
        };
}
