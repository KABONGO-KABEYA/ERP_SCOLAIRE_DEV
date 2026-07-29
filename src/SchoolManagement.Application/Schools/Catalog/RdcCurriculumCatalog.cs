namespace SchoolManagement.Application.Schools.Catalog;

using System.Globalization;
using System.Text;
using SchoolManagement.Application.Schools;
using SchoolManagement.Domain.Enums;

public sealed record CurriculumBranchDefinition(string Code, string Name, SchoolProgram? Program = null);

public sealed record CurriculumCourseDefinition(
    string Code,
    string Name,
    string? BranchCode = null,
    decimal Coefficient = 1,
    int MaxScore = 20);

/// <summary>
/// Catalogue officiel RDC — branches, cours et liaisons par classe pédagogique.
/// Règle : si une branche n'a pas de sous-cours, la branche est enregistrée comme cours autonome.
/// </summary>
public static class RdcCurriculumCatalog
{
    private static readonly Lazy<IReadOnlyList<CurriculumBranchDefinition>> Branches = new(BuildBranches);
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<CurriculumCourseDefinition>>> Profiles = new(BuildProfiles);

    private static readonly CurriculumNode[] MaternelleNodes =
    [
        Node("Langage"),
        Node("Prélecture"),
        Node("Préécriture"),
        Node("Précalcul"),
        Node("Psychomotricité"),
        Branch("Éducation artistique", "Chant", "Dessin", "Travaux manuels"),
        Node("Hygiène"),
        Node("Éducation morale"),
    ];

    private static readonly CurriculumNode[] PrimaireNodes =
    [
        Branch("Français",
            "Lecture",
            "Langage",
            "Écriture",
            "Orthographe",
            "Conjugaison",
            "Grammaire",
            "Vocabulaire",
            "Expression écrite"),
        Branch("Mathématiques",
            "Numération",
            "Calcul mental",
            "Calcul écrit",
            "Problèmes",
            "Géométrie",
            "Grandeurs et mesures"),
        Branch("Sciences",
            "Observation",
            "Hygiène",
            "Environnement"),
        Branch("Éducation civique",
            "Morale",
            "Civisme"),
        Branch("Langue nationale",
            "Lecture",
            "Orthographe",
            "Expression orale"),
        Branch("Éducation artistique",
            "Chant",
            "Dessin",
            "Travaux manuels"),
        Node("Éducation physique"),
    ];

    public static IReadOnlyList<CurriculumBranchDefinition> GetBranches() => Branches.Value;

    public static IReadOnlyDictionary<string, IReadOnlyList<CurriculumCourseDefinition>> GetProfiles() => Profiles.Value;

    public static string ResolveProfileKey(string templateCode)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
        {
            return "DEFAULT";
        }

        if (templateCode.StartsWith("MAT-", StringComparison.OrdinalIgnoreCase))
        {
            return "MAT";
        }

