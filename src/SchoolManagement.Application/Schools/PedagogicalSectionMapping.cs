namespace SchoolManagement.Application.Schools;

using SchoolManagement.Domain.Enums;

public static class PedagogicalSectionMapping
{
    public static string GetSectionCode(SchoolProgram program) =>
        program switch
        {
            SchoolProgram.Maternelle => "MAT",
            SchoolProgram.Primaire => "PRI",
            SchoolProgram.CTEB => "CTEB",
            SchoolProgram.Humanites => "HUM",
            SchoolProgram.HumanitesProfessionnelles => "HUM",
            SchoolProgram.FilieresSpecialisees => "HUM",
            _ => "PRI"
        };
}
