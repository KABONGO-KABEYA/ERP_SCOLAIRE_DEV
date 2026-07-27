using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.DocumentBranding;

/// <summary>
/// Compose l'en-tête imprimé : étiré de gauche à droite sur la largeur du contenu
/// (mêmes bords que les tableaux), avec marges additionnelles configurables.
/// </summary>
public static class DocumentPrintHeaderComposer
{
    private const float MmToPoints = 2.834645669f;
    private const float DefaultMaxHeightMm = 20f;

    /// <summary>
    /// Affiche l'image d'en-tête en pleine largeur (non centrée).
    /// Retourne true si l'image a été rendue.
    /// </summary>
    public static bool TryComposeFullWidthImage(
        IContainer container,
        DocumentPrintBrandingDto branding,
        Func<string?, byte[]?> loadImage)
    {
        if (branding.PrintMode != HeaderPrintMode.FullImage)
        {
            return false;
        }

        var headerBytes = loadImage(branding.HeaderImagePath) ?? loadImage(branding.PrimaryLogoPath);
        if (headerBytes is null)
        {
            return false;
        }

        var left = ClampMmToPoints(branding.HeaderMarginLeftMm);
        var right = ClampMmToPoints(branding.HeaderMarginRightMm);
        var heightMm = branding.HeaderMaxHeightMm is > 0
            ? (float)branding.HeaderMaxHeightMm.Value
            : DefaultMaxHeightMm;
        var height = Math.Clamp(heightMm, 8f, 60f) * MmToPoints;

        // Pas d'AlignCenter : l'image occupe toute la largeur utile (= tableaux).
        // FitUnproportionally = étirement gauche→droite dans la bande définie.
        container
            .PaddingLeft(left)
            .PaddingRight(right)
            .Height(height)
            .Image(headerBytes)
            .FitUnproportionally();

        return true;
    }

    private static float ClampMmToPoints(decimal mm)
    {
        var value = (float)mm;
        if (float.IsNaN(value) || value < 0)
        {
            return 0;
        }

        return Math.Min(value, 40f) * MmToPoints;
    }
}
