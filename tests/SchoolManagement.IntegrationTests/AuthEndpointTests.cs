using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using SchoolManagement.Application.Auth.DTOs;
using SchoolManagement.Shared.Models;
using Xunit;

namespace SchoolManagement.IntegrationTests;

[Collection("ApiIntegration")]
public class AuthEndpointTests
{
    private readonly HttpClient _client;

    public AuthEndpointTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_With_Valid_Credentials_Returns_Token()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Admin@2026"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
        body.Data.RefreshToken.Should().NotBeNullOrEmpty();
        body.Data.User.UserName.Should().Be("admin");
    }

    [Fact]
    public async Task Login_With_Invalid_Credentials_Returns_Unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "wrong-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Students_Endpoint_Requires_Authentication()
    {
        var response = await _client.GetAsync("/api/v1/students");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
