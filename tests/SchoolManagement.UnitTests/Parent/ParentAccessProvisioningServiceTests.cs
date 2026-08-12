using FluentAssertions;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Parent.Services;
using SchoolManagement.Domain.Entities.Security;
using SchoolManagement.Domain.Entities.Students;
using SchoolManagement.Shared.Constants;
using System.Linq.Expressions;
using Xunit;

namespace SchoolManagement.UnitTests.Parent;

public sealed class ParentAccessProvisioningServiceTests
{
    private static readonly Guid SchoolId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ParentRoleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task EnsureAccess_WhenParentHasNoRole_CreatesActiveAssignment()
    {
        var guardian = CreateGuardian("MUSENGA", "Jean");
        var user = CreateUser(guardian.Id, "parent.musenga");
        var roleRepo = new FakeRepository<Role>([CreateParentRole()]);
        var userRepo = new FakeRepository<UserAccount>([user]);
        var assignmentRepo = new TrackingRoleAssignmentRepository();
        var service = CreateService(userRepo, roleRepo, assignmentRepo);

        await service.EnsureAccessForGuardiansAsync(SchoolId, [guardian]);

        assignmentRepo.Items.Should().ContainSingle();
        var created = assignmentRepo.Items.Single();
        created.UserId.Should().Be(user.Id);
        created.RoleId.Should().Be(ParentRoleId);
        created.IsDeleted.Should().BeFalse();
        assignmentRepo.AddCallCount.Should().Be(1);
        assignmentRepo.UpdateCallCount.Should().Be(0);
    }

    [Fact]
    public async Task EnsureAccess_WhenParentHasActiveRole_IsIdempotent()
    {
        var guardian = CreateGuardian("MUSENGA", "Jean");
        var user = CreateUser(guardian.Id, "parent.musenga");
        var existing = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = ParentRoleId,
            IsDeleted = false
        };
        var assignmentRepo = new TrackingRoleAssignmentRepository([existing]);
        var service = CreateService(
            new FakeRepository<UserAccount>([user]),
            new FakeRepository<Role>([CreateParentRole()]),
            assignmentRepo);

        await service.EnsureAccessForGuardiansAsync(SchoolId, [guardian]);
        await service.EnsureAccessForGuardiansAsync(SchoolId, [guardian]);

