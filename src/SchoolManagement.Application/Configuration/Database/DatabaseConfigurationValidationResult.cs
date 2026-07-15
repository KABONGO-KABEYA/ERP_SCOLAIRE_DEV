namespace SchoolManagement.Application.Configuration.Database;

public sealed class DatabaseConfigurationValidationResult
{
    public bool IsValid => FieldErrors.Count == 0;

    public Dictionary<string, string> FieldErrors { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddError(string fieldName, string message) => FieldErrors[fieldName] = message;
}
