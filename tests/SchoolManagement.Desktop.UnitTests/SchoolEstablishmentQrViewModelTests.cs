using FluentAssertions;
using NSubstitute;
using SchoolManagement.Application.SchoolEstablishment;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Domain.Enums;
using Xunit;

namespace SchoolManagement.Desktop.UnitTests;

public sealed class SchoolEstablishmentQrViewModelTests
{
    private readonly ISchoolEstablishmentApiService _api = Substitute.For<ISchoolEstablishmentApiService>();
    private readonly ISchoolApiService _schoolApi = Substitute.For<ISchoolApiService>();
    private readonly IDesktopDialogs _dialogs = Substitute.For<IDesktopDialogs>();

    private SchoolEstablishmentQrViewModel CreateSut() => new(_api, _schoolApi, _dialogs);

    private static SchoolEstablishmentQrDto Qr(
        Guid schoolId,
        Guid credentialId,
        int version,
        bool pending,
        string status,
        string? message = null) =>
        new(
            schoolId,
            credentialId,
            version,
            Token: "jwt-public-token",
            DeepLinkUri: $"erp-scolaire://establish?token=jwt-public-token-{version}",
            QrPayload: $"erp-scolaire://establish?token=jwt-public-token-{version}",
            BootstrapSyncPending: pending,
            BootstrapSyncStatus: status,
            BootstrapSyncMessage: message);

    [Fact]
    public async Task Load_FetchesQr_AndShowsSchoolAndSyncedState()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        _schoolApi.GetCurrentSchoolAsync(Arg.Any<CancellationToken>())
            .Returns(new SchoolDto(
                schoolId,
                "ECOLE DESKTOP",
                null,
                null,
                null,
                null,
                null,
                null,
                Currency.CDF,
                null,
                null,
                true));
        _api.GetQrAsync(Arg.Any<CancellationToken>())
            .Returns(Qr(schoolId, credentialId, 1, pending: false, SchoolEstablishmentBootstrapSyncUi.Synced));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.SchoolName.Should().Be("ECOLE DESKTOP");
        sut.SchoolId.Should().Be(schoolId);
        sut.CredentialId.Should().Be(credentialId);
        sut.CredentialVersion.Should().Be(1);
        sut.HasQr.Should().BeTrue();
        sut.QrImage.Should().NotBeNull();
        sut.QrPayload.Should().StartWith("erp-scolaire://establish?token=");
        sut.BootstrapSyncPending.Should().BeFalse();
        sut.BootstrapSyncStatus.Should().Be(SchoolEstablishmentBootstrapSyncUi.Synced);
        sut.BootstrapSyncStatusLabel.Should().Contain("Synchronisé");
        sut.CanRetryBootstrapSync.Should().BeFalse();
        sut.StatusMessage.Should().Contain("chargé");
        sut.QrPayload.Should().NotContain("SecretHash");
        sut.QrPayload.Should().NotContain("secret");
    }

    [Fact]
    public async Task Load_WhenFailedSync_ExposesRetry()
    {
        var schoolId = Guid.NewGuid();
        _schoolApi.GetCurrentSchoolAsync(Arg.Any<CancellationToken>()).Returns((SchoolDto?)null);
        _api.GetQrAsync(Arg.Any<CancellationToken>())
            .Returns(Qr(
                schoolId,
                Guid.NewGuid(),
                1,
                pending: true,
                SchoolEstablishmentBootstrapSyncUi.Failed,
                "Bootstrap injoignable."));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.BootstrapSyncPending.Should().BeTrue();
        sut.BootstrapSyncStatusLabel.Should().Contain("Échec");
        sut.BootstrapSyncMessage.Should().Be("Bootstrap injoignable.");
        sut.CanRetryBootstrapSync.Should().BeTrue();
    }

    [Fact]
    public async Task Rotate_WhenCancelled_DoesNotCallApi()
    {
        _dialogs.ConfirmYesNo(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var sut = CreateSut();
        await sut.RotateCommand.ExecuteAsync(null);

        await _api.DidNotReceive().RotateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
        sut.HasQr.Should().BeFalse();
    }

    [Fact]
    public async Task Rotate_WhenConfirmed_ReplacesWithNewQrOnly()
    {
        var schoolId = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        _dialogs.ConfirmYesNo(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _api.RotateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Qr(schoolId, newId, 2, pending: false, SchoolEstablishmentBootstrapSyncUi.Synced));

        var sut = CreateSut();
        sut.ApplyQr(Qr(schoolId, oldId, 1, pending: false, SchoolEstablishmentBootstrapSyncUi.Synced));
        sut.CredentialId.Should().Be(oldId);

        await sut.RotateCommand.ExecuteAsync(null);

        await _api.Received(1).RotateAsync("Régénération QR Desktop", Arg.Any<CancellationToken>());
        sut.CredentialId.Should().Be(newId);
        sut.CredentialVersion.Should().Be(2);
        sut.HasQr.Should().BeTrue();
        sut.QrPayload.Should().Contain("token=jwt-public-token-2");
        sut.StatusMessage.Should().Contain("Nouveau QR");
    }

    [Fact]
    public async Task RetryBootstrapSync_UpdatesStatusFromResult()
    {
        var schoolId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var sut = CreateSut();
        sut.ApplyQr(Qr(
            schoolId,
            credentialId,
            1,
            pending: true,
            SchoolEstablishmentBootstrapSyncUi.Failed,
            "down"));

        _api.RetryBootstrapSyncAsync(Arg.Any<CancellationToken>())
            .Returns(new BootstrapSyncRetryResult(
                Success: true,
                BootstrapSyncPending: false,
                BootstrapSyncStatus: SchoolEstablishmentBootstrapSyncUi.Synced,
                Message: "Registre Bootstrap synchronisé.",
                Qr: Qr(schoolId, credentialId, 1, pending: false, SchoolEstablishmentBootstrapSyncUi.Synced)));

        await sut.RetryBootstrapSyncCommand.ExecuteAsync(null);

        sut.BootstrapSyncPending.Should().BeFalse();
        sut.BootstrapSyncStatus.Should().Be(SchoolEstablishmentBootstrapSyncUi.Synced);
        sut.CanRetryBootstrapSync.Should().BeFalse();
        sut.StatusMessage.Should().Contain("synchronisé");
    }

    [Fact]
    public void CopyPayload_CopiesDeepLink_NotSecret()
    {
        var sut = CreateSut();
        sut.ApplyQr(Qr(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            pending: false,
            SchoolEstablishmentBootstrapSyncUi.Synced));

        sut.CopyPayloadCommand.Execute(null);

        _dialogs.Received(1).SetClipboardText(Arg.Is<string>(s =>
            s.StartsWith("erp-scolaire://establish?token=", StringComparison.Ordinal)
            && !s.Contains("SecretHash", StringComparison.OrdinalIgnoreCase)));
        sut.StatusMessage.Should().Contain("copié");
    }

    [Fact]
    public void ApplyQr_PendingState_ShowsPendingLabel()
    {
        var sut = CreateSut();
        sut.ApplyQr(Qr(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            pending: true,
            SchoolEstablishmentBootstrapSyncUi.Pending,
            "En attente"));

        sut.BootstrapSyncStatusLabel.Should().Contain("attente");
        sut.CanRetryBootstrapSync.Should().BeTrue();
        sut.HasQr.Should().BeTrue();
        sut.QrPayload.Should().NotBeNullOrWhiteSpace();
    }
}
