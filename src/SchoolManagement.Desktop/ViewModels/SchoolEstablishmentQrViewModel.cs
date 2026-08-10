using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;
using SchoolManagement.Application.SchoolEstablishment;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// QR établissement (liaison téléphone↔école) — distinct de l'invitation parent.
/// Affiche le JWT/deep link public ; jamais le secret brut.
/// </summary>
public partial class SchoolEstablishmentQrViewModel : ViewModelBase
{
    private readonly ISchoolEstablishmentApiService _api;
    private readonly ISchoolApiService _schoolApi;
    private readonly IDesktopDialogs _dialogs;

    public SchoolEstablishmentQrViewModel(
        ISchoolEstablishmentApiService api,
        ISchoolApiService schoolApi,
        IDesktopDialogs dialogs)
    {
        _api = api;
        _schoolApi = schoolApi;
        _dialogs = dialogs;
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _schoolName = "Établissement";

    [ObservableProperty]
    private Guid _schoolId;

    [ObservableProperty]
    private Guid _credentialId;

    [ObservableProperty]
    private int _credentialVersion;

    [ObservableProperty]
    private BitmapImage? _qrImage;

    /// <summary>Deep link / payload QR (public). Jamais SecretHash.</summary>
    [ObservableProperty]
    private string? _qrPayload;

    [ObservableProperty]
    private bool _hasQr;

    [ObservableProperty]
    private bool _bootstrapSyncPending;

    [ObservableProperty]
    private string _bootstrapSyncStatus = SchoolEstablishmentBootstrapSyncUi.Pending;

    [ObservableProperty]
    private string? _bootstrapSyncMessage;

    public string BootstrapSyncStatusLabel => BootstrapSyncStatus switch
    {
        SchoolEstablishmentBootstrapSyncUi.Synced => "Synchronisé avec Bootstrap",
        SchoolEstablishmentBootstrapSyncUi.Failed => "Échec synchronisation Bootstrap",
        SchoolEstablishmentBootstrapSyncUi.Pending => "Synchronisation Bootstrap en attente",
        _ => BootstrapSyncStatus,
    };

    public string BootstrapSyncStatusBrushKey => BootstrapSyncStatus switch
    {
        SchoolEstablishmentBootstrapSyncUi.Synced => "Ok",
        SchoolEstablishmentBootstrapSyncUi.Failed => "Error",
        _ => "Warn",
    };

    public bool CanRetryBootstrapSync =>
        HasQr && BootstrapSyncPending
        && !string.Equals(BootstrapSyncStatus, SchoolEstablishmentBootstrapSyncUi.Synced, StringComparison.OrdinalIgnoreCase);

    partial void OnBootstrapSyncStatusChanged(string value)
    {
        OnPropertyChanged(nameof(BootstrapSyncStatusLabel));
        OnPropertyChanged(nameof(BootstrapSyncStatusBrushKey));
        OnPropertyChanged(nameof(CanRetryBootstrapSync));
    }

    partial void OnBootstrapSyncPendingChanged(bool value) =>
        OnPropertyChanged(nameof(CanRetryBootstrapSync));

    partial void OnHasQrChanged(bool value) =>
        OnPropertyChanged(nameof(CanRetryBootstrapSync));

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            await RefreshSchoolNameAsync();
            var dto = await _api.GetQrAsync();
            ApplyQr(dto);
            StatusMessage = HasQr
                ? "QR établissement chargé."
                : "Impossible d'afficher le QR.";
        }
        catch (Exception ex)
        {
            ClearQrVisual();
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RotateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var confirmed = _dialogs.ConfirmYesNo(
            "Régénérer le QR établissement ?\n\n"
            + "• L'ancien QR imprimé ne fonctionnera plus pour les nouvelles installations.\n"
            + "• Les téléphones déjà liés à cette école restent liés.\n\n"
            + "Continuer ?",
            "Confirmation — QR établissement");

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            // Afficher uniquement le nouveau QR après régénération.
            ClearQrVisual();
            var dto = await _api.RotateAsync("Régénération QR Desktop");
            ApplyQr(dto);
            StatusMessage = BootstrapSyncPending
                ? "Nouveau QR généré localement — publication Bootstrap en attente / échec. Utilisez « Réessayer la sync »."
                : "Nouveau QR généré et publié sur Bootstrap.";
        }
        catch (Exception ex)
        {
            ClearQrVisual();
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RetryBootstrapSyncAsync()
    {
        if (IsBusy || !CanRetryBootstrapSync)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.RetryBootstrapSyncAsync();
            if (result.Qr is not null)
            {
                ApplyQr(result.Qr);
            }
            else
            {
                BootstrapSyncPending = result.BootstrapSyncPending;
                BootstrapSyncStatus = result.BootstrapSyncStatus;
                BootstrapSyncMessage = result.Message;
            }

            StatusMessage = result.Success
                ? (result.Message ?? "Registre Bootstrap synchronisé.")
                : (result.Message ?? "Échec de synchronisation Bootstrap.");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CopyPayload()
    {
        if (string.IsNullOrWhiteSpace(QrPayload))
        {
            return;
        }

        _dialogs.SetClipboardText(QrPayload);
        StatusMessage = "Payload QR copié (deep link). Le secret brut n'est jamais exposé.";
    }

    [RelayCommand]
    private void PrintPreview()
    {
        if (!HasQr || QrImage is null)
        {
            StatusMessage = "Aucun QR à imprimer.";
            return;
        }

        try
        {
            var document = BuildPrintDocument();
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            document.PageHeight = dialog.PrintableAreaHeight;
            document.PageWidth = dialog.PrintableAreaWidth;
            document.PagePadding = new Thickness(48);
            document.ColumnWidth = dialog.PrintableAreaWidth;
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "QR établissement");
            StatusMessage = "Impression envoyée.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Impression impossible : {ex.Message}";
        }
    }

    /// <summary>Applique un DTO API (tests + runtime). N'expose jamais SecretHash.</summary>
    internal void ApplyQr(SchoolEstablishmentQrDto dto)
    {
        SchoolId = dto.SchoolId;
        CredentialId = dto.CredentialId;
        CredentialVersion = dto.CredentialVersion;
        QrPayload = string.IsNullOrWhiteSpace(dto.DeepLinkUri) ? dto.QrPayload : dto.DeepLinkUri;
        // Ne pas afficher dto.Token séparément comme « secret » — le payload QR suffit.
        QrImage = CreateQrBitmap(dto.QrPayload);
        HasQr = QrImage is not null && !string.IsNullOrWhiteSpace(QrPayload);
        BootstrapSyncPending = dto.BootstrapSyncPending;
        BootstrapSyncStatus = string.IsNullOrWhiteSpace(dto.BootstrapSyncStatus)
            ? SchoolEstablishmentBootstrapSyncUi.Pending
            : dto.BootstrapSyncStatus;
        BootstrapSyncMessage = dto.BootstrapSyncMessage;
    }

    internal FlowDocument BuildPrintDocument()
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
        };

