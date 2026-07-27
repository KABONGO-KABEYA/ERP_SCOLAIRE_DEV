using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.StudentCards.DTOs;
using SchoolManagement.Desktop.Printing.CardLayout;

namespace SchoolManagement.Desktop.Services;

public interface IStudentCardPrintService
{
    Task PrintCardAsync(
        Guid cardId,
        CardPrintLayoutKind layout = CardPrintLayoutKind.Individual,
        CancellationToken cancellationToken = default);

    Task PrintCardsAsync(
        IReadOnlyList<Guid> cardIds,
        CardPrintLayoutKind layout = CardPrintLayoutKind.A4Sheet,
        int a4Rows = CardPrintDocumentFactory.A4DefaultRows,
        CancellationToken cancellationToken = default);

    Task PreviewCardAsync(Guid cardId, CancellationToken cancellationToken = default);

    FrameworkElement BuildPreviewVisual(CardTemplateDto template, CardRenderContext context, double zoom = 2.0);

    CardRenderContext BuildContext(
        StudentCardDetailDto detail,
        SchoolDto? school = null,
        DocumentBrandingConfigurationDto? branding = null);
}

public sealed class StudentCardPrintService : IStudentCardPrintService
{
    private readonly IStudentCardApiService _cardApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly IDocumentBrandingApiService _brandingApi;
    private readonly IStudentDossierPathResolver _dossierPathResolver;
    private readonly IDocumentBrandingPathResolver _brandingPathResolver;

    public StudentCardPrintService(
        IStudentCardApiService cardApi,
        ISchoolApiService schoolApi,
        IDocumentBrandingApiService brandingApi,
        IStudentDossierPathResolver dossierPathResolver,
        IDocumentBrandingPathResolver brandingPathResolver)
    {
        _cardApi = cardApi;
        _schoolApi = schoolApi;
        _brandingApi = brandingApi;
        _dossierPathResolver = dossierPathResolver;
        _brandingPathResolver = brandingPathResolver;
    }

    public async Task PrintCardAsync(
        Guid cardId,
        CardPrintLayoutKind layout = CardPrintLayoutKind.Individual,
        CancellationToken cancellationToken = default) =>
        await PrintCardsAsync([cardId], layout, CardPrintDocumentFactory.A4DefaultRows, cancellationToken);