        if (templateCode.StartsWith("PRI-", StringComparison.OrdinalIgnoreCase))
        {
            var parts = templateCode.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)
                && level is >= 1 and <= 6)
            {
                return $"PRI-{level}";
            }

            return "PRI-1";
        }

        if (templateCode.StartsWith("CTEB-", StringComparison.OrdinalIgnoreCase))
        {
            return "CTEB";
        }

        if (templateCode.StartsWith("HPRO-", StringComparison.OrdinalIgnoreCase))
        {
            var parts = templateCode.Split('-', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 3 ? $"HPRO-{parts[1]}" : "HPRO-GENERAL";
        }

        if (templateCode.StartsWith("FS-", StringComparison.OrdinalIgnoreCase))
        {
            var parts = templateCode.Split('-', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? $"FS-{parts[1]}" : "FS-GENERAL";
        }

        if (templateCode.StartsWith("HUM-", StringComparison.OrdinalIgnoreCase))
        {
            var parts = templateCode.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                return $"HUM-{parts[1]}-{parts[2]}";
            }

            return "HUM-COMMON";
        }

        return "DEFAULT";
    }

    public static IReadOnlyList<CurriculumCourseDefinition> GetCoursesForTemplate(string templateCode)
    {
        var key = ResolveProfileKey(templateCode);
        return Profiles.Value.TryGetValue(key, out var courses)
            ? courses
            : Profiles.Value["DEFAULT"];
    }

    public static IReadOnlyList<CurriculumCourseDefinition> GetAllDistinctCourses()
    {
        return Profiles.Value.Values
            .SelectMany(c => c)
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<CurriculumBranchDefinition> BuildBranches()
    {
        var branchMap = new Dictionary<string, CurriculumBranchDefinition>(StringComparer.OrdinalIgnoreCase);

        void Accumulate(IEnumerable<CurriculumNode> nodes, SchoolProgram? program)
        {
            foreach (var branch in ExtractBranches(nodes, program))
            {
                branchMap.TryAdd(branch.Code, branch);
            }
        }

        Accumulate(MaternelleNodes, SchoolProgram.Maternelle);
        Accumulate(PrimaireNodes, SchoolProgram.Primaire);

        foreach (var branch in BuildLegacyBranches())
        {
            branchMap.TryAdd(branch.Code, branch);
        }

        return branchMap.Values.OrderBy(b => b.Code, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<CurriculumCourseDefinition>> BuildProfiles()
    {
        var profiles = new Dictionary<string, IReadOnlyList<CurriculumCourseDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DEFAULT"] = [Course("REL", "Religion")],
            ["MAT"] = ExpandNodes("MAT", MaternelleNodes),
        };

        AddPrimaireProfiles(profiles);

        profiles["CTEB"] = BuildCteb();
        profiles["HUM-COMMON"] = BuildHumanitiesCommon();

        AddHumanitySectionProfiles(profiles, "SCIENTIFIQUE",
        [
            ("BIOLOGIECHIMIE", ["PHY", "CHI", "BIO", "SVT"], ["Physique", "Chimie", "Biologie", "Sciences de la vie et de la terre"]),
            ("MATHEMATIQUESPHYSIQUE", ["PHY", "CHI", "MATH-APP"], ["Physique", "Chimie", "Mathématiques approfondies"]),
            ("SCIENTIFIQUEPURE", ["PHY", "CHI", "BIO", "MATH-APP"], ["Physique", "Chimie", "Biologie", "Mathématiques approfondies"])
        ]);

        AddHumanitySectionProfiles(profiles, "LITTERAIRE",
        [
            ("LATINPHILO", ["LAT", "PHILO"], ["Latin", "Philosophie"]),
            ("LATINANGLAIS", ["LAT", "ANG-APP"], ["Latin", "Anglais approfondi"]),
            ("LITTERAIRE", ["LIT-FR", "LIT-ANG"], ["Littérature française", "Littérature anglaise"])
        ]);

        AddHumanitySectionProfiles(profiles, "PEDAGOGIQUE",
        [
            ("PEDAGOGIEGENERALE", ["PED-GEN", "PSYCH"], ["Pédagogie générale", "Psychologie de l'enfant"]),
            ("NORMALE", ["PED-GEN", "DIDACT"], ["Pédagogie générale", "Didactique"]),
            ("EDUCATIONPHYSIQUE", ["EDPHY-APP", "SPORT"], ["Éducation physique approfondie", "Sport"])
        ]);

        AddHumanitySectionProfiles(profiles, "COMMERCIALE",
        [
            ("COMMERCIALEETGESTION", ["COMPTA", "ECO", "INFO-GEST"], ["Comptabilité", "Économie", "Informatique de gestion"]),
            ("COMPTABILITE", ["COMPTA", "FISC"], ["Comptabilité", "Fiscalité"]),
            ("SECRETARIAT", ["SECRET", "BUREAU"], ["Secrétariat", "Bureautique"]),
            ("TECHNIQUESCOMMERCIALES", ["TECH-COM", "MARK"], ["Techniques commerciales", "Marketing"]),
            ("INFORMATIQUEDEGESTION", ["INFO-GEST", "BUREAU"], ["Informatique de gestion", "Bureautique"])
        ]);

        AddHumanitySectionProfiles(profiles, "TECHNIQUE",
        [
            ("ELECTRICITE", ["ELEC", "ELECTRO"], ["Électricité générale", "Électrotechnique"]),
            ("ELECTRONIQUE", ["ELECTRO", "ELEC-NUM"], ["Électronique", "Électricité numérique"]),
            ("MECANIQUEGENERALE", ["MECA", "DESS-TECH"], ["Mécanique générale", "Dessin technique"]),
            ("MECANIQUEAUTO", ["MECA-AUTO", "DESS-TECH"], ["Mécanique automobile", "Dessin technique"]),
            ("CONSTRUCTION", ["CONSTR", "DESS-TECH"], ["Construction / Bâtiment", "Dessin technique"]),
            ("MENUISERIE", ["MENUIS", "DESS-TECH"], ["Menuiserie", "Dessin technique"]),
            ("CHIMIEINDUSTRIELLE", ["CHI-IND", "LABO"], ["Chimie industrielle", "Travaux pratiques"]),
            ("ARTSETMETIERS", ["ART-MET", "DESS-TECH"], ["Arts et métiers", "Dessin technique"]),
            ("MINESETGEOLOGIE", ["MINES", "GEO-APP"], ["Mines", "Géologie appliquée"]),
            ("AGRICULTURE", ["AGRI", "ZOO"], ["Agriculture", "Zootechnie"]),
            ("TOPOGRAPHIE", ["TOPO", "DESS-TECH"], ["Topographie", "Dessin technique"])
        ]);

        AddHumanitySectionProfiles(profiles, "SOCIALE",
        [
            ("COUPEETCOUTURE", ["COUT", "TEXT"], ["Coupe et couture", "Textile"]),
            ("ASSISTANCESOCIALE", ["ASS-SOC", "SOC"], ["Assistance sociale", "Sociologie"]),
            ("NUTRITION", ["NUTR", "DIET"], ["Nutrition", "Diététique"]),
            ("HOTELLERIEETRESTAURATION", ["HOTEL", "REST"], ["Hôtellerie", "Restauration"]),
            ("PUERICULTURE", ["PUER", "PED-GEN"], ["Puériculture", "Pédagogie générale"])
        ]);

        AddProfessionalProfiles(profiles);
        AddSpecializedProfiles(profiles);

        return profiles;
    }

    private static void AddPrimaireProfiles(Dictionary<string, IReadOnlyList<CurriculumCourseDefinition>> profiles)
    {
        var baseCourses = ExpandNodes("PRI", PrimaireNodes);
        var level1And2 = MergeCourses(baseCourses, [Course("REL", "Religion")]);
        var level3And4 = MergeCourses(baseCourses,
        [
            Course("HIST", "Histoire"),
            Course("GEO", "Géographie"),
            Course("REL", "Religion"),
        ]);
        var level5And6 = MergeCourses(level3And4, [Course("INFO", "Informatique")]);

        profiles["PRI-1"] = level1And2;
        profiles["PRI-2"] = level1And2;
        profiles["PRI-3"] = level3And4;
        profiles["PRI-4"] = level3And4;
        profiles["PRI-5"] = level5And6;
        profiles["PRI-6"] = level5And6;
    }

    private static List<CurriculumCourseDefinition> MergeCourses(
        IReadOnlyList<CurriculumCourseDefinition> baseCourses,
        IReadOnlyList<CurriculumCourseDefinition> additionalCourses)
    {
        return baseCourses
            .Concat(additionalCourses)
            .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static List<CurriculumCourseDefinition> ExpandNodes(string standalonePrefix, IReadOnlyList<CurriculumNode> nodes)
    {
        var courses = new List<CurriculumCourseDefinition>();

        foreach (var node in nodes)
        {
            if (node.SubCourses is { Length: > 0 })
            {
                var branchCode = ResolveBranchCode(node.Name);
                foreach (var subCourse in node.SubCourses)
                {
                    courses.Add(new CurriculumCourseDefinition(
                        NormalizeCourseCode($"{branchCode}-{ToCourseSlug(subCourse)}"),
                        subCourse,
                        branchCode,
                        1,
                        20));
                }

                continue;
            }

            courses.Add(Course($"{standalonePrefix}-{ToCourseSlug(node.Name)}", node.Name));
        }

        return courses;
    }

    private static IEnumerable<CurriculumBranchDefinition> ExtractBranches(
        IEnumerable<CurriculumNode> nodes,
        SchoolProgram? program)
    {
        foreach (var node in nodes.Where(n => n.SubCourses is { Length: > 0 }))
        {
            yield return new CurriculumBranchDefinition(ResolveBranchCode(node.Name), node.Name, program);
        }
    }

    private static IEnumerable<CurriculumBranchDefinition> BuildLegacyBranches() =>
    [
        new("FR-CTEB", "Français", SchoolProgram.CTEB),
        new("MATH-CTEB", "Mathématiques", SchoolProgram.CTEB),
        new("FR-HUM", "Français", SchoolProgram.Humanites),
        new("MATH-HUM", "Mathématiques", SchoolProgram.Humanites),
    ];

    private static string ResolveBranchCode(string branchName) => branchName switch
    {
        "Français" => "FR",
        "Mathématiques" => "MATH",
        "Sciences" => "SCI",
        "Éducation civique" => "EDCIV",
        "Langue nationale" => "LANG-NAT",
        "Éducation artistique" => "EDU-ART",
        _ => NormalizeCourseCode(ToCourseSlug(branchName))
    };

    private static List<CurriculumCourseDefinition> BuildCteb() =>
    [
        ..BranchCourse("FR-CTEB", "CTEB-FR-LECT", "Lecture"),
        ..BranchCourse("FR-CTEB", "CTEB-FR-LANG", "Langage"),
        ..BranchCourse("FR-CTEB", "CTEB-FR-ORTHO", "Orthographe"),
        ..BranchCourse("FR-CTEB", "CTEB-FR-GRAM", "Grammaire"),
        ..BranchCourse("FR-CTEB", "CTEB-FR-CONJ", "Conjugaison"),
        ..BranchCourse("FR-CTEB", "CTEB-FR-RED", "Rédaction"),
        ..BranchCourse("MATH-CTEB", "CTEB-MATH-NUM", "Numération"),
        ..BranchCourse("MATH-CTEB", "CTEB-MATH-CALM", "Calcul mental"),
        ..BranchCourse("MATH-CTEB", "CTEB-MATH-CALE", "Calcul écrit"),
        ..BranchCourse("MATH-CTEB", "CTEB-MATH-GEO", "Géométrie"),
        ..BranchCourse("MATH-CTEB", "CTEB-MATH-GM", "Grandeurs et mesures"),
        ..BranchCourse("MATH-CTEB", "CTEB-MATH-PROB", "Problèmes"),
        ..Standalone("CTEB-PHY", "Initiation à la physique"),
        ..Standalone("CTEB-CHI", "Initiation à la chimie"),
        ..Standalone("CTEB-SVT", "Sciences de la vie et de la terre"),
        ..Standalone("HIST", "Histoire"),
        ..Standalone("GEO", "Géographie"),
        ..Standalone("ANG", "Anglais"),
        ..Standalone("INFO", "Informatique"),
        ..Standalone("EDCIV", "Éducation civique"),
        ..Standalone("EDPHY", "Éducation physique"),
        ..Standalone("REL", "Religion / Morale"),
    ];

    private static List<CurriculumCourseDefinition> BuildHumanitiesCommon() =>
    [
        ..BranchCourse("FR-HUM", "HUM-FR", "Français", 4),
        ..BranchCourse("MATH-HUM", "HUM-MATH", "Mathématiques", 3),
        ..Standalone("HUM-ANG", "Anglais", 2),
        ..Standalone("HUM-HIST", "Histoire", 2),
        ..Standalone("HUM-GEO", "Géographie", 2),
        ..Standalone("HUM-EDCIV", "Éducation civique", 1),
        ..Standalone("HUM-EDPHY", "Éducation physique", 1),
        ..Standalone("HUM-REL", "Religion / Morale", 1),
    ];

    private static void AddHumanitySectionProfiles(
        Dictionary<string, IReadOnlyList<CurriculumCourseDefinition>> profiles,
        string sectionSlug,
        IEnumerable<(string OptionSlug, string[] Codes, string[] Names)> options)
    {
        foreach (var (optionSlug, codes, names) in options)
        {
            var key = $"HUM-{sectionSlug}-{optionSlug}";
            var list = new List<CurriculumCourseDefinition>(BuildHumanitiesCommon());
            for (var i = 0; i < codes.Length && i < names.Length; i++)
            {
                list.Add(Course(
                    BuildHumanityOptionCourseCode(optionSlug, codes[i]),
                    names[i],
                    coefficient: 3));
            }

            profiles[key] = list;
        }
    }

    private static void AddProfessionalProfiles(Dictionary<string, IReadOnlyList<CurriculumCourseDefinition>> profiles)
    {
        AddProfessionalProfile(profiles, "MECANIQUEAUTO", ["MECA-AUTO", "DESS-TECH", "ELEC-AUTO"], ["Mécanique automobile", "Dessin technique", "Électricité automobile"]);
        AddProfessionalProfile(profiles, "ELECTRICITE", ["ELEC", "ELECTRO", "DESS-TECH"], ["Électricité générale", "Électrotechnique", "Dessin technique"]);
        AddProfessionalProfile(profiles, "TECHNIQUESCOMMERCIALES", ["TECH-COM", "COMPTA", "MARK"], ["Techniques commerciales", "Comptabilité", "Marketing"]);
        AddProfessionalProfile(profiles, "COUPEETCOUTURE", ["COUT", "TEXT", "DESS-MOD"], ["Coupe et couture", "Textile", "Design de mode"]);
        AddProfessionalProfile(profiles, "ACCOUCHEUSES", ["OBST", "ANAT", "HYG"], ["Obstétrique", "Anatomie", "Hygiène"]);
        AddProfessionalProfile(profiles, "AIDESSOIGNANTES", ["SOIN", "ANAT", "HYG"], ["Soins infirmiers", "Anatomie", "Hygiène"]);
        AddProfessionalProfile(profiles, "AGRICULTURE", ["AGRI", "ZOO", "AGRO"], ["Agriculture", "Zootechnie", "Agro-économie"]);
        AddProfessionalProfile(profiles, "CONSTRUCTION", ["CONSTR", "DESS-TECH", "TOPO"], ["Construction", "Dessin technique", "Topographie"]);
    }

    private static void AddProfessionalProfile(
        Dictionary<string, IReadOnlyList<CurriculumCourseDefinition>> profiles,
        string slug,
        string[] codes,
        string[] names)
    {
        var list = new List<CurriculumCourseDefinition>
        {
            Course("FR-PRO", "Français", coefficient: 2),
            Course("MATH-PRO", "Mathématiques", coefficient: 2),
            Course("EDCIV", "Éducation civique", coefficient: 1),
            Course("EDPHY", "Éducation physique", coefficient: 1),
        };

        for (var i = 0; i < codes.Length && i < names.Length; i++)
        {
            list.Add(Course(codes[i], names[i], coefficient: 4));
        }

        profiles[$"HPRO-{slug}"] = list;
    }

    private static void AddSpecializedProfiles(Dictionary<string, IReadOnlyList<CurriculumCourseDefinition>> profiles)
    {
        profiles["FS-NORMALE"] =
        [
            ..BuildHumanitiesCommon(),
            Course("PED-GEN", "Pédagogie générale", coefficient: 4),
            Course("DIDACT", "Didactique", coefficient: 4),
            Course("PSYCH", "Psychologie de l'enfant", coefficient: 3),
            Course("STAGE", "Stage pédagogique", coefficient: 2),
        ];

        profiles["FS-MEDICAL"] =
        [
            Course("ANAT", "Anatomie", coefficient: 4),
            Course("PHYSIO", "Physiologie", coefficient: 4),
            Course("HYG", "Hygiène hospitalière", coefficient: 3),
            Course("SOIN", "Soins infirmiers", coefficient: 4),
            Course("LABO", "Techniques de laboratoire", coefficient: 3),
            Course("FR-MED", "Français médical", coefficient: 2),
            Course("MATH-MED", "Mathématiques appliquées", coefficient: 2),
        ];

        profiles["FS-TECHNIQUE"] =
        [
            Course("FR-TECH", "Français", coefficient: 2),
            Course("MATH-TECH", "Mathématiques", coefficient: 3),
            Course("TECH-GEN", "Technologie générale", coefficient: 4),
            Course("DESS-TECH", "Dessin technique", coefficient: 3),
            Course("ATELIER", "Atelier professionnel", coefficient: 4),
            Course("EDPHY", "Éducation physique", coefficient: 1),
        ];
    }

    private static CurriculumCourseDefinition Course(
        string code,
        string name,
        decimal coefficient = 1,
        int maxScore = 20) =>
        new(NormalizeCourseCode(code), name, null, coefficient, maxScore);

    private static IEnumerable<CurriculumCourseDefinition> BranchCourse(
        string branchCode,
        string code,
        string name,
        decimal coefficient = 1) =>
        [new(NormalizeCourseCode(code), name, branchCode, coefficient, 20)];

    private static IEnumerable<CurriculumCourseDefinition> Standalone(
        string code,
        string name,
        decimal coefficient = 1) =>
        [Course(code, name, coefficient)];

    private static CurriculumNode Node(string name) => new(name, null);

    private static CurriculumNode Branch(string branchName, params string[] subCourses) =>
        new(branchName, subCourses);

    private sealed record CurriculumNode(string Name, string[]? SubCourses);

    private static string BuildHumanityOptionCourseCode(string optionSlug, string courseCode) =>
        NormalizeCourseCode($"{optionSlug}-{courseCode}");

    private static string NormalizeCourseCode(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();

        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Length <= CourseCodeConstraints.MaxCodeLength
            ? normalized
            : normalized[..CourseCodeConstraints.MaxCodeLength].Trim('-');
    }

    private static string ToCourseSlug(string value)
    {
        var normalized = value
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Aggregate(new StringBuilder(), (current, c) => current.Append(c))
            .ToString()
            .ToUpperInvariant();

        var slug = new string(normalized
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug;
    }
}
