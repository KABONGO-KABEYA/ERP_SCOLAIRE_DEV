using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using Xunit;
namespace SchoolManagement.Desktop.UnitTests;
public sealed class SubscriptionExpiryReminderTests
{
    [Theory]
    [InlineData(4, false)]
    [InlineData(3, true)]
    [InlineData(2, true)]
    [InlineData(1, true)]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    public void Warning_starts_three_calendar_days_before_expiry(int days, bool expected)
    {
        var now = new DateTime(2026,10,5,10,0,0,DateTimeKind.Utc);
        var subscription = new SchoolSubscriptionDto(true, Guid.NewGuid(), Guid.NewGuid(), now.AddMonths(-1), 1, now.Date.AddDays(days).AddHours(23), true, false, true, "Valid");
        Assert.Equal(expected, SubscriptionExpiryReminder.GetMessage(subscription, now, TimeZoneInfo.Utc) is not null);
        Assert.Null(SubscriptionExpiryReminder.GetMessage(subscription with { IsValid = false }, now, TimeZoneInfo.Utc));
    }
    [Fact]
    public void Reminder_is_suppressed_on_same_day_and_returns_next_day()
    {
        Assert.False(SubscriptionExpiryReminder.ShouldShow("2026-10-05", new DateTime(2026,10,5,18,0,0)));
        Assert.True(SubscriptionExpiryReminder.ShouldShow("2026-10-05", new DateTime(2026,10,6)));
        Assert.True(SubscriptionExpiryReminder.ShouldShow(null, new DateTime(2026,10,5)));
    }
    [Fact]
    public void No_warning_when_expiration_is_reached()
    {
        var now = DateTime.UtcNow;
        var subscription = new SchoolSubscriptionDto(true, Guid.NewGuid(), Guid.NewGuid(), null, 1, now, true, false, true, "Valid");
        Assert.Null(SubscriptionExpiryReminder.GetMessage(subscription, now));
    }
}
