using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using FluentAssertions;
using SchoolManagement.Infrastructure.ServerIdentity;
using Xunit;

namespace SchoolManagement.UnitTests.Foundations;

[Trait("Category", "Foundations")]
public sealed class ServerIdentityFilePermissionsFoundationTests : IDisposable
{
    private readonly string _dir;

    public ServerIdentityFilePermissionsFoundationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "erp-foundations-acl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public void ApplyRestrictive_Creates_Restricted_File_On_Windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(_dir, ServerIdentityFileStore.FileName);
        File.WriteAllText(path, "{}");
        ServerIdentityFilePermissions.ApplyRestrictive(path);

        var security = new FileInfo(path).GetAccessControl(AccessControlSections.Access);
        security.AreAccessRulesProtected.Should().BeTrue();

        var rules = security.GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Where(r => r.AccessControlType == AccessControlType.Allow)
            .ToList();

        rules.Should().NotBeEmpty();
        rules.Any(r =>
        {
            if (r.IdentityReference is not SecurityIdentifier sid)
            {
                return false;
            }

            return sid.IsWellKnown(WellKnownSidType.LocalSystemSid)
                   || sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
        }).Should().BeTrue();
    }

    [Fact]
    public void ApplyRestrictive_Sets_Unix_Mode_600_When_Supported()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var path = Path.Combine(_dir, ServerIdentityFileStore.FileName);
        File.WriteAllText(path, "{}");
        ServerIdentityFilePermissions.ApplyRestrictive(path);

        var mode = File.GetUnixFileMode(path);
        (mode & UnixFileMode.GroupRead).Should().Be(0);
        (mode & UnixFileMode.OtherRead).Should().Be(0);
        (mode & UnixFileMode.UserRead).Should().NotBe(0);
        (mode & UnixFileMode.UserWrite).Should().NotBe(0);
    }

    [Fact]
    public void Save_Applies_Permissions_On_New_Identity_File()
    {
        var store = new ServerIdentityFileStore(_dir, new TestRoundTripEncryption());
        store.LoadOrCreateIfMissing();

        var path = Path.Combine(_dir, ServerIdentityFileStore.FileName);
        File.Exists(path).Should().BeTrue();

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var mode = File.GetUnixFileMode(path);
            (mode & UnixFileMode.OtherRead).Should().Be(0);
        }
        else if (OperatingSystem.IsWindows())
        {
            var security = new FileInfo(path).GetAccessControl(AccessControlSections.Access);
            security.AreAccessRulesProtected.Should().BeTrue();
        }
    }
}
