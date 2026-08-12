using System.Globalization;
using System.Text.RegularExpressions;

namespace SchoolManagement.Application.EnrollmentWizard;

/// <summary>
/// Format et parsing des matricules ELV-YYYY-##### (séquence variable en historique).
/// </summary>
public static partial class RegistrationNumberFormat
{
    public const string Prefix = "ELV";

    [GeneratedRegex(@"^ELV-(?<year>\d{4})-(?<seq>\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MatriculeRegex();

    public static string Format(int year, int sequence) =>
        $"{Prefix}-{year}-{sequence.ToString("D5", CultureInfo.InvariantCulture)}";

    public static bool TryParse(string? registrationNumber, out int year, out int sequence)
    {
        year = 0;
        sequence = 0;
        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            return false;
        }

        var match = MatriculeRegex().Match(registrationNumber.Trim());
        if (!match.Success)
        {
            return false;
        }

        year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        sequence = int.Parse(match.Groups["seq"].Value, CultureInfo.InvariantCulture);
        return sequence > 0;
    }

    /// <summary>
    /// Calcule le prochain NextValue à partir des matricules existants (y compris soft-deleted).
    /// Les formats non reconnus sont ignorés (pas de modification).
    /// </summary>
    public static int ComputeNextValue(IEnumerable<string> registrationNumbers, int year)
    {
        var max = 0;
        foreach (var number in registrationNumbers)
        {
            if (!TryParse(number, out var parsedYear, out var seq) || parsedYear != year)
            {
                continue;
            }

            if (seq > max)
            {
                max = seq;
            }
        }

        return max + 1;
    }
}
