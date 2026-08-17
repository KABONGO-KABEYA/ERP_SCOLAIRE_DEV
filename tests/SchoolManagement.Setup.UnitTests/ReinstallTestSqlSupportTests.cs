using System.Security.Principal;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SchoolManagement.Setup.UnitTests;

public sealed class ReinstallTestSqlSupportTests
{
    [Fact]
    public void Test_database_name_is_dedicated_and_not_production()
    {
        ReinstallTestSqlSupport.TestDatabaseName.Should().Be("SchoolManagementRDC_SetupReinstallTest");
        ReinstallTestSqlSupport.TestDatabaseName.Should().NotBe(ReinstallTestSqlSupport.ProductionDatabaseName);
    }

    [Fact]
    public void EnsureTestDatabaseName_rejects_production()
    {
        var act = () => ReinstallTestSqlSupport.EnsureTestDatabaseName("SchoolManagementRDC_Production");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WithTestCatalog_replaces_only_initial_catalog()
    {
        var env = new[]
        {
            "ASPNETCORE_ENVIRONMENT=Production",
            "ConnectionStrings__Default=Data Source=SERVER\\INST;Initial Catalog=SchoolManagementRDC_Production;User ID=sa;Password=x;Encrypt=True;Trust Server Certificate=True",
            "SEED_DATABASE=true",
        };

        var retargeted = ReinstallTestSqlSupport.WithTestCatalog(env, ReinstallTestSqlSupport.TestDatabaseName);
        var builder = ReinstallTestSqlSupport.ParseDefaultConnection(retargeted);

        builder.InitialCatalog.Should().Be(ReinstallTestSqlSupport.TestDatabaseName);
        builder.DataSource.Should().Be(@"SERVER\INST");
        env[1].Should().Contain("SchoolManagementRDC_Production");
        retargeted[1].Should().NotContain("SchoolManagementRDC_Production");
        retargeted[0].Should().Be(env[0]);
        retargeted[2].Should().Be(env[2]);
    }

    [Fact]
    public void BuildTestConnectionString_never_targets_production()
    {
        var source = new SqlConnectionStringBuilder
        {
            DataSource = @"SERVER\INST",
            InitialCatalog = ReinstallTestSqlSupport.ProductionDatabaseName,
            IntegratedSecurity = true,
        };

        var testCs = ReinstallTestSqlSupport.BuildTestConnectionString(
            source, ReinstallTestSqlSupport.TestDatabaseName);
        var masterCs = ReinstallTestSqlSupport.BuildMasterConnectionString(source);

        new SqlConnectionStringBuilder(testCs).InitialCatalog.Should().Be(ReinstallTestSqlSupport.TestDatabaseName);
        new SqlConnectionStringBuilder(masterCs).InitialCatalog.Should().Be("master");
        testCs.Should().NotContain(ReinstallTestSqlSupport.ProductionDatabaseName);
    }

    [Fact]
    public void ResolveLocalSystemAccountName_round_trips_sid_s_1_5_18()
    {
        var name = ReinstallTestSqlSupport.ResolveLocalSystemAccountName();
        name.Should().NotBeNullOrWhiteSpace();
        name.Should().Contain("\\");

        var sid = (SecurityIdentifier)new NTAccount(name).Translate(typeof(SecurityIdentifier));
        sid.Value.Should().Be(ReinstallTestSqlSupport.LocalSystemSidValue);
    }
}
