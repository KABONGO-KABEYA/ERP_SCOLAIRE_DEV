using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using SchoolManagement.Application.ParentActivation;
using Xunit;

namespace SchoolManagement.UnitTests.Foundations;

[Trait("Category", "Foundations")]
[Trait("Phase", "7")]
public sealed class ParentActivationTokenTypeGuardTests
{
    [Fact]
    public void EnsureParentActivationTokenType_Accepts_Typ_Claim()
    {
        var jwt = BuildUnsignedJwt(("typ", "parent_activation"));
        var act = () => ParentActivationTokenTypeGuard.EnsureParentActivationTokenType(jwt);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureParentActivationTokenType_Accepts_TokenType_Claim()
    {
        var jwt = BuildUnsignedJwt(("token_type", "parent_activation"));
        var act = () => ParentActivationTokenTypeGuard.EnsureParentActivationTokenType(jwt);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureParentActivationTokenType_Rejects_SchoolEstablishment()
    {
        var jwt = BuildUnsignedJwt(("token_type", "school_establishment"));
        var act = () => ParentActivationTokenTypeGuard.EnsureParentActivationTokenType(jwt);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage(ParentActivationTokenTypeGuard.RejectedEstablishmentMessage);
    }

    [Fact]
    public void EnsureParentActivationTokenType_Rejects_UnknownType()
    {
        var jwt = BuildUnsignedJwt(("token_type", "other"));
        var act = () => ParentActivationTokenTypeGuard.EnsureParentActivationTokenType(jwt);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage(ParentActivationTokenTypeGuard.InvalidTypeMessage);
    }

    [Fact]
    public void IsSchoolEstablishmentToken_Detects_TokenType_Claim()
    {
        var compact = WriteCompact(("token_type", "school_establishment"));
        ParentActivationTokenTypeGuard.IsSchoolEstablishmentToken(compact).Should().BeTrue();
    }

    [Fact]
    public void EnsureNotSchoolEstablishmentToken_Allows_Parent()
    {
        var compact = WriteCompact(("typ", "parent_activation"));
        var act = () => ParentActivationTokenTypeGuard.EnsureNotSchoolEstablishmentToken(compact);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureNotSchoolEstablishmentToken_Rejects_Establishment()
    {
        var compact = WriteCompact(("token_type", "school_establishment"));
        var act = () => ParentActivationTokenTypeGuard.EnsureNotSchoolEstablishmentToken(compact);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage(ParentActivationTokenTypeGuard.RejectedEstablishmentMessage);
    }

    private static JwtSecurityToken BuildUnsignedJwt(params (string Type, string Value)[] claims)
    {
        return new JwtSecurityToken(
            claims: claims.Select(c => new Claim(c.Type, c.Value)),
            expires: DateTime.UtcNow.AddHours(1));
    }

    private static string WriteCompact(params (string Type, string Value)[] claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("phase7-parent-activation-guard-test-key-32b"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims.Select(c => new Claim(c.Type, c.Value)),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
