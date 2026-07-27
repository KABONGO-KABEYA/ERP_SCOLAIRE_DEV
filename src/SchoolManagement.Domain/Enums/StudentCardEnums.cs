namespace SchoolManagement.Domain.Enums;

/// <summary>Cycle de vie d'une carte élève.</summary>
public enum StudentCardStatus
{
    Brouillon = 1,
    Active = 2,
    Suspendue = 3,
    Expiree = 4,
    Perdue = 5,
    Volee = 6,
    Remplacee = 7,
    Desactivee = 8
}

/// <summary>Actions historisées sur une carte.</summary>
public enum StudentCardHistoryAction
{
    Creation = 1,
    Modification = 2,
    Impression = 3,
    Reimpression = 4,
    Renouvellement = 5,
    Desactivation = 6,
    Perte = 7,
    Vol = 8,
    Remplacement = 9,
    SuppressionLogique = 10,
    Activation = 11,
    Suspension = 12
}

/// <summary>Orientation physique du modèle de carte.</summary>
public enum CardTemplateOrientation
{
    Portrait = 1,
    Landscape = 2
}

/// <summary>Type de porteur cible du modèle.</summary>
public enum CardTemplateKind
{
    Eleve = 1,
    Enseignant = 2,
    Personnel = 3
}
