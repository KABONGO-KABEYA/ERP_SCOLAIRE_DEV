namespace SchoolManagement.IntegrationTests.MultiTenant;

using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.Auth.Interfaces;
using SchoolManagement.Infrastructure.Persistence;
using SchoolManagement.Shared.Constants;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Preuve de fonctionnement de l'isolation multi-école : deux écoles réelles sont créées en base,
/// puis chaque ressource métier est attaquée depuis l'autre école, dans les deux sens.
/// </summary>
[Collection("ApiIntegration")]
[Trait("Category", "MultiTenant")]
public sealed class CrossTenantIsolationTests
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public CrossTenantIsolationTests(ApiWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Cross_tenant_access_is_denied_on_every_business_resource()
    {
        var report = new CrossTenantReport();
        TenantTestSchool? schoolA = null;
        TenantTestSchool? schoolB = null;

        try
        {
            (schoolA, schoolB) = await SeedTwoSchoolsAsync();

            await RunControlPassAsync(schoolA, report);
            await RunControlPassAsync(schoolB, report);

            await RunCrossTenantPassAsync(schoolA, schoolB, report);
            await RunCrossTenantPassAsync(schoolB, schoolA, report);

            await RunListLeakPassAsync(schoolA, schoolB, report);
            await RunListLeakPassAsync(schoolB, schoolA, report);
        }
        finally
        {
            var ids = new[] { schoolA?.SchoolId, schoolB?.SchoolId }
                .Where(id => id is not null)
                .Select(id => id!.Value)
                .ToArray();

            if (ids.Length > 0)
            {
                using var scope = _factory.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
                await MultiTenantSeeder.CleanupAsync(context, ids);
            }
        }

        var reportPath = report.Write(schoolA?.Marker, schoolB?.Marker);
        _output.WriteLine(report.Summary());
        _output.WriteLine($"Rapport : {reportPath}");

        report.Failures.Should().BeEmpty(
            "aucun accès inter-école ne doit aboutir :{0}{1}",
            Environment.NewLine,
            string.Join(Environment.NewLine, report.Failures.Select(f => f.Describe())));

        report.Inconclusive.Should().BeEmpty(
            "chaque scénario doit être prouvé par un contrôle propriétaire réussi :{0}{1}",
            Environment.NewLine,
            string.Join(Environment.NewLine, report.Inconclusive.Select(f => f.Describe())));
    }

    private async Task<(TenantTestSchool A, TenantTestSchool B)> SeedTwoSchoolsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        string Mint(Guid userId, Guid schoolId) => tokens.GenerateAccessToken(
            userId, schoolId, $"user-{userId:N}", "Utilisateur de test", ["Admin"], Permissions.All);

        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var a = await MultiTenantSeeder.SeedAsync(context, $"MTA{suffix}", Mint);
        var b = await MultiTenantSeeder.SeedAsync(context, $"MTB{suffix}", Mint);
        return (a, b);
    }

    /// <summary>Le propriétaire doit voir sa propre donnée : sans cela, un 404 cross-école ne prouve rien.</summary>
    private async Task RunControlPassAsync(TenantTestSchool owner, CrossTenantReport report)
    {
        using var client = CreateClient(owner);

        foreach (var path in CrossTenantScenarios.Targeted.Select(s => s.ControlPath).Distinct(StringComparer.Ordinal))
        {
            var response = await client.GetAsync(Resolve(path, owner));
            report.RecordControl(path, owner.Marker, response.StatusCode);
        }
    }

    private async Task RunCrossTenantPassAsync(
        TenantTestSchool attacker,
        TenantTestSchool victim,
        CrossTenantReport report)
    {
        using var client = CreateClient(attacker);

        foreach (var scenario in CrossTenantScenarios.Targeted)
        {
            var path = Resolve(scenario.PathTemplate, victim);
            var payload = scenario.Body is null ? null : Resolve(scenario.Body, victim);

            var request = new HttpRequestMessage(new HttpMethod(scenario.Method), path);
            if (payload is not null)
            {
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            report.RecordCrossTenant(
                scenario,
                attacker.Marker,
                victim.Marker,
                response.StatusCode,
                leaked: RevealsForeignData(body, victim, path + payload));
        }
    }

    private async Task RunListLeakPassAsync(
        TenantTestSchool attacker,
        TenantTestSchool victim,
        CrossTenantReport report)
    {
        using var client = CreateClient(attacker);

        foreach (var scenario in CrossTenantScenarios.Lists)
        {
            using var response = await client.GetAsync(Resolve(scenario.PathTemplate, attacker));
            var body = await response.Content.ReadAsStringAsync();

            report.RecordList(
                scenario,
                attacker.Marker,
                victim.Marker,
                response.StatusCode,
                leaked: RevealsForeignData(body, victim, sentContent: string.Empty),
                seesOwnData: body.Contains(attacker.Marker, StringComparison.Ordinal));
        }
    }

    private HttpClient CreateClient(TenantTestSchool school)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", school.JwtToken);
        return client;
    }

    /// <summary>
    /// Fuite si la réponse contient le marqueur de la victime, ou l'un de ses identifiants que
    /// l'appelant n'avait pas déjà fournis dans sa requête (un identifiant renvoyé en écho n'apprend rien).
    /// </summary>
    private static bool RevealsForeignData(string body, TenantTestSchool victim, string sentContent)
    {
        if (body.Contains(victim.Marker, StringComparison.Ordinal))
        {
            return true;
        }

        return victim.Tokens.Values
            .Where(id => !sentContent.Contains(id, StringComparison.OrdinalIgnoreCase))
            .Any(id => body.Contains(id, StringComparison.OrdinalIgnoreCase));
    }

    private static string Resolve(string template, TenantTestSchool school)
    {
        var resolved = template;
        foreach (var (key, value) in school.Tokens)
        {
            resolved = resolved.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        }

        return resolved.Replace("\r", string.Empty).Replace("\n", string.Empty);
    }
}

internal sealed record CrossTenantOutcome(
    string Resource,
    string Method,
    string Path,
    string Direction,
    HttpStatusCode StatusCode,
    string Verdict,
    string? Detail = null)
{
    internal string Describe() =>
        $"[{Resource}] {Method} {Path} ({Direction}) → {(int)StatusCode} {StatusCode} : {Detail ?? Verdict}";
}
