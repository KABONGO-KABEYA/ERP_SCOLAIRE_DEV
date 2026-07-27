using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.DocumentBranding;

public static class DocumentBrandingLabels
{
    public static string GetDocumentTypeLabel(DocumentBrandingType type) => type switch
    {
        DocumentBrandingType.BulletinScolaire => "Bulletin scolaire",
        DocumentBrandingType.Recu => "Reçu",
        DocumentBrandingType.Attestation => "Attestation",
        DocumentBrandingType.Certificat => "Certificat",
        DocumentBrandingType.Diplome => "Diplôme",
        DocumentBrandingType.Lettre => "Lettre",
        DocumentBrandingType.CarteScolaire => "Carte scolaire",
        DocumentBrandingType.RelevePoints => "Relevé des points",
        DocumentBrandingType.Palmares => "Palmarès",
        DocumentBrandingType.FicheInscription => "Fiche d'inscription",
        DocumentBrandingType.RapportFinancier => "Rapport financier",
        DocumentBrandingType.Autre => "Autre",
        _ => type.ToString()
    };

    public static string GetPrintModeLabel(HeaderPrintMode mode) => mode switch
    {
        HeaderPrintMode.FullImage => "Utiliser une image complète",
        HeaderPrintMode.LogoOnly => "Utiliser uniquement le logo",
        _ => mode.ToString()
    };

    public static IReadOnlyList<DocumentBrandingType> AllDocumentTypes { get; } =
    [
        DocumentBrandingType.BulletinScolaire,
        DocumentBrandingType.Recu,
        DocumentBrandingType.Attestation,
        DocumentBrandingType.Certificat,
        DocumentBrandingType.Diplome,
        DocumentBrandingType.Lettre,
        DocumentBrandingType.CarteScolaire,
        DocumentBrandingType.RelevePoints,
        DocumentBrandingType.Palmares,
        DocumentBrandingType.FicheInscription,
        DocumentBrandingType.RapportFinancier,
        DocumentBrandingType.Autre
    ];
}
