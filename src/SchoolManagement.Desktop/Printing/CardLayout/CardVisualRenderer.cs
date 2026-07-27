using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using QRCoder;

namespace SchoolManagement.Desktop.Printing.CardLayout;

/// <summary>Construit un visuel WPF à partir d'un layout JSON + contexte données.</summary>
public static class CardVisualRenderer
{
    public static FrameworkElement Build(
        CardLayoutDocument layout,
        CardRenderContext context,
        double zoom = 1.0)
    {
        var width = CardLayoutUnits.MmToDip(layout.WidthMm) * zoom;
        var height = CardLayoutUnits.MmToDip(layout.HeightMm) * zoom;

        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = ParseBrush(layout.BackgroundColor, Brushes.White),
            ClipToBounds = true
        };

        if (!string.IsNullOrWhiteSpace(layout.BackgroundImagePath) && File.Exists(layout.BackgroundImagePath))
        {
            canvas.Children.Add(new Image
            {
                Source = LoadBitmap(layout.BackgroundImagePath),
                Width = width,
                Height = height,
                Stretch = Stretch.UniformToFill,
                Opacity = 0.25
            });
        }

        foreach (var element in layout.Elements.Where(e => e.Visible).OrderBy(e => e.ZIndex))
        {
            var visual = CreateElement(element, context, zoom);
            if (visual is null) continue;

            ApplyOpacity(visual, element.Opacity);
            Canvas.SetLeft(visual, CardLayoutUnits.MmToDip(element.X) * zoom);
            Canvas.SetTop(visual, CardLayoutUnits.MmToDip(element.Y) * zoom);
            Panel.SetZIndex(visual, element.ZIndex);

            if (Math.Abs(element.Rotation) > 0.01)
            {
                visual.RenderTransformOrigin = new Point(0.5, 0.5);
                visual.RenderTransform = new RotateTransform(element.Rotation);
            }

            canvas.Children.Add(visual);
        }

