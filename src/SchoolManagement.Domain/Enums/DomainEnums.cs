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
    Approve = 6
}

public enum EvaluationType
{
    Devoir = 1,
    Interrogation = 2,
    Examen = 3,
    Composition = 4
}

public enum PaymentStatus
{
    EnAttente = 1,
    Complet = 2,
    Annule = 3,
    Rembourse = 4
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
