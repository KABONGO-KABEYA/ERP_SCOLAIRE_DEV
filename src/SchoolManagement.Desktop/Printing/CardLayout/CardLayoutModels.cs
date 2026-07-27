using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoolManagement.Desktop.Printing.CardLayout;

/// <summary>Document de layout carte (recto ou verso) — stocké en JSON en base.</summary>
public sealed class CardLayoutDocument
{
    public int Version { get; set; } = 1;

    public double WidthMm { get; set; } = 85.6;

    public double HeightMm { get; set; } = 53.98;

    public string BackgroundColor { get; set; } = "#FFFFFF";

    public string? BackgroundImagePath { get; set; }

    public List<CardLayoutElement> Elements { get; set; } = [];
}

public enum CardElementKind
{
    Text = 1,
    Image = 2,
    Photo = 3,
    Logo = 4,
    QrCode = 5,
    Rectangle = 6,
    Ellipse = 7,
    Line = 8,
    Barcode = 9
}

/// <summary>Champ dynamique lié aux données élève / établissement.</summary>
public enum CardDataField
{
    None = 0,
    FullName = 1,
    LastName = 2,
    FirstName = 3,
    MiddleName = 4,
    Gender = 5,
    DateOfBirth = 6,
    ClassName = 7,
    StudyOption = 8,
    RegistrationNumber = 9,
    CardNumber = 10,
    SchoolName = 11,
    Motto = 12,
    Address = 13,
    Phone = 14,
    Email = 15,
    EmergencyContact = 16,
    AcademicYear = 17,
    ExpiresAt = 18,
    Website = 19
}

public sealed class CardLayoutElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public CardElementKind Kind { get; set; } = CardElementKind.Text;

    /// <summary>Position / taille en millimètres.</summary>
    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; } = 20;

    public double Height { get; set; } = 8;

    public double Rotation { get; set; }

    public bool Visible { get; set; } = true;

    public int ZIndex { get; set; }

    public string? Text { get; set; }

    public CardDataField DataField { get; set; } = CardDataField.None;

    public string FontFamily { get; set; } = "Segoe UI";

    public double FontSizePt { get; set; } = 9;

    public bool Bold { get; set; }

    public string Foreground { get; set; } = "#111827";

    public string Background { get; set; } = "Transparent";

    public string BorderColor { get; set; } = "#D1D5DB";

    public double BorderThickness { get; set; }

    /// <summary>Rayon d'angle en millimètres (photo, logo, rectangles).</summary>
    public double CornerRadiusMm { get; set; }

    /// <summary>Opacité 0–1 (filigrane, ombres).</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>Si renseigné avec Background, dégradé horizontal/vertical selon GradientVertical.</summary>
    public string? GradientTo { get; set; }

    public bool GradientVertical { get; set; } = true;

    public string? ImagePath { get; set; }

    public string HorizontalAlignment { get; set; } = "Left";
}

