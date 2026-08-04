using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using SchoolManagement.Application.ParentActivation;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Xunit;

namespace SchoolManagement.UnitTests.Foundations;

[Trait("Category", "Foundations")]
public sealed class ActivationTokenRoutingReaderTests
{
    [Fact]
    public void TryReadSchoolId_From_Unsigned_Payload_Claim()
    {
        const string payloadJson =
            """{"jti":"11111111-1111-1111-1111-111111111111","school_id":"33333333-3333-3333-3333-333333333333","typ":"parent_activation"}""";
        var payloadSegment = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(payloadJson));
        var jwt = "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0." + payloadSegment + ".";

        var schoolId = ActivationTokenRoutingReader.TryReadSchoolId(jwt);
        schoolId.Should().Be(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    }
}