        assignmentRepo.Items.Should().ContainSingle(a => !a.IsDeleted);
        assignmentRepo.Items.Count(a => a.UserId == user.Id && a.RoleId == ParentRoleId).Should().Be(1);
        assignmentRepo.AddCallCount.Should().Be(0);
    }

    [Fact]
    public async Task EnsureAccess_WhenParentRoleSoftDeleted_ReactivatesSameRow()
    {
        var guardian = CreateGuardian("MUSENGA", "Jean");
        var user = CreateUser(guardian.Id, "parent.musenga");
        var softDeletedId = Guid.Parse("ff60a3b7-aaaa-bbbb-cccc-dddddddddddd");
        var softDeleted = new UserRoleAssignment
        {
            Id = softDeletedId,
            UserId = user.Id,
            RoleId = ParentRoleId,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-10),
            DeletedBy = Guid.NewGuid()
        };
        var assignmentRepo = new TrackingRoleAssignmentRepository([softDeleted]);
        var service = CreateService(
            new FakeRepository<UserAccount>([user]),
            new FakeRepository<Role>([CreateParentRole()]),
            assignmentRepo);

        await service.EnsureAccessForGuardiansAsync(SchoolId, [guardian]);

        assignmentRepo.Items.Should().ContainSingle();
        var row = assignmentRepo.Items.Single();
        row.Id.Should().Be(softDeletedId);
        row.IsDeleted.Should().BeFalse();
        row.DeletedAt.Should().BeNull();
        row.DeletedBy.Should().BeNull();
        row.UpdatedAt.Should().NotBeNull();
        assignmentRepo.AddCallCount.Should().Be(0);
        assignmentRepo.UpdateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task EnsureAccess_ConcurrentCalls_CannotLeaveTwoActiveParentRoles()
    {
        var guardian = CreateGuardian("MUSENGA", "Jean");
        var user = CreateUser(guardian.Id, "parent.musenga");
        var softDeleted = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = ParentRoleId,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-1)
        };
        var assignmentRepo = new TrackingRoleAssignmentRepository([softDeleted]);
        var service = CreateService(
            new FakeRepository<UserAccount>([user]),
            new FakeRepository<Role>([CreateParentRole()]),
            assignmentRepo);

        await Task.WhenAll(
            service.EnsureAccessForGuardiansAsync(SchoolId, [guardian]),
            service.EnsureAccessForGuardiansAsync(SchoolId, [guardian]));

        assignmentRepo.Items
            .Count(a => a.UserId == user.Id && a.RoleId == ParentRoleId && !a.IsDeleted)
            .Should().Be(1);
        assignmentRepo.Items
            .Count(a => a.UserId == user.Id && a.RoleId == ParentRoleId)
            .Should().Be(1);
        assignmentRepo.AddCallCount.Should().Be(0);
    }

    [Fact]
    public async Task EnsureAccess_MultipleChildrenSameParent_DoesNotDuplicateParentRole()
    {
        // Contrainte métier : un parent peut avoir plusieurs enfants.
        // UserRoleAssignment PARENT reste unique au niveau du compte.
        var guardian = CreateGuardian("MUSENGA", "Jean");
        var user = CreateUser(guardian.Id, "parent.musenga");
        var assignmentRepo = new TrackingRoleAssignmentRepository();
        var service = CreateService(
            new FakeRepository<UserAccount>([user]),
            new FakeRepository<Role>([CreateParentRole()]),
            assignmentRepo);

        // Simulation : 3 inscriptions (TSHIBANGILA, NDAYA, AUTRE) réutilisent le même guardian/compte.
        await service.EnsureAccessForGuardiansAsync(SchoolId, [guardian]);
        await service.EnsureAccessForGuardiansAsync(SchoolId, [guardian]);
        await service.EnsureAccessForGuardiansAsync(SchoolId, [guardian]);

        assignmentRepo.Items
            .Count(a => a.UserId == user.Id && a.RoleId == ParentRoleId && !a.IsDeleted)
            .Should().Be(1);
        assignmentRepo.AddCallCount.Should().Be(1);
    }

    [Fact]
    public async Task EnsureAccess_DoesNotAffectOtherRoleAssignments()
    {
        var guardian = CreateGuardian("MUSENGA", "Jean");
        var user = CreateUser(guardian.Id, "parent.musenga");
        var adminRoleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var adminAssignment = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = adminRoleId,
            IsDeleted = false
        };
        var softDeletedParent = new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleId = ParentRoleId,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-5)
        };
        var assignmentRepo = new TrackingRoleAssignmentRepository([adminAssignment, softDeletedParent]);
        var service = CreateService(
            new FakeRepository<UserAccount>([user]),
            new FakeRepository<Role>([CreateParentRole()]),
            assignmentRepo);

        await service.EnsureAccessForGuardiansAsync(SchoolId, [guardian]);

        assignmentRepo.Items.Should().HaveCount(2);
        assignmentRepo.Items.Should().ContainSingle(a => a.RoleId == adminRoleId && !a.IsDeleted);
        assignmentRepo.Items.Should().ContainSingle(a => a.RoleId == ParentRoleId && !a.IsDeleted);
        assignmentRepo.AddCallCount.Should().Be(0);
    }

    private static ParentAccessProvisioningService CreateService(
        IRepository<UserAccount> users,
        IRepository<Role> roles,
        IRepository<UserRoleAssignment> assignments) =>
        new(
            users,
            roles,
            assignments,
            new FakeRepository<Permission>(
            [
                new Permission { Id = Guid.NewGuid(), Code = Permissions.PaymentsRead },
                new Permission { Id = Guid.NewGuid(), Code = Permissions.GradesRead },
                new Permission { Id = Guid.NewGuid(), Code = Permissions.ReportsRead },
                new Permission { Id = Guid.NewGuid(), Code = Permissions.StudentsRead }
            ]),
            new FakeRepository<RolePermission>(),
            new StubPasswordHasher());

    private static Role CreateParentRole() => new()
    {
        Id = ParentRoleId,
        SchoolId = SchoolId,
        Code = "PARENT",
        Name = "Parent"
    };

    private static Guardian CreateGuardian(string lastName, string firstName) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = firstName,
        LastName = lastName,
        Phone = "0990000000"
    };

    private static UserAccount CreateUser(Guid guardianId, string userName) => new()
    {
        Id = Guid.NewGuid(),
        SchoolId = SchoolId,
        GuardianId = guardianId,
        UserName = userName,
        FirstName = "Jean",
        LastName = "MUSENGA",
        Email = $"{userName}@ecole.local",
        PasswordHash = "hash",
        MustChangePassword = false,
        IsActive = true
    };

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string password, string hash) => hash == $"hashed:{password}";
    }

    private sealed class FakeRepository<T> : IRepository<T> where T : class
    {
        private readonly List<T> _items;

        public FakeRepository(IEnumerable<T>? items = null) => _items = items?.ToList() ?? [];

        public Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop is null) return Task.FromResult<T?>(null);
            var match = _items.FirstOrDefault(x => (Guid)prop.GetValue(x)! == id);
            return Task.FromResult(match);
        }

        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<T>>(_items.ToList());

        public Task<IReadOnlyList<T>> FindAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<T>>(_items.AsQueryable().Where(predicate).ToList());

        public Task<IReadOnlyList<T>> FindIncludingDeletedAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            FindAsync(predicate, cancellationToken);

        public Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            _items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            _items.Remove(entity);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Simule le filtre soft-delete EF + contrainte unique active (UserId, RoleId).
    /// </summary>
    private sealed class TrackingRoleAssignmentRepository : IRepository<UserRoleAssignment>
    {
        private readonly object _gate = new();
        public List<UserRoleAssignment> Items { get; }
        public int AddCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }

        public TrackingRoleAssignmentRepository(IEnumerable<UserRoleAssignment>? items = null) =>
            Items = items?.ToList() ?? [];

        public Task<UserRoleAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
            }
        }

        public Task<IReadOnlyList<UserRoleAssignment>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<UserRoleAssignment>>(Items.Where(x => !x.IsDeleted).ToList());
            }
        }

        public Task<IReadOnlyList<UserRoleAssignment>> FindAsync(
            Expression<Func<UserRoleAssignment, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                // Filtre soft-delete comme EF query filter.
                var result = Items.Where(x => !x.IsDeleted).AsQueryable().Where(predicate).ToList();
                return Task.FromResult<IReadOnlyList<UserRoleAssignment>>(result);
            }
        }

        public Task<IReadOnlyList<UserRoleAssignment>> FindIncludingDeletedAsync(
            Expression<Func<UserRoleAssignment, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                var result = Items.AsQueryable().Where(predicate).ToList();
                return Task.FromResult<IReadOnlyList<UserRoleAssignment>>(result);
            }
        }

        public Task<UserRoleAssignment> AddAsync(UserRoleAssignment entity, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                var conflict = Items.Any(a =>
                    a.UserId == entity.UserId
                    && a.RoleId == entity.RoleId
                    && !a.IsDeleted);
                if (conflict)
                {
                    throw new InvalidOperationException(
                        "Cannot insert duplicate key row in object 'dbo.UserRoleAssignments' (simule SQL 2601).");
                }

                AddCallCount++;
                Items.Add(entity);
                return Task.FromResult(entity);
            }
        }

        public Task UpdateAsync(UserRoleAssignment entity, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                UpdateCallCount++;
                var existing = Items.FirstOrDefault(x => x.Id == entity.Id);
                if (existing is null)
                {
                    Items.Add(entity);
                    return Task.CompletedTask;
                }

                existing.IsDeleted = entity.IsDeleted;
                existing.DeletedAt = entity.DeletedAt;
                existing.DeletedBy = entity.DeletedBy;
                existing.UpdatedAt = entity.UpdatedAt;
                existing.UpdatedBy = entity.UpdatedBy;
                return Task.CompletedTask;
            }
        }

        public Task DeleteAsync(UserRoleAssignment entity, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                Items.Remove(entity);
                return Task.CompletedTask;
            }
        }
    }
}