/// <summary>Données résolues pour le rendu / l'impression d'une carte.</summary>
public sealed class CardRenderContext
{
    public string FullName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string? ClassName { get; set; }
    public string? StudyOption { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public string QrPayload { get; set; } = string.Empty;
    public string SchoolName { get; set; } = string.Empty;
    public string? Motto { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? EmergencyContact { get; set; }
    public string AcademicYear { get; set; } = string.Empty;
    public string? ExpiresAt { get; set; }
    public string? PhotoAbsolutePath { get; set; }
    public string? LogoAbsolutePath { get; set; }
}

public static class CardLayoutSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(CardLayoutDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static CardLayoutDocument Deserialize(string? json, double widthMm, double heightMm)
    {
        if (string.IsNullOrWhiteSpace(json))
            return CardLayoutDefaults.CreateStandard(widthMm, heightMm);

        try
        {
            var doc = JsonSerializer.Deserialize<CardLayoutDocument>(json, Options);
            if (doc is null || doc.Elements.Count == 0)
                return CardLayoutDefaults.CreateStandard(widthMm, heightMm);

            doc.WidthMm = widthMm > 0 ? widthMm : doc.WidthMm;
            doc.HeightMm = heightMm > 0 ? heightMm : doc.HeightMm;
            return doc;
        }
        catch
        {
            return CardLayoutDefaults.CreateStandard(widthMm, heightMm);
        }
    }

    public static string ResolveField(CardRenderContext ctx, CardDataField field, string? fallbackText) =>
        field switch
        {
            CardDataField.FullName => ctx.FullName,
            CardDataField.LastName => ctx.LastName,
            CardDataField.FirstName => ctx.FirstName,
            CardDataField.MiddleName => ctx.MiddleName ?? string.Empty,
            CardDataField.Gender => ctx.Gender,
            CardDataField.DateOfBirth => ctx.DateOfBirth,
            CardDataField.ClassName => ctx.ClassName ?? string.Empty,
            CardDataField.StudyOption => ctx.StudyOption ?? string.Empty,
            CardDataField.RegistrationNumber => ctx.RegistrationNumber,
            CardDataField.CardNumber => ctx.CardNumber,
            CardDataField.SchoolName => ctx.SchoolName,
            CardDataField.Motto => ctx.Motto ?? string.Empty,
            CardDataField.Address => ctx.Address ?? string.Empty,
            CardDataField.Phone => ctx.Phone ?? string.Empty,
            CardDataField.Email => ctx.Email ?? string.Empty,
            CardDataField.Website => ctx.Website ?? string.Empty,
            CardDataField.EmergencyContact => ctx.EmergencyContact ?? string.Empty,
            CardDataField.AcademicYear => ctx.AcademicYear,
            CardDataField.ExpiresAt => ctx.ExpiresAt ?? string.Empty,
            _ => fallbackText ?? string.Empty
        };
}

public static class CardLayoutDefaults
{
    public const string ColorBlueDark = "#0F2D5C";
    public const string ColorBlueLight = "#2D7FF9";
    public const string ColorWhite = "#FFFFFF";
    public const string ColorGrayBg = "#F5F7FA";
    public const string ColorGrayText = "#555555";
    public const string FontTitle = "Poppins, Segoe UI";
    public const string FontBody = "Inter, Segoe UI";

    /// <summary>Alias historique — modèle professionnel CR80 recto.</summary>
    public static CardLayoutDocument CreateStandard(double widthMm = 85.6, double heightMm = 53.98) =>
        CreateProfessionalFront(widthMm, heightMm);

    public static (CardLayoutDocument Front, CardLayoutDocument Back) CreateProfessionalPair(
        double widthMm = 85.6,
        double heightMm = 53.98) =>
        (CreateProfessionalFront(widthMm, heightMm), CreateProfessionalBack(widthMm, heightMm));

    /// <summary>Recto CR80 professionnel (point de départ « Nouveau modèle »).</summary>
    public static CardLayoutDocument CreateProfessionalFront(double widthMm = 85.6, double heightMm = 53.98)
    {
        const double margin = 2.5;
        var elements = new List<CardLayoutElement>
        {
            // Fond carte + bordure fine
            Rect(0.4, 0.4, widthMm - 0.8, heightMm - 0.8, ColorWhite, ColorBlueDark, 0.25, 2.1, z: 0),

            // En-tête dégradé
            Rect(0.4, 0.4, widthMm - 0.8, 11.2, ColorBlueDark, null, 0, 2.0, z: 1,
                gradientTo: ColorBlueLight, gradientVertical: false),

            // Logo (aucun fond — transparent sur l'en-tête)
            new CardLayoutElement
            {
                Kind = CardElementKind.Logo,
                X = margin, Y = 1.6, Width = 9, Height = 8.2,
                Background = "Transparent",
                BorderColor = "Transparent",
                BorderThickness = 0,
                CornerRadiusMm = 0,
                ZIndex = 3
            },

            // Nom établissement
            Text("Établissement", CardDataField.SchoolName,
                13, 1.8, widthMm - 16, 4.8,
                FontTitle, 9.5, bold: true, ColorWhite, "Left", z: 3),

            // Devise
            Text("Devise de l'école", CardDataField.Motto,
                13, 6.6, widthMm - 16, 3.5,
                FontBody, 6.5, bold: false, "#D6E4FF", "Left", z: 3),

            // Ligne sous en-tête
            new CardLayoutElement
            {
                Kind = CardElementKind.Line,
                X = margin, Y = 12.2, Width = widthMm - margin * 2, Height = 0.6,
                Foreground = ColorBlueLight, BorderThickness = 0.45, ZIndex = 2
            },

            // Zone photo
            new CardLayoutElement
            {
                Kind = CardElementKind.Photo,
                X = margin, Y = 14.2, Width = 20, Height = 26,
                Background = ColorGrayBg, BorderColor = "#CBD5E1", BorderThickness = 0.3,
                CornerRadiusMm = 1.5, ZIndex = 3
            },
        };

        // Infos élève (libellé + valeur)
        AddLabeledField(elements, "NOM", CardDataField.LastName, "KABONGO", 26.5, 14.2);
        AddLabeledField(elements, "POSTNOM", CardDataField.MiddleName, "KABEYA", 26.5, 19.0);
        AddLabeledField(elements, "PRÉNOM", CardDataField.FirstName, "CHRISTIAN", 26.5, 23.8);
        AddLabeledField(elements, "Matricule", CardDataField.RegistrationNumber, "2026-00125", 26.5, 29.0, labelWidth: 14);
        AddLabeledField(elements, "Classe", CardDataField.ClassName, "5e Scientifique", 26.5, 33.2, labelWidth: 14);
        AddLabeledField(elements, "Option", CardDataField.StudyOption, "Math-Physique", 26.5, 37.4, labelWidth: 14);

        // QR bas droite (marge de sécurité ~2 mm autour)
        elements.Add(Rect(66.5, 30.2, 16.5, 16.5, ColorWhite, "#E5E7EB", 0.2, 1.2, z: 2));
        elements.Add(new CardLayoutElement
        {
            Kind = CardElementKind.QrCode,
            X = 68.0, Y = 31.7, Width = 13.5, Height = 13.5, ZIndex = 3
        });

        // Numéro de carte bas gauche
        elements.Add(Text("Carte N° :", CardDataField.None,
            margin, 47.5, 14, 3.2, FontBody, 6.5, true, ColorGrayText, "Left", z: 3));
        elements.Add(Text("CSB-2026-000125", CardDataField.CardNumber,
            margin + 13.5, 47.5, 40, 3.2, FontBody, 7, true, ColorBlueDark, "Left", z: 3));

        return new CardLayoutDocument
        {
            Version = 1,
            WidthMm = widthMm,
            HeightMm = heightMm,
            BackgroundColor = ColorGrayBg,
            Elements = elements
        };
    }

    /// <summary>Verso CR80 — propriété, coordonnées, validité, signatures, filigrane.</summary>
    public static CardLayoutDocument CreateProfessionalBack(double widthMm = 85.6, double heightMm = 53.98)
    {
        const double margin = 3.0;
        var elements = new List<CardLayoutElement>
        {
            Rect(0.4, 0.4, widthMm - 0.8, heightMm - 0.8, ColorWhite, ColorBlueDark, 0.25, 2.1, z: 0),

            // Filigrane logo (arrière-plan)
            new CardLayoutElement
            {
                Kind = CardElementKind.Logo,
                X = (widthMm - 32) / 2, Y = (heightMm - 32) / 2, Width = 32, Height = 32,
                Background = "Transparent", BorderThickness = 0,
                Opacity = 0.08, ZIndex = 0
            },

            // Bandeau titre
            Rect(0.4, 0.4, widthMm - 0.8, 8.5, ColorBlueDark, null, 0, 2.0, z: 1,
                gradientTo: ColorBlueLight, gradientVertical: false),

            Text("CARTE D'ÉLÈVE", CardDataField.None,
                margin, 2.2, widthMm - margin * 2, 5,
                FontTitle, 10, true, ColorWhite, "Center", z: 2),

            Text("Cette carte est la propriété de l'établissement.", CardDataField.None,
                margin, 10.5, widthMm - margin * 2, 3.5,
                FontBody, 6.5, false, ColorGrayText, "Left", z: 2),

            Text("En cas de perte, veuillez la retourner à :", CardDataField.None,
                margin, 14.2, widthMm - margin * 2, 3.2,
                FontBody, 6.5, false, ColorGrayText, "Left", z: 2),

            Text("Nom de l'établissement", CardDataField.SchoolName,
                margin, 18.2, widthMm - margin * 2, 3.8,
                FontTitle, 8, true, ColorBlueDark, "Left", z: 2),

            Text("Adresse", CardDataField.Address,
                margin, 22.2, widthMm - margin * 2, 3.2,
                FontBody, 6.5, false, ColorGrayText, "Left", z: 2),

            Text("Téléphone", CardDataField.Phone,
                margin, 25.5, 38, 3.0,
                FontBody, 6.5, false, ColorGrayText, "Left", z: 2),

            Text("Email", CardDataField.Email,
                margin + 39, 25.5, 40, 3.0,
                FontBody, 6.5, false, ColorGrayText, "Left", z: 2),

            Text("Site web", CardDataField.Website,
                margin, 28.8, widthMm - margin * 2, 3.0,
                FontBody, 6.5, false, ColorGrayText, "Left", z: 2),

            Text("Valable jusqu'au :", CardDataField.None,
                margin, 33.5, 22, 3.2,
                FontBody, 6.5, true, ColorGrayText, "Left", z: 2),

            Text("31/08/2027", CardDataField.ExpiresAt,
                margin + 22, 33.5, 30, 3.2,
                FontBody, 7, true, ColorBlueDark, "Left", z: 2),

            // Zones signatures
            new CardLayoutElement
            {
                Kind = CardElementKind.Line,
                X = margin, Y = 43.5, Width = 28, Height = 0.5,
                Foreground = "#9CA3AF", BorderThickness = 0.35, ZIndex = 2
            },
            Text("Signature de l'élève", CardDataField.None,
                margin, 44.5, 28, 3.0,
                FontBody, 5.5, false, ColorGrayText, "Center", z: 2),

            new CardLayoutElement
            {
                Kind = CardElementKind.Line,
                X = widthMm - margin - 28, Y = 43.5, Width = 28, Height = 0.5,
                Foreground = "#9CA3AF", BorderThickness = 0.35, ZIndex = 2
            },
            Text("Signature Direction", CardDataField.None,
                widthMm - margin - 28, 44.5, 28, 3.0,
                FontBody, 5.5, false, ColorGrayText, "Center", z: 2),
        };

        return new CardLayoutDocument
        {
            Version = 1,
            WidthMm = widthMm,
            HeightMm = heightMm,
            BackgroundColor = ColorWhite,
            Elements = elements
        };
    }

    private static void AddLabeledField(
        List<CardLayoutElement> elements,
        string label,
        CardDataField field,
        string sample,
        double x,
        double y,
        double labelWidth = 16)
    {
        elements.Add(Text($"{label} :", CardDataField.None,
            x, y, labelWidth, 3.8, FontBody, 6.5, true, ColorGrayText, "Left", z: 3));
        elements.Add(Text(sample, field,
            x + labelWidth, y, 38, 3.8, FontBody, 7.5, false, ColorBlueDark, "Left", z: 3));
    }

    private static CardLayoutElement Text(
        string text,
        CardDataField field,
        double x,
        double y,
        double w,
        double h,
        string font,
        double sizePt,
        bool bold,
        string color,
        string align,
        int z) =>
        new()
        {
            Kind = CardElementKind.Text,
            Text = text,
            DataField = field,
            X = x, Y = y, Width = w, Height = h,
            FontFamily = font,
            FontSizePt = sizePt,
            Bold = bold,
            Foreground = color,
            HorizontalAlignment = align,
            ZIndex = z
        };

    private static CardLayoutElement Rect(
        double x,
        double y,
        double w,
        double h,
        string fill,
        string? border,
        double borderThickness,
        double cornerMm,
        int z,
        string? gradientTo = null,
        bool gradientVertical = true) =>
        new()
        {
            Kind = CardElementKind.Rectangle,
            X = x, Y = y, Width = w, Height = h,
            Background = fill,
            BorderColor = border ?? "Transparent",
            BorderThickness = borderThickness,
            CornerRadiusMm = cornerMm,
            GradientTo = gradientTo,
            GradientVertical = gradientVertical,
            ZIndex = z
        };
}

/// <summary>Conversion mm ↔ DIP (96 DPI).</summary>
public static class CardLayoutUnits
{
    public const double DipPerMm = 96.0 / 25.4;

    public static double MmToDip(double mm) => mm * DipPerMm;

    public static double DipToMm(double dip) => dip / DipPerMm;
}
