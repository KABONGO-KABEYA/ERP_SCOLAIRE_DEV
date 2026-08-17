using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace SchoolManagement.Setup.UnitTests;

public sealed class ServeurDonneesFileWriterTests
{
    private const string PlainPassword = "SetupTest_Sql_P@ssw0rd!";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SchoolManagement.ERP.Scolaire.RDC.v1");

    [Fact]
    public void SqlAuthentication_writes_encrypted_motdepasse_with_enc_prefix()
    {
        using var dir = new TempDirectory();
        var opt = CreateOptions(useWindowsAuth: false, PlainPassword);

        ServeurDonneesFileWriter.Write(dir.Path, opt);

        var content = File.ReadAllText(Path.Combine(dir.Path, "ServeurDonnees.txt"));
        var motDePasse = ReadMotDePasse(content);

        content.Should().Contain("AUTHENTIFICATION=SQL");
        motDePasse.Should().StartWith("ENC:");
        motDePasse.Should().NotBe(PlainPassword);
        content.Should().NotContain(PlainPassword);
    }

    [Fact]
    public void WindowsAuthentication_writes_empty_motdepasse()
    {
        using var dir = new TempDirectory();
        var opt = CreateOptions(useWindowsAuth: true, PlainPassword);

        ServeurDonneesFileWriter.Write(dir.Path, opt);

        var content = File.ReadAllText(Path.Combine(dir.Path, "ServeurDonnees.txt"));
        var motDePasse = ReadMotDePasse(content);

        content.Should().Contain("AUTHENTIFICATION=WINDOWS");
        motDePasse.Should().BeEmpty();
        content.Should().Contain("MOTDEPASSE=");
        content.Should().NotContain(PlainPassword);
    }

    [Fact]
    public void Encrypted_motdepasse_decrypts_with_dpapi_local_machine_contract()
    {
        using var dir = new TempDirectory();
        var opt = CreateOptions(useWindowsAuth: false, PlainPassword);

        ServeurDonneesFileWriter.Write(dir.Path, opt);

        var content = File.ReadAllText(Path.Combine(dir.Path, "ServeurDonnees.txt"));
        var motDePasse = ReadMotDePasse(content);

        DecryptLikeEncryptionService(motDePasse).Should().Be(PlainPassword);
    }

    [Fact]
    public void Desktop_and_api_targets_both_produce_valid_encrypted_config()
    {
        using var apiDir = new TempDirectory();
        using var desktopDir = new TempDirectory();
        var opt = CreateOptions(useWindowsAuth: false, PlainPassword);

        ServeurDonneesFileWriter.Write(apiDir.Path, opt);
        ServeurDonneesFileWriter.Write(desktopDir.Path, opt);

        var apiContent = File.ReadAllText(Path.Combine(apiDir.Path, "ServeurDonnees.txt"));
        var desktopContent = File.ReadAllText(Path.Combine(desktopDir.Path, "ServeurDonnees.txt"));

        var apiMotDePasse = ReadMotDePasse(apiContent);
        var desktopMotDePasse = ReadMotDePasse(desktopContent);

        apiContent.Should().Contain("SERVEUR=DESKTOP-TEST\\SQLEXPRESS");
        desktopContent.Should().Contain("SERVEUR=DESKTOP-TEST\\SQLEXPRESS");
        apiMotDePasse.Should().StartWith("ENC:");
        desktopMotDePasse.Should().StartWith("ENC:");
        DecryptLikeEncryptionService(apiMotDePasse).Should().Be(PlainPassword);
        DecryptLikeEncryptionService(desktopMotDePasse).Should().Be(PlainPassword);
    }

    private static string DecryptLikeEncryptionService(string cipherText)
    {
        const string prefix = "ENC:";
        cipherText.Should().StartWith(prefix);
        var protectedBytes = Convert.FromBase64String(cipherText[prefix.Length..]);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static string ReadMotDePasse(string fileContent)
    {
        var match = Regex.Match(fileContent, "(?m)^MOTDEPASSE=(.*)$");
        match.Success.Should().BeTrue("MOTDEPASSE line must exist");
        return match.Groups[1].Value.Trim();
    }

    private static InstallOptions CreateOptions(bool useWindowsAuth, string sqlPassword) =>
        new()
        {
            SqlServer = @"DESKTOP-TEST\SQLEXPRESS",
            Database = "SchoolManagementRDC_Production",
            UseWindowsAuth = useWindowsAuth,
            SqlUser = "sa",
            SqlPassword = sqlPassword,
        };

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "erp-setup-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // ignore cleanup
            }
        }
    }
}
