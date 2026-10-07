using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Withholdings.DTOs;
using SchoolManagement.Application.Withholdings.Services;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.CloudSync;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.UnitTests.CloudSync;
using Xunit;

namespace SchoolManagement.UnitTests.Withholdings;

public sealed class WithholdingPartialPaymentTests
{
    internal static WithholdingService Service(SchoolDbContext db) => new(
        new Repository<WithholdingType>(db), new Repository<WithholdingConfiguration>(db),
        new Repository<WithholdingApplication>(db), new Repository<Payment>(db),
        new Repository<PaymentLine>(db), new Repository<RevenueAllocationEntry>(db),
        new Repository<StudentFeeBalance>(db), new Repository<ClassFeeAmount>(db),
        new Repository<Enrollment>(db), new Repository<ClassRoom>(db), new Repository<AcademicYear>(db),
        new Repository<FeeType>(db), new Repository<FeeInstallment>(db), new Repository<FeePricingCategory>(db),
        new WithholdingEngine(), new UnitOfWork(db));

    internal sealed record Fixture(Guid School, Guid Year, Guid Student, Guid Fee, Guid Installment,
        Guid Config, Guid Type)
    {
        public WithholdingResolveContext Context(Guid? payment = null, IReadOnlySet<Guid>? preserve = null) =>
            new(Year, Fee, Installment, null, Student, true, preserve, payment);
        public (Payment Payment, PaymentLine Line) Payment(decimal amount)
        {
            var payment = new Payment { SchoolId = School, AcademicYearId = Year, StudentId = Student,
                ReceiptNumber = "REC-" + Guid.NewGuid().ToString("N"), PaymentDate = DateTime.UtcNow,
                TotalAmount = amount, Status = PaymentStatus.Complet };
            return (payment, new PaymentLine { PaymentId = payment.Id, FeeTypeId = Fee,
                FeeInstallmentId = Installment, Amount = amount });
        }
    }

