using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.Printing;

public static class ConfiguredDocumentPreview
{
    public static bool Show(IDocumentPaginatorSource document, string title, DocumentBrandingType type)
    {
        var printed = false;
        var loading = new Window { Title = "Préparation du document", Width = 400, Height = 130,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = UI.ErpFileDialog.ResolveOwnerWindow(),
            Content = new TextBlock { Text = "Chargement de l’en-tête…", Margin = new Thickness(24) } };
        loading.Loaded += async (_, _) =>
        {
            try
            {
                var services = App.Services ?? throw new InvalidOperationException("Application indisponible.");
                var config = await services.GetRequiredService<IDocumentBrandingApiService>().GetConfigurationAsync();
                if (!loading.IsVisible) return;
                var header = config.Headers.FirstOrDefault(h => h.IsActive && h.ApplicableDocumentTypes.Contains(type));
                var configured = Apply(document, header, config.Logos, services.GetRequiredService<IDocumentBrandingPathResolver>());
                printed = DocumentPreview.Show(configured, title);
            }
            catch (Exception ex) { MessageBox.Show(loading, ex.Message, "En-tête du document", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { loading.Close(); }
        };
        loading.ShowDialog();
        return printed;
    }

    public static IDocumentPaginatorSource Apply(IDocumentPaginatorSource source, SchoolDocumentHeaderDto? header,
        IReadOnlyList<SchoolLogoDto> logos, IDocumentBrandingPathResolver resolver)
    {
        if (header is null) return source;
        var relative = header.PrintMode == HeaderPrintMode.FullImage ? header.ImagePath
            : (logos.FirstOrDefault(l => l.IsActive && l.IsPrimary) ?? logos.FirstOrDefault(l => l.IsActive))?.ImagePath;
        var path = resolver.ResolveAbsolutePath(relative);
        if (path is null) throw new InvalidOperationException("L’image de l’en-tête sélectionné est introuvable. Vérifiez le dossier des documents de ce poste.");
        var bitmap = new BitmapImage();
        using (var stream = File.OpenRead(path)) { bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); }
        var height = (double)Math.Clamp(header.MaxHeightMm ?? 20, 8, 60) * 96 / 25.4;
        var left = (double)Math.Clamp(header.MarginLeftMm, 0, 40) * 96 / 25.4;
        var right = (double)Math.Clamp(header.MarginRightMm, 0, 40) * 96 / 25.4;
        var stretch = header.PrintMode == HeaderPrintMode.FullImage ? Stretch.Fill : Stretch.Uniform;
        if (source is FlowDocument flow)
        {
            var width = (double.IsNaN(flow.PageWidth) ? 793.7 : flow.PageWidth) - flow.PagePadding.Left - flow.PagePadding.Right;
            var block = new BlockUIContainer(new Image { Source = bitmap, Height = height,
                Width = Math.Max(20, width - left - right), Stretch = stretch, HorizontalAlignment = HorizontalAlignment.Left })
                { Margin = new Thickness(left, 0, right, 8) };
            if (flow.Blocks.FirstBlock is { } first) flow.Blocks.InsertBefore(first, block); else flow.Blocks.Add(block);
            return flow;
        }
        var paginator = source.DocumentPaginator;
        paginator.ComputePageCount();
        var result = new FixedDocument();
        for (var i = 0; i < paginator.PageCount; i++)
        {
            var original = paginator.GetPage(i);
            var page = new FixedPage { Width = original.Size.Width, Height = original.Size.Height };
            var bandHeight = Math.Min(height, page.Height / 3);
            var image = new Image { Source = bitmap, Width = Math.Max(20, page.Width - left - right), Height = bandHeight, Stretch = stretch };
            FixedPage.SetLeft(image, left); page.Children.Add(image);
            var body = new System.Windows.Shapes.Rectangle { Width = page.Width, Height = page.Height - bandHeight - 8,
                Fill = new VisualBrush(original.Visual) { Stretch = Stretch.Uniform, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(original.Size) } };
            FixedPage.SetTop(body, bandHeight + 8); page.Children.Add(body);
            page.Measure(original.Size); page.Arrange(new Rect(original.Size)); page.UpdateLayout();
            result.Pages.Add(new PageContent { Child = page });
        }
        return result;
    }
}
