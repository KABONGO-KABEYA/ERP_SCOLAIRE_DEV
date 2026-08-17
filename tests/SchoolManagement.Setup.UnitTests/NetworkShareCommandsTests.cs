using System.Security.Principal;
using FluentAssertions;
using Xunit;

namespace SchoolManagement.Setup.UnitTests;

public sealed class NetworkShareCommandsTests
{
    [Fact]
    public void BuildDeleteArguments_has_no_y_switch()
    {
        var args = NetworkShareCommands.BuildDeleteArguments("ERP_Dossiers");
        args.Should().Be("share ERP_Dossiers /delete");
        args.Should().NotContain("/y");
    }

    [Fact]
    public void ResolveEveryoneAccountName_round_trips_sid_s_1_1_0()
    {
        var name = NetworkShareCommands.ResolveEveryoneAccountName();
        name.Should().NotBeNullOrWhiteSpace();

        var sid = (SecurityIdentifier)new NTAccount(name).Translate(typeof(SecurityIdentifier));
        sid.Value.Should().Be(NetworkShareCommands.EveryoneSidValue);
    }

    [Fact]
    public void BuildCreateArguments_uses_resolved_sid_not_hardcoded_everyone()
    {
        var resolved = NetworkShareCommands.ResolveEveryoneAccountName();
        var grant = NetworkShareCommands.FormatGrantPrincipal(resolved);
        var args = NetworkShareCommands.BuildCreateArguments("ERP_Dossiers", @"D:\ERP_SCOLAIRE");

        args.Should().Be($@"share ERP_Dossiers=""D:\ERP_SCOLAIRE"" /GRANT:{grant},FULL");
        args.Should().Contain("/GRANT:");
        args.Should().EndWith(",FULL");
        args.Should().Contain(@"D:\ERP_SCOLAIRE");
        args.Should().NotContain(@"\\");
        args.Should().NotContain("/y");

        if (!resolved.Equals("Everyone", StringComparison.OrdinalIgnoreCase))
            args.Should().NotContain("Everyone");
    }

    [Fact]
    public void BuildCreateArguments_rejects_unc_path()
    {
        var act = () => NetworkShareCommands.BuildCreateArguments(
            "ERP_Dossiers", @"\\Desktop-CT9VNDV\erp_scolaire");
        act.Should().Throw<InvalidOperationException>().WithMessage("*chemin local*");
    }

    [Fact]
    public void BuildUncAccessPath_is_network_address_only()
    {
        NetworkShareCommands.BuildUncAccessPath("ERP_Dossiers")
            .Should().Be($@"\\{Environment.MachineName}\ERP_Dossiers");
    }

    [Fact]
    public void TryResolveLocalPath_keeps_drive_path()
    {
        NetworkShareCommands.TryResolveLocalPath(@"D:\ERP_SCOLAIRE", out var local, out var error)
            .Should().BeTrue();
        local.Should().Be(@"D:\ERP_SCOLAIRE");
        error.Should().BeEmpty();
    }

    [Fact]
    public void TryResolveLocalPath_rejects_remote_unc()
    {
        NetworkShareCommands.TryResolveLocalPath(@"\\other-pc\erp_scolaire", out _, out var error)
            .Should().BeFalse();
        error.Should().Contain("chemin LOCAL");
    }
}
