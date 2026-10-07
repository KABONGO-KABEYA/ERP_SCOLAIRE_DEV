using SchoolManagement.Update;

namespace SchoolManagement.Update.UnitTests;

public class PayloadFilePreserverTests
{
    [Fact]
    public void CopyPayloadDirectory_preserves_local_config_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "erp-update-test-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);

        try
        {
            File.WriteAllText(Path.Combine(source, "SchoolManagement.API.dll"), "new-binary");
            File.WriteAllText(Path.Combine(source, "ServeurDonnees.txt"), "FROM-PAYLOAD");
            File.WriteAllText(Path.Combine(target, "ServeurDonnees.txt"), "LOCAL-CONFIG");

            var snapshot = PayloadFilePreserver.SnapshotFiles(
                target,
                PayloadFilePreserver.ApiPreservedFileNames);

            PayloadFilePreserver.CopyPayloadDirectory(
                source,
                target,
                PayloadFilePreserver.ApiPreservedFileNames,
                snapshot);

            Assert.Equal("LOCAL-CONFIG", File.ReadAllText(Path.Combine(target, "ServeurDonnees.txt")));
            Assert.Equal("new-binary", File.ReadAllText(Path.Combine(target, "SchoolManagement.API.dll")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Subscription_defaults_match_business_rule()
    {
        var expiration = ExistingInstallationSubscriptionBootstrap.DefaultInstallationDate
            .AddMonths(ExistingInstallationSubscriptionBootstrap.DefaultDurationMonths);

        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ExistingInstallationSubscriptionBootstrap.DefaultInstallationDate);
        Assert.Equal(1, ExistingInstallationSubscriptionBootstrap.DefaultDurationMonths);
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), expiration);
    }

    [Fact]
    public void DetectInstallation_requires_desktop_exe()
    {
        var root = Path.Combine(Path.GetTempPath(), "erp-detect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Desktop"));
        try
        {
            var engine = new UpdateEngine();
            var ex = Assert.Throws<InvalidOperationException>(() => engine.DetectInstallation(root));
            Assert.Contains("introuvable", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
