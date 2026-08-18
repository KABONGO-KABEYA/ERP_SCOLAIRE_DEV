using System.Data.Common;
using FluentAssertions;
using SchoolManagement.Infrastructure.CloudSync;
using Xunit;

namespace SchoolManagement.UnitTests.CloudSync;

public sealed class CloudSyncFailureClassificationTests
{
    [Fact]
    public void ClassifyUnitFailure_marks_timeout_as_transient()
    {
        var ex = new TestDbException("Execution timeout expired.");

        CloudSyncEngine.ClassifyUnitFailure(ex)
            .Should().Be(CloudSyncEngine.SyncUnitFailureCategory.Transient);
    }

    [Fact]
    public void ClassifyUnitFailure_marks_duplicate_key_as_structural()
    {
        var ex = new InvalidOperationException(
            "Cannot insert duplicate key row in object 'dbo.Courses' with unique index 'IX_Courses_Code'.");

        CloudSyncEngine.ClassifyUnitFailure(ex)
            .Should().Be(CloudSyncEngine.SyncUnitFailureCategory.MultiTenant);
    }

    [Fact]
    public void ClassifyUnitFailure_marks_missing_object_as_structural()
    {
        var ex = new InvalidOperationException("Nom d'objet 'RegistrationNumberCounters' non valide.");

        CloudSyncEngine.ClassifyUnitFailure(ex)
            .Should().Be(CloudSyncEngine.SyncUnitFailureCategory.Structural);
    }

    private sealed class TestDbException(string message) : DbException(message);
}
