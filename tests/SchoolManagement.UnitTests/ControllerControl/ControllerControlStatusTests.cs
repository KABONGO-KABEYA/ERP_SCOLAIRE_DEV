using SchoolManagement.Application.ControllerControl.DTOs;
using SchoolManagement.Application.ControllerControl.Services;
using Xunit;

namespace SchoolManagement.UnitTests.ControllerControl;

public sealed class ControllerControlStatusTests
{
    private static readonly DateOnly Today = new(2026, 10, 6);

    [Theory]
    [InlineData(100, 100, 0, ControllerPaymentStatus.UpToDate)]
    [InlineData(100, 120, -20, ControllerPaymentStatus.Credit)]
    [InlineData(100, 40, 60, ControllerPaymentStatus.Partial)]
    [InlineData(100, 0, 100, ControllerPaymentStatus.Unpaid)]
    [InlineData(0, 0, 0, ControllerPaymentStatus.UpToDate)]
    public void ResolveStatus_MapsAmounts(
        decimal expected,
        decimal paid,
        decimal balance,
        ControllerPaymentStatus status)
    {
        var actual = ControllerControlService.ResolveStatus(
            expected,
            paid,
            balance,
            dueDate: null,
            today: Today);

        Assert.Equal(status, actual);
    }

    [Fact]
    public void ResolveStatus_UsesDueDateForOverdueBalance()
    {
        var actual = ControllerControlService.ResolveStatus(
            expected: 100,
            paid: 40,
            balance: 60,
            dueDate: Today.AddDays(-1),
            today: Today);

        Assert.Equal(ControllerPaymentStatus.Overdue, actual);
    }
}
