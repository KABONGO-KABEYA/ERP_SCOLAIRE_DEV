using FluentAssertions;
using SchoolManagement.Application.ServerIdentity;
using SchoolManagement.Infrastructure.ServerIdentity;
using Xunit;

namespace SchoolManagement.UnitTests.Foundations;

[Trait("Category", "Foundations")]
public sealed class ServerIdentityFileStoreFoundationTests : IDisposable
{
    private readonly string _dir;
    private readonly TestRoundTripEncryption _encryption = new();

    public ServerIdentityFileStoreFoundationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "erp-foundations-si-" + Guid.NewGuid().ToString("N"));
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

    private ServerIdentityFileStore CreateStore() => new(_dir, _encryption);

    [Fact]
    public void LoadOrCreateIfMissing_Creates_File_On_First_Run()
    {
        var path = Path.Combine(_dir, ServerIdentityFileStore.FileName);
        File.Exists(path).Should().BeFalse();

        var model = CreateStore().LoadOrCreateIfMissing();

        File.Exists(path).Should().BeTrue();
        model.ServerInstanceId.Should().NotBe(Guid.Empty);
        model.KeyVersion.Should().Be(1);
        model.PublicKeyFingerprint.Should().StartWith("sha256:");
    }

    [Fact]
    public void LoadOrCreateIfMissing_Reloads_Same_Identity_After_Restart()
    {
        var store = CreateStore();
        var first = store.LoadOrCreateIfMissing();

        var second = CreateStore().LoadOrCreateIfMissing();

        second.ServerInstanceId.Should().Be(first.ServerInstanceId);
        second.PublicKeyFingerprint.Should().Be(first.PublicKeyFingerprint);
        second.KeyVersion.Should().Be(first.KeyVersion);
    }

    [Fact]
    public void LoadOrCreateIfMissing_Creates_When_File_Absent()
    {
        CreateStore().LoadOrCreateIfMissing();
        File.Delete(Path.Combine(_dir, ServerIdentityFileStore.FileName));

        var created = CreateStore().LoadOrCreateIfMissing();

        created.ServerInstanceId.Should().NotBe(Guid.Empty);
        File.Exists(Path.Combine(_dir, ServerIdentityFileStore.FileName)).Should().BeTrue();
    }

    [Fact]
    public void LoadOrCreateIfMissing_Throws_When_File_Corrupted_Json()
    {
        CreateStore().LoadOrCreateIfMissing();
        File.WriteAllText(Path.Combine(_dir, ServerIdentityFileStore.FileName), "{ not-json");

        var act = () => CreateStore().LoadOrCreateIfMissing();

        act.Should().Throw<ServerIdentityCorruptedException>()
            .WithMessage("*JSON valide*");
    }

    [Fact]
    public void LoadOrCreateIfMissing_Restores_From_Bak_After_Manual_Replace()
    {
        var store = CreateStore();
        var good = store.LoadOrCreateIfMissing();

        store.Save(new ServerIdentityFileModel
        {
            ServerInstanceId = good.ServerInstanceId,
            KeyVersion = good.KeyVersion,
            PublicKeyBase64 = good.PublicKeyBase64,
            PublicKeyFingerprint = good.PublicKeyFingerprint,
            PrivateKeyProtected = good.PrivateKeyProtected,
            InstalledAtUtc = good.InstalledAtUtc
        });

        File.WriteAllText(Path.Combine(_dir, ServerIdentityFileStore.FileName), "{ \"serverInstanceId\": \"00000000-0000-0000-0000-000000000000\" }");

        var act = () => CreateStore().LoadOrCreateIfMissing();
        act.Should().Throw<ServerIdentityCorruptedException>();

        File.Copy(
            Path.Combine(_dir, ServerIdentityFileStore.BackupFileName),
            Path.Combine(_dir, ServerIdentityFileStore.FileName),
            overwrite: true);

        var restored = CreateStore().LoadOrCreateIfMissing();
        restored.ServerInstanceId.Should().Be(good.ServerInstanceId);
    }

    [Fact]
    public void LoadOrCreateIfMissing_Throws_When_Fingerprint_Does_Not_Match_Public_Key()
    {
        var first = CreateStore().LoadOrCreateIfMissing();
        var path = Path.Combine(_dir, ServerIdentityFileStore.FileName);
        var json = File.ReadAllText(path);
        json = json.Replace(
            first.PublicKeyFingerprint,
            "sha256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            StringComparison.Ordinal);
        File.WriteAllText(path, json);

        var act = () => CreateStore().LoadOrCreateIfMissing();

        act.Should().Throw<ServerIdentityCorruptedException>()
            .WithMessage("*empreinte*");
    }

    [Fact]
    public void LoadOrCreateIfMissing_Throws_When_KeyVersion_Invalid()
    {
        CreateStore().LoadOrCreateIfMissing();
        var path = Path.Combine(_dir, ServerIdentityFileStore.FileName);
        var json = File.ReadAllText(path).Replace("\"keyVersion\": 1", "\"keyVersion\": 0");
        File.WriteAllText(path, json);

        var act = () => CreateStore().LoadOrCreateIfMissing();

        act.Should().Throw<ServerIdentityCorruptedException>()
            .WithMessage("*incomplet ou invalide*");
    }

    [Fact]
    public void Save_Writes_Backup_Before_Overwrite()
    {
        var store = CreateStore();
        var initial = store.LoadOrCreateIfMissing();
        var updated = new ServerIdentityFileModel
        {
            ServerInstanceId = initial.ServerInstanceId,
            KeyVersion = 2,
            PublicKeyBase64 = initial.PublicKeyBase64,
            PublicKeyFingerprint = initial.PublicKeyFingerprint,
            PrivateKeyProtected = initial.PrivateKeyProtected,
            InstalledAtUtc = initial.InstalledAtUtc
        };

        store.Save(updated);

        File.Exists(Path.Combine(_dir, ServerIdentityFileStore.BackupFileName)).Should().BeTrue();
        var reloaded = CreateStore().LoadOrCreateIfMissing();
        reloaded.KeyVersion.Should().Be(2);
    }
}
