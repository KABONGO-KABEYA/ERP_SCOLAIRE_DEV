namespace SchoolManagement.Domain.Enums;

/// <summary>
/// Cycles du système éducatif congolais (héritage / regroupement).
/// </summary>
public enum EducationCycle
{
    Primaire = 1,
    Secondaire = 2
}

/// <summary>
/// Programmes officiels de la structure pédagogique RDC.
/// </summary>
public enum SchoolProgram
{
    Maternelle = 1,
    Primaire = 2,
    CTEB = 3,
    Humanites = 4,
    HumanitesProfessionnelles = 5,
    FilieresSpecialisees = 6
}

public enum Gender
{
    Masculin = 1,
    Feminin = 2
}

public enum Currency
{
    CDF = 1,
    USD = 2
}

public enum EnrollmentStatus
{
    PreInscription = 1,
    Inscrit = 2,
    Reinscrit = 3,
    Transfere = 4,
    Abandon = 5,
    Exclusion = 6,
    Archive = 7
}

/// <summary>
/// Type d'inscription saisi dans l'assistant (RDC).
/// </summary>
public enum RegistrationKind
{
    NouvelleInscription = 1,
    Reinscription = 2,
    Transfert = 3,
    RetourApresAbandon = 4
}

/// <summary>Type de document pour l'en-tête graphique.</summary>
public enum DocumentBrandingType
{
    BulletinScolaire = 1,
    Recu = 2,
    Attestation = 3,
    Certificat = 4,
    Diplome = 5,
    Lettre = 6,
    CarteScolaire = 7,
    RelevePoints = 8,
    Palmares = 9,
    FicheInscription = 10,
    RapportFinancier = 11,
    Autre = 99
}

/// <summary>Mode d'impression de l'en-tête.</summary>
public enum HeaderPrintMode
{
    FullImage = 1,
    LogoOnly = 2
}

public enum UserRole
{
    Administrateur = 1,
    Direction = 2,
    Enseignant = 3,
    Parent = 4,
    Comptable = 5
}

public enum PermissionAction
{
    Read = 1,
    Create = 2,
    Update = 3,
    Delete = 4,
    Export = 5,
    Approve = 6,
    Print = 7,
    Renew = 8
}

/// <summary>Effet d'une exception de permission utilisateur.</summary>
public enum PermissionExceptionEffect
{
    Grant = 1,
    Deny = 2
}

/// <summary>Type d'acteur pour le journal d'audit sécurité.</summary>
public enum SecurityAuditActorKind
{
    User = 1,
    SchoolAdmin = 2,
    PlatformSuperAdmin = 3,
    System = 4
}

public enum PaymentStatus
{
    EnAttente = 1,
    Complet = 2,
    Annule = 3,
    Rembourse = 4
}

/// <summary>Mode de calcul d'une ligne de clé de répartition des recettes.</summary>
public enum AllocationCalculationType
{
    Pourcentage = 1,
    MontantFixe = 2
}

/// <summary>Source d'une clé de répartition : type de frais ou type de retenue.</summary>
public enum RevenueAllocationSourceKind
{
    FeeType = 1,
    Withholding = 2
}

/// <summary>Mode de calcul d'une retenue sur encaissement.</summary>
public enum WithholdingCalculationMode
{
    Pourcentage = 1,
    MontantFixe = 2
}

/// <summary>Statut d'une demande de paiement comptable.</summary>
public enum ExpenseRequestStatus
{
    Brouillon = 1,
    Soumise = 2,
    Approuvee = 3,
    Payee = 4,
    Annulee = 5
}

public enum AcademicPeriodType
{
    Trimestre = 1,
    Semestre = 2
}

/// <summary>Groupe de structure pédagogique (trimestres vs semestres).</summary>
public enum PedagogicalCycleGroup
{
    /// <summary>Maternelle + Primaire — trimestres.</summary>
    MaternellePrimaire = 1,

    /// <summary>Secondaire (CTEB, Humanités…) — semestres.</summary>
    Secondaire = 2
}

/// <summary>Type de sous-période (travaux continus ou examen).</summary>
public enum AcademicSubPeriodKind
{
    Travail = 1,
    Examen = 2
}

/// <summary>État opérationnel d'une sous-période.</summary>
public enum AcademicSubPeriodStatus
{
    AVenir = 1,
    Ouverte = 2,
    Cloturee = 3,
    Verrouillee = 4
}

public enum ClassCouncilDecision
{
    EnAttente = 1,
    Admis = 2,
    Ajourne = 3,
    Exclu = 4
}

/// <summary>
/// Décision officielle de passage (fin d'année uniquement).
/// Les mentions honorifiques (Satisfaction…) ne sont PAS des décisions : elles viennent de ResultMentionDefinition.
/// Les valeurs 1–4 sont conservées pour compatibilité historique (anciennes saisies) mais ne sont plus proposées.
/// </summary>
public enum FinalCouncilDecision
{
    Satisfaction = 1,
    Distinction = 2,
    GrandeDistinction = 3,
    Elite = 4,
    PasseDeClasse = 5,
    Redouble = 6,
    PasseAilleurs = 7,
    Repechage = 8,
    Exclu = 9,
    Dispense = 10
}

/// <summary>Mode d'interface du conseil, dérivé automatiquement de la période pédagogique.</summary>
public enum DeliberationPeriodMode
{
    /// <summary>Périodes / examens intermédiaires — pas de décision de passage.</summary>
    Intermediate = 1,

    /// <summary>Examen de fin d'année primaire / maternelle.</summary>
    YearEndPrimary = 2,

    /// <summary>Examen de fin d'année secondaire (repêchage possible).</summary>
    YearEndSecondary = 3
}

/// <summary>Contexte de session d'évaluation (cotation normale vs 2ᵉ session).</summary>
public enum EvaluationSessionKind
{
    Normale = 1,
    DeuxiemeSession = 2
}

/// <summary>Statut d'un cours de repêchage (2ᵉ session).</summary>
public enum RemedialCourseStatus
{
    ACoter = 1
}

/// <summary>Statut administratif de validation des résultats d'une classe / sous-période.</summary>
public enum ResultValidationStatus
{
    NonValide = 1,
    Valide = 2,
    Verrouille = 3
}

/// <summary>Opérations historisées du module Validation des résultats.</summary>
public enum ResultValidationOperation
{
    CalculEffectue = 1,
    Validation = 2,
    Annulation = 3,
    Verrouillage = 4,
    Deverrouillage = 5
}
