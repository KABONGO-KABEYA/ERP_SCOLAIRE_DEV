using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SchoolManagement.Application.Configuration.Encryption;
using Xunit;

namespace SchoolManagement.UnitTests.Foundations;

[Trait("Category", "Foundations")]
public sealed class ProductionEncryptionKeyGuardFoundationTests
{
    private const string KeyName = ProductionEncryptionKeyGuard.EnvironmentVariableName;

    [Fact]
    public void EnsureConfigured_Allows_Development_Without_Key()
    {
        var env = new TestHostEnvironment(Environments.Development);
        var config = new ConfigurationBuilder().Build();

        var act = () => ProductionEncryptionKeyGuard.EnsureConfigured(env, config);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureConfigured_Throws_When_Production_Cloud_Without_Key()
    {
        var previous = Environment.GetEnvironmentVariable(KeyName);
        try
        {
            Environment.SetEnvironmentVariable(KeyName, null);

            var env = new TestHostEnvironment(Environments.Production);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Deployment:Role"] = "Cloud"
                })
                .Build();

            var act = () => ProductionEncryptionKeyGuard.EnsureConfigured(env, config);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage($"*{KeyName}*obligatoire*");
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyName, previous);
        }
    }

    [Fact]
    public void EnsureConfigured_Throws_When_Production_Uses_Dev_Default_Key()
    {
        var previous = Environment.GetEnvironmentVariable(KeyName);
        try
        {
            Environment.SetEnvironmentVariable(KeyName, AesConfigurationEncryptionService.DevFallbackKey);

            var env = new TestHostEnvironment(Environments.Production);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Deployment:Role"] = "Cloud"
                })
                .Build();

            var act = () => ProductionEncryptionKeyGuard.EnsureConfigured(env, config);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*développement*");
        }
        finally
        {
            Environment.SetEnvironmentVariable(KeyName, previous);
        }
    }

    [Fact]
    public void AesEncryption_Throws_At_Construction_When_Production_And_Key_Missing()
    {
        var previousEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var previousKey = Environment.GetEnvironmentVariable(KeyName);
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Production);
            Environment.SetEnvironmentVariable(KeyName, null);

            var act = static () => _ = new AesConfigurationEncryptionService();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*ERP_CONFIG_ENCRYPTION_KEY*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnv);
            Environment.SetEnvironmentVariable(KeyName, previousKey);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