        return canvas;
    }

    private static FrameworkElement? CreateElement(CardLayoutElement element, CardRenderContext context, double zoom)
    {
        var w = Math.Max(1, CardLayoutUnits.MmToDip(element.Width) * zoom);
        var h = Math.Max(1, CardLayoutUnits.MmToDip(element.Height) * zoom);

        return element.Kind switch
        {
            CardElementKind.Text => CreateText(element, context, w, h, zoom),
            CardElementKind.Photo => CreateImageBox(context.PhotoAbsolutePath, w, h, element, zoom),
            CardElementKind.Logo => CreateImageBox(
                !string.IsNullOrWhiteSpace(element.ImagePath) && File.Exists(element.ImagePath)
                    ? element.ImagePath
                    : context.LogoAbsolutePath,
                w, h, element, zoom),
            CardElementKind.Image => CreateImageBox(element.ImagePath, w, h, element, zoom),
            CardElementKind.QrCode => CreateQr(context.QrPayload, w, h),
            CardElementKind.Rectangle => CreateRectangle(element, w, h, zoom),
            CardElementKind.Ellipse => CreateEllipse(element, w, h),
            CardElementKind.Line => CreateLine(element, w, h),
            CardElementKind.Barcode => CreateBarcodePlaceholder(context.CardNumber, w, h, element, zoom),
            _ => null
        };
    }

    private static FrameworkElement CreateBarcodePlaceholder(
        string cardNumber,
        double w,
        double h,
        CardLayoutElement element,
        double zoom)
    {
        var copy = new CardLayoutElement
        {
            Kind = CardElementKind.Text,
            Text = cardNumber,
            DataField = CardDataField.CardNumber,
            FontFamily = element.FontFamily,
            FontSizePt = element.FontSizePt,
            Bold = true,
            Foreground = element.Foreground,
            Background = element.Background,
            HorizontalAlignment = "Center"
        };
        return CreateText(copy, new CardRenderContext { CardNumber = cardNumber }, w, h, zoom);
    }

    private static FrameworkElement CreateText(
        CardLayoutElement element,
        CardRenderContext context,
        double w,
        double h,
        double zoom)
    {
        var value = CardLayoutSerializer.ResolveField(context, element.DataField, element.Text);
        var align = element.HorizontalAlignment?.ToLowerInvariant() switch
        {
            "center" => TextAlignment.Center,
            "right" => TextAlignment.Right,
            _ => TextAlignment.Left
        };

        return new TextBlock
        {
            Text = value,
            Width = w,
            Height = h,
            FontFamily = new FontFamily(element.FontFamily),
            FontSize = Math.Max(6, element.FontSizePt * zoom * (96.0 / 72.0)),
            FontWeight = element.Bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = ParseBrush(element.Foreground, Brushes.Black),
            Background = ParseBrush(element.Background, Brushes.Transparent),
            TextAlignment = align,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(1)
        };
    }

    private static FrameworkElement CreateImageBox(
        string? path,
        double w,
        double h,
        CardLayoutElement element,
        double zoom)
    {
        var isLogo = element.Kind is CardElementKind.Logo or CardElementKind.Image;
        var radius = CardLayoutUnits.MmToDip(Math.Max(0, element.CornerRadiusMm)) * zoom;
        var hasExplicitBg = !string.IsNullOrWhiteSpace(element.Background)
            && !element.Background.Equals("Transparent", StringComparison.OrdinalIgnoreCase);

        var border = new Border
        {
            Width = w,
            Height = h,
            BorderBrush = element.BorderThickness > 0
                ? ParseBrush(element.BorderColor, Brushes.Transparent)
                : Brushes.Transparent,
            BorderThickness = new Thickness(Math.Max(0, element.BorderThickness)),
            // Logo / image : jamais de fond gris par défaut — uniquement si demandé.
            Background = hasExplicitBg
                ? ParseBrush(element.Background, Brushes.Transparent)
                : isLogo
                    ? Brushes.Transparent
                    : ParseBrush(element.Background, Brushes.WhiteSmoke),
            CornerRadius = new CornerRadius(isLogo ? Math.Max(0, radius) : Math.Max(2, radius)),
            ClipToBounds = true
        };

        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            border.Child = new Image
            {
                Source = LoadBitmap(path),
                Stretch = isLogo ? Stretch.Uniform : Stretch.UniformToFill
            };
        }
        else
        {
            border.Child = new TextBlock
            {
                Text = element.Kind == CardElementKind.Photo ? "PHOTO" : "LOGO",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isLogo
                    ? new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
                    : Brushes.Gray,
                FontSize = Math.Max(8, 9 * zoom)
            };
        }

        return border;
    }

    private static FrameworkElement CreateQr(string payload, double w, double h)
    {
        // Marge de sécurité (quiet zone) autour du QR.
        var pad = Math.Max(2, Math.Min(w, h) * 0.08);
        var size = (int)Math.Max(32, Math.Min(w, h) - pad * 2);
        return new Border
        {
            Width = w,
            Height = h,
            Background = Brushes.White,
            Padding = new Thickness(pad),
            Child = new Image
            {
                Stretch = Stretch.Uniform,
                Source = CreateQrBitmap(string.IsNullOrWhiteSpace(payload) ? "ERP_CARD:PREVIEW" : payload, size)
            }
        };
    }

    private static FrameworkElement CreateRectangle(CardLayoutElement element, double w, double h, double zoom)
    {
        var fill = CreateFillBrush(element);
        var radius = CardLayoutUnits.MmToDip(Math.Max(0, element.CornerRadiusMm)) * zoom;
        if (radius > 0.5)
        {
            return new Border
            {
                Width = w,
                Height = h,
                Background = fill,
                BorderBrush = ParseBrush(element.BorderColor, Brushes.Transparent),
                BorderThickness = new Thickness(Math.Max(0, element.BorderThickness)),
                CornerRadius = new CornerRadius(radius)
            };
        }

        return new System.Windows.Shapes.Rectangle
        {
            Width = w,
            Height = h,
            Fill = fill,
            Stroke = ParseBrush(element.BorderColor, Brushes.Transparent),
            StrokeThickness = element.BorderThickness
        };
    }

    private static FrameworkElement CreateEllipse(CardLayoutElement element, double w, double h) =>
        new Ellipse
        {
            Width = w,
            Height = h,
            Fill = CreateFillBrush(element),
            Stroke = ParseBrush(element.BorderColor, Brushes.Gray),
            StrokeThickness = Math.Max(0.5, element.BorderThickness)
        };

    private static FrameworkElement CreateLine(CardLayoutElement element, double w, double h) =>
        new Line
        {
            X1 = 0,
            Y1 = h / 2,
            X2 = w,
            Y2 = h / 2,
            Stroke = ParseBrush(element.Foreground, Brushes.Black),
            StrokeThickness = Math.Max(0.5, element.BorderThickness > 0 ? element.BorderThickness : 1)
        };

    private static Brush CreateFillBrush(CardLayoutElement element)
    {
        if (!string.IsNullOrWhiteSpace(element.GradientTo) &&
            !string.IsNullOrWhiteSpace(element.Background) &&
            !element.Background.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var start = (Color)ColorConverter.ConvertFromString(element.Background)!;
                var end = (Color)ColorConverter.ConvertFromString(element.GradientTo)!;
                return new LinearGradientBrush(start, end, element.GradientVertical ? 90 : 0);
            }
            catch
            {
                // fallback solid
            }
        }

        return ParseBrush(element.Background, Brushes.Transparent);
    }

    private static void ApplyOpacity(FrameworkElement visual, double opacity)
    {
        if (opacity is >= 0 and < 0.999)
            visual.Opacity = Math.Clamp(opacity, 0, 1);
    }

    private static BitmapSource? CreateQrBitmap(string content, int pixels)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(data);
            var png = qrCode.GetGraphic(pixels < 80 ? 4 : 6);
            using var stream = new MemoryStream(png);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? LoadBitmap(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static Brush ParseBrush(string? color, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(color) || color.Equals("Transparent", StringComparison.OrdinalIgnoreCase))
            return Brushes.Transparent;

        try
        {
            return (Brush)new BrushConverter().ConvertFromString(color)!;
        }
        catch
        {
            return fallback;
        }
    }
}
