using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;
using SchoolManagement.Application.ParentActivation;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Émission QR / deep link d'activation parent (connexion mobile v2).</summary>
public partial class ParentActivationQrViewModel : ViewModelBase
{
    private readonly IParentActivationApiService _api;

    public ParentActivationQrViewModel(IParentActivationApiService api)
    {
        _api = api;
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _suggestedUserName;

    [ObservableProperty]
    private string _validityMinutesText = "60";

    [ObservableProperty]
    private BitmapImage? _qrImage;

    [ObservableProperty]
    private string? _deepLinkUri;

    [ObservableProperty]
    private string? _expiresAtText;

    [ObservableProperty]
    private bool _hasQr;

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var minutes = int.TryParse(ValidityMinutesText.Trim(), out var m) ? m : 60;
            if (minutes is < 5 or > 120)
            {
                StatusMessage = "Durée de validité : entre 5 et 120 minutes.";
                return;
            }

            var result = await _api.IssueTokenAsync(
                new IssueParentActivationTokenRequest(
                    string.IsNullOrWhiteSpace(SuggestedUserName) ? null : SuggestedUserName.Trim(),
                    minutes));

            DeepLinkUri = result.DeepLinkUri;
            ExpiresAtText = result.ExpiresAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            QrImage = CreateQrBitmap(result.QrPayload);
            HasQr = QrImage is not null;
            StatusMessage = HasQr
                ? "QR généré. Le parent scanne avec l'app (Activer avec QR code)."
                : "Token émis, mais le rendu QR a échoué — copiez le lien ci-dessous.";
        }
        catch (Exception ex)
        {
            HasQr = false;
            QrImage = null;
            DeepLinkUri = null;
            ExpiresAtText = null;
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CopyDeepLink()
    {
        if (string.IsNullOrWhiteSpace(DeepLinkUri))
        {
            return;
        }

        Clipboard.SetText(DeepLinkUri);
        StatusMessage = "Lien copié dans le presse-papiers.";
    }

    private static BitmapImage? CreateQrBitmap(string content)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data).GetGraphic(8);
            using var stream = new MemoryStream(png);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
