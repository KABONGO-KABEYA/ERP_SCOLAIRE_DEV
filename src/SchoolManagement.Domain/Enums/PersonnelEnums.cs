namespace SchoolManagement.Domain.Enums;

public enum PersonnelCategory
{
    Enseignant = 1,
    Direction = 2,
    Prefecture = 3,
    Comptabilite = 4,
    Secretariat = 5,
    Surveillance = 6,
    Bibliotheque = 7,
    Laboratoire = 8,
    Informatique = 9,
    Intendance = 10,
    Chauffeur = 11,
    Entretien = 12,
    Sentinelle = 13,
    Cuisine = 14,
    Autre = 99
}

public enum PersonnelContractType
{
    Cdi = 1,
    Cdd = 2,
    Stage = 3,
    Vacataire = 4,
    Prestation = 5
}

public enum PersonnelStatus
{
    Actif = 1,
    Conge = 2,
    FinContrat = 3,
    Inactif = 4
}

public enum PersonnelPaymentMethod
{
    Virement = 1,
    Espece = 2,
    MobileMoney = 3,
    Cheque = 4
}
