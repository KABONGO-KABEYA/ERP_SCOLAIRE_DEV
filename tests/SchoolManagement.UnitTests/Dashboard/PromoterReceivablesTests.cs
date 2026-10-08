using System.Linq.Expressions;
using Moq;
using Xunit;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Dashboard.Services;
using SchoolManagement.Application.RevenueAllocation.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.UnitTests.Dashboard;

public class PromoterReceivablesTests
{
    private sealed class Fixture
    {
        public List<object> Data { get; } = [];
        public School School { get; } = new();
        public AcademicYear Year { get; }
        public FeeType Fee { get; }
        public FeeInstallment Past { get; } = new() { Name = "Échue" };
        public FeeInstallment Future { get; } = new() { Name = "Future" };
        public Student First { get; }
        public Student Second { get; }
        public ClassFeeAmount PastTariff { get; }
        public ClassFeeAmount FutureTariff { get; }
        public RevenueAllocationDestination Principal { get; }
        public Fixture()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            Year = new() { SchoolId = School.Id, IsCurrent = true, StartDate = today.AddMonths(-1), EndDate = today.AddMonths(10) };
            Fee = new() { SchoolId = School.Id, Name = "Scolarité", Currency = Currency.CDF };
            var room = new ClassRoom { SchoolId = School.Id, PedagogicalClassId = Guid.NewGuid() };
            var category = Guid.NewGuid();
            First = new() { SchoolId = School.Id, FirstName = "A" };
            Second = new() { SchoolId = School.Id, FirstName = "B" };
            PastTariff = new() { SchoolId = School.Id, AcademicYearId = Year.Id, FeeTypeId = Fee.Id, FeeInstallmentId = Past.Id,
                PedagogicalClassId = room.PedagogicalClassId.Value, FeePricingCategoryId = category, Amount = 100, DueDate = today.AddDays(-1) };
            FutureTariff = new() { SchoolId = School.Id, AcademicYearId = Year.Id, FeeTypeId = Fee.Id, FeeInstallmentId = Future.Id,
                PedagogicalClassId = room.PedagogicalClassId.Value, FeePricingCategoryId = category, Amount = 100, DueDate = today.AddDays(20) };
            Principal = new() { SchoolId = School.Id, Code = "PRN", Name = "Principal" };
            Data.AddRange([School, Year, Fee, room, First, Second, Past, Future, PastTariff, FutureTariff, Principal]);
            foreach (var student in new[] { First, Second })
                Data.Add(new Enrollment { StudentId = student.Id, AcademicYearId = Year.Id, ClassRoomId = room.Id,
                    FeePricingCategoryId = category, IsActive = true });
        }
        public IRepository<T> Repo<T>() where T : class
        {
            var repo = new Mock<IRepository<T>>();
            repo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<T, bool>> filter, CancellationToken _) =>
                    Task.FromResult<IReadOnlyList<T>>(Data.OfType<T>().Where(filter.Compile()).ToList()));
            return repo.Object;
        }
        public PromoterDashboardService Service() => new(
            Repo<School>(), Repo<AcademicYear>(), Repo<Payment>(), Repo<PaymentLine>(), Repo<Student>(), Repo<Enrollment>(),
            Repo<ClassRoom>(), Repo<FeeType>(), Repo<StudentFeeBalance>(), Repo<RevenueAllocationEntry>(),
            Repo<RevenueAllocationDestination>(), Repo<StudentAttendance>(), Repo<ExpensePayment>(), Repo<ClassFeeAmount>(),
            Repo<RevenueAllocationKey>(), Repo<RevenueAllocationKeyDetail>(), Repo<WithholdingType>(), Repo<FeeInstallment>(),
            Repo<SchoolLogo>(), Repo<Section>(), Repo<PedagogicalClass>(), Mock.Of<IRevenueAllocationService>(),
            Repo<WithholdingConfiguration>(), Repo<ExpensePaymentAllocation>(), Repo<ExpenseRequest>(), Repo<CurrencyDefinition>());
        public Payment Pay(Student student, decimal amount, FeeInstallment installment, PaymentStatus status = PaymentStatus.Complet)
        {
            var payment = new Payment { SchoolId = School.Id, AcademicYearId = Year.Id, StudentId = student.Id, Status = status };
            Data.Add(payment);
            Data.Add(new PaymentLine { PaymentId = payment.Id, FeeTypeId = Fee.Id, FeeInstallmentId = installment.Id, Amount = amount });
            return payment;
        }
        public void Key(Guid? fee, Guid? withholding, params (Guid Destination, decimal Percentage)[] shares)
        {
            var key = new RevenueAllocationKey { SchoolId = School.Id, AcademicYearId = Year.Id, FeeTypeId = fee, WithholdingTypeId = withholding };
            Data.Add(key);
            foreach (var (destination, percentage) in shares)
                Data.Add(new RevenueAllocationKeyDetail { AllocationKeyId = key.Id, DestinationId = destination, Value = percentage });
        }
    }

    [Fact]
    public async Task Cards_IncludeFullAnnualExpectedAndPaid_WhileDebtorsRemainOverdueOnly()
    {
        var f = new Fixture();
        f.Pay(f.First, 100, f.Past);
        f.Pay(f.Second, 40, f.Past);
        f.Pay(f.First, 25, f.Future);
        f.Data.Add(new StudentFeeBalance { StudentId = f.First.Id, ClassFeeAmountId = f.PastTariff.Id, AmountDue = 100, AmountPaid = 100 });
        f.Data.Add(new StudentFeeBalance { StudentId = f.Second.Id, ClassFeeAmountId = f.PastTariff.Id, AmountDue = 100, AmountPaid = 40 });
        var result = await f.Service().GetFeeReceivablesBreakdownAsync(f.School.Id, f.Fee.Id);
        Assert.Equal(400m, result.TotalExpected);
        Assert.Equal(165m, result.TotalPaid);
        Assert.Equal(235m, result.TotalRemaining);
        Assert.Equal(result.TotalPaid, result.ByInstallment.Sum(x => x.AmountPaid));
        Assert.Equal(60m, Assert.Single(result.Debtors).Remaining);
        Assert.Equal(165m, Assert.Single(result.ByDestination, x => x.DestinationCode == "NON_VENTILE").AmountCollected);
    }

    [Fact]
    public async Task Cards_StayAnnualWhenAllOverdueInstallmentsArePaid_AndIgnoreCancelledReceipts()
    {
        var f = new Fixture();
        f.Pay(f.First, 100, f.Past);
        f.Pay(f.Second, 100, f.Past);
        f.Pay(f.Second, 500, f.Future, PaymentStatus.Annule);
        foreach (var student in new[] { f.First, f.Second })
            f.Data.Add(new StudentFeeBalance { StudentId = student.Id, ClassFeeAmountId = f.PastTariff.Id, AmountDue = 100, AmountPaid = 100 });
        var result = await f.Service().GetFeeReceivablesBreakdownAsync(f.School.Id, f.Fee.Id);
        Assert.Equal(400m, result.TotalExpected);
        Assert.Equal(200m, result.TotalPaid);
        Assert.Equal(200m, result.TotalRemaining);
        Assert.Empty(result.Debtors);
    }

    [Fact]
    public async Task Destinations_IncludeNetAndWithholdingShares_AndHistoricalDestinations()
    {
        var f = new Fixture();
        var retained = new RevenueAllocationDestination { SchoolId = f.School.Id, Name = "Retenue" };
        var old = new RevenueAllocationDestination { SchoolId = f.School.Id, Name = "Ancien compte", IsActive = false };
        var type = new WithholdingType { SchoolId = f.School.Id, Name = "Fonds social" };
        f.Data.AddRange([retained, old, type, new WithholdingConfiguration { SchoolId = f.School.Id, AcademicYearId = f.Year.Id,
            FeeTypeId = f.Fee.Id, WithholdingTypeId = type.Id, CalculationMode = WithholdingCalculationMode.Pourcentage, Value = 10 }]);
        f.Key(f.Fee.Id, null, (f.Principal.Id, 100));
        f.Key(null, type.Id, (f.Principal.Id, 50), (retained.Id, 50));
        var payment = f.Pay(f.First, 400, f.Past);
        foreach (var (destination, amount, withholding) in new[] {
            (f.Principal.Id, 360m, (Guid?)null), (f.Principal.Id, 20m, (Guid?)type.Id),
            (retained.Id, 15m, (Guid?)type.Id), (old.Id, 5m, (Guid?)type.Id) })
            f.Data.Add(new RevenueAllocationEntry { SchoolId = f.School.Id, AcademicYearId = f.Year.Id,
                FeeTypeId = f.Fee.Id, PaymentId = payment.Id, DestinationId = destination, Amount = amount, WithholdingTypeId = withholding });
        var result = await f.Service().GetFeeReceivablesBreakdownAsync(f.School.Id, f.Fee.Id);
        Assert.Equal(400m, result.ByDestination.Sum(x => x.AmountExpected));
        Assert.Equal(400m, result.ByDestination.Sum(x => x.AmountCollected));
        var principal = Assert.Single(result.ByDestination, x => x.DestinationId == f.Principal.Id);
        Assert.Equal(380m, principal.AmountExpected);
        Assert.Equal(380m, principal.AmountCollected);
        Assert.Contains("Frais net", principal.AllocationSources);
        Assert.Contains("Fonds social", principal.AllocationSources);
        Assert.Contains(result.ByDestination, x => x.DestinationId == old.Id && x.AmountCollected == 5m);
    }

    [Fact]
    public async Task Expenses_RespectAccountYearAndFundingCurrency_AndDoNotCountPaidCommitmentsTwice()
    {
        var f = new Fixture();
        var currency = new CurrencyDefinition { Code = "CDF" };
        var other = new CurrencyDefinition { Code = "USD" };
        var request = new ExpenseRequest { SchoolId = f.School.Id, AcademicYearId = f.Year.Id,
            DestinationId = f.Principal.Id, RequestedAmount = 50, Status = ExpenseRequestStatus.Payee };
        var paid = new ExpensePayment { SchoolId = f.School.Id, AcademicYearId = f.Year.Id,
            DestinationId = f.Principal.Id, Amount = 0.3m, Currency = Currency.USD, PrimaryCurrencyId = other.Id, ExpenseRequestId = request.Id };
        var multi = new ExpensePayment { SchoolId = f.School.Id, AcademicYearId = f.Year.Id,
            DestinationId = f.Principal.Id, Amount = 100, Currency = Currency.USD };
        f.Data.AddRange([currency, other, request, paid, multi,
            new ExpensePaymentAllocation { SchoolId = f.School.Id, ExpensePaymentId = paid.Id, CurrencyId = currency.Id, Amount = 30, AppliedExchangeRate = 0.01m },
            new ExpensePaymentAllocation { SchoolId = f.School.Id, ExpensePaymentId = multi.Id, CurrencyId = currency.Id, Amount = 10 },
            new ExpensePaymentAllocation { SchoolId = f.School.Id, ExpensePaymentId = multi.Id, CurrencyId = other.Id, Amount = 90 },
            new ExpensePayment { SchoolId = f.School.Id, AcademicYearId = Guid.NewGuid(), DestinationId = f.Principal.Id, Amount = 1000 }]);
        var payment = f.Pay(f.First, 100, f.Past);
        f.Data.Add(new RevenueAllocationEntry { SchoolId = f.School.Id, AcademicYearId = f.Year.Id,
            FeeTypeId = f.Fee.Id, PaymentId = payment.Id, DestinationId = f.Principal.Id, Amount = 100 });
        // Another fee also funds this account: its receipts affect available funds, not this fee's receivable.
        var otherFee = new FeeType { SchoolId = f.School.Id, Currency = Currency.CDF };
        f.Data.Add(otherFee);
        f.Data.Add(new RevenueAllocationEntry { SchoolId = f.School.Id, AcademicYearId = f.Year.Id,
            FeeTypeId = otherFee.Id, PaymentId = payment.Id, DestinationId = f.Principal.Id, Amount = 50 });
        var row = Assert.Single((await f.Service().GetFeeReceivablesBreakdownAsync(f.School.Id, f.Fee.Id)).ByDestination);
        Assert.Equal(40m, row.AmountSpent);
        Assert.Equal(20m, row.AmountCommitted);
        Assert.Equal(110m, row.Available);
        Assert.Equal(90m, row.AvailableAfterCommitments);
        Assert.Equal(300m, row.Remaining);
    }

    [Fact]
    public void Projection_GeneralFixedRetentionAppliesOncePerStudent_WithSpecificCategoryPriority()
    {
        var student = Guid.NewGuid(); var installment = Guid.NewGuid(); var type = Guid.NewGuid(); var category = Guid.NewGuid();
        var general = new WithholdingConfiguration { WithholdingTypeId = type, CalculationMode = WithholdingCalculationMode.MontantFixe, Value = 10 };
        var obligations = new[] { new AnnualFeeObligation(student, installment, category, 100),
            new AnnualFeeObligation(student, Guid.NewGuid(), category, 100),
            new AnnualFeeObligation(Guid.NewGuid(), installment, category, 100) };
        var projection = AnnualReceivableAllocation.Project(obligations, [general]);
        Assert.Equal(280m, projection.NetAmount);
        Assert.Equal(20m, projection.Withholdings[type]);
        var specific = new WithholdingConfiguration { WithholdingTypeId = type, PricingCategoryId = category,
            CalculationMode = WithholdingCalculationMode.Pourcentage, Value = 20 };
        projection = AnnualReceivableAllocation.Project(obligations, [general, specific]);
        Assert.Equal(240m, projection.NetAmount);
        Assert.Equal(60m, projection.Withholdings[type]);
    }
}
