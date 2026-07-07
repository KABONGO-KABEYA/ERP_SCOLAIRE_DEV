namespace SchoolManagement.Application.Schools.Catalog;

using SchoolManagement.Domain.Enums;

public sealed record PedagogicalTemplate(
    string TemplateCode,
    SchoolProgram Program,
    string DisplayName,
    int LevelOrder,
    string? HumanitiesSection,
    string? StudyOption,
    int? MinAge,
    int? MaxAge);

/// <summary>
/// Catalogue officiel RDC — source : Structure_Systeme_Educatif_RDC.md
/// Les établissements activent uniquement les classes qu'ils organisent réellement.
/// </summary>
public static class RdcPedagogicalCatalog
{
    private static readonly string[] PrimaryOrdinals = ["1ère", "2ème", "3ème", "4ème", "5ème", "6ème"];
    private static readonly string[] HumanityOrdinals = ["1ère", "2ème", "3ème", "4ème"];
    private static readonly string[] ProfessionalOrdinals = ["1ère", "2ème", "3ème"];

    private static readonly Lazy<IReadOnlyList<PedagogicalTemplate>> AllTemplates = new(BuildAll);

    public static IReadOnlyList<PedagogicalTemplate> GetAll() => AllTemplates.Value;

    public static PedagogicalTemplate? FindByCode(string templateCode) =>
        AllTemplates.Value.FirstOrDefault(t => t.TemplateCode.Equals(templateCode, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<PedagogicalTemplate> BuildAll()
    {
        var list = new List<PedagogicalTemplate>();

        AddMaternelle(list);
        AddPrimaire(list);
        AddCteb(list);
        AddHumanities(list);
        AddHumanitesProfessionnelles(list);
        AddFilieresSpecialisees(list);

        return list;
    }

    private static void AddMaternelle(List<PedagogicalTemplate> list)
    {
        var ages = new[] { 3, 4, 5 };
        for (var i = 0; i < ages.Length; i++)
        {
            var level = i + 1;
            list.Add(new PedagogicalTemplate(
                $"MAT-{level}",
                SchoolProgram.Maternelle,
                $"{PrimaryOrdinals[i]} maternelle",
                level,
                null,
                null,
                ages[i],
                ages[i]));
        }
    }

    private static void AddPrimaire(List<PedagogicalTemplate> list)
    {
        for (var level = 1; level <= 6; level++)
        {
            list.Add(new PedagogicalTemplate(
                $"PRI-{level}",
                SchoolProgram.Primaire,
                $"{PrimaryOrdinals[level - 1]} année primaire",
                level,
                null,
                null,
                null,
                null));
        }
    }

    private static void AddCteb(List<PedagogicalTemplate> list)
    {
        foreach (var level in new[] { 7, 8 })
        {
            list.Add(new PedagogicalTemplate(
                $"CTEB-{level}",
                SchoolProgram.CTEB,
                $"{level}ème année de l'éducation de base",
                level - 6,
                null,
                null,
                null,
                null));
        }
    }

    private static void AddHumanities(List<PedagogicalTemplate> list)
    {
        AddHumanitySection(list, "Scientifique",
        [
            "Biologie-Chimie",
            "Mathématiques-Physique",
            "Scientifique pure"
        ]);

        AddHumanitySection(list, "Littéraire",
        [
            "Latin-Philo",
            "Latin-Anglais",
            "Littéraire"
        ]);

        AddHumanitySection(list, "Pédagogique",
        [
            "Pédagogie générale",
            "Normale",
            "Éducation physique"
        ]);

        AddHumanitySection(list, "Commerciale",
        [
            "Commerciale et Gestion",
            "Comptabilité",
            "Secrétariat",
            "Techniques commerciales",
            "Informatique de gestion"
        ]);

        AddHumanitySection(list, "Technique",
        [
            "Électricité",
            "Électronique",
            "Mécanique générale",
            "Mécanique auto",
            "Construction",
            "Menuiserie",
            "Chimie industrielle",
            "Arts et métiers",
            "Mines et géologie",
            "Agriculture",
            "Topographie"
        ]);

        AddHumanitySection(list, "Sociale",
        [
            "Coupe et couture",
            "Assistance sociale",
            "Nutrition",
            "Hôtellerie et restauration",
            "Puériculture"
        ]);
    }

    private static void AddHumanitySection(List<PedagogicalTemplate> list, string section, string[] options)
    {
        var sectionSlug = ToSlug(section);
        foreach (var option in options)
        {
            var optionSlug = ToSlug(option);
            for (var level = 1; level <= 4; level++)
            {
                list.Add(new PedagogicalTemplate(
                    $"HUM-{sectionSlug}-{optionSlug}-{level}",
                    SchoolProgram.Humanites,
                    $"{HumanityOrdinals[level - 1]} Humanité {section} {option}",
                    level,
                    section,
                    option,
                    null,
                    null));
            }
        }
    }

    private static void AddHumanitesProfessionnelles(List<PedagogicalTemplate> list)
    {
        foreach (var filiere in new[]
        {
            "Mécanique auto",
            "Électricité",
            "Techniques commerciales",
            "Coupe et couture",
            "Accoucheuses",
            "Aides-soignantes",
            "Agriculture",
            "Construction"
        })
        {
            AddShortCycleTrack(list, filiere);
        }
    }

    private static void AddShortCycleTrack(List<PedagogicalTemplate> list, string filiere)
    {
        var slug = ToSlug(filiere);
        for (var level = 1; level <= 3; level++)
        {
            list.Add(new PedagogicalTemplate(
                $"HPRO-{slug}-{level}",
                SchoolProgram.HumanitesProfessionnelles,
                $"{ProfessionalOrdinals[level - 1]} année — {filiere} (Humanités professionnelles)",
                level,
                "Professionnelle",
                filiere,
                null,
                null));
        }
    }

    private static void AddFilieresSpecialisees(List<PedagogicalTemplate> list)
    {
        AddSpecializedTrack(list, "FS-NORMALE", "École normale d'instituteurs");
        AddSpecializedTrack(list, "FS-MEDICAL", "Enseignement médical");
        AddSpecializedTrack(list, "FS-TECHNIQUE", "Enseignement technique professionnel");
    }

    private static void AddSpecializedTrack(List<PedagogicalTemplate> list, string codePrefix, string filiere)
    {
        for (var level = 1; level <= 4; level++)
        {
            list.Add(new PedagogicalTemplate(
                $"{codePrefix}-{level}",
                SchoolProgram.FilieresSpecialisees,
                $"{HumanityOrdinals[level - 1]} année — {filiere}",
                level,
                "Spécialisée",
                filiere,
                null,
                null));
        }
    }

    private static string ToSlug(string value)
    {
        var normalized = value
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Aggregate("", (current, c) => current + c);

        return new string(normalized
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }
}
