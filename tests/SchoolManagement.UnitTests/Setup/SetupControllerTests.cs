using System.Net;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SchoolManagement.API.Controllers;
using SchoolManagement.Application.Setup.DTOs;
using SchoolManagement.Application.Setup.Interfaces;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Models;
using Xunit;

namespace SchoolManagement.UnitTests.Setup;

public sealed class SetupControllerTests
{
    [Fact]
    public void Setup_Endpoints_Remain_AllowAnonymous()
    {
        typeof(SetupController).GetMethod(nameof(SetupController.GetStatus))!
            .GetCustomAttribute<AllowAnonymousAttribute>()
            .Should().NotBeNull();

        typeof(SetupController).GetMethod(nameof(SetupController.Complete))!
            .GetCustomAttribute<AllowAnonymousAttribute>()
            .Should().NotBeNull();
    }

    [Fact]
    public async Task Complete_When_NeedsSetup_Returns_Ok_Without_Requiring_Jwt()
    {
        var setup = Substitute.For<IInitialSetupService>();
        setup.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new InitialSetupStatusDto(true, true, "Configuration initiale requise."));

        var resultDto = new CompleteInitialSetupResultDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "École Test",
            "admin");
        setup.CompleteAsync(Arg.Any<CompleteInitialSetupRequest>(), Arg.Any<CancellationToken>())
            .Returns(resultDto);

        var controller = new SetupController(setup);
        var response = await controller.Complete(SampleRequest(), CancellationToken.None);

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be((int)HttpStatusCode.OK);
        await setup.Received(1).CompleteAsync(Arg.Any<CompleteInitialSetupRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Complete_When_Already_Configured_Returns_Conflict()
    {
        var setup = Substitute.For<IInitialSetupService>();
        setup.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new InitialSetupStatusDto(false, true, "Établissement déjà configuré."));

        var controller = new SetupController(setup);
        var response = await controller.Complete(SampleRequest(), CancellationToken.None);

        var conflict = response.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        var body = conflict.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        body.Success.Should().BeFalse();
        body.Message.Should().Be("La configuration initiale est déjà terminée.");
        await setup.DidNotReceive().CompleteAsync(
            Arg.Any<CompleteInitialSetupRequest>(),
            Arg.Any<CancellationToken>());
    }

    private static CompleteInitialSetupRequest SampleRequest() => new(
        "École Test",
        null,
        null,
        null,
        null,
        null,
        null,
        Currency.CDF,
        null,
        null,
        "2026-2027",
        new DateOnly(2026, 9, 1),
        new DateOnly(2027, 7, 31),
        "admin",
        "admin@test.local",
        "Admin@2026",
        "Jean",
        "Admin",
        null,
        null,
        null);
}