    internal static async Task<Fixture> SeedAsync(SchoolDbContext db,
        bool general = false, WithholdingCalculationMode mode = WithholdingCalculationMode.MontantFixe)
    {
        db.IgnoreSchoolScope = true;
        var school = new School { Name = "École test" };
        var year = new AcademicYear { SchoolId = school.Id, Label = "2026-2027",
            StartDate = new DateOnly(2026,9,1), EndDate = new DateOnly(2027,7,1) };
        var student = new Student { SchoolId = school.Id, FirstName = "Test", LastName = "Élève",
            RegistrationNumber = "TEST-001", DateOfBirth = new DateOnly(2010,1,1) };
        var fee = new FeeType { SchoolId = school.Id, Code = "FRAIS", Name = "Frais scolaires" };
        var installment = new FeeInstallment { SchoolId = school.Id, Name = "Acompte" };
        var type = new WithholdingType { SchoolId = school.Id, Code = "RET_TEST", Name = "Retenue test" };
        var config = new WithholdingConfiguration { SchoolId = school.Id, AcademicYearId = year.Id,
            WithholdingTypeId = type.Id, FeeTypeId = fee.Id, FeeInstallmentId = general ? null : installment.Id,
            Value = 5, CalculationMode = mode, IsActive = true };
        db.AddRange(school, year, student, fee, installment, type, config);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new(school.Id, year.Id, student.Id, fee.Id, installment.Id, config.Id, type.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saved_fixed_application_prevents_second_withholding_even_without_balance(bool general)
    {
        await using var db = CloudSyncTariffIdentityTests.CreateContext();
        var f = await SeedAsync(db, general);
        db.Add(new WithholdingApplication { SchoolId = f.School, StudentId = f.Student,
            AcademicYearId = f.Year, WithholdingConfigurationId = f.Config, Amount = 5 });
        await db.SaveChangesAsync();
        var result = await Service(db).CalculateForPaymentLineAsync(f.School, 80, f.Context());
        result.TotalWithheld.Should().Be(0);
        result.NetAmount.Should().Be(80);
        (await db.Set<WithholdingConfiguration>().SingleAsync()).IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_fixed_application_is_visible_to_next_line_before_SaveChanges(bool general)
    {
        await using var db = CloudSyncTariffIdentityTests.CreateContext();
        var f = await SeedAsync(db, general);
        var service = Service(db);
        var first = await service.CalculateForPaymentLineAsync(f.School, 20, f.Context());
        first.TotalWithheld.Should().Be(5);
        await service.RecordApplicationsAsync(f.School, f.Student, f.Year, Guid.NewGuid(), Guid.NewGuid(), first);
        var next = await service.CalculateForPaymentLineAsync(f.School, 80, f.Context());
        next.TotalWithheld.Should().Be(0);
        (first.NetAmount + next.NetAmount).Should().Be(95);
    }

    [Fact]
    public async Task Prior_completed_payment_prevents_fixed_charge_without_application_or_balance_history()
    {
        await using var db = CloudSyncTariffIdentityTests.CreateContext();
        var f = await SeedAsync(db);
        var (payment, line) = f.Payment(20);
        db.AddRange(payment, line);
        await db.SaveChangesAsync();
        (await Service(db).CalculateForPaymentLineAsync(f.School, 80, f.Context())).TotalWithheld.Should().Be(0);
    }

    [Fact]
    public async Task Cancelled_previous_payment_does_not_block_first_fixed_charge()
    {
        await using var db = CloudSyncTariffIdentityTests.CreateContext();
        var f = await SeedAsync(db);
        var (payment, line) = f.Payment(20);
        payment.Status = PaymentStatus.Annule;
        db.AddRange(payment, line);
        db.Add(new WithholdingApplication { SchoolId = f.School, StudentId = f.Student,
            AcademicYearId = f.Year, WithholdingConfigurationId = f.Config,
            PaymentId = payment.Id, PaymentLineId = line.Id, Amount = 5, IsDeleted = true });
        await db.SaveChangesAsync();
        (await Service(db).CalculateForPaymentLineAsync(f.School, 80, f.Context())).TotalWithheld.Should().Be(5);
    }

    [Fact]
    public async Task Current_payment_already_saved_is_not_counted_as_a_previous_installment_payment()
    {
        await using var db = CloudSyncTariffIdentityTests.CreateContext();
        var f = await SeedAsync(db);
        var (payment, line) = f.Payment(20);
        db.AddRange(payment, line);
        await db.SaveChangesAsync();
        (await Service(db).CalculateForPaymentLineAsync(f.School, 20, f.Context(payment.Id)))
            .TotalWithheld.Should().Be(5);
    }

    [Fact]
    public async Task Legacy_general_withholding_allocation_prevents_double_charge_without_application_table_history()
    {
        await using var db = CloudSyncTariffIdentityTests.CreateContext();
        var f = await SeedAsync(db, general: true);
        var (payment, line) = f.Payment(20);
        db.AddRange(payment, line);
        db.Add(new RevenueAllocationEntry { SchoolId = f.School, PaymentId = payment.Id,
            AcademicYearId = f.Year, WithholdingTypeId = f.Type, FeeTypeId = f.Fee, Amount = 5 });
        await db.SaveChangesAsync();
        (await Service(db).CalculateForPaymentLineAsync(f.School, 80, f.Context()))
            .TotalWithheld.Should().Be(0);
    }

    [Fact]
    public async Task Percentage_still_applies_to_each_partial_payment()
    {
        await using var db = CloudSyncTariffIdentityTests.CreateContext();
        var f = await SeedAsync(db, mode: WithholdingCalculationMode.Pourcentage);
        var service = Service(db);
        var first = await service.CalculateForPaymentLineAsync(f.School, 20, f.Context());
        var second = await service.CalculateForPaymentLineAsync(f.School, 80, f.Context());
        first.TotalWithheld.Should().Be(1);
        second.TotalWithheld.Should().Be(4);
        (first.NetAmount + second.NetAmount).Should().Be(95);
    }

    [Fact]
    public async Task Other_student_fixed_application_does_not_block_current_student()
    {
        await using var db = CloudSyncTariffIdentityTests.CreateContext();
        var f = await SeedAsync(db);
        db.Add(new WithholdingApplication { SchoolId = f.School, StudentId = Guid.NewGuid(),
            AcademicYearId = f.Year, WithholdingConfigurationId = f.Config, Amount = 5 });
        await db.SaveChangesAsync();
        (await Service(db).CalculateForPaymentLineAsync(f.School, 20, f.Context())).TotalWithheld.Should().Be(5);
    }

    [SyncSqlFact]
    public async Task Sql_fixed_withholding_survives_partial_complement_and_edit_with_unique_index_active()
    {
        await using var scratch = await CloudSyncTariffSqlTests.ScratchDatabase.CreateAsync();
        var options = new DbContextOptionsBuilder<SchoolDbContext>().UseSqlServer(scratch.ConnectionString).Options;
        Fixture f;
        await using (var seed = new SchoolDbContext(options) { SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true })
        {
            await CloudSyncDestinationSchema.EnsureEmptyCloudSchemaAsync(seed);
            f = await SeedAsync(seed);
        }
        await scratch.ExecuteAsync("""
            CREATE UNIQUE INDEX IX_FinRetenueApplication_Unique ON dbo.FinRetenueApplication
                (SchoolId,StudentId,AcademicYearId,WithholdingConfigurationId) WHERE IsDeleted=0;
            """);
        Guid firstPayment = default;
        decimal net = 0;
        foreach (var amount in new[] { 20m, 80m })
        {
            await using var db = new SchoolDbContext(options) { SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true };
            var service = Service(db);
            var (payment, line) = f.Payment(amount);
            if (firstPayment == default) firstPayment = payment.Id;
            db.AddRange(payment, line);
            var result = await service.CalculateForPaymentLineAsync(f.School, amount, f.Context(payment.Id));
            net += result.NetAmount;
            await service.RecordApplicationsAsync(f.School, f.Student, f.Year, payment.Id, line.Id, result);
            await db.SaveChangesAsync();
        }
        net.Should().Be(95);
        await using (var db = new SchoolDbContext(options) { SuppressCloudSyncEnqueue = true, IgnoreSchoolScope = true })
        {
            var service = Service(db);
            var preserve = await service.GetFixedApplicationConfigurationIdsByLineAsync(f.School, firstPayment);
            var line = await db.Set<PaymentLine>().SingleAsync(l => l.PaymentId == firstPayment);
            await service.RemoveApplicationsForPaymentAsync(f.School, firstPayment);
            var result = await service.CalculateForPaymentLineAsync(f.School, 20,
                f.Context(firstPayment, preserve[line.Id]));
            result.TotalWithheld.Should().Be(5);
            await service.RecordApplicationsAsync(f.School, f.Student, f.Year, firstPayment, line.Id, result);
            await db.SaveChangesAsync();
        }
        (await scratch.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Payments")).Should().Be(2);
        (await scratch.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.FinRetenueApplication WHERE IsDeleted=0")).Should().Be(1);
        (await scratch.ScalarAsync<decimal>("SELECT SUM(Amount) FROM dbo.FinRetenueApplication WHERE IsDeleted=0")).Should().Be(5);
        (await scratch.ScalarAsync<bool>("SELECT IsActive FROM dbo.FinRetenueConfiguration")).Should().BeTrue();
        (await scratch.ScalarAsync<int>("SELECT COUNT(*) FROM sys.indexes WHERE name='IX_FinRetenueApplication_Unique' AND is_unique=1 AND is_disabled=0"))
            .Should().Be(1);
    }
}