    public async Task PrintCardsAsync(
        IReadOnlyList<Guid> cardIds,
        CardPrintLayoutKind layout = CardPrintLayoutKind.A4Sheet,
        int a4Rows = CardPrintDocumentFactory.A4DefaultRows,
        CancellationToken cancellationToken = default)
    {
        if (cardIds.Count == 0)
            throw new InvalidOperationException("Aucune carte à imprimer.");

        var school = await _schoolApi.GetCurrentSchoolAsync(cancellationToken);
        var branding = await TryGetBrandingAsync(cancellationToken);
        var templates = await _cardApi.ListTemplatesAsync(activeOnly: false, cancellationToken);
        var pairs = new List<CardPrintPair>();

        foreach (var id in cardIds)
        {
            var detail = await _cardApi.GetByIdAsync(id, cancellationToken);
            var template = templates.FirstOrDefault(t => t.Id == detail.TemplateId)
                ?? throw new InvalidOperationException($"Modèle introuvable pour la carte {detail.CardNumber}.");

            var context = BuildContext(detail, school, branding);
            var widthMm = (double)template.WidthMm;
            var heightMm = (double)template.HeightMm;

            var frontLayout = CardLayoutSerializer.Deserialize(template.LayoutJsonFront, widthMm, heightMm);
            var frontVisual = CardVisualRenderer.Build(frontLayout, context, zoom: 1.0);
            var front = new CardPrintSideVisual(frontVisual, widthMm, heightMm, detail.CardNumber, IsBack: false);

            CardPrintSideVisual? back = null;
            if (!string.IsNullOrWhiteSpace(template.LayoutJsonBack))
            {
                var backLayout = CardLayoutSerializer.Deserialize(template.LayoutJsonBack, widthMm, heightMm);
                var backVisual = CardVisualRenderer.Build(backLayout, context, zoom: 1.0);
                back = new CardPrintSideVisual(backVisual, widthMm, heightMm, detail.CardNumber, IsBack: true);
            }

            pairs.Add(new CardPrintPair(front, back));
        }

        var printed = false;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var document = CardPrintDocumentFactory.Build(pairs, layout, a4Rows);
            var dialog = new PrintDialog();

            if (layout == CardPrintLayoutKind.A4Sheet)
            {
                try
                {
                    dialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
                    dialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
                }
                catch
                {
                    // Certaines imprimantes ignorent le ticket — le FixedDocument reste en A4.
                }
            }

            if (dialog.ShowDialog() != true)
                return;

            dialog.PrintDocument(document.DocumentPaginator, $"Cartes élèves ({pairs.Count})");
            printed = true;
        });

        if (!printed)
            return;

        await _cardApi.PrintAsync(
            new PrintStudentCardsRequest(
                CardIds: cardIds.ToList(),
                Reason: layout == CardPrintLayoutKind.A4Sheet
                    ? $"Impression planche A4 {CardPrintDocumentFactory.A4Columns}x{a4Rows}"
                    : "Impression graphique unitaire"),
            cancellationToken);
    }

    public async Task PreviewCardAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        var detail = await _cardApi.GetByIdAsync(cardId, cancellationToken);
        var templates = await _cardApi.ListTemplatesAsync(activeOnly: false, cancellationToken);
        var template = templates.FirstOrDefault(t => t.Id == detail.TemplateId)
            ?? throw new InvalidOperationException("Modèle de carte introuvable.");
        var school = await _schoolApi.GetCurrentSchoolAsync(cancellationToken);
        var branding = await TryGetBrandingAsync(cancellationToken);
        var context = BuildContext(detail, school, branding);

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var front = BuildPreviewVisual(template, context, zoom: 2.5);
            FrameworkElement content = front;
            if (!string.IsNullOrWhiteSpace(template.LayoutJsonBack))
            {
                var backLayout = CardLayoutSerializer.Deserialize(
                    template.LayoutJsonBack,
                    (double)template.WidthMm,
                    (double)template.HeightMm);
                var back = CardVisualRenderer.Build(backLayout, context, zoom: 2.5);
                content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Children =
                    {
                        new TextBlock { Text = "Recto", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) },
                        front,
                        new TextBlock { Text = "Verso", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 8) },
                        back
                    }
                };
            }

            var window = new Window
            {
                Title = $"Aperçu — {detail.CardNumber}",
                Width = Math.Max(520, front.Width + 80),
                Height = Math.Max(420, front.Height + 160),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow,
                Content = new ScrollViewer
                {
                    Content = new Border
                    {
                        Padding = new Thickness(24),
                        Child = content,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
            window.ShowDialog();
        });
    }

    public FrameworkElement BuildPreviewVisual(CardTemplateDto template, CardRenderContext context, double zoom = 2.0)
    {
        var layout = CardLayoutSerializer.Deserialize(
            template.LayoutJsonFront,
            (double)template.WidthMm,
            (double)template.HeightMm);
        return CardVisualRenderer.Build(layout, context, zoom);
    }

    public CardRenderContext BuildContext(
        StudentCardDetailDto detail,
        SchoolDto? school = null,
        DocumentBrandingConfigurationDto? branding = null)
    {
        var footer = branding?.Footer;
        var logo = branding?.Logos.FirstOrDefault(l => l.IsPrimary && l.IsActive)
            ?? branding?.Logos.FirstOrDefault(l => l.IsActive);

        var address = FirstNonEmpty(
            footer?.Address,
            FormatSchoolAddress(school));

        return new CardRenderContext
        {
            FullName = detail.StudentFullName,
            LastName = detail.StudentLastName,
            FirstName = detail.StudentFirstName,
            MiddleName = detail.StudentMiddleName,
            Gender = detail.GenderLabel,
            DateOfBirth = detail.DateOfBirth,
            ClassName = detail.ClassName,
            StudyOption = detail.StudyOption,
            RegistrationNumber = detail.RegistrationNumber,
            CardNumber = detail.CardNumber,
            QrPayload = detail.QrPayload,
            SchoolName = school?.Name ?? "Établissement",
            Motto = FirstNonEmpty(footer?.SchoolMotto, "Savoir · Discipline · Excellence"),
            Address = address,
            Phone = FirstNonEmpty(footer?.Phone, school?.Phone),
            Email = FirstNonEmpty(footer?.Email, school?.Email),
            Website = footer?.Website,
            AcademicYear = detail.AcademicYearLabel,
            ExpiresAt = detail.ExpiresAt?.ToLocalTime().ToString("dd/MM/yyyy"),
            PhotoAbsolutePath = ResolvePhoto(detail.StudentPhotoPath),
            LogoAbsolutePath = ResolveLogo(logo?.ImagePath)
        };
    }

    private async Task<DocumentBrandingConfigurationDto?> TryGetBrandingAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _brandingApi.GetConfigurationAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private string? ResolvePhoto(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
            return null;
        try
        {
            return _dossierPathResolver.ResolveAbsolutePath(photoPath);
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveLogo(string? relativePath)
    {
        try
        {
            var fromBranding = _brandingPathResolver.ResolveAbsolutePath(relativePath);
            if (!string.IsNullOrWhiteSpace(fromBranding) && File.Exists(fromBranding))
                return fromBranding;

            var root = _brandingPathResolver.DocumentsRoot;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return null;

            var logos = Path.Combine(root, "Logos");
            if (!Directory.Exists(logos))
                return null;

            return Directory.EnumerateFiles(logos, "*.*", SearchOption.AllDirectories)
                .FirstOrDefault(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                     || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                     || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static string? FormatSchoolAddress(SchoolDto? school)
    {
        if (school is null) return null;
        var parts = new[] { school.Address, school.City, school.Province }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
