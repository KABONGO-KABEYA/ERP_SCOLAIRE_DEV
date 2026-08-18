using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.DocumentBranding.Interfaces;
using SchoolManagement.Application.Finance.Interfaces;
using SchoolManagement.Application.Reports.Services;
using SchoolManagement.Application.RevenueAllocation.Interfaces;
using SchoolManagement.Domain.Entities.Academic;
using SchoolManagement.Domain.Entities.Finance;
using SchoolManagement.Domain.Entities.Grades;
using SchoolManagement.Domain.Entities.Hr;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Infrastructure.Persistence;
using Xunit;

namespace SchoolManagement.UnitTests.Reports;

public sealed class ReportServiceTests
{
    [Fact]
    public async Task GetClassAveragesAsync_filters_by_school_via_classroom_without_periodresult_schoolid_mapping()
    {
        await using var db = CreateContext();
        db.IgnoreSchoolScope = true;

        var schoolA = new School { Id = Guid.NewGuid(), Name = "A", IsActive = true, CreatedAt = DateTime.UtcNow };
        var schoolB = new School { Id = Guid.NewGuid(), Name = "B", IsActive = true, CreatedAt = DateTime.UtcNow };
        var yearA = new AcademicYear { Id = Guid.NewGuid(), SchoolId = schoolA.Id, Label = "2026", CreatedAt = DateTime.UtcNow };
        var yearB = new AcademicYear { Id = Guid.NewGuid(), SchoolId = schoolB.Id, Label = "2026", CreatedAt = DateTime.UtcNow };
        var pedA = new PedagogicalClass
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolA.Id,
            TemplateCode = "A1",
            DisplayName = "Classe A",
            LevelOrder = 1,
            Program = 0,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        var pedB = new PedagogicalClass
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolB.Id,
            TemplateCode = "B1",
            DisplayName = "Classe B",
            LevelOrder = 1,
            Program = 0,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        var sectionA = new Section { Id = Guid.NewGuid(), SchoolId = schoolA.Id, Code = "A", Name = "Section A", CreatedAt = DateTime.UtcNow };
        var sectionB = new Section { Id = Guid.NewGuid(), SchoolId = schoolB.Id, Code = "B", Name = "Section B", CreatedAt = DateTime.UtcNow };
        var classA = new ClassRoom
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolA.Id,
            AcademicYearId = yearA.Id,
            PedagogicalClassId = pedA.Id,
            SectionId = sectionA.Id,
            Code = "A-001",
            Name = "Classe A",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var classB = new ClassRoom
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolB.Id,
            AcademicYearId = yearB.Id,
            PedagogicalClassId = pedB.Id,
            SectionId = sectionB.Id,
            Code = "B-001",
            Name = "Classe B",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var periodA = new AcademicPeriod
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolA.Id,
            AcademicYearId = yearA.Id,
            Name = "P1",
            OrderIndex = 1,
            PeriodType = AcademicPeriodType.Trimestre,
            Kind = AcademicSubPeriodKind.Travail,
            Status = AcademicSubPeriodStatus.Ouverte,
            CreatedAt = DateTime.UtcNow
        };
        var periodB = new AcademicPeriod
        {
            Id = Guid.NewGuid(),
            SchoolId = schoolB.Id,
            AcademicYearId = yearB.Id,
            Name = "P1",
            OrderIndex = 1,
            PeriodType = AcademicPeriodType.Trimestre,
            Kind = AcademicSubPeriodKind.Travail,
            Status = AcademicSubPeriodStatus.Ouverte,
            CreatedAt = DateTime.UtcNow
        };
        var studentA = new Student { Id = Guid.NewGuid(), SchoolId = schoolA.Id, FirstName = "Alice", LastName = "A", CreatedAt = DateTime.UtcNow };
        var studentB = new Student { Id = Guid.NewGuid(), SchoolId = schoolB.Id, FirstName = "Bob", LastName = "B", CreatedAt = DateTime.UtcNow };

        db.AddRange(schoolA, schoolB, yearA, yearB, pedA, pedB, sectionA, sectionB, classA, classB, periodA, periodB, studentA, studentB);
        db.PeriodResults.AddRange(
            new PeriodResult
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolA.Id,
                StudentId = studentA.Id,
                AcademicYearId = yearA.Id,
                AcademicPeriodId = periodA.Id,
                ClassRoomId = classA.Id,
                Average = 14,
                Percentage = 70,
                Rank = 1,
                ClassSize = 1,
                CreatedAt = DateTime.UtcNow
            },
            new PeriodResult
            {
                Id = Guid.NewGuid(),
                SchoolId = schoolB.Id,
                StudentId = studentB.Id,
                AcademicYearId = yearB.Id,
                AcademicPeriodId = periodB.Id,
                ClassRoomId = classB.Id,
                Average = 8,
                Percentage = 40,
                Rank = 1,
                ClassSize = 1,
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.GetClassAveragesAsync(schoolA.Id, cancellationToken: CancellationToken.None);

        result.Should().ContainSingle();
        result[0].ClassRoomId.Should().Be(classA.Id);
        result[0].ClassName.Should().Be(classA.Name);
        result[0].ClassAverage.Should().Be(14);
    }

    private static ReportService CreateService(SchoolDbContext db) => new(
        new Repository<Student>(db),
        new Repository<Enrollment>(db),
        new Repository<ClassRoom>(db),
        new Repository<PedagogicalClass>(db),
        new Repository<Section>(db),
        new Repository<Teacher>(db),
        new Repository<Payment>(db),
        new Repository<PaymentLine>(db),
        new Repository<FeeType>(db),
        new Repository<FeeInstallment>(db),
        new Repository<PeriodResult>(db),
        new Repository<AcademicPeriod>(db),
        new Repository<AcademicYear>(db),
        new Repository<StudentFeeBalance>(db),
        new Repository<ClassFeeAmount>(db),
        Substitute.For<IFinanceOperationService>(),
        Substitute.For<IRevenueAllocationService>(),
        new Repository<School>(db),
        Substitute.For<IDocumentPrintBrandingResolver>(),
        Substitute.For<IDocumentBrandingStorageService>());

    private static SchoolDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SchoolDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SchoolDbContext(options) { SuppressCloudSyncEnqueue = true };
    }
}
