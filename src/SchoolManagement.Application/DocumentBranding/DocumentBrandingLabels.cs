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
        DocumentBrandingType.SituationPaiements => "Situation des paiements",
        DocumentBrandingType.RecettesRealisees => "Recettes réalisées",
        DocumentBrandingType.RepartitionRecettes => "Répartition des recettes",
        DocumentBrandingType.ConfigurationRetenues => "Configuration des retenues",
        DocumentBrandingType.ListeEleves => "Liste des élèves",
        DocumentBrandingType.ResultatsClasse => "Résultats par classe",
        DocumentBrandingType.ResultatIndividuel => "Résultat individuel",
        DocumentBrandingType.ValidationResultats => "Validation des résultats",
        DocumentBrandingType.Deliberation => "Délibération",
        DocumentBrandingType.NotesCours => "Récapitulatif des notes par cours",
        DocumentBrandingType.FichePedagogique => "Fiche pédagogique / vue globale",
        DocumentBrandingType.ListePersonnel => "Liste du personnel",
        DocumentBrandingType.FichePersonnel => "Fiche du personnel",
        DocumentBrandingType.ListeDepenses => "Liste des dépenses",
        DocumentBrandingType.QrEtablissement => "QR de l’établissement",
        DocumentBrandingType.AvisParents => "Avis aux parents",
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

        DocumentBrandingType.CarteScolaire,
        DocumentBrandingType.RelevePoints,
        DocumentBrandingType.Palmares,
        DocumentBrandingType.FicheInscription,
        DocumentBrandingType.SituationPaiements,
        DocumentBrandingType.RecettesRealisees,
        DocumentBrandingType.RepartitionRecettes,
        DocumentBrandingType.ConfigurationRetenues,
        DocumentBrandingType.ListeEleves,
        DocumentBrandingType.ResultatsClasse,
        DocumentBrandingType.ResultatIndividuel,
        DocumentBrandingType.ValidationResultats,
        DocumentBrandingType.Deliberation,
        DocumentBrandingType.NotesCours,
        DocumentBrandingType.FichePedagogique,
        DocumentBrandingType.ListePersonnel,
        DocumentBrandingType.FichePersonnel,
        DocumentBrandingType.ListeDepenses,
        DocumentBrandingType.QrEtablissement,
        DocumentBrandingType.AvisParents,
        DocumentBrandingType.Autre
    ];
}
