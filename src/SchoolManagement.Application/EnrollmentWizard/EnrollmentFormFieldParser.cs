namespace SchoolManagement.Application.EnrollmentWizard;

public static class EnrollmentFormFieldParser
{
    public static string? ExtractLabeledValue(string? source, string label)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        foreach (var segment in source.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (segment.StartsWith($"{label}:", StringComparison.OrdinalIgnoreCase))
            {
                return segment[(label.Length + 1)..].Trim();
            }
        }

        return null;
    }

    public static string? ExtractNoteValue(string? notes, string prefix)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        foreach (var segment in notes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return segment[prefix.Length..].Trim();
            }
        }

        return null;
    }

    public static string? ExtractMedicalValue(string? medicalNotes, string label)
    {
        if (string.IsNullOrWhiteSpace(medicalNotes))
        {
            return null;
        }

        foreach (var line in medicalNotes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith($"{label}:", StringComparison.OrdinalIgnoreCase))
            {
                return line[(label.Length + 1)..].Trim();
            }
        }

        return null;
    }
}