        doc.Blocks.Add(new Paragraph(new Run("QR établissement"))
        {
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        });
        doc.Blocks.Add(new Paragraph(new Run(SchoolName))
        {
            FontSize = 18,
            Margin = new Thickness(0, 0, 0, 4),
        });
        doc.Blocks.Add(new Paragraph(new Run($"SchoolId : {SchoolId:D}"))
        {
            FontSize = 11,
            Foreground = Brushes.DimGray,
        });
        doc.Blocks.Add(new Paragraph(new Run($"Credential v{CredentialVersion} — {BootstrapSyncStatusLabel}"))
        {
            Margin = new Thickness(0, 0, 0, 16),
        });

        if (QrImage is not null)
        {
            var image = new System.Windows.Controls.Image
            {
                Source = QrImage,
                Width = 280,
                Height = 280,
                Stretch = Stretch.Uniform,
            };
            doc.Blocks.Add(new BlockUIContainer(image) { Margin = new Thickness(0, 0, 0, 16) });
        }

        if (!string.IsNullOrWhiteSpace(QrPayload))
        {
            doc.Blocks.Add(new Paragraph(new Run("Payload (deep link) :"))
            {
                FontWeight = FontWeights.SemiBold,
            });
            doc.Blocks.Add(new Paragraph(new Run(QrPayload))
            {
                FontSize = 10,
                TextAlignment = TextAlignment.Left,
            });
        }

        doc.Blocks.Add(new Paragraph(new Run(
            "Scannez ce QR avec l'application mobile pour rejoindre l'établissement. "
            + "Ne contient pas le secret brut du credential."))
        {
            Margin = new Thickness(0, 16, 0, 0),
            FontSize = 11,
            Foreground = Brushes.DimGray,
        });

        return doc;
    }

    private async Task RefreshSchoolNameAsync()
    {
        try
        {
            var school = await _schoolApi.GetCurrentSchoolAsync();
            if (school is not null && !string.IsNullOrWhiteSpace(school.Name))
            {
                SchoolName = school.Name;
                SchoolId = school.Id;
            }
        }
        catch
        {
            // garde le libellé par défaut
        }
    }

    private void ClearQrVisual()
    {
        HasQr = false;
        QrImage = null;
        QrPayload = null;
        CredentialId = Guid.Empty;
        CredentialVersion = 0;
        BootstrapSyncPending = false;
        BootstrapSyncStatus = SchoolEstablishmentBootstrapSyncUi.Pending;
        BootstrapSyncMessage = null;
    }

    private static BitmapImage? CreateQrBitmap(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

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
