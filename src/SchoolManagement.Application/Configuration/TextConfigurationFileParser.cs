namespace SchoolManagement.Application.Configuration;

/// <summary>
/// Parseur générique clé=valeur pour fichiers texte de configuration (ServeurDonnees.txt, etc.).
/// </summary>
public static class TextConfigurationFileParser
{
    public static Dictionary<string, string> Parse(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        return values;
    }

    public static string Serialize(IReadOnlyDictionary<string, string> values, string header)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(header.TrimEnd());
        builder.AppendLine();

        foreach (var pair in values)
        {
            builder.AppendLine($"{pair.Key}={pair.Value}");
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }
}
