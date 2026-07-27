using System.Linq.Expressions;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.StudentCards.DTOs;
using SchoolManagement.Application.StudentCards.Services;
using SchoolManagement.Domain.Entities.Settings;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Domain.Exceptions;
using Xunit;

namespace SchoolManagement.UnitTests.StudentCards;

public sealed class StudentCardServiceTests
{
    private readonly Guid _schoolId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _yearId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void ExtractQrToken_AcceptsPrefixedAndRaw()
    {
        Assert.Equal("ABC123", StudentCardService.ExtractQrToken("ERP_CARD:ABC123"));
        Assert.Equal("ABC123", StudentCardService.ExtractQrToken("ABC123"));
        Assert.Equal(string.Empty, StudentCardService.ExtractQrToken("  "));
    }

    [Fact]
    public async Task Create_GeneratesUniqueCardNumberAndQrWithoutPii()
    {
        var ctx = CreateContext();
        var card = await ctx.Service.CreateAsync(
            _schoolId,
            new CreateStudentCardRequest(_studentId, _yearId, _templateId),
            _userId);

        Assert.StartsWith("CSB-", card.CardNumber);
        Assert.Contains("-000001", card.CardNumber);
        Assert.False(string.IsNullOrWhiteSpace(card.QrToken));
        Assert.Equal($"ERP_CARD:{card.QrToken}", card.QrPayload);
        Assert.DoesNotContain("Jean", card.QrPayload, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StudentCardStatus.Active, card.Status);
        Assert.True(ctx.Histories.Items.Count >= 1);
    }

    [Fact]
    public async Task Create_Throws_WhenActiveCardAlreadyExists()
    {
        var ctx = CreateContext();
        await ctx.Service.CreateAsync(
            _schoolId,
            new CreateStudentCardRequest(_studentId, _yearId, _templateId),
            _userId);

        await Assert.ThrowsAsync<DomainException>(() =>
            ctx.Service.CreateAsync(
                _schoolId,
                new CreateStudentCardRequest(_studentId, _yearId, _templateId),
                _userId));
    }

    [Fact]
    public async Task DeclareLost_BlocksFurtherPrint()
    {
        var ctx = CreateContext();
        var created = await ctx.Service.CreateAsync(
            _schoolId,
            new CreateStudentCardRequest(_studentId, _yearId, _templateId),
            _userId);

        await ctx.Service.DeclareLostAsync(
            _schoolId,
            created.Id,
            new DeclareCardIncidentRequest("Perdue au bus"),
            _userId);

        await Assert.ThrowsAsync<DomainException>(() =>
            ctx.Service.ReprintAsync(
                _schoolId,
                created.Id,
                new ReprintStudentCardRequest("retry"),
                _userId));
    }

    [Fact]
    public async Task Renew_CanKeepOrRotateQr()
    {
        var ctx = CreateContext();
        var created = await ctx.Service.CreateAsync(
            _schoolId,
            new CreateStudentCardRequest(_studentId, _yearId, _templateId),
            _userId);

        var renewedKeep = await ctx.Service.RenewAsync(
            _schoolId,
            created.Id,
            new RenewStudentCardRequest(KeepQrToken: true),
            _userId);

        Assert.Equal(created.QrToken, renewedKeep.QrToken);
        Assert.Equal(2, renewedKeep.Version);
        Assert.Equal(created.Id, renewedKeep.ReplacesCardId);

        var old = await ctx.Cards.GetByIdAsync(created.Id);
        Assert.Equal(StudentCardStatus.Remplacee, old!.Status);

        var renewedNew = await ctx.Service.RenewAsync(
            _schoolId,
            renewedKeep.Id,
            new RenewStudentCardRequest(KeepQrToken: false),
            _userId);

        Assert.NotEqual(renewedKeep.QrToken, renewedNew.QrToken);
    }

    [Fact]
    public async Task ResolveByQr_MarksExpiredUnusable()
    {
        var ctx = CreateContext();
        var created = await ctx.Service.CreateAsync(
            _schoolId,
            new CreateStudentCardRequest(
                _studentId,
                _yearId,
                _templateId,
                ExpiresAt: DateTime.UtcNow.AddDays(-1)),
            _userId);

        var resolved = await ctx.Service.ResolveByQrAsync(
            _schoolId,
            new ResolveCardByQrRequest(created.QrPayload));

        Assert.NotNull(resolved);
        Assert.False(resolved!.IsUsable);
    }

    private TestContext CreateContext()
    {
        var students = new FakeRepository<Student>(
        [
            new Student
            {
                Id = _studentId,
                SchoolId = _schoolId,
                FirstName = "Jean",
                LastName = "Kabongo",
                RegistrationNumber = "MAT-001",
                Gender = Gender.Masculin,
                DateOfBirth = new DateOnly(2012, 1, 1)
            }
        ]);

        var years = new FakeRepository<AcademicYear>(
        [
            new AcademicYear
            {
                Id = _yearId,
                SchoolId = _schoolId,
                Label = "2025-2026",
                StartDate = new DateOnly(2025, 9, 1),
                EndDate = new DateOnly(2026, 7, 31),
                IsCurrent = true
            }
        ]);

        var templates = new FakeRepository<CardTemplate>(
        [
            new CardTemplate
            {
                Id = _templateId,
                SchoolId = _schoolId,
                Name = "Carte Élève",
                IsActive = true,
                WidthMm = 85.6m,
                HeightMm = 54m
            }
        ]);

        var settings = new FakeRepository<CardSchoolSettings>(
        [
            new CardSchoolSettings
            {
                Id = Guid.NewGuid(),
                SchoolId = _schoolId,
                CardNumberPrefix = "CSB",
                DefaultValidityMonths = 12,
                KeepQrOnRenewal = false,
                NextSequence = 1
            }
        ]);

        var cards = new FakeRepository<StudentCard>();
        var histories = new FakeRepository<StudentCardHistory>();
        var prints = new FakeRepository<StudentCardPrintLog>();

        var service = new StudentCardService(
            cards,
            templates,
            settings,
            histories,
            prints,
            students,
            years,
            new FakeRepository<Domain.Entities.Students.Enrollment>(),
            new FakeRepository<ClassRoom>(),
            new FakeRepository<StudyOption>(),
            new FakeRepository<PedagogicalClass>(),
            new FakeUnitOfWork());

        return new TestContext(service, cards, histories);
    }

    private sealed record TestContext(
        StudentCardService Service,
        FakeRepository<StudentCard> Cards,
        FakeRepository<StudentCardHistory> Histories);

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRepository<T> : IRepository<T> where T : class
    {
        public List<T> Items { get; }

        public FakeRepository(IEnumerable<T>? items = null) => Items = items?.ToList() ?? [];

        public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop is null) return Task.FromResult<T?>(null);
            var match = Items.FirstOrDefault(x => (Guid)prop.GetValue(x)! == id);
            return Task.FromResult(match);
        }

        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<T>>(Items.ToList());

        public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult<IReadOnlyList<T>>(Items.Where(compiled).ToList());
        }

        public Task<IReadOnlyList<T>> FindIncludingDeletedAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            FindAsync(predicate, cancellationToken);

        public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            Items.Remove(entity);
            return Task.CompletedTask;
        }
    }
}
