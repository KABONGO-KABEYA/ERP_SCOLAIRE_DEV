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

public enum ClassCouncilDecision
{
    EnAttente = 1,
    Admis = 2,
    Ajourne = 3,
    Exclu = 4
}
