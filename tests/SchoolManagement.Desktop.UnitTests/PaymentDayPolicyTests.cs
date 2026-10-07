using SchoolManagement.Application.Payments.Services;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using Xunit;
namespace SchoolManagement.Desktop.UnitTests;

public sealed class PaymentDayPolicyTests
{
    [Theory]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(21, true)]
    public void Creation_compares_calendar_days(int day, bool allowed)
    {
        var error = Record.Exception(() => PaymentMutationPolicy.EnsureCreationDateAllowed(
            new DateTime(2026, 10, day), new DateTime(2026, 10, 20, 23, 59, 0)));
        if (allowed) Assert.Null(error); else Assert.IsType<DomainException>(error);
    }

    [Fact]
    public void First_payment_has_no_minimum_date() =>
        PaymentMutationPolicy.EnsureCreationDateAllowed(new DateTime(2026, 10, 5), null);

    [Theory]
    [InlineData(5, PaymentStatus.Complet, true)]
    [InlineData(6, PaymentStatus.Complet, false)]
    [InlineData(6, PaymentStatus.Annule, true)]
    [InlineData(6, PaymentStatus.EnAttente, true)]
    public void Mutations_allow_same_day_and_ignore_non_completed_payments(int otherDay, PaymentStatus status, bool allowed)
    {
        var target = new Payment { Id = Guid.NewGuid(), StudentId = Guid.NewGuid(), PaymentDate = new DateTime(2026, 10, 5), Status = PaymentStatus.Complet };
        var other = new Payment { Id = Guid.NewGuid(), StudentId = Guid.NewGuid(), PaymentDate = new DateTime(2026, 10, otherDay, 23, 59, 0), Status = status };
        var error = Record.Exception(() => PaymentMutationPolicy.EnsureIsLatestCompletedPayment(target, new[] { target, other }));
        if (allowed) Assert.Null(error); else Assert.IsType<DomainException>(error);
        Assert.Equal(allowed, PaymentMutationPolicy.IsLatestPaymentDay(target.PaymentDate,
            status == PaymentStatus.Complet ? other.PaymentDate : target.PaymentDate));
    }

    [Fact]
    public void Later_installment_protection_is_unchanged()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        Assert.Throws<DomainException>(() => PaymentMutationPolicy.EnsureNoLaterInstallmentsPaid(
            new[] { new PaymentLine { FeeInstallmentId = first } },
            new[] { (first, 1), (second, 2) }, new Dictionary<Guid, decimal> { [second] = 10 }));
    }
}
