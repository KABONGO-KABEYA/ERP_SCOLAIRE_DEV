using System.Net.Http;
using SchoolManagement.Updates;
using Xunit;

namespace SchoolManagement.UnitTests.Updates;

public sealed class UpdateTlsPolicyTests
{
    [Fact]
    public void Does_not_accept_any_server_certificate()
    {
        Assert.False(UpdateTlsPolicy.AcceptsAnyServerCertificate);

        using var handler = UpdateTlsPolicy.CreateHandler();
        var httpHandler = Assert.IsType<HttpClientHandler>(handler);
        Assert.False(ReferenceEquals(
            httpHandler.ServerCertificateCustomValidationCallback,
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator));
    }
}
